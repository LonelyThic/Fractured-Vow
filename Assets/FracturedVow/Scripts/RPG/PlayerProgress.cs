using UnityEngine;
using UnityEngine.SceneManagement;

namespace FracturedVow
{
    /// <summary>
    /// The knight's RPG side: turns level, attributes, gear and skill-tree unlocks into real numbers
    /// (health, stamina, mana, damage, costs), pays out Memory Shards for kills, and handles levelling up.
    /// Added to the player automatically.
    /// </summary>
    public class PlayerProgress : MonoBehaviour
    {
        public const float BaseHealth = 100f, BaseStamina = 100f, BaseMana = 100f;

        Health health;
        PlayerStats stats;
        PlayerMagic magic;

        void Awake()
        {
            health = GetComponent<Health>();
            stats = GetComponent<PlayerStats>();
            magic = GetComponent<PlayerMagic>();
        }

        void OnEnable()
        {
            Health.AnyDied += OnAnyDied;
            Skills.Changed += Refresh;
            RunState.NewRunStarted += OnNewRun;
        }

        void OnDisable()
        {
            Health.AnyDied -= OnAnyDied;
            Skills.Changed -= Refresh;
            RunState.NewRunStarted -= OnNewRun;
        }

        void Start()
        {
            if (!RunState.Started) RunState.NewRun();
            CheckAnimator();
            RunState.SecondChanceUsed = false; // once per floor
            if (health != null) health.preventDeath = SecondChance;
            LoadRun();
            if (Skills.Has(SkillId.ExtraFlask)) Inventory.Add("hp_flask", 1, false);
        }

        void OnNewRun() => LoadRun();

        /// <summary>Says plainly in the Console if the knight can't animate (he would stand in a T-pose).</summary>
        void CheckAnimator()
        {
            var a = GetComponentInChildren<Animator>();
            string problem = a == null ? "has no Animator"
                : a.runtimeAnimatorController == null ? "has no Animator Controller (it may have been deleted)"
                : a.avatar == null ? "has no Avatar"
                : !a.avatar.isValid ? "has an invalid Avatar" : null;
            if (problem != null)
                Debug.LogError("[Fractured Vow] The player's model " + problem + ", so he can't animate. " +
                               "Run Tools > Fractured Vow > Set Up Imported Packs again, then rebuild the floor (Build Level + Production Pass).");
        }

        void LoadRun()
        {
            SyncWithRun();
            SaveSystem.MarkFloorStart(); // Restart Floor comes back to exactly this
        }

        /// <summary>Applies the run's character (gear, spells, look, stats) and refills him (also after a respawn).</summary>
        public void SyncWithRun()
        {
            if (stats != null)
            {
                stats.equippedGear.Clear();
                foreach (var g in RunState.Gear) stats.equippedGear.Add(g.name);
            }
            if (magic != null) magic.LoadFromRun();
            PlayerLook.Apply(gameObject);
            Recalculate(false);
            if (health != null) health.Heal(99999f);
            if (stats != null) stats.RefillAll();
            var flask = GetComponent<PlayerFlask>();
            if (flask != null) flask.Refill();
        }

        void Refresh() => Recalculate(true);

        float GearTotal(GearStat stat)
        {
            float t = 0f;
            foreach (var g in RunState.Gear) if (g.stat == stat) t += g.amount;
            return t;
        }

