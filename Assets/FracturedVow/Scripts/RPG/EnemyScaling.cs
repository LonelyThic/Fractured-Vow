using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>The five difficulty levels, chosen when starting a New Game (saved with the run).</summary>
    public static class Difficulty
    {
        public static readonly string[] Names = { "Story", "Easy", "Normal", "Hard", "Nightmare" };
        public static readonly string[] Blurbs =
        {
            "For the tale. Enemies are frail and hit softly.",
            "A gentler descent. Weaker enemies.",
            "The vow as intended.",
            "Tougher, deadlier enemies, and 30% more of them.",
            "Merciless. Enemies are far stronger, and there are 60% more of them.",
        };
        static readonly float[] Hp = { 0.6f, 0.8f, 1f, 1.3f, 1.7f };
        static readonly float[] Dmg = { 0.5f, 0.75f, 1f, 1.3f, 1.7f };
        static readonly float[] Mobs = { 1f, 1f, 1f, 1.3f, 1.6f };
        static readonly float[] Reward = { 0.8f, 0.9f, 1f, 1.15f, 1.3f };

        public static int Level => Mathf.Clamp(RunState.Difficulty, 0, 4);
        public static string Name => Names[Level];
        public static float EnemyHealth => Hp[Level];
        public static float EnemyDamage => Dmg[Level];
        public static float MobCount => Mobs[Level];
        public static float Rewards => Reward[Level];
    }

    /// <summary>
    /// Keeps enemies and bosses matched to the knight: +10% health and +6% damage per player level on top of the
    /// difficulty, recalculated whenever he levels up (wounded enemies keep their share of health). Kill rewards
    /// grow only 4% per level and are based on the enemy's unscaled health, so out-levelling the dungeon isn't possible.
    /// Harder difficulties also add extra husks next to the existing ones.
    /// </summary>
    public class EnemyScaling : MonoBehaviour
    {
        public float baseMaxHealth;   // after elite bonuses, before level and difficulty
        Health health;

        static int PlayerLevel => Mathf.Max(1, RunState.Level);
        public static float HealthMult => (1f + 0.10f * (PlayerLevel - 1)) * Difficulty.EnemyHealth;
        public static float DamageMult => (1f + 0.06f * (PlayerLevel - 1)) * Difficulty.EnemyDamage;
        public static float RewardMult => (1f + 0.04f * (PlayerLevel - 1)) * Difficulty.Rewards;

        static readonly List<EnemyScaling> all = new List<EnemyScaling>();
        static int scaledAtLevel = -1;

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public static bool IsHostile(GameObject go) =>
            go != null && (go.GetComponentInParent<EnemyAI>() != null || go.GetComponentInParent<WardenBoss>() != null ||
                           go.GetComponentInParent<LeviathanBoss>() != null);

        /// <summary>How much harder a hit lands on the knight or a companion (traps follow the difficulty only).</summary>
        public static float DamageFrom(GameObject attacker)
        {
            if (attacker == null) return Difficulty.EnemyDamage;
            if (IsHostile(attacker)) return DamageMult;
            if (attacker.GetComponentInParent<PlayerController>() != null || attacker.GetComponentInParent<CompanionAI>() != null) return 1f;
            return Difficulty.EnemyDamage; // traps, wisps and other hazards
        }

        /// <summary>Called once per floor, after elites are chosen: remembers each foe's base health, then scales it.</summary>
        public static void ScaleAll()
        {
            var foes = new List<Health>();
            foreach (var ai in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None)) { var h = ai.GetComponent<Health>(); if (h != null) foes.Add(h); }
            foreach (var b in FindObjectsByType<WardenBoss>(FindObjectsSortMode.None)) { var h = b.GetComponent<Health>(); if (h != null) foes.Add(h); }
            foreach (var b in FindObjectsByType<LeviathanBoss>(FindObjectsSortMode.None)) { var h = b.GetComponent<Health>(); if (h != null) foes.Add(h); }
            foreach (var h in foes)
            {
                var s = h.GetComponent<EnemyScaling>();
                if (s == null) { s = h.gameObject.AddComponent<EnemyScaling>(); s.baseMaxHealth = h.maxHealth; }
                s.health = h;
                s.Apply();
            }
            scaledAtLevel = PlayerLevel;
        }

        /// <summary>The knight levelled up: every foe on the floor is brought up to match.</summary>
        public static void RefreshIfLevelChanged()
        {
            if (PlayerLevel == scaledAtLevel) return;
            scaledAtLevel = PlayerLevel;
            foreach (var s in all) if (s != null) s.Apply();
        }

        void Apply()
        {
            if (health == null) health = GetComponent<Health>();
            if (health != null && baseMaxHealth > 0f) health.ScaleMax(baseMaxHealth * HealthMult);
        }

        /// <summary>Shards for a kill, from the foe's unscaled health (so higher levels don't snowball).</summary>
        public static int KillReward(Health h, bool elite)
        {
            var s = h.GetComponent<EnemyScaling>();
            float baseHp = s != null && s.baseMaxHealth > 0f ? s.baseMaxHealth : h.maxHealth;
            return Mathf.RoundToInt(baseHp * 0.6f * (elite ? 2.5f : 1f) * RewardMult);
        }

        /// <summary>Hard and Nightmare: extra husks appear beside the existing ones (once, when the floor loads).</summary>
        public static void Populate(System.Random rng)
        {
            float mult = Difficulty.MobCount;
            if (mult <= 1.001f) return;
            var foes = new List<EnemyAI>(FindObjectsByType<EnemyAI>(FindObjectsSortMode.None));
            foes.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); // same picks every time for this seed
            float owed = 0f;
            foreach (var ai in foes)
            {
                owed += mult - 1f;
                while (owed >= 1f)
                {
                    owed -= 1f;
                    TryCloneBeside(ai, rng);
                }
            }
        }

        static void TryCloneBeside(EnemyAI ai, System.Random rng)
        {
            Vector3 p0 = ai.transform.position;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float ang = (float)rng.NextDouble() * Mathf.PI * 2f, dist = 1.7f + (float)rng.NextDouble() * 1.2f;
                Vector3 p = p0 + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var ground, 3.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (ground.collider.GetComponentInParent<EnemyAI>() != null) continue;
                Vector3 feet = ground.point;
                if (Mathf.Abs(feet.y - p0.y) > 0.8f) continue;
                if (Physics.CheckCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.6f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (WaterZone.BlocksWalking(feet.y, feet)) continue;
                var clone = Instantiate(ai.gameObject, feet + Vector3.up * 0.05f, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f), ai.transform.parent);
                clone.name = ai.name + " (horde)";
                return;
            }
        }
    }
}
