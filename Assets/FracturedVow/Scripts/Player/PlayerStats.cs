using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    public enum GearStat { Damage, Armor, MaxHealth, MaxMana }

    /// <summary>Mana, stamina and gear bonuses for the knight. Health lives in Health.cs.</summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Mana (spells)")]
        public float maxMana = 100f;
        public float manaRegenPerSecond = 3f;

        [Header("Stamina (sprint, jump, swing, block)")]
        public float maxStamina = 100f;
        public float staminaRegenPerSecond = 28f;
        public float staminaRegenDelay = 0.8f;

        [Header("Gear")]
        public float damageMultiplier = 1f;

        // Set by PlayerProgress from level, attributes, gear and the skill tree.
        [HideInInspector] public float spellMultiplier = 1f, attackCostMult = 1f, blockCostMult = 1f, dodgeCostMult = 1f;
        [HideInInspector] public float cooldownMult = 1f, heavyMult = 1f, finisherMult = 1f, blockReduction = 0.85f;
        [HideInInspector] public float parryWindow = 0.2f, iFrameBonus = 0f, manaOnHit = 0f, flaskHealMult = 1f;
        [HideInInspector] public int flaskMax = 3;

        public float Mana { get; private set; }
        public float Stamina { get; private set; }
        public readonly List<string> equippedGear = new List<string>();

        float lastStaminaUse = -99f;

        void Awake()
        {
            Mana = maxMana;
            Stamina = maxStamina;
        }

        void Update()
        {
            Mana = Mathf.Min(maxMana, Mana + manaRegenPerSecond * Time.deltaTime);
            if (Time.time - lastStaminaUse > staminaRegenDelay)
                Stamina = Mathf.Min(maxStamina, Stamina + staminaRegenPerSecond * Time.deltaTime);
        }

        Health ownHealth;
        bool God
        {
            get
            {
                if (ownHealth == null) ownHealth = GetComponent<Health>();
                return ownHealth != null && ownHealth.godMode;
            }
        }

        public bool TryUseMana(float amount)
        {
            if (God) { Mana = maxMana; return true; } // god mode: endless mana
            if (Mana < amount) return false;
            Mana -= amount;
            return true;
        }

        public bool TryUseStamina(float amount)
        {
            if (God) { Stamina = maxStamina; return true; } // god mode: endless stamina
            if (Stamina < amount) return false;
            Stamina -= amount;
            lastStaminaUse = Time.time;
            return true;
        }

        public bool AddMana(float amount)
        {
            if (Mana >= maxMana) return false;
            Mana = Mathf.Min(maxMana, Mana + amount);
            return true;
        }

        public bool AddStamina(float amount)
        {
            if (Stamina >= maxStamina) return false;
            Stamina = Mathf.Min(maxStamina, Stamina + amount);
            return true;
        }

        public void RefillAll()
        {
            Mana = maxMana;
            Stamina = maxStamina;
        }

        public void ApplyGear(GearStat stat, float amount, string gearName, Health health)
        {
            // with the RPG layer active, gear is remembered for the run and every stat is rebuilt from scratch
            var progress = GetComponent<PlayerProgress>();
            if (progress != null)
            {
                RunState.Gear.Add(new GearSave { name = gearName, stat = stat, amount = amount });
                equippedGear.Add(gearName);
                progress.Recalculate(true);
                if (stat == GearStat.MaxMana) Mana += amount;
                return;
            }
            switch (stat)
            {
                case GearStat.Damage: damageMultiplier += amount; break;
                case GearStat.Armor: if (health != null) health.armor = Mathf.Min(0.75f, health.armor + amount); break;
                case GearStat.MaxHealth: if (health != null) health.IncreaseMax(amount); break;
                case GearStat.MaxMana: maxMana += amount; Mana += amount; break;
            }
            equippedGear.Add(gearName);
        }
    }
}
