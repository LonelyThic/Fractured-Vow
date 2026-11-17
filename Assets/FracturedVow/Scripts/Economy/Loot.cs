using UnityEngine;

namespace FracturedVow
{
    /// <summary>What drops when something dies or a chest opens: gold coins, flasks, gear and spell tomes.</summary>
    public static class Loot
    {
        public static int FloorTier => PlayerProgress.SceneName.Contains("Floor2") ? 2 : 1;

        /// <summary>Drops from a slain enemy. Elites and bosses drop more and better.</summary>
        public static void EnemyDrop(Vector3 at, bool elite, bool boss)
        {
            int coins = boss ? 30 : elite ? Random.Range(6, 10) : Random.Range(2, 5);
            int each = boss ? 10 : 3 + FloorTier * 2;
            SpawnGold(at, coins, each);
            float roll = Random.value;
            float mul = boss ? 10f : elite ? 2.5f : 1f;
            if (roll < 0.12f * mul) SpawnItem(at, Random.value < 0.7f ? "hp_flask" : "mp_flask");
            else if (roll < 0.17f * mul) SpawnItem(at, FVLibrary.RandomItem(null, FloorTier + (elite ? 1 : 0), ItemKind.Weapon, ItemKind.Armor, ItemKind.Trinket));
            else if (roll < 0.20f * mul) SpawnItem(at, FVLibrary.RandomItem(null, FloorTier + 1, ItemKind.SpellTome));
        }

        /// <summary>A chest's contents. Tier 3 is the big chest after a boss.</summary>
        public static void ChestDrop(Vector3 at, int tier)
        {
            SpawnGold(at, 6 + tier * 6, 5 + tier * 5);
            SpawnItem(at, Random.value < 0.65f ? "hp_flask" : "mp_flask");
            if (tier >= 2 || Random.value < 0.45f)
                SpawnItem(at, FVLibrary.RandomItem(null, Mathf.Min(3, FloorTier + tier - 1), ItemKind.Weapon, ItemKind.Armor, ItemKind.Trinket));
            if (tier >= 3)
            {
                SpawnItem(at, FVLibrary.RandomItem(null, 3, ItemKind.Weapon));
                SpawnItem(at, FVLibrary.RandomItem(null, 3, ItemKind.Armor));
                SpawnItem(at, FVLibrary.RandomItem(null, 3, ItemKind.SpellTome));
                SpawnItem(at, "hp_flask"); SpawnItem(at, "hp_flask");
            }
            else if (Random.value < 0.25f) SpawnItem(at, FVLibrary.RandomItem(null, 2, ItemKind.SpellTome));
        }

        public static void SpawnGold(Vector3 at, int coins, int valueEach)
        {
            for (int i = 0; i < coins; i++) GoldCoin.Spawn(at + Vector3.up * 0.8f, valueEach);
        }

        public static void SpawnItem(Vector3 at, string id) => SpawnItem(at, FVLibrary.Item(id));

        public static void SpawnItem(Vector3 at, ItemDef def)
        {
            if (def == null) return;
            Vector2 r = Random.insideUnitCircle * 1.2f;
            ItemPickup.Spawn(at + new Vector3(r.x, 0f, r.y), def);
        }
    }
}
