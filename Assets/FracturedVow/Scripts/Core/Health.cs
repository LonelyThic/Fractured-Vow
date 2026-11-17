using System;
using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>Full breakdown of one hit, for sound, effects and the combat overlay.</summary>
    public struct DamageReport
    {
        public Health target;
        public GameObject attacker;
        public float raw, afterBlock, armor, final, before, after;
        public bool blocked;
    }

    /// <summary>Shared health component for the knight, enemies and the training dummy.</summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable
    {
        public static readonly List<Health> All = new List<Health>();
        public static event Action<DamageReport> DamageCalculated;
        public static event Action<Health> AnyDied;

        [Min(1f)] public float maxHealth = 100f;
        [Tooltip("Fraction of damage ignored (0 = none, 0.5 = half). Gear raises this.")]
        [Range(0f, 0.9f)] public float armor = 0f;
        [Tooltip("Seconds of invulnerability after taking a hit.")]
        public float invulnerabilityTime = 0f;

        [Header("HUD")]
        public bool showWorldBar = false;
        public float worldBarHeight = 2.2f;
        public bool showDamageNumbers = true;
        [Tooltip("Optional: where damage numbers appear (e.g. a boss's moving head). Defaults to this object.")]
        public Transform anchor;

        public float Current { get; private set; }
        public bool IsDead { get; private set; }
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
        public float LastDamageTime { get; private set; } = -999f;

        public event Action<DamageInfo> Damaged;
        public event Action Died;
        public event Action Revived;

        IBlocker blocker;
        AnimatorBridge anim;
        float invulnerableUntil;

        /// <summary>Cheat: ignore all damage.</summary>
        [HideInInspector] public bool godMode;
        /// <summary>Asked when a hit would be lethal; returning true leaves 1 health instead (Second Chance skill).</summary>
        public System.Func<bool> preventDeath;

        /// <summary>No damage for the next few seconds (dodge rolls).</summary>
        public void SetInvulnerable(float seconds)
        {
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);
        }

        void Awake()
        {
            Current = maxHealth;
            blocker = GetComponent<IBlocker>();
            anim = GetComponent<AnimatorBridge>();
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public void TakeDamage(DamageInfo info)
        {
            if (IsDead || godMode || info.amount <= 0f || Time.time < invulnerableUntil) return;
            if (Friendly) info.amount *= EnemyScaling.DamageFrom(info.attacker); // enemies scale with level and difficulty

            float raw = info.amount;
            bool blocked = blocker != null && blocker.TryBlock(ref info);
            float afterBlock = info.amount;
            float dmg = info.amount * (1f - armor);
            if (dmg <= 0.01f) return;

            float before = Current;
            Current = Mathf.Max(0f, Current - dmg);
            LastDamageTime = Time.time;
            invulnerableUntil = Time.time + invulnerabilityTime;
            info.amount = dmg;
            DamageCalculated?.Invoke(new DamageReport
            {
                target = this, attacker = info.attacker, raw = raw, afterBlock = afterBlock,
                armor = armor, final = dmg, before = before, after = Current, blocked = blocked
            });

            if (showDamageNumbers)
                PrototypeHUD.DamageNumber((anchor != null ? anchor.position : transform.position) + Vector3.up * worldBarHeight, dmg,
                    blocked ? new Color(0.5f, 0.85f, 1f) : Color.white);

            Damaged?.Invoke(info);

            if (Current <= 0f && preventDeath != null && preventDeath()) { Current = 1f; invulnerableUntil = Time.time + 1f; }
            if (Current <= 0f) Die();
            else if (!blocked && anim != null) anim.Trigger("Hurt");
        }

        /// <summary>Returns false if already at full health.</summary>
        public bool Heal(float amount)
        {
            if (IsDead || Current >= maxHealth) return false;
            Current = Mathf.Min(maxHealth, Current + amount);
            return true;
        }

        public void IncreaseMax(float amount, bool alsoHeal = true)
        {
            maxHealth += amount;
            if (alsoHeal) Current = Mathf.Min(maxHealth, Current + amount);
        }

        int friendly = -1;
        /// <summary>The knight or one of his companions (their incoming damage follows enemy scaling).</summary>
        bool Friendly
        {
            get
            {
                if (friendly < 0) friendly = GetComponent<PlayerController>() != null || GetComponent<CompanionAI>() != null ? 1 : 0;
                return friendly == 1;
            }
        }

        /// <summary>Changes maximum health; a wounded character keeps the same share of it.</summary>
        public void ScaleMax(float newMax)
        {
            newMax = Mathf.Max(1f, newMax);
            float share = maxHealth > 0f ? Current / maxHealth : 1f;
            maxHealth = newMax;
            if (!IsDead) Current = Mathf.Clamp(newMax * share, 1f, newMax);
        }

        public void Kill()
        {
            if (IsDead) return;
            Current = 0f;
            Die();
        }

        void Die()
        {
            IsDead = true;
            if (anim != null) anim.SetBool("Dead", true);
            Died?.Invoke();
            AnyDied?.Invoke(this);
        }

        /// <summary>Sets health directly (revived at half health, for example).</summary>
        public void SetCurrent(float value) { if (!IsDead) Current = Mathf.Clamp(value, 1f, maxHealth); }

        public void Revive()
        {
            IsDead = false;
            Current = maxHealth;
            if (anim != null) anim.SetBool("Dead", false);
            Revived?.Invoke();
        }
    }
}
