using System;
using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Saves the current run to this computer (at every checkpoint and when you go through a floor's exit), so it
    /// survives quitting the game. The title screen's Continue button picks it up on the floor you reached.
    /// The skill tree is part of the run (a new run starts with none). Settings and controls are kept separately.
    /// It also remembers the character as they were when they entered the current floor, for Restart Floor.
    /// There are three save slots (New Game picks one; Load Game lists them); Continue opens the most recent.
    /// </summary>
    public static class SaveSystem
    {
        public const int Slots = 3;
        /// <summary>The slot this run saves into (0-2). Remembered between sessions.</summary>
        public static int ActiveSlot
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("fv_slot", 0), 0, Slots - 1);
            set { PlayerPrefs.SetInt("fv_slot", Mathf.Clamp(value, 0, Slots - 1)); PlayerPrefs.Save(); }
        }
        // slot 1 keeps the original key, so a save from before slots existed shows up there
        static string KeyFor(int slot) => slot == 0 ? "fv_run_save" : "fv_run_save_" + slot;
        static string Key => KeyFor(ActiveSlot);

        [Serializable]
        class RunSave
        {
            public string scene;
            public int runNumber, seed, level, shards, gold, kills, lostShards;
            public int difficulty = 2;
            public long savedAt;
            public int vigor, endurance, strength, intelligence;
            public Vector3 lostAt;
            public string lostScene, weapon, armor, trinket;
            public bool starterTaken;
            public int skillPoints;
            public string skills = "";
            public List<SpellSave> spells = new List<SpellSave>();
            public List<GearSave> gear = new List<GearSave>();
            public List<ItemStack> items = new List<ItemStack>();
            public List<CompanionSave> companions = new List<CompanionSave>();
            public List<string> notes = new List<string>();
            public List<ItemStack> shopBought = new List<ItemStack>();
            public List<ItemStack> upgrades = new List<ItemStack>();
        }

        /// <summary>True if any slot holds a run (for Continue).</summary>
        public static bool HasSave
        {
            get { for (int i = 0; i < Slots; i++) if (PlayerPrefs.HasKey(KeyFor(i))) return true; return false; }
        }
        public static bool HasSaveIn(int slot) => PlayerPrefs.HasKey(KeyFor(slot));

        /// <summary>The slot saved most recently (Continue), or -1.</summary>
        public static int MostRecentSlot()
        {
            int best = -1; long bestAt = long.MinValue;
            for (int i = 0; i < Slots; i++)
            {
                var s = ReadSlot(i);
                if (s != null && s.savedAt >= bestAt) { bestAt = s.savedAt; best = i; }
            }
            return best;
        }

        /// <summary>One line describing a slot for the menus, e.g. "Level 7 - Floor 2 - Hard - Nov 12, 21:40".</summary>
        public static string Describe(int slot)
        {
            var s = ReadSlot(slot);
            if (s == null) return null;
            string floor = s.scene != null && s.scene.Contains("Floor2") ? "Floor 2" : "Floor 1";
            string when = s.savedAt > 0 ? new DateTime(s.savedAt).ToString("MMM d, HH:mm") : "earlier";
            return "Level " + s.level + "  -  " + floor + "  -  " + Difficulty.Names[Mathf.Clamp(s.difficulty, 0, 4)] + "  -  " + when;
        }

        static RunSave ReadSlot(int slot)
        {
            if (!PlayerPrefs.HasKey(KeyFor(slot))) return null;
            try { return JsonUtility.FromJson<RunSave>(PlayerPrefs.GetString(KeyFor(slot))); }
            catch (Exception) { return null; }
        }

        /// <summary>The floor a saved run continues on ("Floor 1" / "Floor 2"), for the Continue button.</summary>
        public static string SavedFloorName
        {
            get
            {
                var s = Read();
                if (s == null) return "";
                return s.scene.Contains("Floor2") ? "Floor 2" : "Floor 1";
            }
        }

        static string floorStart;

        static string checkpointState;

        /// <summary>Remembers the character as they are at a checkpoint (and when a floor begins).</summary>
        public static void MarkCheckpoint()
        {
            if (RunState.Started) checkpointState = JsonUtility.ToJson(Build(""));
        }

        /// <summary>A death: the character goes back to exactly how they were at the last checkpoint.</summary>
        public static bool RewindToCheckpoint()
        {
            if (string.IsNullOrEmpty(checkpointState)) return false;
            Apply(JsonUtility.FromJson<RunSave>(checkpointState));
            return true;
        }

        /// <summary>Remembers the character as they are now (called when a floor begins).</summary>
        public static void MarkFloorStart()
        {
            if (RunState.Started) floorStart = JsonUtility.ToJson(Build(""));
        }

        /// <summary>Puts the character back to how they were when the floor began. False if there's nothing to go back to.</summary>
        public static bool RewindToFloorStart()
        {
            if (string.IsNullOrEmpty(floorStart)) return false;
            Apply(JsonUtility.FromJson<RunSave>(floorStart));
            return true;
        }

        /// <summary>Saves the run; 'resumeScene' is where Continue will start (the next floor when leaving one).</summary>
        public static void Save(string resumeScene)
        {
            if (!RunState.Started) return;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Build(resumeScene)));
            PlayerPrefs.Save();
        }

        static RunSave Build(string resumeScene)
        {
            return new RunSave
            {
                scene = resumeScene, runNumber = RunState.RunNumber, seed = RunState.Seed, level = RunState.Level,
                difficulty = RunState.Difficulty, savedAt = DateTime.Now.Ticks,
                shards = RunState.Shards, gold = RunState.Gold, kills = RunState.Kills, lostShards = RunState.LostShards,
                vigor = RunState.Vigor, endurance = RunState.Endurance, strength = RunState.Strength, intelligence = RunState.Intelligence,
                lostAt = RunState.LostAt, lostScene = RunState.LostScene ?? "",
                weapon = RunState.EquippedWeapon ?? "", armor = RunState.EquippedArmor ?? "", trinket = RunState.EquippedTrinket ?? "",
                starterTaken = RunState.StarterTaken,
                skillPoints = Skills.Points, skills = Skills.Export(),
                spells = new List<SpellSave>(RunState.Spells), gear = new List<GearSave>(RunState.Gear),
                items = new List<ItemStack>(RunState.Items), companions = new List<CompanionSave>(RunState.Companions),
                notes = new List<string>(RunState.Notes),
                shopBought = new List<ItemStack>(RunState.ShopBought),
                upgrades = new List<ItemStack>(RunState.Upgrades),
            };
        }

        static RunSave Read()
        {
            if (!HasSave) return null;
            try { return JsonUtility.FromJson<RunSave>(PlayerPrefs.GetString(Key)); }
            catch (Exception) { return null; }
        }

        /// <summary>Loads the saved run into RunState and returns the scene to open (or null if there is none).</summary>
        public static string Load()
        {
            var s = Read();
            if (s == null || string.IsNullOrEmpty(s.scene)) return null;
            Apply(s);
            return s.scene;
        }

        static void Apply(RunSave s)
        {
            RunState.Restore(s.runNumber, s.seed, s.level, s.shards, s.gold, s.kills,
                s.vigor, s.endurance, s.strength, s.intelligence,
                s.lostShards, s.lostAt, string.IsNullOrEmpty(s.lostScene) ? null : s.lostScene,
                NullIfEmpty(s.weapon), NullIfEmpty(s.armor), NullIfEmpty(s.trinket), s.starterTaken,
                s.spells, s.gear, s.items, s.companions);
            Skills.Import(s.skills, s.skillPoints); // the run's skill tree comes back with it
            RunState.Difficulty = Mathf.Clamp(s.difficulty, 0, 4);
            RunState.Notes.Clear();
            if (s.notes != null) RunState.Notes.AddRange(s.notes);
            RunState.ShopBought.Clear();
            if (s.shopBought != null) foreach (var b in s.shopBought) RunState.ShopBought.Add(new ItemStack { id = b.id, count = b.count });
            RunState.Upgrades.Clear();
            if (s.upgrades != null) foreach (var u in s.upgrades) RunState.Upgrades.Add(new ItemStack { id = u.id, count = u.count });
            RunState.Notify();
        }

        /// <summary>Loads a slot and makes it the one this run saves into.</summary>
        public static string LoadSlot(int slot)
        {
            ActiveSlot = slot;
            return Load();
        }

        public static void Delete() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
        public static void DeleteSlot(int slot) { PlayerPrefs.DeleteKey(KeyFor(slot)); PlayerPrefs.Save(); }
        public static void DeleteAll() { for (int i = 0; i < Slots; i++) PlayerPrefs.DeleteKey(KeyFor(i)); PlayerPrefs.Save(); }

        static string NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
    }
}
