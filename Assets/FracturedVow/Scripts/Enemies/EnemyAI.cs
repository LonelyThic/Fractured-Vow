using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Husked sentinel AI: Patrol (or stand guard) -> Alert when the knight is seen/heard -> Chase -> Attack.
    /// Attacks are telegraphed (body flashes white during the wind-up) and a sword hit during the wind-up
    /// staggers the enemy, matching the GDD ("light slash interrupts windups"). Line of sight is checked
    /// with a raycast, so pillars and Stone Walls break detection. No NavMesh baking needed.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(Health))]
    public class EnemyAI : MonoBehaviour, ISlowable
    {
        public enum State { Idle, Patrol, Alert, Chase, Attack, Stagger, Dead }

        public static readonly List<EnemyAI> All = new List<EnemyAI>();

        [Header("Patrol")]
        public Transform[] patrolPoints;
        public float patrolSpeed = 1.8f;
        public float waitAtPoint = 1.5f;

        [Header("Detection")]
        public float sightRange = 11f;
        public float fieldOfView = 120f;
        public float hearingRange = 3.5f;
        public float loseSightRange = 18f;
        public float eyeHeight = 1.6f;
        public float alertDuration = 0.6f;
        public float memoryTime = 3f;

        [Header("Chase & Attack")]
        public float chaseSpeed = 3.6f;
        public float turnSpeed = 8f;
        public float attackRange = 1.9f;
        public float attackDamage = 12f;
        public float attackWindup = 0.55f;
        public float attackRecovery = 0.5f;
        public float attackCooldown = 1.2f;
        public float attackKnockback = 4f;
        public float staggerTime = 0.6f;

        [Header("Dodging")]
        [Tooltip("Chance to sidestep when the knight swings at close range (Elites dodge twice as often).")]
        [Range(0f, 1f)] public float dodgeChance = 0.2f;
        [HideInInspector] public bool elite;

        [Header("Ranged (necromancers)")]
        [Tooltip("Keeps its distance and casts bolts instead of swinging.")]
        public bool ranged;
        public float rangedRange = 13f;
        public float preferredRange = 7.5f;
        public Color boltColor = new Color(0.65f, 0.35f, 1f);

        [Header("Visuals")]
        public Renderer bodyRenderer;
        public Color baseColor = new Color(0.85f, 0.1f, 0.1f);
        public Color alertColor = new Color(1f, 0.45f, 0.05f);
        public Color windupColor = Color.white;
        public Color slowColor = new Color(0.45f, 0.7f, 1f);

        public State CurrentState { get; private set; }

        CharacterController cc;
        Health health;
        AnimatorBridge anim;
        Transform player;
        Health playerHealth;
        MaterialPropertyBlock block;

        int patrolIndex;
        float waitUntil;
        bool waiting;
        float stateTime;
        float lastSeenTime = -99f;
        Vector3 lastKnownPos;
        float nextAttackTime;
        float verticalVelocity;
        Vector3 knockback;
        Vector3 desiredVelocity;
        float slowMultiplier = 1f, slowUntil;
        float flashUntil;
        bool inWindup;
        Coroutine attackRoutine;
        Quaternion idleFacing;
        float staggerEnd, vulnerableUntil, nextDodge;
        bool asleep, sightCache;
        float nextSight;
        ProceduralMotion fx;

        public bool IsVulnerable => Time.time < vulnerableUntil;
        public void MakeVulnerable(float seconds) { vulnerableUntil = Time.time + seconds; }

        /// <summary>Knocks the husk off balance (heavy hits, parries): any attack is cancelled.</summary>
        public void Stagger(float seconds)
        {
            if (CurrentState == State.Dead) return;
            if (attackRoutine != null) StopCoroutine(attackRoutine);
            attackRoutine = null;
            inWindup = false;
            staggerEnd = Time.time + seconds;
            nextAttackTime = staggerEnd + 0.3f;
            Enter(State.Stagger);
        }

        void OnPlayerSwing(Vector3 from, bool heavy)
        {
            if (health.IsDead || (CurrentState != State.Chase && CurrentState != State.Alert)) return;
            if (Time.time < nextDodge) return;
            Vector3 away = transform.position - from; away.y = 0f;
            if (away.magnitude > 3.2f) return;
            float chance = (elite ? dodgeChance * 2f : dodgeChance) * (heavy ? 1.3f : 1f);
            if (Random.value > chance) return;
            nextDodge = Time.time + 2.5f;
            Vector3 side = Vector3.Cross(Vector3.up, away.normalized) * (Random.value < 0.5f ? 1f : -1f);
            knockback += (away.normalized * 0.6f + side).normalized * 11f;
            health.SetInvulnerable(0.3f);
            if (fx == null) fx = GetComponent<ProceduralMotion>();
            if (fx != null) { fx.Hop(0.3f, 0.35f); fx.Roll(0.35f, true); }
            if (anim != null) anim.Trigger("Dodge");
            GameAudio.Play("jump", transform.position + Vector3.up * 0.5f, 0.5f, 0.1f, 1.1f);
            GameFX.Dust(transform.position + Vector3.up * 0.1f, 0.5f, 8);
        }

        /// <summary>Every husk in the scene, alive or dead (dead ones are hidden, not destroyed, so they can come back).</summary>
        public static readonly List<EnemyAI> Registry = new List<EnemyAI>();
        Vector3 spawnPos;
        Quaternion spawnRot;
        float groundY;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = GetComponent<Health>();
            anim = GetComponent<AnimatorBridge>();
            if (anim != null && anim.animator != null) anim.animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms; // no animation work off screen
            block = new MaterialPropertyBlock();
            idleFacing = transform.rotation;
            spawnPos = transform.position;
            spawnRot = transform.rotation;
            groundY = spawnPos.y;
            Registry.Add(this);
        }

        void OnDestroy() { Registry.Remove(this); }

        /// <summary>Brings every husk back to its post, alive, at full health (used when the knight respawns).</summary>
        public static void ResetAll()
        {
            foreach (var e in Registry.ToArray())
            {
                if (e == null) continue;
                // killed before the last checkpoint: stays dead
                if (e.health != null && e.health.IsDead && !WorldLog.ChangedSinceCheckpoint(e)) continue;
                e.ResetToSpawn();
            }
        }

        public void ResetToSpawn()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            StopAllCoroutines();
            cc.enabled = false;
            transform.SetPositionAndRotation(spawnPos, spawnRot);
            cc.enabled = true;
            health.Revive();
            if (anim != null) anim.ResetAnimator();
            knockback = Vector3.zero; desiredVelocity = Vector3.zero; verticalVelocity = 0f;
            slowUntil = 0f; flashUntil = 0f; inWindup = false; attackRoutine = null;
            nextAttackTime = 0f; lastSeenTime = -99f; waiting = false; patrolIndex = 0;
            idleFacing = spawnRot; groundY = spawnPos.y;
            Enter(patrolPoints != null && patrolPoints.Length > 0 ? State.Patrol : State.Idle);
            RuntimeVisuals.SetRendererColor(bodyRenderer, baseColor, block);
        }

        void OnEnable()
        {
            All.Add(this);
            PlayerCombat.SwingStarted += OnPlayerSwing;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            All.Remove(this);
            PlayerCombat.SwingStarted -= OnPlayerSwing;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        void Start()
        {
            Enter(patrolPoints != null && patrolPoints.Length > 0 ? State.Patrol : State.Idle);
        }

        Transform realPlayer;
        Health realPlayerHealth;
        float nextRetarget;
        static Material boltMat;

        void FindPlayer()
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;
            realPlayer = player = go.transform;
            realPlayerHealth = playerHealth = go.GetComponent<Health>();
        }

        /// <summary>Fights whichever is closer: the knight or one of his companions.</summary>
        void Retarget()
        {
            if (Time.time < nextRetarget || realPlayer == null) return;
            nextRetarget = Time.time + 0.5f;
            Transform best = realPlayer;
            Health bestH = realPlayerHealth;
            bool playerUp = realPlayerHealth == null || !realPlayerHealth.IsDead;
            float bestD = playerUp ? (realPlayer.position - transform.position).magnitude - 1.5f : float.MaxValue;
            foreach (var c in CompanionAI.All)
            {
                if (c == null || c.Health.IsDead) continue;
                float d = (c.transform.position - transform.position).magnitude;
                if (d < 9f && d < bestD) { bestD = d; best = c.transform; bestH = c.Health; }
            }
            player = best;
            playerHealth = bestH;
        }

        void Update()
        {
            if (CurrentState == State.Dead) return;
            if (realPlayer == null) FindPlayer();

            // far-away husks that aren't hunting anyone sleep (no AI, no animation) until the knight comes within 45 m
            if (realPlayer != null && (CurrentState == State.Idle || CurrentState == State.Patrol))
            {
                bool far = (realPlayer.position - transform.position).sqrMagnitude > 45f * 45f;
                asleep = far;
                if (far)
                {
                    if (anim != null) anim.SetFloat("Speed", 0f, 0.2f); // stand idle (still animated) while far away
                    return;
                }
            }
            else asleep = false;

            if (CurrentState != State.Attack) Retarget();

            bool playerAlive = player != null && (playerHealth == null || !playerHealth.IsDead);
            if (Time.time >= nextSight)
            {
                nextSight = Time.time + 0.14f + Random.value * 0.04f; // staggered, so they don't all check on the same frame
                sightCache = playerAlive && CanSeePlayer();
            }
            bool canSee = playerAlive && sightCache;
            if (canSee) { lastSeenTime = Time.time; lastKnownPos = player.position; }

            desiredVelocity = Vector3.zero;

            switch (CurrentState)
            {
                case State.Idle:
                    if (canSee) { Enter(State.Alert); break; }
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        idleFacing * Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.6f) * 35f, 0f), 2f * Time.deltaTime);
                    break;

                case State.Patrol:
                    if (canSee) { Enter(State.Alert); break; }
                    Patrol();
                    break;

                case State.Alert:
                    Face(lastKnownPos);
                    if (Time.time - stateTime >= alertDuration) Enter(State.Chase);
                    break;

                case State.Chase:
                    if (!playerAlive || Time.time - lastSeenTime > memoryTime)
                    {
                        Enter(patrolPoints != null && patrolPoints.Length > 0 ? State.Patrol : State.Idle);
                        break;
                    }
                    Vector3 to = (canSee ? player.position : lastKnownPos) - transform.position;
                    to.y = 0f;
                    if (ranged)
                    {
                        Face(transform.position + to);
                        if (canSee && to.magnitude <= rangedRange && Time.time >= nextAttackTime) { attackRoutine = StartCoroutine(AttackRoutine()); break; }
                        if (to.magnitude > preferredRange) desiredVelocity = to.normalized * chaseSpeed;
                        else if (to.magnitude < preferredRange - 3f) desiredVelocity = -to.normalized * chaseSpeed * 0.7f;
                        break;
                    }
                    if (canSee && to.magnitude <= attackRange && Time.time >= nextAttackTime)
                    {
                        attackRoutine = StartCoroutine(AttackRoutine());
                        break;
                    }
                    if (to.magnitude > 0.8f)
                    {
                        desiredVelocity = to.normalized * chaseSpeed;
                        Face(transform.position + to);
                    }
                    break;

                case State.Attack:
                    if (inWindup && player != null) Face(player.position);
                    break;

                case State.Stagger:
                    if (Time.time >= staggerEnd) Enter(State.Chase);
                    break;
            }

            Move();
            UpdateTint();
            if (anim != null) anim.SetFloat("Speed", desiredVelocity.magnitude / chaseSpeed, 0.1f);
            if (fx == null) fx = GetComponent<ProceduralMotion>();
            if (fx != null) fx.Move01 = desiredVelocity.magnitude / Mathf.Max(0.1f, chaseSpeed);
            if (fx == null) fx = GetComponent<ProceduralMotion>();
            if (fx != null) fx.Move = desiredVelocity.magnitude / chaseSpeed;
        }

        void Patrol()
        {
            if (patrolPoints == null || patrolPoints.Length == 0) return;
            Transform p = patrolPoints[patrolIndex];
            if (p == null) return;
            Vector3 to = p.position - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.4f)
            {
                if (!waiting) { waiting = true; waitUntil = Time.time + waitAtPoint; }
                else if (Time.time >= waitUntil)
                {
                    waiting = false;
                    patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                }
                return;
            }
            desiredVelocity = to.normalized * patrolSpeed;
            Face(p.position);
        }

        bool CanSeePlayer()
        {
            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 target = player.position + Vector3.up * 1f;
            Vector3 to = target - eye;
            float dist = to.magnitude;

            if (dist <= hearingRange) return true;

            bool hunting = CurrentState == State.Chase || CurrentState == State.Attack || CurrentState == State.Alert;
            if (dist > (hunting ? loseSightRange : sightRange)) return false;

            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (!hunting && Vector3.Angle(transform.forward, flat) > fieldOfView * 0.5f) return false;

            if (Physics.Raycast(eye, to / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore))
                return hit.transform.IsChildOf(player);
            return true;
        }

        IEnumerator AttackRoutine()
        {
            Enter(State.Attack);
            inWindup = true;
            if (anim != null) anim.Trigger("Attack");
            GameAudio.Play("enemy_attack", transform.position + Vector3.up * 1.5f, 0.75f);
            yield return new WaitForSeconds(attackWindup);
            inWindup = false;

            if (CurrentState != State.Attack) yield break;

            if (ranged)
            {
                if (player != null && playerHealth != null && !playerHealth.IsDead)
                {
                    if (boltMat == null) boltMat = Resources.Load<Material>("FVFX/FVP_FX_Additive");
                    Vector3 origin = transform.position + Vector3.up * 1.5f + transform.forward * 0.7f;
                    Vector3 dir = (player.position + Vector3.up * 1f - origin).normalized;
                    MagicProjectile.Spawn(origin, dir, gameObject, boltColor, boltMat, attackDamage, 2f, 1f, 0f, 13f, 0.32f);
                    GameAudio.Play("spell_cast", origin, 0.7f, 0.05f, 0.8f);
                }
                yield return new WaitForSeconds(attackRecovery);
                nextAttackTime = Time.time + attackCooldown * 1.6f;
                attackRoutine = null;
                if (CurrentState == State.Attack) Enter(State.Chase);
                yield break;
            }

            if (player != null && playerHealth != null && !playerHealth.IsDead)
            {
                Vector3 to = player.position - transform.position;
                to.y = 0f;
                if (to.magnitude <= attackRange + 0.4f && Vector3.Angle(transform.forward, to) < 60f)
                    playerHealth.TakeDamage(new DamageInfo(attackDamage, transform.position, gameObject, attackKnockback));
            }
            knockback += transform.forward * 3f; // small lunge
            if (fx == null) fx = GetComponent<ProceduralMotion>();
            if (fx != null) fx.Swing(0.4f, Random.value < 0.5f ? 1f : -1f);
            GameAudio.Play("sword_swing", transform.position + Vector3.up * 1.2f, 0.7f);

            yield return new WaitForSeconds(attackRecovery);
            nextAttackTime = Time.time + attackCooldown;
            attackRoutine = null;
            if (CurrentState == State.Attack) Enter(State.Chase);
        }

        void OnDamaged(DamageInfo info)
        {
            flashUntil = Time.time + 0.12f;
            if (fx == null) fx = GetComponent<ProceduralMotion>();
            if (fx != null) fx.Flinch(info.sourcePosition);
            Vector3 dir = transform.position - info.sourcePosition;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) knockback += dir.normalized * info.knockback;

            if (player != null) { lastKnownPos = player.position; lastSeenTime = Time.time; }

            if (CurrentState == State.Attack && inWindup)
            {
                Stagger(staggerTime);
                PrototypeHUD.Message("Interrupted!", 0.6f);
            }
            else if (CurrentState == State.Idle || CurrentState == State.Patrol || CurrentState == State.Alert)
            {
                Enter(State.Chase);
            }
        }

        void OnDied()
        {
            WorldLog.Changed(this); // revived if the knight dies before the next checkpoint
            StopAllCoroutines();
            Enter(State.Dead);

            if (anim != null)
                anim.Trigger("Dead");

            cc.enabled = false;
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            RuntimeVisuals.SetRendererColor(bodyRenderer, baseColor * 0.4f, block);
            bool animated = anim != null && anim.HasController;
            if (!animated)
            {
                Quaternion from = transform.rotation;
                Quaternion to = from * Quaternion.Euler(-90f, 0f, 0f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.4f)
                {
                    transform.rotation = Quaternion.Slerp(from, to, t);
                    yield return null;
                }
            }
            yield return new WaitForSeconds(2f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / 1.5f)
            {
                transform.position += Vector3.down * Time.deltaTime * 0.8f;
                yield return null;
            }
            gameObject.SetActive(false); // hidden, not destroyed: it returns when the knight respawns
        }

        void Move()
        {
            if (!cc.enabled) return;
            float mul = Time.time < slowUntil ? slowMultiplier : 1f;
            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += -20f * Time.deltaTime;

            Vector3 motion = desiredVelocity * mul + knockback;
            knockback = Vector3.MoveTowards(knockback, Vector3.zero, 20f * Time.deltaTime);
            if (cc.isGrounded) groundY = transform.position.y;
            // husks can't cross deep water either
            Vector3 step = WaterZone.ClampMove(transform.position, groundY, new Vector3(motion.x, 0f, motion.z) * Time.deltaTime, cc.radius);
            if (Mathf.Abs(step.x) < 0.000001f) { motion.x = 0f; knockback.x = 0f; }
            if (Mathf.Abs(step.z) < 0.000001f) { motion.z = 0f; knockback.z = 0f; }
            motion.y = verticalVelocity;
            cc.Move(motion * Time.deltaTime);

            float speed = new Vector3(desiredVelocity.x, 0f, desiredVelocity.z).magnitude * mul;
            if (cc.isGrounded && speed > 0.5f)
            {
                stepDistance += speed * Time.deltaTime;
                if (stepDistance >= 1.3f) { stepDistance = 0f; GameAudio.Play("step_stone", transform.position, 0.3f, 0.12f); }
            }
        }

        float stepDistance;

        void Face(Vector3 worldPoint)
        {
            Vector3 to = worldPoint - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.001f) return;
            float mul = Time.time < slowUntil ? slowMultiplier : 1f;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), turnSpeed * mul * Time.deltaTime);
        }

        void Enter(State s)
        {
            if (s == State.Alert && CurrentState != State.Alert) GameAudio.Play("enemy_alert", transform.position + Vector3.up * 1.5f, 0.8f);
            CurrentState = s;
            stateTime = Time.time;
        }

        void UpdateTint()
        {
            if (bodyRenderer == null) return;
            Color c = baseColor;
            if (CurrentState == State.Alert || CurrentState == State.Chase) c = alertColor;
            if (CurrentState == State.Attack && inWindup)
                c = Color.Lerp(alertColor, windupColor, Mathf.PingPong(Time.time * 8f, 1f));
            if (Time.time < slowUntil) c = Color.Lerp(c, slowColor, 0.6f);
            if (Time.time < flashUntil) c = Color.white;
            RuntimeVisuals.SetRendererColor(bodyRenderer, c, block);
        }

        public void ApplySlow(float speedMultiplier, float duration)
        {
            slowMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 1f);
            slowUntil = Time.time + duration;
            PrototypeHUD.Message("Enemy slowed", 0.8f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, sightRange);
            Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
