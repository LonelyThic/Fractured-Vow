using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>The merchant: buy gear, spell tomes and flasks for gold, or sell what you don't need.</summary>
    public class ShopNPC : MonoBehaviour, IInteractable
    {
        public string merchantName = "Odran the Peddler";
        public int model = 0;
        public readonly List<string> stock = new List<string>();
        [Tooltip("How many of each flask a merchant has for the whole run (gear and tomes: one each).")]
        public int flaskStock = 5;

        string Key(string id) => PlayerProgress.SceneName + "|" + merchantName + "|" + id;

        /// <summary>How many more of this item the merchant can sell (stock is limited and doesn't refill).</summary>
        public int Left(ItemDef d)
        {
            int limit = d.kind == ItemKind.HealthFlask || d.kind == ItemKind.ManaFlask ? flaskStock : 1;
            string k = Key(d.id);
            foreach (var b in RunState.ShopBought) if (b.id == k) return Mathf.Max(0, limit - b.count);
            return limit;
        }

        public void RecordSale(string id)
        {
            string k = Key(id);
            foreach (var b in RunState.ShopBought) if (b.id == k) { b.count++; return; }
            RunState.ShopBought.Add(new ItemStack { id = k, count = 1 });
        }

        public string InteractionPrompt => "Trade with " + merchantName;
        public bool CanInteract(PlayerInteractor who) => true;
        public void Interact(PlayerInteractor who) => ShopUI.Open(this);

        void Start()
        {
            var lib = FVLibrary.Get;
            if (lib != null && lib.MerchantChar != null && CharacterVisuals.NeedsRealModel(transform))
                CharacterVisuals.SpawnModel(lib.MerchantChar, transform, 1.8f);
            var m = transform.Find("Model");
            if (m != null) ModularOutfit.Apply(m.gameObject, "cloth_00", 7);
            NpcFacing.On(gameObject);
            Restock();
        }

        public void Restock()
        {
            stock.Clear();
            var rng = new System.Random(RunState.Seed * 7 + PlayerProgress.SceneName.GetHashCode());
            int tier = Loot.FloorTier + 1;
            stock.Add("hp_flask");
            stock.Add("mp_flask");
            void AddRandom(params ItemKind[] kinds)
            {
                for (int i = 0; i < 6; i++)
                {
                    var d = FVLibrary.RandomItem(rng, tier, kinds);
                    if (d != null && !stock.Contains(d.id)) { stock.Add(d.id); return; }
                }
            }
            AddRandom(ItemKind.Weapon); AddRandom(ItemKind.Weapon);
            AddRandom(ItemKind.Armor); AddRandom(ItemKind.Armor);
            AddRandom(ItemKind.Trinket);
            AddRandom(ItemKind.SpellTome); AddRandom(ItemKind.SpellTome);
        }
    }
}
