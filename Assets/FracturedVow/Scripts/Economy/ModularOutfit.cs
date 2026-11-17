using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Dresses a Polytope modular character: of all the armor pieces built into the model, shows one coherent set
    /// (cloth_00 = plain clothes, 01_A = ranger's mail, 05_C = knight's plate) plus one head, hair and beard.
    /// Characters without modular parts are left untouched.
    /// </summary>
    public static class ModularOutfit
    {
        static readonly string[] Slots = { "body", "legs", "boots", "gauntlets", "helmet", "cape" };
        static readonly string[] Sets = { "05_c", "01_a", "cloth_00", "naked" };

        public static bool IsModular(GameObject model)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name.Contains("_Armor_")) return true;
            return false;
        }

        public static void Apply(GameObject model, string outfit, int variant)
        {
            if (model == null) return;
            var parts = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("_Armor_") && t.GetComponent<Renderer>() != null) parts.Add(t);
            if (parts.Count == 0) return;
            string set = string.IsNullOrEmpty(outfit) ? "cloth_00" : outfit.ToLowerInvariant();
            bool armored = set == "01_a" || set == "05_c";

            var heads = parts.FindAll(p => p.name.ToLowerInvariant().Contains("head_"));
            var hairs = parts.FindAll(p => p.name.ToLowerInvariant().Contains("hair_"));
            var beards = parts.FindAll(p => p.name.ToLowerInvariant().Contains("beard_"));
            foreach (var p in parts) p.gameObject.SetActive(false);

            bool helmet = false;
            foreach (var slot in Slots)
            {
                Transform pick = Find(parts, set, slot);
                if (pick == null && (slot == "helmet" || slot == "cape")) continue;  // plain clothes: no helmet or cape
                if (pick == null) pick = Find(parts, "cloth_00", slot) ?? Find(parts, "naked", slot);
                if (pick == null) continue;
                if ((slot == "helmet" || slot == "cape") && !armored) continue;
                pick.gameObject.SetActive(true);
                if (slot == "helmet") helmet = true;
            }
            int v = Mathf.Abs(variant);
            if (heads.Count > 0) heads[v % heads.Count].gameObject.SetActive(true);
            if (hairs.Count > 0 && !helmet) hairs[(v / 2) % hairs.Count].gameObject.SetActive(true);
            if (beards.Count > 0 && v % 3 != 2) beards[v % beards.Count].gameObject.SetActive(true);
        }

        static Transform Find(List<Transform> parts, string set, string slot)
        {
            foreach (var p in parts)
            {
                string n = p.name.ToLowerInvariant();
                if (n.Contains(set) && n.Contains(slot)) return p;
            }
            return null;
        }
    }
}
