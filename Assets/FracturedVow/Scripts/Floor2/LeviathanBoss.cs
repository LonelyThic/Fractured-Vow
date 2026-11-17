using System.Collections;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Floor 2's final boss: a sea serpent that lives in the lake. It wakes when the knight sails out by boat.
    ///  - Knight on a platform: it surfaces beside the platform and bites (red strip shows where) or spits orbs.
    ///    A bite leaves its head stuck on the stone for a moment: that's the opening to hit it with the sword.
    ///    Then it rears up and smashes the platform (it cracks and glows red first): get back in the boat.
    ///  - Knight in the boat: it rams up from below (red circle), spits from range, or sends a wave with a gap.
    /// Fall in the water and it hauls you back into a boat (with a bite of damage). Respawning resets the fight,
    /// the platforms and the boats. Its whole body can be hurt whenever it's above the water.
    /// Every time the knight takes away 1/N of its health (N = number of platforms), it rears up and smashes the
    /// platform he stands on (or the one nearest him): 5 seconds of cracking, then anyone not in a boat dies.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class LeviathanBoss : MonoBehaviour, IBossHitter
    {
        public enum State { Dormant, Rising, Fighting, Dead }

        [Header("Identity")]
        public string bossName = "The Leviathan";
        public string subtitle = "The beast the order fed its prisoners to";

        [Header("Links (set by the production pass)")]
        public LeviathanBody body;
        public WaterZone lake;
        public Door[] unlockOnDeath;
        public GameObject[] activateOnDeath;
        public Material telegraphMaterial, orbMaterial, waveMaterial;
        public ParticleSystem wake;
        public ParticleSystem splash;

        [Header("Movement")]
        public float activationRadius = 75f;
        public float swimSpeed = 11f;
        public float surfaceHeight = 5f;
        public float diveDepth = 6f;

        [Header("Platform attacks")]
        public float biteDamage = 22f;
        public float biteKnockback = 7f;
        public float biteWindup = 1f;
        public float stuckTime = 2.5f;
        public float smashWarning = 3f;
        [Tooltip("Seconds a platform cracks before it collapses (when a health threshold is crossed).")]
        public float platformBreakSeconds = 5f;
        public float smashDamage = 30f;

        [Header("Boat attacks")]
        public float spitDamage = 12f;
        public float spitSpeed = 12f;
        public int spitCount = 3;
        public float ramDamage = 18f;
        public float ramRadius = 4.5f;
        public float ramWindup = 1.3f;
        public float waveDamage = 18f;
        public float waveSpeed = 12f;
        public float waveWidth = 34f;
        public float waveGap = 9f;

        [Header("Pacing")]
        [Range(0f, 0.9f)] public float baseArmor = 0.4f;
        [Range(0f, 1f)] public float enragedAt = 0.4f;
        public float fightManaRegen = 10f;
        public float wispInterval = 10f;

        public State CurrentState { get; private set; } = State.Dormant;
        public bool Enraged { get; private set; }
        public bool Exposed => Time.time < exposedUntil;

        Health health;
        Transform head;
        Collider headCollider;
        Transform player;
        Health playerHealth;
        PlayerStats playerStats;
        PlayerInteractor playerInteractor;
        BreakablePlatform[] platforms;
        float savedRegen = -1f;
        Vector3 home;
        float exposedUntil, nextWisp, flashUntil;
        Renderer[] renderers;
        MaterialPropertyBlock mpb;
        GameObject telegraph;
        GUIStyle nameStyle;
        Vector3 lastHeadPos;
        readonly System.Collections.Generic.List<Collider> bodyColliders = new System.Collections.Generic.List<Collider>();
        int pendingSmashes;
        bool firstRise = true;
        static readonly string[] SmashLines =
        {
            "THE STONE REMEMBERS NOTHING. NEITHER WILL YOU.",
            "EVERY WOUND COSTS YOU GROUND.",
            "RUN TO YOUR LITTLE BOAT.",
        };
        float nextBreakAt = -1f;

        void Awake()
        {
            health = GetComponent<Health>();
            home = transform.position;
            mpb = new MaterialPropertyBlock();
            if (body != null) head = body.head;
            if (head != null) headCollider = head.GetComponent<Collider>();
            renderers = GetComponentsInChildren<Renderer>();
            health.armor = baseArmor;
            BuildBodyHitbox();
        }

        /// <summary>Gives every body segment a hit zone, so swords and spells land anywhere on the Leviathan.</summary>
        void BuildBodyHitbox()
        {
            if (body == null || body.segments == null) return;
            foreach (var seg in body.segments)
            {
                if (seg == null) continue;
                var col = seg.GetComponent<Collider>();
                if (col == null)
                {
                    float size = 0.8f;
                    foreach (var r in seg.GetComponentsInChildren<Renderer>())
                        size = Mathf.Max(size, Mathf.Max(r.bounds.extents.x, r.bounds.extents.y));
                    var sc = seg.gameObject.AddComponent<SphereCollider>();
                    sc.radius = size / Mathf.Max(0.01f, seg.lossyScale.x);
                    col = sc;
                }
                col.enabled = false;
                bodyColliders.Add(col);
                Kinematic(seg.gameObject);
            }
            if (head != null) Kinematic(head.gameObject);
        }

        /// <summary>
        /// Colliders that move every frame are cheap for the physics engine only with a kinematic rigidbody;
        /// without one, each move forces an expensive rebuild (the boss stuttered).
        /// </summary>
        static void Kinematic(GameObject go)
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
        }

        Vector3 idleOffset;

        /// <summary>A slow living sway while it's above the water, so it never looks frozen between attacks.</summary>
        void IdleSway()
        {
            if (head == null || head.position.y < 0.3f) { idleOffset = Vector3.zero; return; }
            float t = Time.time;
            Vector3 off = new Vector3(Mathf.Sin(t * 0.9f) * 0.3f, Mathf.Sin(t * 1.4f) * 0.4f, Mathf.Cos(t * 0.7f) * 0.25f);
            if (Exposed) off += Random.insideUnitSphere * 0.08f; // struggling to pull free
            head.position += off - idleOffset;
            idleOffset = off;
        }

        void Start()
        {
            platforms = FindObjectsByType<BreakablePlatform>(FindObjectsSortMode.None);
            // the body's hit zones never shove the boats around
            foreach (var b in FindObjectsByType<BoatController>(FindObjectsSortMode.None))
                foreach (var bc in b.GetComponentsInChildren<Collider>())
                    foreach (var c in bodyColliders) Physics.IgnoreCollision(bc, c, true);
            HideHead();
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            GameManager.Respawned += ResetFight;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
            GameManager.Respawned -= ResetFight;
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        }

        void FindPlayer()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            player = go.transform;
            playerHealth = go.GetComponent<Health>();
            playerStats = go.GetComponent<PlayerStats>();
            playerInteractor = go.GetComponent<PlayerInteractor>();
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;
        }

        bool InBoat() => player != null && player.GetComponentInParent<BoatController>() != null;

        BreakablePlatform PlatformUnderPlayer()
        {
            if (player == null || platforms == null) return null;
            foreach (var p in platforms) if (p != null && p.IsStandingOn(player.position)) return p;
            return null;
        }

        void Update()
        {
            if (CurrentState == State.Dead || head == null) return;
            if (player == null) { FindPlayer(); return; }

            RescueFromWater();

            if (CurrentState == State.Dormant)
            {
                if (InBoat() && lake != null && lake.ContainsXZ(player.position) && Flat(player.position - home).magnitude < activationRadius)
                    StartCoroutine(Rise());
                return;
            }

            if (wake != null)
            {
                bool swimming = head.position.y < -0.5f && (head.position - lastHeadPos).sqrMagnitude > 0.0004f;
                wake.transform.position = new Vector3(head.position.x, 0.05f, head.position.z);
                var em = wake.emission; em.enabled = swimming;
            }
            lastHeadPos = head.position;

            if (CurrentState == State.Fighting && Time.time >= nextWisp && lake != null)
            {
                nextWisp = Time.time + wispInterval;
                var b = lake.WorldBounds;
                for (int i = 0; i < 2; i++)
                    LakeWisp.Spawn(new Vector3(Random.Range(b.min.x + 6f, b.max.x - 6f), 0.7f, Random.Range(b.min.z + 6f, b.max.z - 6f)), 30f, 10f, orbMaterial);
            }
            if (CurrentState == State.Fighting) IdleSway();
            UpdateFlash();
        }

        /// <summary>
        /// A knight who ends up in the lake on foot: during the fight the black water takes him (back to the
        /// checkpoint); before it, he's hauled back into the nearest free boat.
        /// </summary>
        void RescueFromWater()
        {
            if (playerHealth == null || playerHealth.IsDead || InBoat() || lake == null) return;
            if (!lake.ContainsXZ(player.position) || player.position.y > -0.35f) return;
            if (CurrentState != State.Dormant)
            {
                Drown("The black water drags you under.");
                return;
            }
            BoatController best = null;
            float bestD = float.MaxValue;
            foreach (var b in FindObjectsByType<BoatController>(FindObjectsSortMode.None))
            {
                if (b.IsOccupied) continue;
                float d = (b.transform.position - player.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            if (best == null || playerInteractor == null) return;
            best.Interact(playerInteractor);
            playerHealth.TakeDamage(new DamageInfo(10f, player.position, gameObject, 0f));
            GameAudio.Play("splash", player.position, 0.9f);
            PrototypeHUD.Message("The black water drags you back into a boat.", 2.5f);
        }

        // ---------------- fight flow ----------------

        IEnumerator Rise()
        {
            CurrentState = State.Rising;
            if (playerStats != null && savedRegen < 0f) { savedRegen = playerStats.manaRegenPerSecond; playerStats.manaRegenPerSecond = fightManaRegen; }
            GameAudio.PlayMusic("music_boss", 1.5f);
            Cinema.Say("The Leviathan", firstRise ? "THE ONE WHO SWORE BEFORE THE WATER... YOU CAME BACK TO DROWN WITH YOUR PROMISE."
                                                  : "BACK AGAIN, LITTLE KNIGHT? THE DEEP IS PATIENT.", 6f);
            firstRise = false;
            PrototypeHUD.BigMessage(bossName.ToUpperInvariant(), subtitle + "\n\nStrike it anywhere when it surfaces. Each wound costs a platform: when one cracks, get to the boat!", 6f);

            Vector3 toward = Flat(home - player.position);
            if (toward.sqrMagnitude < 1f) toward = Vector3.forward;
            Vector3 p = ClampToLake(player.position + toward.normalized * 22f);
            head.position = new Vector3(p.x, -diveDepth, p.z);
            head.rotation = Quaternion.LookRotation(Flat(player.position - p).normalized);
            if (body != null) body.Snap();
            SetHittable(true);
            Splash(p);
            GameAudio.Play("boss_roar", p, 1f, 0f, 0.7f);
            CameraShake.Shake(0.4f, 2f);
            if (body != null) body.jawOpen = 1f;
            yield return StartCoroutine(MoveHeadTimed(p + Vector3.up * surfaceHeight * 1.4f, 1.6f, true));
            yield return new WaitForSeconds(1f);
            if (body != null) body.jawOpen = 0f;
            CurrentState = State.Fighting;
            nextWisp = Time.time + 4f;
            StartCoroutine(FightLoop());
        }

        IEnumerator FightLoop()
        {
            while (CurrentState != State.Dead)
            {
                if (player == null || playerHealth == null || playerHealth.IsDead) { yield return null; continue; }
                if (pendingSmashes > 0)
                {
                    var target = PlatformUnderPlayer() ?? NearestPlatform();
                    pendingSmashes--;
                    if (target != null) { yield return StartCoroutine(BreakPlatform(target)); continue; }
                }
                var plat = PlatformUnderPlayer();
                if (plat != null && !InBoat()) yield return StartCoroutine(Siege(plat));
                else yield return StartCoroutine(Hunt());
                yield return new WaitForSeconds(Enraged ? 0.6f : 1.2f);
            }
        }

        IEnumerator Siege(BreakablePlatform plat)
        {
            Vector3 c = plat.transform.position;
            Vector3 from = Flat(head.position - c);
            if (from.sqrMagnitude < 1f) from = Vector3.back;
            Vector3 side = c + from.normalized * 10.5f;
            side.y = 0f;

            yield return StartCoroutine(Dive());
            yield return StartCoroutine(MoveHead(new Vector3(side.x, -diveDepth, side.z), swimSpeed));
            Splash(side);
            yield return StartCoroutine(MoveHeadTimed(side + Vector3.up * surfaceHeight, 0.8f, true));
            SetHittable(true);

            int attacks = Enraged ? 3 : 2;
            for (int i = 0; i < attacks; i++)
            {
                if (!plat.IsStandingOn(player.position) || InBoat()) break;
                if (Random.value < 0.6f) yield return StartCoroutine(Bite(side));
                else yield return StartCoroutine(Spit(Enraged ? 2 : 1));
                yield return new WaitForSeconds(0.6f);
            }
            yield return StartCoroutine(Dive());
        }

        IEnumerator Bite(Vector3 rest)
        {
            Vector3 target = player.position;
            Vector3 start = head.position;
            Vector3 dir = Flat(target - start);
            if (dir.sqrMagnitude < 0.01f) dir = head.forward;
            dir.Normalize();
            float len = Mathf.Min(Flat(target - start).magnitude + 1.5f, 13f);
            Vector3 end = new Vector3(start.x, target.y + 1f, start.z) + dir * len;

            Vector3 windPos = start - dir * 1.5f + Vector3.up * 1.5f;
            for (float t = 0f; t < biteWindup; t += Time.deltaTime)
            {
                float k = t / biteWindup;
                head.position = Vector3.Lerp(start, windPos, Mathf.SmoothStep(0f, 1f, k));
                head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(dir + Vector3.down * 0.3f), 6f * Time.deltaTime);
                if (body != null) body.jawOpen = k;
                ShowStrip(new Vector3(start.x, target.y + 0.06f, start.z), new Vector3(end.x, target.y + 0.06f, end.z), k);
                yield return null;
            }
            HideTelegraph();
            GameAudio.Play("boss_cleave", head.position, 1f, 0.05f, 0.8f);
            yield return StartCoroutine(MoveHeadTimed(end, 0.22f, false));
            if (body != null) body.jawOpen = 0f;
            CameraShake.Shake(0.3f, 0.3f);
            if (player != null && Flat(player.position - end).magnitude < 2.4f && Mathf.Abs(player.position.y + 1f - end.y) < 2.5f)
                playerHealth.TakeDamage(new DamageInfo(biteDamage, head.position - dir * 2f, gameObject, biteKnockback));
            yield return StartCoroutine(MoveHeadTimed(end + Vector3.down * 0.25f, 0.12f, false));

            // stuck on the stone: the opening to hit it
            exposedUntil = Time.time + stuckTime;
            health.armor = 0f;
            PrototypeHUD.Message("Its head is stuck - strike now!", 2f);
            Vector3 stuck = head.position;
            for (float t = 0f; t < stuckTime; t += Time.deltaTime)
            {
                head.position = stuck + new Vector3(Mathf.Sin(t * 37f), 0f, Mathf.Cos(t * 31f)) * 0.06f;
                yield return null;
            }
            health.armor = baseArmor;
            yield return StartCoroutine(MoveHeadTimed(rest + Vector3.up * surfaceHeight, 0.7f, true));
        }

        IEnumerator Spit(int volleys)
        {
            for (int v = 0; v < volleys; v++)
            {
                if (body != null) body.jawOpen = 0.7f;
                for (float t = 0f; t < 0.5f; t += Time.deltaTime) { Face(player.position); yield return null; }
                int count = spitCount + (Enraged ? 2 : 0);
                Vector3 origin = head.position + head.forward * 1.6f;
                Vector3 baseDir = (player.position + Vector3.up * 0.8f - origin).normalized;
                for (int i = 0; i < count; i++)
                {
                    float a = count == 1 ? 0f : Mathf.Lerp(-18f, 18f, i / (float)(count - 1));
                    InquisitorOrb.Launch(origin, Quaternion.AngleAxis(a, Vector3.up) * baseDir * spitSpeed, spitDamage, orbMaterial, this, player);
                }
                GameAudio.Play("enemy_attack", head.position, 1f, 0.05f, 0.6f);
                if (body != null) body.jawOpen = 0f;
                yield return new WaitForSeconds(0.6f);
            }
        }

        BreakablePlatform NearestPlatform()
        {
            BreakablePlatform best = null;
            float bestD = float.MaxValue;
            if (platforms == null || player == null) return null;
            foreach (var p in platforms)
            {
                if (p == null || p.IsBroken || p.Breaking) continue;
                float d = Flat(p.transform.position - player.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        /// <summary>Surfaces beside a platform and smashes it (a health threshold was crossed).</summary>
        IEnumerator BreakPlatform(BreakablePlatform plat)
        {
            Vector3 c = plat.transform.position;
            Vector3 from = Flat(head.position - c);
            if (from.sqrMagnitude < 1f) from = Vector3.back;
            Vector3 side = c + from.normalized * 10.5f;
            side.y = 0f;
            yield return StartCoroutine(Dive());
            yield return StartCoroutine(MoveHead(new Vector3(side.x, -diveDepth, side.z), swimSpeed * 1.3f));
            Splash(side);
            yield return StartCoroutine(MoveHeadTimed(side + Vector3.up * surfaceHeight, 0.8f, true));
            SetHittable(true);
            yield return StartCoroutine(Smash(plat, side));
            yield return StartCoroutine(Dive());
        }

        IEnumerator Smash(BreakablePlatform plat, Vector3 rest)
        {
            Vector3 c = plat.transform.position;
            float warn = Mathf.Max(3f, platformBreakSeconds);
            Cinema.Say("The Leviathan", SmashLines[Random.Range(0, SmashLines.Length)], 3.5f);
            plat.BeginBreak(warn);
            GameAudio.Play("boss_roar", head.position, 1f, 0f, 0.7f);
            if (body != null) body.jawOpen = 1f;
            float start = Time.time;
            int shown = -1;
            yield return StartCoroutine(MoveHeadTimed(rest + Vector3.up * (surfaceHeight + 4f), 1f, false));
            while (Time.time - start < warn - 0.35f)
            {
                Face(c);
                int left = Mathf.CeilToInt(warn - (Time.time - start));
                if (left != shown)
                {
                    shown = left;
                    bool threatened = player != null && plat.IsStandingOn(player.position) && !InBoat();
                    PrototypeHUD.BigMessage(threatened ? "GET TO THE BOAT!" : "A platform is breaking", left + "", 1.1f);
                }
                yield return null;
            }
            if (body != null) body.jawOpen = 0f;
            yield return StartCoroutine(MoveHeadTimed(c + Vector3.up * 1.2f, 0.35f, false));
            plat.Collapse();
            GameAudio.Play("boss_slam", c, 1f, 0.03f);
            CameraShake.Shake(0.5f, 0.8f);
            Splash(c);
            // anyone still on it (or swimming) and not in a boat is lost
            if (player != null && !InBoat() && (plat.IsStandingOn(player.position) || (lake != null && lake.ContainsXZ(player.position) && PlatformUnderPlayer() == null)))
                Drown("The platform sinks beneath you.");
            yield return new WaitForSeconds(0.5f);
        }

        /// <summary>The knight dies (back to the last checkpoint); god mode still protects him.</summary>
        void Drown(string why)
        {
            if (playerHealth == null || playerHealth.IsDead || playerHealth.godMode) return;
            GameAudio.Play("splash", player.position, 1f);
            PrototypeHUD.Message(why, 3f);
            playerHealth.Kill();
        }

        IEnumerator Hunt()
        {
            float r = Random.value;
            if (r < 0.45f) yield return StartCoroutine(Ram());
            else if (r < 0.75f) yield return StartCoroutine(RangedSpit());
            else yield return StartCoroutine(Wave());
        }

        IEnumerator Ram()
        {
            yield return StartCoroutine(Dive());
            float speed = swimSpeed * (Enraged ? 1.3f : 1f);
            for (float t = 0f; t < 5f; t += Time.deltaTime)
            {
                if (player == null) yield break;
                Vector3 goal = new Vector3(player.position.x, -diveDepth, player.position.z);
                if (Flat(goal - head.position).magnitude < 3f) break;
                Step(goal, speed);
                yield return null;
            }
            Vector3 target = player.position;
            float wind = Enraged ? ramWindup * 0.75f : ramWindup;
            for (float t = 0f; t < wind; t += Time.deltaTime)
            {
                if (t < wind * 0.7f && player != null) target = player.position;
                ShowCircle(target, ramRadius * Mathf.Clamp01(t / wind * 1.2f));
                Step(new Vector3(target.x, -diveDepth, target.z), speed);
                yield return null;
            }
            HideTelegraph();
            head.position = new Vector3(target.x, -diveDepth, target.z);
            head.rotation = Quaternion.LookRotation(Vector3.up + head.forward * 0.3f);
            Splash(target);
            GameAudio.Play("boss_slam", target, 1f, 0.03f, 0.9f);
            CameraShake.Shake(0.5f, 0.6f);
            if (body != null) body.jawOpen = 1f;
            yield return StartCoroutine(MoveHeadTimed(new Vector3(target.x, surfaceHeight, target.z), 0.35f, false));
            if (player != null && Flat(player.position - target).magnitude <= ramRadius)
                HitPlayer(ramDamage, Flat(player.position - target).normalized, 12f);
            if (body != null) body.jawOpen = 0f;
            exposedUntil = Time.time + 2f;
            health.armor = 0f;
            for (float t = 0f; t < 2f; t += Time.deltaTime) { if (player != null) Face(player.position); yield return null; }
            health.armor = baseArmor;
            yield return StartCoroutine(Dive());
        }

        IEnumerator RangedSpit()
        {
            yield return StartCoroutine(SurfaceNear(16f));
            yield return StartCoroutine(Spit(Enraged ? 2 : 1));
            yield return new WaitForSeconds(0.4f);
            yield return StartCoroutine(Dive());
        }

        IEnumerator Wave()
        {
            yield return StartCoroutine(SurfaceNear(20f));
            if (body != null) body.jawOpen = 1f;
            Vector3 rear = head.position + Vector3.up * 3f;
            yield return StartCoroutine(MoveHeadTimed(rear, 0.8f, false));
            Vector3 hit = new Vector3(head.position.x, 0.2f, head.position.z) + Flat(player.position - head.position).normalized * 3f;
            yield return StartCoroutine(MoveHeadTimed(hit, 0.3f, false));
            if (body != null) body.jawOpen = 0f;
            Splash(hit);
            GameAudio.Play("boss_slam", hit, 1f, 0.03f);
            CameraShake.Shake(0.3f, 0.5f);
            int waves = Enraged ? 2 : 1;
            for (int k = 0; k < waves; k++)
            {
                if (player == null) break;
                Vector3 dir = Flat(player.position - hit).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                float half = waveWidth * 0.5f;
                TidalWave.Spawn(hit + dir * 2f, dir, waveSpeed, waveWidth, Random.Range(-half + waveGap, half - waveGap), waveGap, waveDamage, 90f, waveMaterial, this, player);
                if (k < waves - 1) yield return new WaitForSeconds(1.4f);
            }
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(Dive());
        }

        IEnumerator SurfaceNear(float distance)
        {
            if (head.position.y > -1f) yield return StartCoroutine(Dive());
            Vector3 toward = Flat(home - player.position);
            if (toward.sqrMagnitude < 1f) toward = Flat(head.position - player.position);
            if (toward.sqrMagnitude < 0.01f) toward = Vector3.forward;
            Vector3 p = ClampToLake(player.position + toward.normalized * distance);
            yield return StartCoroutine(MoveHead(new Vector3(p.x, -diveDepth, p.z), swimSpeed * 1.4f));
            Splash(p);
            SetHittable(true);
            yield return StartCoroutine(MoveHeadTimed(new Vector3(p.x, surfaceHeight, p.z), 0.7f, true));
            for (float t = 0f; t < 0.3f; t += Time.deltaTime) { Face(player.position); yield return null; }
        }

        IEnumerator Dive()
        {
            if (head.position.y < -diveDepth + 0.5f) yield break;
            Splash(head.position);
            Vector3 down = new Vector3(head.position.x, -diveDepth, head.position.z) + Flat(head.forward) * 3f;
            yield return StartCoroutine(MoveHeadTimed(down, 1f, true));
            SetHittable(false);
        }

        // ---------------- movement helpers ----------------

        IEnumerator MoveHead(Vector3 target, float speed)
        {
            for (float guard = 0f; guard < 12f && (head.position - target).sqrMagnitude > 0.09f; guard += Time.deltaTime)
            {
                Step(target, speed);
                yield return null;
            }
        }

        void Step(Vector3 target, float speed)
        {
            Vector3 next = Vector3.MoveTowards(head.position, target, speed * Time.deltaTime);
            Vector3 dir = next - head.position;
            if (dir.sqrMagnitude > 0.00001f) head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(dir), 6f * Time.deltaTime);
            head.position = next;
        }

        IEnumerator MoveHeadTimed(Vector3 target, float duration, bool faceMotion)
        {
            Vector3 from = head.position;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                Vector3 next = Vector3.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / duration));
                Vector3 dir = next - head.position;
                if (faceMotion && dir.sqrMagnitude > 0.00001f) head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
                else if (!faceMotion && player != null) Face(player.position);
                head.position = next;
                yield return null;
            }
            head.position = target;
        }

        void Face(Vector3 point)
        {
            Vector3 to = point - head.position;
            if (to.sqrMagnitude > 0.01f) head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(to), 5f * Time.deltaTime);
        }

        Vector3 ClampToLake(Vector3 p)
        {
            if (lake == null) return p;
            var b = lake.WorldBounds;
            return new Vector3(Mathf.Clamp(p.x, b.min.x + 6f, b.max.x - 6f), 0f, Mathf.Clamp(p.z, b.min.z + 6f, b.max.z - 6f));
        }

        void Splash(Vector3 at)
        {
            if (splash != null)
            {
                splash.transform.position = new Vector3(at.x, 0.05f, at.z);
                splash.Play();
            }
            var lib = FVLibrary.Get;
            if (lib != null && lib.splash != null) MagicProjectile.PlayEffect(lib.splash, new Vector3(at.x, 0f, at.z), 1.6f);
            GameAudio.Play("splash", at, 0.9f);
        }

        void HideHead()
        {
            if (head == null) return;
            head.position = home + Vector3.down * (diveDepth + 4f);
            head.rotation = transform.rotation;
            if (body != null) { body.jawOpen = 0f; body.Snap(); }
            SetHittable(false);
        }

        /// <summary>True while it's above the water and can be struck (companions attack it then).</summary>
        public bool Hittable { get; private set; }

        /// <summary>The closest point of its body (head or any segment) to 'from', for companions to aim at.</summary>
        public Vector3 NearestHitPoint(Vector3 from)
        {
            Vector3 best = head != null ? head.position : transform.position;
            float bestD = (best - from).sqrMagnitude;
            foreach (var c in bodyColliders)
            {
                if (c == null || !c.enabled) continue;
                Vector3 p = c.ClosestPoint(from);
                float d = (p - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = p; }
            }
            if (headCollider != null && headCollider.enabled)
            {
                Vector3 p = headCollider.ClosestPoint(from);
                if ((p - from).sqrMagnitude < bestD) best = p;
            }
            return best;
        }

        void SetHittable(bool on)
        {
            Hittable = on && CurrentState != State.Dead;
            if (headCollider != null) headCollider.enabled = on;
            foreach (var c in bodyColliders) if (c != null) c.enabled = on;
        }

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

        // ---------------- telegraphs ----------------

        void ShowCircle(Vector3 center, float radius)
        {
            Ensure(PrimitiveType.Cylinder);
            telegraph.transform.SetPositionAndRotation(new Vector3(center.x, -0.06f, center.z), Quaternion.identity);
            telegraph.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
        }

        void ShowStrip(Vector3 from, Vector3 to, float grow)
        {
            Ensure(PrimitiveType.Cube);
            Vector3 d = to - from;
            float len = Mathf.Max(0.1f, d.magnitude * Mathf.Clamp01(grow * 1.3f));
            telegraph.transform.SetPositionAndRotation(from + d.normalized * len * 0.5f, Quaternion.LookRotation(Flat(d).normalized));
            telegraph.transform.localScale = new Vector3(2.4f, 0.02f, len);
        }

        void Ensure(PrimitiveType type)
        {
            string wanted = "Leviathan Telegraph " + type;
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

        // ---------------- damage, death, reset ----------------

        public void HitPlayer(float amount, Vector3 direction, float shove)
        {
            if (playerHealth == null || playerHealth.IsDead) return;
            bool boat = InBoat();
            playerHealth.TakeDamage(new DamageInfo(amount, head != null ? head.position : transform.position, gameObject, boat ? 0f : shove * 0.5f));
            if (boat && shove > 0f && direction.sqrMagnitude > 0.001f)
            {
                var b = player.GetComponentInParent<BoatController>();
                b.GetComponent<Rigidbody>().AddForce(direction.normalized * shove, ForceMode.VelocityChange);
            }
            CameraShake.Shake(0.25f, 0.3f);
        }

        void OnDamaged(DamageInfo info)
        {
            flashUntil = Time.time + 0.1f;
            // every 1/N of its health lost (N = platforms), it will break a platform
            int n = platforms != null ? platforms.Length : 0;
            if (n > 0)
            {
                float step = 1f / n;
                if (nextBreakAt < 0f) nextBreakAt = 1f - step;
                while (health.Normalized <= nextBreakAt + 0.0001f && nextBreakAt > 0.001f)
                {
                    pendingSmashes++;
                    nextBreakAt -= step;
                }
            }
            if (!Enraged && health.Normalized <= enragedAt)
            {
                Enraged = true;
                PrototypeHUD.Message("The Leviathan is enraged!", 3f);
                Cinema.Say("The Leviathan", "I HAVE SWALLOWED KINGDOMS. I WILL SWALLOW YOUR NAME.");
                GameAudio.Play("boss_roar", head != null ? head.position : transform.position, 1f, 0f, 0.65f);
                CameraShake.Shake(0.3f, 0.8f);
            }
        }

        void OnDied()
        {
            if (GameManager.Instance != null) GameManager.Instance.CheckpointHereSoon(3f); // the victory is kept
            StopAllCoroutines();
            HideTelegraph();
            ClearHazards();
            Cinema.Say("The Leviathan", "...THE VOW... WAS NEVER... YOURS... TO BREAK...", 7f);
            RestoreRegen();
            CurrentState = State.Dead;
            if (wake != null) { var em = wake.emission; em.enabled = false; }
            if (unlockOnDeath != null) foreach (var d in unlockOnDeath) if (d != null) d.Unlock(true);
            if (activateOnDeath != null) foreach (var g in activateOnDeath) if (g != null) g.SetActive(true);
            PrototypeHUD.BigMessage("THE LEVIATHAN FALLS", "The deep goes still. The north gate grinds open,\nand the last memory waits on the pier.", 6f);
            GameAudio.Play("boss_roar", head != null ? head.position : transform.position, 1f, 0f, 0.5f);
            GameAudio.PlayUI("victory_sting");
            GameAudio.PlayMusic("music_dungeon", 4f);
            StartCoroutine(DeathThrash());
        }

        IEnumerator DeathThrash()
        {
            if (head == null) yield break;
            if (body != null) body.jawOpen = 1f;
            CameraShake.Shake(0.5f, 2.5f);
            Vector3 up = new Vector3(head.position.x, surfaceHeight + 5f, head.position.z);
            yield return StartCoroutine(MoveHeadTimed(up, 1.2f, false));
            Vector3 down = new Vector3(head.position.x, 0f, head.position.z) + Flat(head.forward) * 6f;
            yield return StartCoroutine(MoveHeadTimed(down, 0.6f, true));
            Splash(down);
            SetHittable(false);
            yield return StartCoroutine(MoveHeadTimed(down + Vector3.down * 12f, 6f, true));
        }

        void OnPlayerDied()
        {
            if (CurrentState != State.Fighting && CurrentState != State.Rising) return;
            Cinema.Say("The Leviathan", "SINK, LITTLE KNIGHT. THE DEEP WILL REMEMBER FOR YOU.");
            StopAllCoroutines();
            HideTelegraph();
            ClearHazards();
            StartCoroutine(Dive());
        }

        /// <summary>Called when the knight respawns: the Leviathan sleeps again at full health (unless it's dead).</summary>
        public void ResetFight()
        {
            if (CurrentState == State.Dead) return;
            StopAllCoroutines();
            HideTelegraph();
            ClearHazards();
            RestoreRegen();
            health.Revive();
            health.armor = baseArmor;
            pendingSmashes = 0;
            nextBreakAt = -1f;
            Enraged = false;
            exposedUntil = 0f;
            CurrentState = State.Dormant;
            HideHead();
            if (wake != null) { var em = wake.emission; em.enabled = false; }
        }

        void RestoreRegen()
        {
            if (playerStats != null && savedRegen >= 0f) playerStats.manaRegenPerSecond = savedRegen;
            savedRegen = -1f;
        }

        static void ClearHazards()
        {
            foreach (var o in FindObjectsByType<InquisitorOrb>(FindObjectsSortMode.None)) Destroy(o.gameObject);
            foreach (var o in FindObjectsByType<TidalWave>(FindObjectsSortMode.None)) Destroy(o.gameObject);
            foreach (var o in FindObjectsByType<LakeWisp>(FindObjectsSortMode.None)) Destroy(o.gameObject);
        }

        void UpdateFlash()
        {
            bool flash = Time.time < flashUntil;
            bool glow = Exposed;
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                mpb.Clear();
                if (flash) { mpb.SetColor("_BaseColor", Color.white); mpb.SetColor("_Color", Color.white); }
                else if (glow)
                {
                    var c = Color.Lerp(Color.white, new Color(1f, 0.85f, 0.4f), Mathf.PingPong(Time.time * 4f, 1f));
                    mpb.SetColor("_BaseColor", c); mpb.SetColor("_Color", c);
                }
                r.SetPropertyBlock(mpb);
            }
        }

        void OnGUI()
        {
            if (CurrentState == State.Dormant || CurrentState == State.Dead || UIFocus.Any || PrototypeHUD.Hidden) return;
            float scale = Screen.height / 900f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                nameStyle.normal.textColor = new Color(0.75f, 1f, 0.95f);
            }
            float bw = 660f, bx = (w - bw) / 2f, by = h - 120f;
            string tag = Exposed ? "  (EXPOSED)" : Enraged ? "  (enraged)" : "";
            GUI.Label(new Rect(bx, by - 28f, bw, 24f), bossName + tag, nameStyle);
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f); GUI.DrawTexture(new Rect(bx - 2f, by - 2f, bw + 4f, 18f), Texture2D.whiteTexture);
            GUI.color = Exposed ? new Color(1f, 0.85f, 0.3f) : new Color(0.1f, 0.6f, 0.55f);
            GUI.DrawTexture(new Rect(bx, by, bw * health.Normalized, 14f), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
