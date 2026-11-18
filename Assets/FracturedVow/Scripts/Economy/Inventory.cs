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
                float m = UpgradeMult(id);
                damage += d.damage * m; armor += d.armor * m; health += d.health * m; mana += d.mana * m;
            }
        }
                // ---------------- upgrades (bought at a merchant) ----------------

        public const int MaxUpgrade = 5;

        /// <summary>How many times this piece of gear has been upgraded (0 to MaxUpgrade).</summary>
        public static int UpgradeLevel(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            foreach (var u in RunState.Upgrades) if (u.id == id) return u.count;
            return 0;
        }

        /// <summary>Each upgrade adds 25% of the item's base bonuses.</summary>
        public static float UpgradeMult(string id) => 1f + 0.25f * UpgradeLevel(id);

        /// <summary>Gold for the next upgrade: half the item's price, then more for each level after.</summary>
        public static int UpgradeCost(ItemDef d)
        {
            int lvl = UpgradeLevel(d.id);
            return Mathf.RoundToInt(Mathf.Max(40, d.price) * 0.5f * (lvl + 1));
        }

        public static bool Owns(string id) =>
            Count(id) > 0 || id == RunState.EquippedWeapon || id == RunState.EquippedArmor || id == RunState.EquippedTrinket;

        /// <summary>Pays gold to raise a piece of gear one level. Returns false if it can't.</summary>
        public static bool Upgrade(string id)
        {
            var d = FVLibrary.Item(id);
            if (d == null || !d.IsGear || UpgradeLevel(id) >= MaxUpgrade) return false;
            int cost = UpgradeCost(d);
            if (RunState.Gold < cost) return false;
            RunState.AddGold(-cost);
            var u = RunState.Upgrades.Find(x => x.id == id);
            if (u == null) RunState.Upgrades.Add(new ItemStack { id = id, count = 1 });
            else u.count++;
            PrototypeHUD.Message(d.name + " upgraded to +" + UpgradeLevel(id), 1.8f);
            Refresh();
            return true;
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
