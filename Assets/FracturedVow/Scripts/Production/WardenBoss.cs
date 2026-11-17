using System.Collections;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Floor 1 boss. Kneels dormant until the knight enters the arena, then fights with three telegraphed attacks:
    /// Cleave (frontal arc), Ground Slam (ring around him) and, once enraged below half health, Charge.
    /// Red markers on the floor show where each attack will land. On death he unlocks the exit gate and
    /// reveals the memory fragment. Draws his own health bar.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class WardenBoss : MonoBehaviour
    {
        public enum State { Dormant, Waking, Fighting, Attacking, Dead }

        [Header("Identity")]
        public string bossName = "The Warden of the Pit";
        public string subtitle = "Keeper of the first seal";

        [Header("Movement")]
        public float moveSpeed = 2.4f;
        public float enragedMoveSpeed = 3.4f;
        public float turnSpeed = 5f;

        [Header("Cleave")]
        public float cleaveRange = 3.4f;
        public float cleaveArc = 130f;
        public float cleaveDamage = 26f;
        public float cleaveKnockback = 6f;
        public float cleaveWindup = 0.85f;

        [Header("Ground Slam")]
        public float slamRadius = 5.5f;
        public float slamDamage = 22f;
        public float slamKnockback = 10f;
        public float slamWindup = 1.3f;
        public float slamCooldown = 7f;

        [Header("Charge (enraged only)")]
        public float chargeSpeed = 12f;
        public float chargeDamage = 20f;
        public float chargeKnockback = 8f;
        public float chargeWindup = 0.8f;
        public float chargeTime = 1f;
        public float chargeMinDistance = 7f;
        public float chargeCooldown = 9f;

        [Header("Pacing")]
        public float recovery = 1f;
        [Range(0f, 1f)] public float enragedAt = 0.5f;
        public float enragedWindupMultiplier = 0.75f;

        [Header("Anti-cheese: pull down from ledges")]
        [Tooltip("If the knight stands this much higher than the Warden, he gets dragged into the pit.")]
        public float elevatedHeight = 1.5f;
        public float pullWindup = 1f;
        public float pullCooldown = 4f;
        public float pullDamage = 8f;
        [Tooltip("x unused, y = upward speed, z = speed toward the Warden")]
        public Vector3 pullLaunch = new Vector3(0f, 9f, 14f);
        [Tooltip("Fallback spot on the arena floor if the knight is still stuck up high after the throw.")]
        public Vector3 pitLanding = new Vector3(44f, 0.1f, 74f);

        [Header("Links (set by the production pass)")]
        public Door[] unlockOnDeath;
        public GameObject[] activateOnDeath;
        public PrimitiveRig rig;
        public ParticleSystem aura;
        public ParticleSystem slamDust;
        public Material telegraphMaterial;

        public State CurrentState { get; private set; } = State.Dormant;
        public bool Enraged { get; private set; }

        CharacterController cc;
        Health health;
        AnimatorBridge anim;
        Transform player;
        Health playerHealth;
        float vy;
        Vector3 knock;
        float nextSlam, nextCharge, nextPull, flashUntil;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        GameObject telegraph;
        GUIStyle nameStyle;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            anim = GetComponent<AnimatorBridge>();
            renderers = GetComponentsInChildren<Renderer>();
            mpb = new MaterialPropertyBlock();
            if (rig != null) rig.pose = PrimitiveRig.Pose.Kneel;
            spawnPos = transform.position;
            spawnRot = transform.rotation;
            if (aura != null) auraRate = aura.emission.rateOverTimeMultiplier;
        }

        Vector3 spawnPos;
        Quaternion spawnRot;
        float auraRate = 1f;

        /// <summary>
        /// Puts the Warden back to sleep at full health on his spot (used when the knight respawns).
        /// A Warden who has already been defeated stays defeated.
        /// </summary>
        public void ResetFight()
        {
            if (CurrentState == State.Dead) return;
            saidLow = false;
            saidPlayerDeath = false;
            StopAllCoroutines();
            HideTelegraph();
            cc.enabled = false;
            transform.SetPositionAndRotation(spawnPos, spawnRot);
            cc.enabled = true;
            knock = Vector3.zero; vy = 0f;
            health.Revive();
            if (anim != null) anim.ResetAnimator();
            Enraged = false;
            if (aura != null) { aura.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); var em = aura.emission; em.rateOverTimeMultiplier = auraRate; }
            nextSlam = nextCharge = nextPull = 0f;
            CurrentState = State.Dormant;
            if (rig != null) { rig.pose = PrimitiveRig.Pose.Kneel; rig.moveSpeed01 = 0f; }
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        void FindPlayer()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            player = go.transform;
            playerHealth = go.GetComponent<Health>();
        }

        public void Activate()
        {
            if (CurrentState == State.Dormant) StartCoroutine(WakeRoutine());
        }

        bool saidLow, saidPlayerDeath;
        const string Speaker = "The Warden";

        IEnumerator WakeRoutine()
        {
            CurrentState = State.Waking;
            if (!RunState.SeenWardenIntro)
            {
                // the first time: an introduction (he waits, rising, until it ends)
                RunState.SeenWardenIntro = true;
                var intro = Cinema.PlayWardenIntro(transform);
                if (intro != null) yield return intro;
            }
            else Cinema.Say(Speaker, Random.value < 0.5f ? "Back again? The pit always wins." : "Crawled out of the dark once more? Good. I was bored.");
            PrototypeHUD.BigMessage(bossName.ToUpperInvariant(), subtitle, 3f);
            GameAudio.Play("boss_roar", transform.position + Vector3.up * 3f, 1f, 0.02f);
            GameAudio.PlayMusic("music_boss", 1.5f);
            CameraShake.Shake(0.25f, 1.2f);
            if (aura != null) aura.Play();
            if (rig != null) rig.pose = PrimitiveRig.Pose.Idle;
            yield return new WaitForSeconds(1.6f);
            if (CurrentState == State.Dead) yield break;
            CurrentState = State.Fighting;
            nextSlam = Time.time + 4f;
            nextCharge = Time.time + 6f;
        }

        void Update()
        {
            if (CurrentState == State.Dead) return;
            if (player == null) FindPlayer();
            if (playerHealth != null && playerHealth.IsDead && !saidPlayerDeath && CurrentState != State.Dormant)
            {
                saidPlayerDeath = true;
                Cinema.Say(Speaker, Random.value < 0.5f ? "Kneel. They all kneel in the end." : "Another name for the pit to forget.");
            }

            Vector3 desired = Vector3.zero;
            if (CurrentState == State.Fighting && player != null && (playerHealth == null || !playerHealth.IsDead))
            {
                Vector3 to = Flat(player.position - transform.position);
                float dist = to.magnitude;
                Face(to, 1f);

                if (PlayerElevated()) { if (Time.time >= nextPull) StartCoroutine(PullDownRoutine()); }
                else if (Enraged && dist >= chargeMinDistance && Time.time >= nextCharge) StartCoroutine(ChargeRoutine());
                else if (dist <= slamRadius * 0.6f && Time.time >= nextSlam) StartCoroutine(SlamRoutine());
                else if (dist <= cleaveRange * 0.9f) StartCoroutine(CleaveRoutine());
                else desired = to.normalized * (Enraged ? enragedMoveSpeed : moveSpeed);
            }

            Move(desired);
            float speed01 = desired.magnitude / enragedMoveSpeed;
            if (rig != null && CurrentState != State.Attacking) rig.moveSpeed01 = speed01;
            if (anim != null) anim.SetFloat("Speed", speed01, 0.1f);
            UpdateFlash();
        }

        // ---------------- attacks ----------------

        float W(float t) => Enraged ? t * enragedWindupMultiplier : t;

        IEnumerator CleaveRoutine()
        {
            CurrentState = State.Attacking;
            SetPose(PrimitiveRig.Pose.WindupSide);
            if (anim != null) anim.Trigger("Attack");
            float wind = W(cleaveWindup);
            for (float t = 0f; t < wind; t += Time.deltaTime)
            {
                if (player != null) Face(Flat(player.position - transform.position), 0.6f);
                ShowCircle(transform.position + transform.forward * cleaveRange * 0.55f, cleaveRange * 0.75f * (t / wind));
                yield return null;
            }
            HideTelegraph();
            SetPose(PrimitiveRig.Pose.StrikeSide);
            CameraShake.Shake(0.12f, 0.2f);
            GameAudio.Play("boss_cleave", transform.position + Vector3.up * 2f);
            if (PlayerInArc(cleaveRange + 0.5f, cleaveArc))
                playerHealth.TakeDamage(new DamageInfo(cleaveDamage, transform.position, gameObject, cleaveKnockback));
            yield return new WaitForSeconds(0.35f);
            SetPose(PrimitiveRig.Pose.Idle);
            yield return new WaitForSeconds(recovery);
            if (CurrentState != State.Dead) CurrentState = State.Fighting;
        }

        IEnumerator SlamRoutine()
        {
            CurrentState = State.Attacking;
            SetPose(PrimitiveRig.Pose.WindupOverhead);
            if (anim != null) anim.Trigger("Attack");
            float wind = W(slamWindup);
            for (float t = 0f; t < wind; t += Time.deltaTime)
            {
                ShowCircle(transform.position, slamRadius * (t / wind));
                yield return null;
            }
            HideTelegraph();
            SetPose(PrimitiveRig.Pose.StrikeOverhead);
            if (slamDust != null) slamDust.Play();
            CameraShake.Shake(0.45f, 0.5f);
            GameAudio.Play("boss_slam", transform.position, 1f, 0.03f);
            if (player != null && playerHealth != null && Flat(player.position - transform.position).magnitude <= slamRadius)
                playerHealth.TakeDamage(new DamageInfo(slamDamage, transform.position, gameObject, slamKnockback));
            nextSlam = Time.time + (Enraged ? slamCooldown * 0.7f : slamCooldown);
            yield return new WaitForSeconds(0.6f);
            SetPose(PrimitiveRig.Pose.Idle);
            yield return new WaitForSeconds(recovery);
            if (CurrentState != State.Dead) CurrentState = State.Fighting;
        }

        IEnumerator ChargeRoutine()
        {
            CurrentState = State.Attacking;
            SetPose(PrimitiveRig.Pose.Lean);
            float wind = W(chargeWindup);
            float length = chargeSpeed * chargeTime;
            for (float t = 0f; t < wind; t += Time.deltaTime)
            {
                if (player != null) Face(Flat(player.position - transform.position), 1.5f);
                ShowLine(transform.position + transform.forward * length * 0.5f, transform.rotation, length * Mathf.Clamp01(t / wind * 1.3f));
                yield return null;
            }
            HideTelegraph();
            bool hit = false;
            GameAudio.Play("boss_charge", transform.position + Vector3.up * 2f);
            for (float t = 0f; t < chargeTime; t += Time.deltaTime)
            {
                var flags = cc.Move((transform.forward * chargeSpeed + Vector3.up * -5f) * Time.deltaTime);
                if (!hit && player != null && playerHealth != null && Flat(player.position - transform.position).magnitude < 2f)
                {
                    hit = true;
                    playerHealth.TakeDamage(new DamageInfo(chargeDamage, transform.position, gameObject, chargeKnockback));
                }
                if ((flags & CollisionFlags.Sides) != 0) { CameraShake.Shake(0.35f, 0.4f); break; }
                yield return null;
            }
            nextCharge = Time.time + chargeCooldown;
            SetPose(PrimitiveRig.Pose.Stagger); // winded: an opening for the player
            yield return new WaitForSeconds(recovery * 1.4f);
            SetPose(PrimitiveRig.Pose.Idle);
            if (CurrentState != State.Dead) CurrentState = State.Fighting;
        }

        bool PlayerElevated() =>
            player != null && player.position.y - transform.position.y > elevatedHeight;

        IEnumerator PullDownRoutine()
        {
            CurrentState = State.Attacking;
            nextPull = Time.time + pullCooldown;
            SetPose(PrimitiveRig.Pose.WindupOverhead);
            PrototypeHUD.Message("The Warden's chains reach for you!", 1.5f);
            for (float t = 0f; t < pullWindup; t += Time.deltaTime)
            {
                if (player != null)
                {
                    Face(Flat(player.position - transform.position), 1.5f);
                    ShowCircleAt(player.position, 1.6f * (t / pullWindup));
                }
                yield return null;
            }
            HideTelegraph();
            SetPose(PrimitiveRig.Pose.StrikeOverhead);
            CameraShake.Shake(0.35f, 0.4f);

            if (player != null && PlayerElevated())
            {
                Vector3 toBoss = Flat(transform.position - player.position).normalized;
                var pc = player.GetComponent<PlayerController>();
                if (pc != null) pc.Launch(toBoss * pullLaunch.z + Vector3.up * pullLaunch.y);
                if (playerHealth != null) playerHealth.TakeDamage(new DamageInfo(pullDamage, transform.position, gameObject, 0f));

                yield return new WaitForSeconds(1.2f);
                // still up there (wedged against the railing)? drag him down anyway
                if (pc != null && PlayerElevated() && (playerHealth == null || !playerHealth.IsDead))
                    pc.Teleport(pitLanding, Quaternion.LookRotation(toBoss));
            }

            SetPose(PrimitiveRig.Pose.Idle);
            yield return new WaitForSeconds(recovery * 0.5f);
            if (CurrentState != State.Dead) CurrentState = State.Fighting;
        }

        void ShowCircleAt(Vector3 center, float radius)
        {
            ShowCircle(center, radius);
            var p = telegraph.transform.position;
            telegraph.transform.position = new Vector3(p.x, center.y + 0.04f, p.z);
        }

        bool PlayerInArc(float range, float arc)
        {
            if (player == null || playerHealth == null || playerHealth.IsDead) return false;
            Vector3 to = Flat(player.position - transform.position);
            return to.magnitude <= range && Vector3.Angle(transform.forward, to) <= arc * 0.5f;
        }

        // ---------------- telegraphs ----------------

        void ShowCircle(Vector3 center, float radius)
        {
            EnsureTelegraph(PrimitiveType.Cylinder);
            telegraph.transform.SetPositionAndRotation(new Vector3(center.x, transform.position.y + 0.04f, center.z), Quaternion.identity);
            telegraph.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
        }

        void ShowLine(Vector3 center, Quaternion rot, float length)
        {
            EnsureTelegraph(PrimitiveType.Cube);
            telegraph.transform.SetPositionAndRotation(transform.position + transform.forward * length * 0.5f + Vector3.up * 0.04f, rot);
            telegraph.transform.localScale = new Vector3(2.2f, 0.02f, Mathf.Max(0.1f, length));
        }

        void EnsureTelegraph(PrimitiveType type)
        {
            string wanted = "Telegraph_" + type;
            if (telegraph != null && telegraph.name == wanted) return;
            HideTelegraph();
            telegraph = GameObject.CreatePrimitive(type);
            telegraph.name = wanted;
            Destroy(telegraph.GetComponent<Collider>());
            var r = telegraph.GetComponent<Renderer>();
            if (telegraphMaterial != null) r.sharedMaterial = telegraphMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void HideTelegraph()
        {
            if (telegraph != null) Destroy(telegraph);
            telegraph = null;
        }

        // ---------------- reactions ----------------

        void OnDamaged(DamageInfo info)
        {
            flashUntil = Time.time + 0.1f;
            if (CurrentState == State.Dormant) Activate();
            if (!saidLow && health.Normalized <= 0.2f && !health.IsDead)
            {
                saidLow = true;
                Cinema.Say(Speaker, "No... that fire in you... the vow still burns?");
            }
            if (!Enraged && health.Normalized <= enragedAt)
            {
                Enraged = true;
                PrototypeHUD.Message("The Warden is enraged!", 3f);
                Cinema.Say(Speaker, "ENOUGH! I will grind your bones into the stone!");
                GameAudio.Play("boss_roar", transform.position + Vector3.up * 3f, 1f, 0.02f);
                CameraShake.Shake(0.3f, 0.8f);
                if (aura != null) { var em = aura.emission; em.rateOverTimeMultiplier *= 2.5f; }
            }
        }

        void OnDied()
        {
            if (GameManager.Instance != null) GameManager.Instance.CheckpointHereSoon(3f); // the victory is kept
            CurrentState = State.Dead;
            StopAllCoroutines();
            HideTelegraph();
            SetPose(PrimitiveRig.Pose.Dead);
            if (aura != null) aura.Stop();
            cc.enabled = false;
            CameraShake.Shake(0.4f, 1.2f);
            if (unlockOnDeath != null) foreach (var d in unlockOnDeath) if (d != null) d.Unlock(true);
            if (activateOnDeath != null) foreach (var g in activateOnDeath) if (g != null) g.SetActive(true);
            PrototypeHUD.BigMessage("WARDEN DEFEATED", "The seal breaks and the north gate grinds open.\nA memory fragment glows on the pedestal.", 5f);
            Cinema.Say(Speaker, "The way up... is yours, vowbreaker. May you remember... what you swore.", 6f);
            GameAudio.Play("enemy_death", transform.position + Vector3.up * 2f, 1f, 0f);
            GameAudio.PlayUI("victory_sting");
            GameAudio.PlayMusic("music_dungeon", 4f);
            GameFX.Dust(transform.position + Vector3.up * 0.3f, 2.5f, 50);
        }

        void SetPose(PrimitiveRig.Pose p)
        {
            if (rig != null) { rig.pose = p; rig.moveSpeed01 = 0f; }
        }

        void Move(Vector3 desired)
        {
            if (!cc.enabled) return;
            if (cc.isGrounded && vy < 0f) vy = -2f;
            vy += -20f * Time.deltaTime;
            Vector3 motion = desired + knock;
            knock = Vector3.MoveTowards(knock, Vector3.zero, 15f * Time.deltaTime);
            motion.y = vy;
            cc.Move(motion * Time.deltaTime);
        }

        void Face(Vector3 dir, float speedMul)
        {
            if (dir.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), turnSpeed * speedMul * Time.deltaTime);
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        void UpdateFlash()
        {
            bool flash = Time.time < flashUntil;
            foreach (var r in renderers)
            {
                if (r == null || (telegraph != null && r.gameObject == telegraph)) continue;
                if (r is ParticleSystemRenderer) continue;
                mpb.Clear();
                if (flash) { mpb.SetColor("_BaseColor", Color.white); mpb.SetColor("_Color", Color.white); }
                r.SetPropertyBlock(mpb);
            }
        }

        void OnGUI()
        {
            if (UIFocus.Any) return;
            if (CurrentState == State.Dormant || CurrentState == State.Dead) return;
            float scale = Screen.height / 900f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                nameStyle.normal.textColor = new Color(1f, 0.85f, 0.75f);
            }
            float bw = 620f, bx = (w - bw) / 2f, by = h - 120f;
            GUI.Label(new Rect(bx, by - 28f, bw, 24f), bossName + (Enraged ? "  (enraged)" : ""), nameStyle);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f); GUI.DrawTexture(new Rect(bx - 2f, by - 2f, bw + 4f, 18f), Texture2D.whiteTexture);
            GUI.color = Enraged ? new Color(0.95f, 0.25f, 0.1f) : new Color(0.7f, 0.08f, 0.08f);
            GUI.DrawTexture(new Rect(bx, by, bw * health.Normalized, 14f), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
