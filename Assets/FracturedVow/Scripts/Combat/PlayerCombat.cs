using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Sword and shield. LMB = light slash, chained into a 3-hit combo (third hit is a heavier finisher).
    /// RMB = raise shield: blocks hits from the front at a stamina cost; out of stamina = guard broken.
    /// Soft lock-on turns the knight toward the closest enemy in front when swinging.
    /// </summary>
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerCombat : MonoBehaviour, IBlocker
    {
        [Header("Sword combo")]
        public float[] comboDamage = { 12f, 14f, 24f };
        public float[] comboKnockback = { 2f, 2f, 6f };
        public float attackRange = 1.3f;
        public float attackRadius = 1.1f;
        public float hitDelay = 0.18f;
        public float attackDuration = 0.45f;
        public float finisherDuration = 0.7f;
        public float comboWindow = 0.6f;
        public float attackStaminaCost = 8f;
        public float softLockRange = 3.5f;
        public LayerMask hitMask = ~0;

        [Header("Slash effect angles")]
        public float firstSlashAngle = -30f;
        public float secondSlashAngle = 30f;
        public float thirdSlashAngle = -90f;

        [Header("Shield")]
        public float blockAngle = 140f;
        [Range(0f, 1f)] public float blockReduction = 0.85f;
        public float blockStaminaCost = 12f;

        [Header("Placeholder visuals (optional)")]
        public Transform shieldVisual;
        public Vector3 shieldBlockLocalPos = new Vector3(-0.05f, 1.2f, 0.55f);
        public Vector3 shieldBlockLocalEuler = new Vector3(0f, 90f, 0f);
        public bool showSlashEffect = true;
        public Material slashMaterial;

        [HideInInspector] public bool combatEnabled = true;

        [Header("Charged (heavy) attack: hold attack")]
        public float chargeTime = 0.45f;
        public float heavyDamage = 32f;
        public float heavyKnockback = 8f;
        public float heavyStaminaCost = 22f;
        public float heavyDuration = 0.75f;

        /// <summary>Raised when the knight starts a swing (position, heavy): enemies use it to try to dodge.</summary>
        public static event System.Action<Vector3, bool> SwingStarted;

        public bool IsAttacking => Time.time < attackEndTime;
        public bool IsCharging { get; private set; }
        public bool IsBlocking { get; private set; }
        public int ComboIndex => comboIndex;

        PlayerInputHandler input;
        PlayerController controller;
        PlayerStats stats;
        PlayerMagic magic;
        Health health;
        AnimatorBridge anim;

        int comboIndex = -1;
        float lastAttackEnd = -99f;
        float attackEndTime;
        float attackQueuedAt = -99f;
        float pressTime;
        bool pressing, heavyQueued;
        float blockStartTime = -99f;
        ProceduralMotion motionFx;
        Vector3 shieldRestPos;
        Quaternion shieldRestRot;
        readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

        void Awake()
        {
            input = GetComponent<PlayerInputHandler>();
            controller = GetComponent<PlayerController>();
            stats = GetComponent<PlayerStats>();
            magic = GetComponent<PlayerMagic>();
            health = GetComponent<Health>();
            anim = GetComponent<AnimatorBridge>();
            if (shieldVisual != null)
            {
                shieldRestPos = shieldVisual.localPosition;
                shieldRestRot = shieldVisual.localRotation;
            }
        }

        void Update()
        {
            bool active = combatEnabled && (health == null || !health.IsDead) && (controller == null || controller.ControlEnabled);
            if (!active)
            {
                SetBlocking(false);
                UpdateShieldVisual();
                return;
            }

            bool casting = magic != null && magic.IsCasting;
            bool dodging = controller != null && controller.IsDodging;
            if (motionFx == null) motionFx = GetComponent<ProceduralMotion>();
            SetBlocking(input.BlockHeld && !IsAttacking && !casting && !dodging && !IsCharging);

            // tap = light combo, hold = charged heavy swing (released or auto after a moment)
            if (input.AttackPressed && !IsBlocking && !casting) { pressTime = Time.time; pressing = true; }
            if (pressing)
            {
                float held = Time.time - pressTime;
                bool released = !input.AttackHeld || input.AttackReleased || held > chargeTime + 1.2f;
                if (released)
                {
                    pressing = false;
                    if (IsCharging) heavyQueued = true; else attackQueuedAt = Time.time;
                    IsCharging = false;
                    if (motionFx != null) motionFx.Charge = 0f;
                }
                else if (held >= chargeTime && !IsCharging && !IsAttacking && !dodging)
                {
                    IsCharging = true;
                    if (motionFx != null) motionFx.Charge = 1f;
                    GameAudio.Play("spell_cast", transform.position + Vector3.up, 0.35f, 0.02f, 0.6f);
                }
            }

            bool free = !IsAttacking && !IsBlocking && !casting && !dodging;
            if (heavyQueued && free) { heavyQueued = false; StartHeavy(); }
            bool queued = Time.time - attackQueuedAt < 0.25f;
            if (queued && free)
            {
                attackQueuedAt = -99f;
                StartAttack();
            }

            UpdateShieldVisual();
        }

        void SetBlocking(bool value)
        {
            if (value && !IsBlocking) blockStartTime = Time.time;
            IsBlocking = value;
            if (motionFx != null) motionFx.Guard = value;
            if (anim != null) anim.SetBool("Blocking", value);
        }

        void StartAttack()
        {
            if (stats != null && !stats.TryUseStamina(attackStaminaCost * stats.attackCostMult))
            {
                PrototypeHUD.Message("Too tired to swing", 1f);
                return;
            }

            bool chain = comboIndex >= 0 && comboIndex < comboDamage.Length - 1 && Time.time - lastAttackEnd <= comboWindow;
            comboIndex = chain ? comboIndex + 1 : 0;

            float duration = comboIndex == comboDamage.Length - 1 ? finisherDuration : attackDuration;
            attackEndTime = Time.time + duration;
            lastAttackEnd = attackEndTime;

            SoftLock();
            GameAudio.Play(comboIndex == comboDamage.Length - 1 ? "sword_swing_heavy" : "sword_swing", transform.position + Vector3.up * 1.2f, 0.85f);
            if (anim != null)
            {
                anim.SetInt("AttackIndex", comboIndex);
                anim.Trigger("Attack");
            }
            SwingStarted?.Invoke(transform.position, false);
            if (motionFx != null) motionFx.Swing(duration * 0.8f, comboIndex == 1 ? -1f : 1f);
            StartCoroutine(HitRoutine(comboIndex, false));
        }

        void StartHeavy()
        {
            if (stats != null && !stats.TryUseStamina(heavyStaminaCost * stats.attackCostMult))
            {
                PrototypeHUD.Message("Too tired for a heavy swing", 1f);
                return;
            }
            comboIndex = -1;
            attackEndTime = Time.time + heavyDuration;
            lastAttackEnd = attackEndTime;
            SoftLock();
            GameAudio.Play("sword_swing_heavy", transform.position + Vector3.up * 1.2f, 1f, 0.03f, 0.85f);
            if (anim != null)
            {
                anim.SetInt("AttackIndex", 2);
                anim.Trigger("Attack");
                anim.Trigger("Heavy");
            }
            if (controller != null) controller.AddImpulse(transform.forward * 4f);
            SwingStarted?.Invoke(transform.position, true);
            if (motionFx != null) motionFx.Swing(heavyDuration * 0.8f, 1.6f);
            StartCoroutine(HitRoutine(2, true));
        }

        /// <summary>
        /// Swings go where you're looking: the knight turns to the camera's direction (or his lock-on target),
        /// then nudges onto an enemy only if one is close and almost straight ahead of where you aim.
        /// </summary>
        void SoftLock()
        {
            Vector3 aim = transform.forward;
            var lockOn = controller != null ? controller.LockTarget : null;
            if (lockOn != null) aim = lockOn.position - transform.position;
            else if (Camera.main != null) aim = Camera.main.transform.forward;
            aim.y = 0f;
            if (aim.sqrMagnitude < 0.001f) return;
            aim.Normalize();

            Health best = null;
            float bestScore = float.MaxValue;
            foreach (var h in Health.All)
            {
                if (h == null || h.IsDead || h.transform.IsChildOf(transform)) continue;
                if (h.GetComponent<CompanionAI>() != null) continue; // never turn toward your own companions
                Vector3 to = (h.anchor != null ? h.anchor.position : h.transform.position) - transform.position;
                to.y = 0f;
                float d = to.magnitude, ang = Vector3.Angle(aim, to);
                if (d > softLockRange || ang > 30f) continue;
                float score = ang + d * 4f;
                if (score < bestScore) { bestScore = score; best = h; }
            }
            if (best != null)
            {
                Vector3 to = (best.anchor != null ? best.anchor.position : best.transform.position) - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.001f) aim = to.normalized;
            }
            transform.rotation = Quaternion.LookRotation(aim);
        }

        IEnumerator HitRoutine(int index, bool heavy)
        {
            // the light trail follows the sword itself, from just before the hit frame until the blade has passed
            float delay = heavy ? hitDelay * 1.8f : hitDelay;
            float lead = delay * 0.55f;
            yield return new WaitForSeconds(delay - lead);
            bool onBlade = false;
            if (showSlashEffect && (health == null || !health.IsDead))
            {
                bool finisher = heavy || index == comboDamage.Length - 1;
                onBlade = WeaponTrail.Swing(transform, lead + (finisher ? 0.24f : 0.16f),
                                            finisher ? new Color(1f, 0.88f, 0.55f) : Color.white);
            }
            yield return new WaitForSeconds(lead);
            if (health != null && health.IsDead) yield break;

            // no sword in hand: the plain arc appears on the hit frame, at the angle set for this swing in the Inspector
            if (showSlashEffect && !onBlade)
            {
                float angle = index == 0 ? firstSlashAngle
                            : index == 1 ? secondSlashAngle
                            : thirdSlashAngle;
                SlashEffect.Spawn(transform, index, slashMaterial, angle);
            }

            hitThisSwing.Clear();
            Vector3 center = transform.position + Vector3.up + transform.forward * attackRange;
            var hits = Physics.OverlapSphere(center, attackRadius, hitMask, QueryTriggerInteraction.Ignore);
            float mul = stats != null ? stats.damageMultiplier : 1f;
            float dmg = heavy ? heavyDamage * mul * (stats != null ? stats.heavyMult : 1f)
                              : comboDamage[index] * mul * (index == comboDamage.Length - 1 && stats != null ? stats.finisherMult : 1f);
            float kb = heavy ? heavyKnockback : comboKnockback[index];
            if (heavy) { center += transform.forward * 0.3f; hits = Physics.OverlapSphere(center, attackRadius * 1.25f, hitMask, QueryTriggerInteraction.Ignore); }
            bool landed = false;
            foreach (var c in hits)
            {
                var d = c.GetComponentInParent<IDamageable>();
                if (d == null || d.IsDead) continue;
                var comp = d as Component;
                if (comp == null || comp.transform.IsChildOf(transform)) continue;
                if (comp.GetComponentInParent<CompanionAI>() != null) continue; // never your own sellswords
                if (!hitThisSwing.Add(d)) continue;
                var ai = comp.GetComponent<EnemyAI>();
                float final = ai != null && ai.IsVulnerable ? dmg * 1.5f : dmg;
                d.TakeDamage(new DamageInfo(final, transform.position, gameObject, kb));
                if (heavy && ai != null) ai.Stagger(0.8f);
                if (stats != null && stats.manaOnHit > 0f) stats.AddMana(stats.manaOnHit);
                landed = true;
            }
            if (landed) StartCoroutine(HitStop(heavy ? 0.11f : index == comboDamage.Length - 1 ? 0.08f : 0.045f));
        }

        /// <summary>A few frames of near-freeze on impact so hits feel heavy.</summary>
        IEnumerator HitStop(float seconds)
        {
            if (UIFocus.Any) yield break;
            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(seconds);
            if (!UIFocus.Any) Time.timeScale = 1f;
        }

        public bool TryBlock(ref DamageInfo info)
        {
            if (!IsBlocking) return false;
            Vector3 to = info.sourcePosition - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.001f && Vector3.Angle(transform.forward, to) > blockAngle * 0.5f) return false;

            // parry: the shield went up just before the hit
            float window = stats != null ? stats.parryWindow : 0.2f;
            if (Time.time - blockStartTime <= window)
            {
                info.amount = 0f;
                info.knockback = 0f;
                var ai = info.attacker != null ? info.attacker.GetComponent<EnemyAI>() : null;
                if (ai != null)
                {
                    ai.Stagger(1.4f);
                    if (Skills.Has(SkillId.Riposte)) ai.MakeVulnerable(3f);
                }
                PrototypeHUD.Message("PARRY!", 0.8f);
                GameAudio.Play("block_clang", transform.position + Vector3.up, 1f, 0.02f, 1.25f);
                GameFX.Sparks(transform.position + Vector3.up * 1.2f + transform.forward * 0.6f, new Color(1f, 0.95f, 0.6f), 30);
                StartCoroutine(HitStop(0.12f));
                return true;
            }

            if (stats == null || stats.TryUseStamina(blockStaminaCost * stats.blockCostMult))
            {
                info.amount *= 1f - (stats != null ? stats.blockReduction : blockReduction);
                info.knockback *= 0.4f;
                PrototypeHUD.Message("Blocked!", 0.6f);
                return true;
            }
            PrototypeHUD.Message("Guard broken - no stamina!", 1.2f);
            GameAudio.Play("guard_break", transform.position + Vector3.up, 0.9f);
            return false;
        }

        void UpdateShieldVisual()
        {
            if (shieldVisual == null) return;
            Vector3 pos = IsBlocking ? shieldBlockLocalPos : shieldRestPos;
            Quaternion rot = IsBlocking ? Quaternion.Euler(shieldBlockLocalEuler) : shieldRestRot;
            float k = 1f - Mathf.Exp(-20f * Time.deltaTime);
            shieldVisual.localPosition = Vector3.Lerp(shieldVisual.localPosition, pos, k);
            shieldVisual.localRotation = Quaternion.Slerp(shieldVisual.localRotation, rot, k);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.5f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up + transform.forward * attackRange, attackRadius);
        }
    }
}
