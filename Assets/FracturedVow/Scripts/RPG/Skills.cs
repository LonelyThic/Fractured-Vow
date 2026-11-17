using System;
using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    public enum SkillId
    {
        HonedEdge, SecondWind, HeavyHands, Executioner,
        ThickHide, SteelWall, Riposte,
        DeepWell, Spellblade, ManaThief, QuickenedRunes,
        LightFeet, GhostStep, Momentum,
        ExtraFlask, PotentFlask, SecondChance
    }

    public class SkillNode
    {
        public SkillId id;
        public string name, description, branch;
        public int cost, column, row;
        public SkillId[] requires;
    }

    /// <summary>
    /// The skill tree: five branches, part of the current run (a new run starts with an empty tree). Every level-up earns
    /// one skill point. Nodes need the one above them in their branch.
    /// </summary>
    public static class Skills
    {
        public static readonly string[] Branches = { "WARRIOR", "GUARDIAN", "ARCANIST", "SHADOW", "SURVIVOR" };

        public static readonly SkillNode[] All =
        {
            N(SkillId.HonedEdge, "Honed Edge", "+10% sword damage.", 0, 0, 1),
            N(SkillId.SecondWind, "Second Wind", "Sword swings cost 25% less stamina.", 0, 1, 1, SkillId.HonedEdge),
            N(SkillId.HeavyHands, "Heavy Hands", "Charged attacks deal 30% more damage.", 0, 2, 1, SkillId.SecondWind),
            N(SkillId.Executioner, "Executioner", "The third hit of a combo deals 40% more damage.", 0, 3, 2, SkillId.HeavyHands),

            N(SkillId.ThickHide, "Thick Hide", "+25 maximum health.", 1, 0, 1),
            N(SkillId.SteelWall, "Steel Wall", "Blocks stop 95% of damage and cost 30% less stamina.", 1, 1, 1, SkillId.ThickHide),
            N(SkillId.Riposte, "Riposte", "Wider parry window. Parried enemies take 50% more damage for 3 s.", 1, 2, 2, SkillId.SteelWall),

            N(SkillId.DeepWell, "Deep Well", "+30 maximum mana.", 2, 0, 1),
            N(SkillId.Spellblade, "Spellblade", "+20% spell damage.", 2, 1, 1, SkillId.DeepWell),
            N(SkillId.ManaThief, "Mana Thief", "Every sword hit restores 4 mana.", 2, 2, 1, SkillId.Spellblade),
            N(SkillId.QuickenedRunes, "Quickened Runes", "Spell cooldowns are 35% shorter.", 2, 3, 2, SkillId.ManaThief),

            N(SkillId.LightFeet, "Light Feet", "Dodging costs 30% less stamina.", 3, 0, 1),
            N(SkillId.GhostStep, "Ghost Step", "Longer invulnerability while dodging.", 3, 1, 1, SkillId.LightFeet),
            N(SkillId.Momentum, "Momentum", "Stamina recovers 35% faster.", 3, 2, 2, SkillId.GhostStep),

            N(SkillId.ExtraFlask, "Extra Flask", "Find a free health flask at the start of every floor.", 4, 0, 1),
            N(SkillId.PotentFlask, "Potent Flask", "Health flasks heal 40% more.", 4, 1, 1, SkillId.ExtraFlask),
            N(SkillId.SecondChance, "Second Chance", "Once per floor, survive a killing blow with 1 health.", 4, 2, 3, SkillId.PotentFlask),
        };

        public static event Action Changed;
        static HashSet<SkillId> unlocked;

        static SkillNode N(SkillId id, string name, string desc, int col, int row, int cost, params SkillId[] req) =>
            new SkillNode { id = id, name = name, description = desc, column = col, row = row, cost = cost, requires = req, branch = Branches[col] };

        static void Load()
        {
            if (unlocked != null) return;
            unlocked = new HashSet<SkillId>();
            foreach (var s in PlayerPrefs.GetString("fv_skills", "").Split(','))
                if (int.TryParse(s, out int v)) unlocked.Add((SkillId)v);
        }

        static void Save()
        {
            var parts = new List<string>();
            foreach (var s in unlocked) parts.Add(((int)s).ToString());
            PlayerPrefs.SetString("fv_skills", string.Join(",", parts));
            PlayerPrefs.Save();
        }

        public static int Points
        {
            get => PlayerPrefs.GetInt("fv_skillpts", 0);
            set { PlayerPrefs.SetInt("fv_skillpts", Mathf.Max(0, value)); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        public static bool Has(SkillId id) { Load(); return unlocked.Contains(id); }

        public static SkillNode Get(SkillId id)
        {
            foreach (var n in All) if (n.id == id) return n;
            return null;
        }

        public static bool CanUnlock(SkillNode n)
        {
            if (Has(n.id) || Points < n.cost) return false;
            foreach (var r in n.requires) if (!Has(r)) return false;
            return true;
        }

        public static bool Unlock(SkillId id)
        {
            var n = Get(id);
            if (n == null || !CanUnlock(n)) return false;
            Points -= n.cost;
            unlocked.Add(id);
            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Console helper: unlock every node for free.</summary>
        public static void UnlockAll()
        {
            Load();
            foreach (var n in All) unlocked.Add(n.id);
            Save();
            Changed?.Invoke();
        }

        /// <summary>The learned skills as text, for the run's save file.</summary>
        public static string Export()
        {
            Load();
            var parts = new List<string>();
            foreach (var s in unlocked) parts.Add(((int)s).ToString());
            return string.Join(",", parts);
        }

        /// <summary>Puts a saved run's skill tree back.</summary>
        public static void Import(string saved, int points)
        {
            Load();
            unlocked.Clear();
            foreach (var s in (saved ?? "").Split(','))
                if (int.TryParse(s, out int v)) unlocked.Add((SkillId)v);
            Save();
            PlayerPrefs.SetInt("fv_skillpts", Mathf.Max(0, points));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>A new run: no skills learned and no skill points.</summary>
        public static void WipeForNewRun()
        {
            Load();
            unlocked.Clear();
            Save();
            PlayerPrefs.SetInt("fv_skillpts", 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Forget every node and refund the points.</summary>
        /// <summary>Forgets every skill and every skill point, as on the very first launch.</summary>
        public static void WipeAll()
        {
            Load();
            unlocked.Clear();
            Save();
            PlayerPrefs.DeleteKey("fv_skillpts");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void ResetTree()
        {
            Load();
            int refund = 0;
            foreach (var id in unlocked) { var n = Get(id); if (n != null) refund += n.cost; }
            unlocked.Clear();
            Save();
            Points += refund;
        }
    }
}