        /// <summary>Rebuilds every derived number. keepRatio: raise current health/mana along with the maximum.</summary>
        public void Recalculate(bool keepRatio)
        {
            if (health != null)
            {
                float oldMax = health.maxHealth;
                Inventory.Bonuses(out float eqDmg, out float eqArmor, out float eqHealth, out float eqMana);
                health.maxHealth = BaseHealth + RunState.Vigor * 12f + GearTotal(GearStat.MaxHealth) + eqHealth + (Skills.Has(SkillId.ThickHide) ? 25f : 0f);
                health.armor = Mathf.Min(0.75f, GearTotal(GearStat.Armor) + eqArmor);
                if (keepRatio && health.maxHealth > oldMax) health.Heal(health.maxHealth - oldMax);
            }
            if (stats != null)
            {
                stats.maxStamina = BaseStamina + RunState.Endurance * 10f;
                stats.staminaRegenPerSecond = (28f + RunState.Endurance * 0.8f) * (Skills.Has(SkillId.Momentum) ? 1.35f : 1f);
                Inventory.Bonuses(out float eqDmg2, out float eqArmor2, out float eqHealth2, out float eqMana2);
                stats.maxMana = BaseMana + RunState.Intelligence * 8f + GearTotal(GearStat.MaxMana) + eqMana2 + (Skills.Has(SkillId.DeepWell) ? 30f : 0f);
                stats.damageMultiplier = 1f + RunState.Strength * 0.06f + GearTotal(GearStat.Damage) + eqDmg2 + (Skills.Has(SkillId.HonedEdge) ? 0.1f : 0f);
                stats.spellMultiplier = 1f + RunState.Intelligence * 0.07f + (Skills.Has(SkillId.Spellblade) ? 0.2f : 0f);
                stats.attackCostMult = Skills.Has(SkillId.SecondWind) ? 0.75f : 1f;
                stats.heavyMult = Skills.Has(SkillId.HeavyHands) ? 1.3f : 1f;
                stats.finisherMult = Skills.Has(SkillId.Executioner) ? 1.4f : 1f;
                stats.blockReduction = Skills.Has(SkillId.SteelWall) ? 0.95f : 0.85f;
                stats.blockCostMult = Skills.Has(SkillId.SteelWall) ? 0.7f : 1f;
                stats.parryWindow = Skills.Has(SkillId.Riposte) ? 0.4f : 0.25f;
                stats.manaOnHit = Skills.Has(SkillId.ManaThief) ? 4f : 0f;
                stats.cooldownMult = Skills.Has(SkillId.QuickenedRunes) ? 0.65f : 1f;
                stats.dodgeCostMult = Skills.Has(SkillId.LightFeet) ? 0.7f : 1f;
                stats.iFrameBonus = Skills.Has(SkillId.GhostStep) ? 0.12f : 0f;
                stats.flaskMax = 3 + (Skills.Has(SkillId.ExtraFlask) ? 1 : 0);
                stats.flaskHealMult = Skills.Has(SkillId.PotentFlask) ? 1.4f : 1f;
            }
            RunState.Notify();
        }

        /// <summary>Spend shards to raise one attribute: 0 Vigor, 1 Endurance, 2 Strength, 3 Intelligence.</summary>
        public bool LevelUp(int attribute)
        {
            int cost = RunState.LevelCost;
            if (RunState.Shards < cost) { GameAudio.PlayUI("ui_click", 0.5f); return false; }
            RunState.Shards -= cost;
            RunState.Level++;
            switch (attribute)
            {
                case 0: RunState.Vigor++; break;
                case 1: RunState.Endurance++; break;
                case 2: RunState.Strength++; break;
                default: RunState.Intelligence++; break;
            }
            Skills.Points++;
            Recalculate(true);
            EnemyScaling.RefreshIfLevelChanged(); // the dungeon grows with him
            if (health != null) health.Heal(99999f);
            if (stats != null) stats.RefillAll();
            GameAudio.PlayUI("spell_learn");
            GameFX.Burst(transform.position + Vector3.up, new Color(1f, 0.85f, 0.4f), 40);
            PrototypeHUD.Message("LEVEL " + RunState.Level + "  (+1 skill point)", 2.5f);
            return true;
        }

        public void GrantLevels(int levels)
        {
            for (int i = 0; i < levels; i++)
            {
                RunState.Shards += RunState.LevelCost;
                LevelUp(i % 4);
            }
        }

        void OnAnyDied(Health h)
        {
            if (h == null || h.gameObject == gameObject) return;
            int reward = 0;
            var ai = h.GetComponent<EnemyAI>();
            bool boss = false;
            if (ai != null) reward = EnemyScaling.KillReward(h, ai.elite);
            else if (h.GetComponent<WardenBoss>() != null) { reward = Mathf.RoundToInt(400 * EnemyScaling.RewardMult); boss = true; }
            else if (h.GetComponent<LeviathanBoss>() != null) { reward = Mathf.RoundToInt(1000 * EnemyScaling.RewardMult); boss = true; }
            if (reward <= 0) return;
            Loot.EnemyDrop(h.transform.position + Vector3.up * 0.2f, ai != null && ai.elite, boss);
            RunState.Kills++;
            RunState.AddShards(reward);
            Vector3 at = (h.anchor != null ? h.anchor.position : h.transform.position) + Vector3.up * 2.4f;
            PrototypeHUD.FloatText(at, "+" + reward + " shards", new Color(1f, 0.85f, 0.4f));
        }

        bool SecondChance()
        {
            if (!Skills.Has(SkillId.SecondChance) || RunState.SecondChanceUsed) return false;
            RunState.SecondChanceUsed = true;
            PrototypeHUD.Message("SECOND CHANCE", 2.5f);
            GameAudio.PlayUI("memory_chime", 0.8f);
            GameFX.Burst(transform.position + Vector3.up, new Color(0.7f, 0.9f, 1f), 50);
            return true;
        }

        public static string SceneName => SceneManager.GetActiveScene().name;
    }
}
