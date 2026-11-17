using System;
using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    [Serializable] public class SpellSave { public SpellType type; public int level = 1; }
    [Serializable] public class GearSave { public string name; public GearStat stat; public float amount; }
    [Serializable] public class ItemStack { public string id; public int count; }
    [Serializable] public class CompanionSave { public string name; public int model; public string weapon, armor; public bool dead; }

    /// <summary>
    /// Everything that belongs to the current run and carries from Floor 1 to Floor 2: level, attributes,
    /// Memory Shards, spells, gear, and shards dropped on death. A new run (title screen or "Restart Run")
    /// wipes it, the skill tree included (see Skills).
    /// </summary>
    public static class RunState
    {
        public static bool Started { get; private set; }
        public static int RunNumber, Seed;
        /// <summary>0 Story, 1 Easy, 2 Normal, 3 Hard, 4 Nightmare (picked in New Game; kept for the whole run).</summary>
        public static int Difficulty = 2;
        public static int Level = 1, Shards;
        public static int Vigor, Endurance, Strength, Intelligence;
        public static readonly List<SpellSave> Spells = new List<SpellSave>();
        public static readonly List<GearSave> Gear = new List<GearSave>();
        public static int LostShards;
        public static Vector3 LostAt;
        public static string LostScene;
        public static bool SecondChanceUsed;
        public static int Kills;

        public static int Gold;
        public static readonly List<ItemStack> Items = new List<ItemStack>();
        public static string EquippedWeapon, EquippedArmor, EquippedTrinket;
        public static readonly List<CompanionSave> Companions = new List<CompanionSave>();
        public const int MaxCompanions = 2;

        /// <summary>Raised when the run is replaced by a new one.</summary>
        public static event Action NewRunStarted;
        /// <summary>Raised whenever shards, level or attributes change.</summary>
        public static event Action Changed;

        /// <summary>Shards needed for the next level: rises with every level gained.</summary>
        public static int LevelCost => Mathf.RoundToInt(80f + 40f * (Level - 1) + 6f * (Level - 1) * (Level - 1));

        public static void NewRun()
        {
            Started = true;
            Skills.WipeForNewRun(); // a fresh character: no skills and no skill points
            RunNumber = PlayerPrefs.GetInt("fv_runs", 0) + 1;
            PlayerPrefs.SetInt("fv_runs", RunNumber);
            PlayerPrefs.Save();
            Seed = UnityEngine.Random.Range(1000, 99999);
            Level = 1; Shards = 0; Kills = 0;
            Vigor = Endurance = Strength = Intelligence = 0;
            Spells.Clear();
            Spells.Add(new SpellSave { type = SpellType.Firebolt, level = 1 });
            Gear.Clear();
            LostShards = 0; LostScene = null;
            SecondChanceUsed = false;
            Gold = 60;
            Items.Clear();
            Items.Add(new ItemStack { id = "hp_flask", count = 3 });
            Items.Add(new ItemStack { id = "mp_flask", count = 1 });
            EquippedWeapon = EquippedArmor = EquippedTrinket = null;
            StarterTaken = false; SeenWardenIntro = false; Notes.Clear(); ShopBought.Clear(); // the starter sword waits on the floor of the first cell
            Companions.Clear();
            NewRunStarted?.Invoke();
            Changed?.Invoke();
        }

        /// <summary>
        /// Every run starts with a sword in hand: the Polytope arming sword if it's imported (it fits the hero's hand
        /// slot exactly), otherwise the plainest sword in the item list.
        /// </summary>
        /// <summary>True once the knight has picked up the starter sword this run.</summary>
        public static bool StarterTaken;
        /// <summary>The Warden's introduction plays once per run (later attempts get a short taunt instead).</summary>
        public static bool SeenWardenIntro;
        /// <summary>Titles of the notes picked up this run (read them in Tab > Journal).</summary>
        public static readonly List<string> Notes = new List<string>();
        /// <summary>What each merchant has sold this run ("scene|merchant|item", count), for their limited stock.</summary>
        public static readonly List<ItemStack> ShopBought = new List<ItemStack>();

        public static string StarterSword()
        {
            ItemDef best = null;
            foreach (var it in FVLibrary.Catalog)
            {
                if (it.kind != ItemKind.Weapon) continue;
                string m = it.model != null ? it.model.name : "";
                if (m.StartsWith("PT_Sword_01")) return it.id;
                bool sword = it.name.ToLowerInvariant().Contains("sword") || it.name.ToLowerInvariant().Contains("blade");
                if (sword && (best == null || it.tier < best.tier)) best = it;
            }
            return best != null ? best.id : null;
        }

        /// <summary>Puts a saved run back (SaveSystem.Load).</summary>
        public static void Restore(int runNumber, int seed, int level, int shards, int gold, int kills,
            int vigor, int endurance, int strength, int intelligence, int lostShards, Vector3 lostAt, string lostScene,
            string weapon, string armor, string trinket, bool starterTaken,
            List<SpellSave> spells, List<GearSave> gear, List<ItemStack> items, List<CompanionSave> companions)
        {
            Started = true;
            RunNumber = runNumber; Seed = seed; Level = Mathf.Max(1, level); Shards = shards; Gold = gold; Kills = kills;
            Vigor = vigor; Endurance = endurance; Strength = strength; Intelligence = intelligence;
            LostShards = lostShards; LostAt = lostAt; LostScene = lostScene;
            EquippedWeapon = weapon; EquippedArmor = armor; EquippedTrinket = trinket; StarterTaken = starterTaken;
            SecondChanceUsed = false;
            Spells.Clear(); if (spells != null) Spells.AddRange(spells);
            Gear.Clear(); if (gear != null) Gear.AddRange(gear);
            Items.Clear(); if (items != null) Items.AddRange(items);
            Companions.Clear(); if (companions != null) Companions.AddRange(companions);
            Changed?.Invoke();
        }

        public static void AddGold(int n)
        {
            Gold = Mathf.Max(0, Gold + n);
            Changed?.Invoke();
        }

        public static void AddShards(int n)
        {
            Shards = Mathf.Max(0, Shards + n);
            Changed?.Invoke();
        }

        public static void Notify() => Changed?.Invoke();
    }
}
