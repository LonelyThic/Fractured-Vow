using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// A hired sellsword (up to two). Follows the knight, picks fights with nearby enemies, and enemies fight back.
    /// Give them weapons and armor from the bag on the Tab > Companions page. If they fall they get back up when
    /// the knight respawns. They travel with the knight to Floor 2.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CompanionAI : MonoBehaviour, IInteractable
    {
        public static readonly List<CompanionAI> All = new List<CompanionAI>();

        public int saveIndex;
        public float baseDamage = 14f;
        public float baseHealth = 220f;
        [Tooltip("Seconds between spells (sellswords cast at foes out of sword reach, and at the Leviathan).")]
        public float castCooldown = 4.5f;
        public float attackRange = 2f;
        public float attackCooldown = 1.2f;
        public float moveSpeed = 4.6f;

        public Health Health { get; private set; }
        public CompanionSave Save => saveIndex >= 0 && saveIndex < RunState.Companions.Count ? RunState.Companions[saveIndex] : null;
        public string DisplayName => Save != null ? Save.name : name;
        public float WeaponBonus { get; private set; }

        CharacterController cc;
        AnimatorBridge anim;
        ProceduralMotion fx;
        Transform player;
        PlayerController playerCtrl;
        Health target;
        float nextAttack, vy, groundY, nextThink;
        Vector3 knock;
        bool attacking;
        GUIStyle tagStyle;

        /// <summary>Creates a companion for RunState.Companions[index] next to 'pos'.</summary>
        public static CompanionAI Spawn(int index, Vector3 pos, Quaternion rot)
        {
            var save = RunState.Companions[index];
            var go = new GameObject(save.name);
            go.transform.SetPositionAndRotation(pos, rot);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.9f, 0f);
            var h = go.AddComponent<Health>();
            h.showWorldBar = false;
            var lib = FVLibrary.Get;
            GameObject model = null;
            if (lib != null && lib.CompanionChar(save.model) != null) model = CharacterVisuals.SpawnModel(lib.CompanionChar(save.model), go.transform, 1.8f);
            if (model == null)
            {
                var body = RuntimeVisuals.Primitive(PrimitiveType.Capsule, null, new Color(0.35f, 0.45f, 0.7f), false);
                body.name = "Model";
                body.transform.SetParent(go.transform, false);
                body.transform.localPosition = Vector3.up * 0.9f;
            }
            go.AddComponent<AnimatorBridge>();
            go.AddComponent<ProceduralMotion>();
            var c = go.AddComponent<CompanionAI>();
            c.saveIndex = index;
            c.ApplyGear();
            return c;
        }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            Health = GetComponent<Health>();
            anim = GetComponent<AnimatorBridge>();
            fx = GetComponent<ProceduralMotion>();
            groundY = transform.position.y;
        }

        void OnEnable()
        {
            All.Add(this);
            Health.Died += OnDied;
            Health.Damaged += OnDamaged;
            GameManager.Respawned += Revive;
            GameManager.CheckpointReached += Rest;
        }

        void OnDisable()
        {
            All.Remove(this);
            Health.Died -= OnDied;
            Health.Damaged -= OnDamaged;
            GameManager.Respawned -= Revive;
            GameManager.CheckpointReached -= Rest;
        }

        /// <summary>Applies the companion's weapon and armor: stats and the weapon in their hand.</summary>
        public void ApplyGear()
        {
            var s = Save;
            var w = s != null && !string.IsNullOrEmpty(s.weapon) ? FVLibrary.Item(s.weapon) : null;
            var a = s != null && !string.IsNullOrEmpty(s.armor) ? FVLibrary.Item(s.armor) : null;
            WeaponBonus = w != null ? w.damage : 0f;
            armorHealth = a != null ? a.health : 0f;
            Health.maxHealth = MaxHealthNow;
            Health.armor = Mathf.Min(0.6f, 0.1f + (a != null ? a.armor : 0f)); // sellswords wear a little armor of their own
            if (s != null && s.dead) Health.Kill(); else Health.Heal(99999f);
            scaledLevel = RunState.Level;
            CharacterVisuals.HoldWeapon(transform, w != null ? w.model : null, 1f);
            var model = transform.Find("Model");
            if (model != null) ModularOutfit.Apply(model.gameObject, a != null ? a.outfit : "cloth_00", DisplayName.GetHashCode());
        }

        void Update()
        {
            if (Health.IsDead) return;
            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go == null) return;
                player = go.transform;
                playerCtrl = go.GetComponent<PlayerController>();
            }
            float dt = Time.deltaTime;
            bool playerOnFoot = playerCtrl == null || playerCtrl.ControlEnabled;

            KeepScaled(dt);
            if (UpdateBoat()) return;                    // riding in the boat with the knight
            if (transform.position.y < player.position.y - 8f && playerOnFoot) { Warp(); return; } // fell into the water
            if (Health.Normalized < 0.35f && draughts > 0 && !attacking) { DrinkDraught(); return; }

            // keep up with the knight (after boat rides, teleports, falls)
            if (playerOnFoot && (player.position - transform.position).sqrMagnitude > 22f * 22f) { Warp(); return; }
            RecordTrail(playerOnFoot);

            if (Time.time >= nextThink) { nextThink = Time.time + 0.4f; target = Time.time < ignoreTargetsUntil ? null : PickTarget(); }
            if (target != null && !attacking && Time.time >= nextCast && CanCastAt(target)) { StartCoroutine(Cast(target)); return; }

            Vector3 goal = transform.position;
            float speed = 0f;
            if (target != null && !attacking)
            {
                Vector3 tp = TargetPos(target);
                Vector3 to = tp - transform.position; to.y = 0f;
                if (to.magnitude > attackRange) { goal = Route(tp); speed = moveSpeed * 1.15f; }
                else
                {
                    Face(tp);
                    if (Time.time >= nextAttack) StartCoroutine(Attack(target));
                }
            }
            else if (!attacking && playerOnFoot)
            {
                Vector3 side = Vector3.Cross(Vector3.up, player.forward) * (saveIndex == 0 ? 1.4f : -1.4f);
                Vector3 follow = player.position - player.forward * 1.8f + side;
                Vector3 to = follow - transform.position; to.y = 0f;
                bool holdOn = holding && (player.position - holdPlayerPos).sqrMagnitude < 2f * 2f;
                if (!holdOn) holding = false;
                if (to.magnitude > 1.2f && !holdOn) { goal = Route(follow); speed = to.magnitude > 6f ? moveSpeed * 1.3f : moveSpeed; }
                else if (holdOn) Face(player.position); // can't reach the spot beside him: wait here, facing him
            }
            CheckStuck(speed > 0f, playerOnFoot);

            Vector3 dir = goal - transform.position; dir.y = 0f;
            Vector3 motion = dir.sqrMagnitude > 0.01f ? dir.normalized * speed : Vector3.zero;
            if (speed > 0f) Face(goal);
            motion += knock;
            knock = Vector3.MoveTowards(knock, Vector3.zero, 18f * dt);
            if (cc.isGrounded) { groundY = transform.position.y; if (vy < 0f) vy = -2f; }
            vy += -20f * dt;
            Vector3 step = WaterZone.ClampMove(transform.position, groundY, new Vector3(motion.x, 0f, motion.z) * dt, cc.radius);
            if (Mathf.Abs(step.x) < 0.000001f) motion.x = 0f;
            if (Mathf.Abs(step.z) < 0.000001f) motion.z = 0f;
            motion.y = vy;
            cc.Move(motion * dt);
            if (anim != null) anim.SetFloat("Speed", Mathf.Clamp01(speed / (moveSpeed * 1.3f)), 0.1f);
            if (fx != null) fx.Move = Mathf.Clamp01(speed / (moveSpeed * 1.3f));
        }

        Health PickTarget()
        {
            Health best = null;
            float bestD = float.MaxValue;
            foreach (var e in EnemyAI.All)
            {
                if (e == null) continue;
                var h = e.GetComponent<Health>();
                if (h == null || h.IsDead) continue;
                float dMe = (e.transform.position - transform.position).magnitude;
                float dPlayer = player != null ? (e.transform.position - player.position).magnitude : dMe;
                if (dPlayer > 12f || dMe > 16f) continue;
                if (dMe < bestD) { bestD = dMe; best = h; }
            }
            var lev = FindFirstObjectByType<LeviathanBoss>();
            if (best == null && lev != null && lev.Hittable && (lev.NearestHitPoint(transform.position) - transform.position).magnitude < 26f)
                best = lev.GetComponent<Health>();
            var warden = FindFirstObjectByType<WardenBoss>();
            if (best == null && warden != null && warden.CurrentState != WardenBoss.State.Dormant && warden.CurrentState != WardenBoss.State.Dead
                && (warden.transform.position - transform.position).magnitude < 18f)
                best = warden.GetComponent<Health>();
            return best;
        }

        Vector3 TargetPos(Health h)
        {
            var lev = h.GetComponent<LeviathanBoss>();
            if (lev != null) return lev.NearestHitPoint(transform.position); // the nearest part of its long body
            return h.anchor != null ? h.anchor.position : h.transform.position;
        }

        IEnumerator Attack(Health t)
        {
            attacking = true;
            nextAttack = Time.time + attackCooldown;
            if (anim != null) anim.Trigger("Attack");
            GameAudio.Play("sword_swing", transform.position + Vector3.up, 0.6f);
            yield return new WaitForSeconds(0.35f);
            if (t != null && !t.IsDead && !Health.IsDead)
            {
                Vector3 to = TargetPos(t) - transform.position; to.y = 0f;
                if (to.magnitude <= attackRange + 0.6f)
                    t.TakeDamage(new DamageInfo(baseDamage * (1f + WeaponBonus) * Power, transform.position, gameObject, 3f));
            }
            yield return new WaitForSeconds(0.3f);
            attacking = false;
        }

        void Face(Vector3 p)
        {
            Vector3 to = p - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 10f * Time.deltaTime);
        }

        void OnDamaged(DamageInfo info)
        {
            if (fx != null) fx.Flinch(info.sourcePosition);
            Vector3 d = transform.position - info.sourcePosition; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) knock += d.normalized * info.knockback;
            if (anim != null) anim.Trigger("Hurt");
        }

        void OnDied()
        {
            if (Save != null) Save.dead = true;
            StopAllCoroutines();
            attacking = false;
            if (anim != null) anim.SetBool("Dead", true);
            if (aboard != null) LeaveBoat(false);
            PrototypeHUD.Message(DisplayName + " has fallen! Stand over them and press F to revive them with a Health Flask.", 4f);
        }

        /// <summary>Back on their feet beside the knight (a respawn).</summary>
        public void Revive()
        {
            if (Save != null) Save.dead = false;
            Health.Revive();
            if (anim != null) { anim.SetBool("Dead", false); anim.ResetAnimator(); }
            draughts = MaxDraughts;
            Warp();
        }

        // ---------------- revived with a Health Flask ----------------

        public string InteractionPrompt =>
            "Revive " + DisplayName + (Inventory.Count("hp_flask") > 0 ? "  (uses 1 Health Flask)" : "  (needs a Health Flask)");
        public bool CanInteract(PlayerInteractor who) => Health != null && Health.IsDead;

        public void Interact(PlayerInteractor who)
        {
            if (!Health.IsDead) return;
            if (!Inventory.Remove("hp_flask")) { PrototypeHUD.Message("You need a Health Flask to revive " + DisplayName, 2f); return; }
            if (Save != null) Save.dead = false;
            Health.Revive();
            Health.ScaleMax(MaxHealthNow);
            Health.SetCurrent(MaxHealthNow * 0.5f);  // back up at half health
            if (anim != null) { anim.SetBool("Dead", false); anim.ResetAnimator(); }
            GameAudio.Play("spell_learn", transform.position, 0.6f);
            GameFX.Burst(transform.position + Vector3.up, new Color(1f, 0.35f, 0.3f), 30);
            PrototypeHUD.Message(DisplayName + " is back on their feet!", 2f);
        }

        // ---------------- keeping pace with the dungeon ----------------

        const int MaxDraughts = 2;
        int draughts = MaxDraughts, scaledLevel;
        float armorHealth, nextCast, regenAt;

        /// <summary>
        /// Sellswords grow with the knight and the difficulty at the same rate the enemies do: they take as many
        /// hits to fall at level 10 as at level 1, and kill as quickly.
        /// </summary>
        float MaxHealthNow => (baseHealth + armorHealth) * EnemyScaling.DamageMult;
        float Power => Mathf.Pow(EnemyScaling.HealthMult, 0.9f);

        void KeepScaled(float dt)
        {
            if (scaledLevel != RunState.Level) { scaledLevel = RunState.Level; Health.ScaleMax(MaxHealthNow); }
            // out of the fight for 5 seconds: they bind their wounds (4% a second)
            if (Time.time - Health.LastDamageTime > 5f && target == null && Health.Current < Health.maxHealth && Time.time >= regenAt)
            {
                regenAt = Time.time + 0.5f;
                Health.Heal(Health.maxHealth * 0.02f);
            }
        }

        /// <summary>A checkpoint: rested and their draughts topped up.</summary>
        void Rest()
        {
            draughts = MaxDraughts;
            if (!Health.IsDead) Health.Heal(99999f);
        }

        /// <summary>Their own healing draught (2, refilled at checkpoints), drunk when badly hurt.</summary>
        void DrinkDraught()
        {
            draughts--;
            float amount = Health.maxHealth * 0.45f;
            Health.Heal(amount);
            if (anim != null) anim.Trigger("Cast"); // raises the bottle
            GameAudio.Play("pickup", transform.position + Vector3.up, 0.5f);
            PrototypeHUD.FloatText(transform.position + Vector3.up * 2.2f, "+" + Mathf.RoundToInt(amount), new Color(0.5f, 1f, 0.55f));
            PrototypeHUD.Message(DisplayName + " drinks a healing draught (" + draughts + " left)", 1.5f);
        }

        // ---------------- spells ----------------

        bool CanCastAt(Health t)
        {
            Vector3 tp = TargetPos(t);
            float d = Vector3.Distance(tp, transform.position);
            if (d > 18f) return false;
            bool boss = t.GetComponent<LeviathanBoss>() != null || t.GetComponent<WardenBoss>() != null;
            if (!boss && d < attackRange + 1.5f) return false; // in sword reach: they'd rather swing
            Vector3 eye = transform.position + Vector3.up * 1.5f;
            return !Physics.Linecast(eye, tp, out var hit, ~0, QueryTriggerInteraction.Ignore) ||
                   hit.collider.GetComponentInParent<Health>() == t || hit.transform.IsChildOf(t.transform);
        }

        /// <summary>The first sellsword hurls Firebolts; the second, Frost Shards that slow.</summary>
        IEnumerator Cast(Health t)
        {
            attacking = true;
            nextCast = Time.time + castCooldown + Random.Range(0f, 1.5f);
            Face(TargetPos(t));
            if (anim != null) anim.Trigger("Cast");
            GameAudio.Play("spell_cast", transform.position + Vector3.up, 0.5f);
            yield return new WaitForSeconds(0.45f);
            if (t != null && !t.IsDead && !Health.IsDead)
            {
                var lib = FVLibrary.Get;
                Vector3 origin = transform.position + Vector3.up * 1.4f + transform.forward * 0.6f;
                Vector3 dir = (TargetPos(t) - origin).normalized;
                if (saveIndex % 2 == 0)
                    MagicProjectile.Spawn(origin, dir, gameObject, new Color(1f, 0.5f, 0.15f), null,
                        (12f + 2f * RunState.Level) * Power, 2f, 1f, 0f, 22f, 0.3f,
                        lib != null ? lib.fireBolt : null, lib != null ? lib.fireImpact : null, lib != null ? lib.fireTrail : null);
                else
                    MagicProjectile.Spawn(origin, dir, gameObject, new Color(0.5f, 0.85f, 1f), null,
                        (7f + 1.2f * RunState.Level) * Power, 0.5f, 0.5f, 3f, 26f, 0.22f,
                        lib != null ? lib.frostBolt : null, lib != null ? lib.frostImpact : null);
            }
            yield return new WaitForSeconds(0.35f);
            attacking = false;
        }

        // ---------------- riding in the boat ----------------

        BoatController aboard;

        /// <summary>While the knight rows, his sellswords sit in the boat with him (and still cast at foes).</summary>
        bool UpdateBoat()
        {
            BoatController boat = null;
            foreach (var b in BoatController.All)
                if (b != null && b.Driver != null && b.Driver.transform == player) { boat = b; break; }
            if (boat != null && aboard != boat) BoardBoat(boat);
            else if (boat == null && aboard != null) LeaveBoat(true);
            if (aboard == null) return false;
            transform.localPosition = seatLocal;
            transform.localRotation = Quaternion.identity;
            if (anim != null) anim.SetFloat("Speed", 0f, 0.1f);
            if (Time.time >= nextThink) { nextThink = Time.time + 0.4f; target = PickTarget(); }
            if (target != null && !attacking && Time.time >= nextCast && CanCastAt(target)) StartCoroutine(Cast(target));
            return true;
        }

        Vector3 seatLocal;

        void BoardBoat(BoatController boat)
        {
            aboard = boat;
            cc.enabled = false;
            transform.SetParent(boat.transform, true);
            // a seat behind the knight's (or in front, if he sits at the back), side by side for two
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (var r in boat.GetComponentsInChildren<Renderer>())
            {
                if (r.transform.IsChildOf(transform) || r.GetComponentInParent<PlayerController>() != null || r.GetComponentInParent<CompanionAI>() != null) continue;
                var lb = new Bounds(boat.transform.InverseTransformPoint(r.bounds.center), Vector3.zero);
                lb.Encapsulate(boat.transform.InverseTransformPoint(r.bounds.min));
                lb.Encapsulate(boat.transform.InverseTransformPoint(r.bounds.max));
                if (!any) { bounds = lb; any = true; } else bounds.Encapsulate(lb);
            }
            Vector3 seatL = boat.seat != null ? boat.transform.InverseTransformPoint(boat.seat.position) : Vector3.zero;
            float len = any ? bounds.size.z : 3f, wid = any ? bounds.size.x : 1.2f;
            float z = seatL.z >= bounds.center.z ? seatL.z - len * 0.3f : seatL.z + len * 0.3f;
            float x = (saveIndex % 2 == 0 ? -1f : 1f) * wid * 0.18f;
            seatLocal = new Vector3(x, seatL.y, z);
            transform.localPosition = seatLocal;
            transform.localRotation = Quaternion.identity;
            knock = Vector3.zero; vy = 0f;
        }

        void LeaveBoat(bool besideKnight)
        {
            aboard = null;
            transform.SetParent(null, true);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            cc.enabled = true;
            if (besideKnight) Warp();
        }

        /// <summary>After a respawn: companions match the run again (hired since the checkpoint: gone; dismissed: back).</summary>
        public static void SyncWithRun(Vector3 near)
        {
            foreach (var c in All.ToArray())
            {
                if (c == null) continue;
                var s = c.Save;
                if (s == null || s.name != c.name) { Destroy(c.gameObject); continue; }
                c.ApplyGear();
            }
            for (int i = 0; i < RunState.Companions.Count; i++)
            {
                bool present = false;
                foreach (var c in All) if (c != null && c.saveIndex == i && c.name == RunState.Companions[i].name) present = true;
                if (!present) Spawn(i, near + Vector3.right * (i == 0 ? 1.5f : -1.5f), Quaternion.identity);
            }
        }

        // ---------------- getting around without a navigation mesh ----------------

        readonly List<Vector3> trail = new List<Vector3>();
        Vector3 routeGoal, routeFor;
        float nextRoute, stuckSince = -1f, ignoreTargetsUntil;
        Vector3 lastPos;

        /// <summary>Remembers where the knight walked, so companions can follow his footsteps around corners.</summary>
        void RecordTrail(bool playerOnFoot)
        {
            if (!playerOnFoot) return;
            if (trail.Count == 0 || (player.position - trail[trail.Count - 1]).sqrMagnitude > 0.8f * 0.8f)
            {
                trail.Add(player.position);
                if (trail.Count > 90) trail.RemoveAt(0);
            }
        }

        /// <summary>True if a person could walk straight from 'a' to 'b' (no wall or prop in between).</summary>
        bool Walkable(Vector3 a, Vector3 b)
        {
            Vector3 from = a + Vector3.up * 0.9f, to = b + Vector3.up * 0.9f;
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 0.05f) return true;
            foreach (var hit in Physics.SphereCastAll(from, 0.3f, d / len, len, ~0, QueryTriggerInteraction.Ignore))
            {
                var t = hit.transform;
                if (t.IsChildOf(transform) || (player != null && t.IsChildOf(player))) continue;
                if (hit.collider.GetComponentInParent<CompanionAI>() != null || hit.collider.GetComponentInParent<EnemyAI>() != null) continue;
                if (hit.collider.GetComponentInParent<Health>() != null) continue;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Where to walk next to reach 'dest': straight there if the way is clear, otherwise the newest point of
        /// the knight's trail that can be reached directly (his footsteps lead around the walls).
        /// </summary>
        Vector3 Route(Vector3 dest)
        {
            if (Time.time < nextRoute && (dest - routeFor).sqrMagnitude < 1f) return routeGoal;
            nextRoute = Time.time + 0.25f;
            routeFor = dest;
            routeGoal = dest;
            if (Walkable(transform.position, dest)) return routeGoal;
            for (int i = trail.Count - 1; i >= 0; i--)
                if ((trail[i] - transform.position).sqrMagnitude > 0.5f && Walkable(transform.position, trail[i]))
                {
                    routeGoal = trail[i];
                    break;
                }
            return routeGoal;
        }

        /// <summary>If trying to move but going nowhere for 2 seconds, catch up beside the knight (and drop an unreachable fight).</summary>
        bool holding;
        Vector3 holdPlayerPos;

        /// <summary>
        /// Trying to move but getting nowhere (sliding along a wall or crate counts as nowhere) for 1.5 seconds:
        /// near the knight, the companion simply waits beside him until he moves on; far away, it catches up.
        /// </summary>
        void CheckStuck(bool moving, bool playerOnFoot)
        {
            if (!moving) { stuckSince = -1f; return; }
            if (stuckSince < 0f) { stuckSince = Time.time; lastPos = transform.position; return; }
            if (Time.time - stuckSince < 1.5f) return;
            bool gotNowhere = (transform.position - lastPos).sqrMagnitude < 0.6f * 0.6f;
            stuckSince = Time.time;
            lastPos = transform.position;
            if (!gotNowhere) return;
            if (target != null) { target = null; ignoreTargetsUntil = Time.time + 3f; return; }
            if (player == null || !playerOnFoot) return;
            if ((player.position - transform.position).sqrMagnitude > 5f * 5f) Warp();
            else { holding = true; holdPlayerPos = player.position; }
        }

        /// <summary>Appears next to the knight, on a free spot (behind him first, then to the sides).</summary>
        void Warp()
        {
            if (player == null) return;
            Vector3 side = Vector3.Cross(Vector3.up, player.forward) * (saveIndex == 0 ? 1f : -1f);
            Vector3[] spots = { -player.forward * 2f + side * 1.4f, -player.forward * 2f, side * 1.8f, -side * 1.8f, player.forward * 1.8f };
            Vector3 p = player.position + spots[0];
            foreach (var s in spots)
            {
                Vector3 c = player.position + s;
                if (!Physics.CheckCapsule(c + Vector3.up * 0.45f, c + Vector3.up * 1.4f, 0.3f, ~0, QueryTriggerInteraction.Ignore) && Walkable(player.position, c))
                { p = c; break; }
            }
            cc.enabled = false;
            transform.position = p + Vector3.up * 0.1f;
            cc.enabled = true;
            vy = 0f; knock = Vector3.zero;
            trail.Clear();
        }

        /// <summary>Lets the companion go: they leave the party for good.</summary>
        public void Dismiss()
        {
            int i = saveIndex;
            if (i >= 0 && i < RunState.Companions.Count) RunState.Companions.RemoveAt(i);
            foreach (var c in All) if (c != this && c.saveIndex > i) c.saveIndex--;
            Destroy(gameObject);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || UIFocus.Any || PrototypeHUD.Hidden || Camera.main == null) return;
            Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2.2f);
            if (sp.z < 0f || sp.z > 25f) return;
            if (tagStyle == null)
            {
                tagStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                tagStyle.normal.textColor = new Color(0.55f, 1f, 0.6f);
            }
            tagStyle.fontSize = Mathf.RoundToInt(Screen.height / 60f);
            float w = 120f, x = sp.x - w / 2f, y = Screen.height - sp.y;
            GUI.Label(new Rect(x - 40f, y - 22f, w + 80f, 20f), DisplayName + (Health.IsDead ? " (down)" : ""), tagStyle);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f); GUI.DrawTexture(new Rect(x, y, w, 6f), Texture2D.whiteTexture);
            GUI.color = new Color(0.35f, 0.9f, 0.4f); GUI.DrawTexture(new Rect(x, y, w * Health.Normalized, 6f), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
