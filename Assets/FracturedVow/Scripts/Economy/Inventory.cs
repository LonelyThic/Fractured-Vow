using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// The knight's bag (part of the run): flasks, gear and tomes with counts, plus the three equipment slots
    /// (weapon, armor, trinket). Equipped gear adds to the knight's stats through PlayerProgress.
    /// </summary>
    public static class Inventory
    {
        public static int Count(string id)
        {
            foreach (var s in RunState.Items) if (s.id == id) return s.count;
            return 0;
        }

        public static void Add(string id, int count = 1, bool announce = true)
        {
            var def = FVLibrary.Item(id);
            if (def == null || count <= 0) return;
            var stack = RunState.Items.Find(s => s.id == id);
            if (stack == null) RunState.Items.Add(new ItemStack { id = id, count = count });
            else stack.count += count;
            if (announce) PrototypeHUD.Message("+" + (count > 1 ? count + " " : "") + def.name, 1.8f);
            RunState.Notify();
        }

        public static bool Remove(string id, int count = 1)
        {
            var stack = RunState.Items.Find(s => s.id == id);
            if (stack == null || stack.count < count) return false;
            stack.count -= count;
            if (stack.count <= 0) RunState.Items.Remove(stack);
            RunState.Notify();
            return true;
        }

        public static string Equipped(ItemKind kind) =>
            kind == ItemKind.Weapon ? RunState.EquippedWeapon : kind == ItemKind.Armor ? RunState.EquippedArmor : kind == ItemKind.Trinket ? RunState.EquippedTrinket : null;

        static void SetSlot(ItemKind kind, string id)
        {
            if (kind == ItemKind.Weapon) RunState.EquippedWeapon = id;
            else if (kind == ItemKind.Armor) RunState.EquippedArmor = id;
            else if (kind == ItemKind.Trinket) RunState.EquippedTrinket = id;
        }

        /// <summary>Equips gear from the bag; whatever was in that slot goes back into the bag.</summary>
        public static void Equip(string id)
        {
            var def = FVLibrary.Item(id);
            if (def == null || !def.IsGear || !Remove(id)) return;
            string old = Equipped(def.kind);
            if (!string.IsNullOrEmpty(old)) Add(old, 1, false);
            SetSlot(def.kind, id);
            PrototypeHUD.Message("Equipped " + def.name, 1.5f);
            GameAudio.PlayUI("pickup_gear", 0.6f);
            Refresh();
        }

        public static void Unequip(ItemKind kind)
        {
            string old = Equipped(kind);
            if (string.IsNullOrEmpty(old)) return;
            SetSlot(kind, null);
            Add(old, 1, false);
            Refresh();
        }

        /// <summary>The total bonus of everything the knight is wearing.</summary>
        public static void Bonuses(out float damage, out float armor, out float health, out float mana)
        {
            damage = armor = health = mana = 0f;
            foreach (var id in new[] { RunState.EquippedWeapon, RunState.EquippedArmor, RunState.EquippedTrinket })
            {
                var d = string.IsNullOrEmpty(id) ? null : FVLibrary.Item(id);
                if (d == null) continue;
                damage += d.damage; armor += d.armor; health += d.health; mana += d.mana;
            }
        }

        /// <summary>Uses an item from the bag: flasks heal, tomes teach spells, gear gets equipped.</summary>
        public static void Use(string id, GameObject player)
        {
            var def = FVLibrary.Item(id);
            if (def == null || player == null) return;
            if (def.IsGear) { Equip(id); return; }
            if (def.kind == ItemKind.SpellTome)
            {
                var magic = player.GetComponent<PlayerMagic>();
                if (magic != null && Remove(id)) magic.LearnSpell(def.spell);
                return;
            }
            var flask = player.GetComponent<PlayerFlask>();
            if (flask != null) flask.Drink(def.kind == ItemKind.ManaFlask);
        }

        static void Refresh()
        {
            var p = Object.FindFirstObjectByType<PlayerProgress>();
            if (p != null) { p.Recalculate(true); PlayerLook.Apply(p.gameObject); }
            RunState.Notify();
        }
    }
}
