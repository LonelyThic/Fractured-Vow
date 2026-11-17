using System;
using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    public enum ItemKind { HealthFlask, ManaFlask, Weapon, Armor, Trinket, SpellTome }

    /// <summary>One kind of item: what it is, what it does, what it costs, and (optionally) its 3D model.</summary>
    [Serializable]
    public class ItemDef
    {
        public string id;
        public string name;
        public ItemKind kind;
        [Tooltip("Weapons: extra sword damage (0.2 = +20%).")] public float damage;
        [Tooltip("Armor: damage reduction (0.1 = 10%).")] public float armor;
        public float health, mana;
        public int price = 50;
        public int tier = 1;
        public SpellType spell;
        [Tooltip("Armor only: which Polytope modular armor set a companion wears with it (cloth_00, 01_A, 05_C).")]
        public string outfit;
        public GameObject model;
        public Color color = Color.white;

        public bool IsGear => kind == ItemKind.Weapon || kind == ItemKind.Armor || kind == ItemKind.Trinket;

        public string Describe()
        {
            switch (kind)
            {
                case ItemKind.HealthFlask: return "Restores " + PlayerFlask.HealthRestore + " health (R).";
                case ItemKind.ManaFlask: return "Restores " + PlayerFlask.ManaRestore + " mana (V).";
                case ItemKind.SpellTome: return "Teaches or upgrades " + Spell.Create(spell).displayName + ".";
            }
            var parts = new List<string>();
            if (damage > 0f) parts.Add("+" + Mathf.RoundToInt(damage * 100f) + "% damage");
            if (armor > 0f) parts.Add("+" + Mathf.RoundToInt(armor * 100f) + "% armor");
            if (health > 0f) parts.Add("+" + Mathf.RoundToInt(health) + " health");
            if (mana > 0f) parts.Add("+" + Mathf.RoundToInt(mana) + " mana");
            return string.Join(", ", parts);
        }
    }

    /// <summary>
    /// Everything the imported Asset Store packs provide, gathered by Tools > Fractured Vow > Set Up Imported Packs
    /// and saved as Resources/FVLibrary. The game reads it at runtime; anything missing falls back to built-in visuals.
    /// </summary>
    [CreateAssetMenu(menuName = "Fractured Vow/Asset Library")]
    public class FVLibrary : ScriptableObject
    {
        [Serializable]
        public class Character
        {
            public string name;
            public GameObject prefab;
            public RuntimeAnimatorController controller;
            [Tooltip("The model has no skeleton (Monster Pack): it gets a code-driven stomping walk instead of clips.")]
            public bool stomp;
        }

        [Serializable]
        public class Prop
        {
            public string category;
            public GameObject prefab;
        }

        [Header("Characters")]
        public List<Character> heroes = new List<Character>();      // merchants, companions, the prisoner
        public List<Character> enemyMelee = new List<Character>();  // skeletons
        public List<Character> enemyRanged = new List<Character>(); // necromancers
        public List<Character> enemyBrute = new List<Character>();  // monsters

        [Header("Items and props")]
        public List<ItemDef> items = new List<ItemDef>();
        public List<Prop> props = new List<Prop>();
        public List<GameObject> arenaProps = new List<GameObject>(); // Deep Sea Hunter's Station
        public GameObject chest, bigChest, coin, boat;

        [Header("Special characters")]
        [Tooltip("The shopkeeper (Stylized NPC - Peasant Nolant).")]
        public Character merchant;
        [Tooltip("Sellswords you can hire: the same model as the player's hero.")]
        public Character companion;

        /// <summary>The merchant's character, or the first hero if Peasant Nolant isn't imported.</summary>
        public Character MerchantChar => merchant != null && merchant.prefab != null ? merchant : heroes.Count > 0 ? heroes[0] : null;
        /// <summary>The companions' character (the player's model), or a hero from the list.</summary>
        public Character CompanionChar(int i) => companion != null && companion.prefab != null ? companion : heroes.Count > 0 ? heroes[i % heroes.Count] : null;

        [Header("Dungeon dressing")]
        public GameObject torchModel, chandelier, throne, fireplace, carpet;

        [Header("World effects (Particle Pack)")]
        public GameObject hitFlesh, hitMetal, torchFlame, groundFog, fireflies, respawn, candles;
        [Tooltip("Flame trail behind a fireball (Particle Pack RocketTrail).")]
        public GameObject fireTrail;

        [Header("Spell effects (Particle Pack)")]
        public GameObject fireBolt, fireImpact, frostBolt, frostImpact, castFlash, wallBurst, splash;

        static FVLibrary cached;
        static bool looked;

        public static FVLibrary Get
        {
            get
            {
                if (!looked) { looked = true; cached = Resources.Load<FVLibrary>("FVLibrary"); }
                return cached;
            }
        }

        // ---------------- item catalog ----------------

        static List<ItemDef> catalog;

        /// <summary>All items: the library's (with models) plus built-in flasks, tomes and trinkets.</summary>
        public static List<ItemDef> Catalog
        {
            get
            {
                if (catalog != null) return catalog;
                catalog = new List<ItemDef>();
                catalog.Add(new ItemDef { id = "hp_flask", name = "Health Flask", kind = ItemKind.HealthFlask, price = 40, color = new Color(0.9f, 0.2f, 0.18f) });
                catalog.Add(new ItemDef { id = "mp_flask", name = "Mana Flask", kind = ItemKind.ManaFlask, price = 35, color = new Color(0.25f, 0.45f, 1f) });
                catalog.Add(new ItemDef { id = "tome_fire", name = "Firebolt Tome", kind = ItemKind.SpellTome, spell = SpellType.Firebolt, price = 120, tier = 1, color = new Color(1f, 0.45f, 0.1f) });
                catalog.Add(new ItemDef { id = "tome_frost", name = "Frost Shard Tome", kind = ItemKind.SpellTome, spell = SpellType.FrostShard, price = 140, tier = 2, color = new Color(0.45f, 0.8f, 1f) });
                catalog.Add(new ItemDef { id = "tome_wall", name = "Stone Wall Tome", kind = ItemKind.SpellTome, spell = SpellType.StoneWall, price = 160, tier = 2, color = new Color(0.75f, 0.65f, 0.5f) });
                catalog.Add(new ItemDef { id = "ring_vigor", name = "Ring of Vigor", kind = ItemKind.Trinket, health = 25f, price = 150, tier = 1, color = new Color(1f, 0.4f, 0.35f) });
                catalog.Add(new ItemDef { id = "amulet_mind", name = "Amulet of the Mind", kind = ItemKind.Trinket, mana = 30f, price = 150, tier = 1, color = new Color(0.45f, 0.6f, 1f) });
                catalog.Add(new ItemDef { id = "sigil_oath", name = "Oathkeeper Sigil", kind = ItemKind.Trinket, health = 40f, armor = 0.05f, price = 320, tier = 3, color = new Color(1f, 0.8f, 0.4f) });

                var lib = Get;
                bool hasWeapons = false, hasArmor = false;
                if (lib != null)
                    foreach (var it in lib.items)
                    {
                        if (it == null || string.IsNullOrEmpty(it.id)) continue;
                        catalog.Add(it);
                        if (it.kind == ItemKind.Weapon) hasWeapons = true;
                        if (it.kind == ItemKind.Armor) hasArmor = true;
                    }
                if (!hasWeapons)
                {
                    catalog.Add(new ItemDef { id = "w_rusty", name = "Rusted Blade", kind = ItemKind.Weapon, damage = 0.1f, price = 90, tier = 1, color = new Color(0.6f, 0.5f, 0.4f) });
                    catalog.Add(new ItemDef { id = "w_knight", name = "Knight's Longsword", kind = ItemKind.Weapon, damage = 0.22f, price = 220, tier = 2, color = new Color(0.8f, 0.85f, 0.95f) });
                    catalog.Add(new ItemDef { id = "w_vow", name = "Vowbreaker", kind = ItemKind.Weapon, damage = 0.38f, price = 420, tier = 3, color = new Color(1f, 0.75f, 0.4f) });
                }
                if (!hasArmor)
                {
                    catalog.Add(new ItemDef { id = "a_leather", name = "Leather Brigandine", kind = ItemKind.Armor, armor = 0.08f, price = 100, tier = 1, color = new Color(0.5f, 0.35f, 0.2f) });
                    catalog.Add(new ItemDef { id = "a_mail", name = "Chainmail Hauberk", kind = ItemKind.Armor, armor = 0.15f, health = 10f, price = 230, tier = 2, color = new Color(0.65f, 0.65f, 0.7f) });
                    catalog.Add(new ItemDef { id = "a_plate", name = "Oathsworn Plate", kind = ItemKind.Armor, armor = 0.25f, health = 20f, price = 450, tier = 3, color = new Color(0.85f, 0.8f, 0.7f) });
                }
                return catalog;
            }
        }

        public static ItemDef Item(string id)
        {
            foreach (var i in Catalog) if (i.id == id) return i;
            return null;
        }

        /// <summary>A random item of the given kinds, at or below a tier.</summary>
        public static ItemDef RandomItem(System.Random rng, int maxTier, params ItemKind[] kinds)
        {
            var pool = new List<ItemDef>();
            foreach (var i in Catalog)
                if (i.tier <= maxTier && Array.IndexOf(kinds, i.kind) >= 0) pool.Add(i);
            if (pool.Count == 0) return null;
            return pool[rng != null ? rng.Next(pool.Count) : UnityEngine.Random.Range(0, pool.Count)];
        }

        public static GameObject PropOf(string category, System.Random rng)
        {
            var lib = Get;
            if (lib == null) return null;
            var pool = lib.props.FindAll(p => p != null && p.prefab != null && p.category == category);
            if (pool.Count == 0) return null;
            return pool[rng != null ? rng.Next(pool.Count) : UnityEngine.Random.Range(0, pool.Count)].prefab;
        }
    }
}
