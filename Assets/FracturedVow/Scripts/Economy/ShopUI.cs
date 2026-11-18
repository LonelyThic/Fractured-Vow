using UnityEngine;
using UnityEngine.UI;

namespace FracturedVow
{
    /// <summary>The merchant's window: BUY (gold for gear, tomes, flasks) and SELL (bag items for 40% of their price).</summary>
    public class ShopUI : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        static ShopUI inst;

        ShopNPC shop;
        GameObject root;
        RectTransform page;
        bool selling;
        int tab; // 0 buy, 1 sell, 2 upgrade
        int openFrame;

        public static void Open(ShopNPC npc)
        {
            if (inst == null) inst = new GameObject("Shop UI").AddComponent<ShopUI>();
            inst.Show(npc);
        }

        void Awake()
        {
            UIKit.EnsureEventSystem();
            var canvas = UIKit.Canvas("Shop Canvas", 95, transform);
            root = UIKit.Box(canvas, "Shop", new Color(0f, 0f, 0f, 0.7f)).gameObject;
            UIKit.Stretch((RectTransform)root.transform);
            root.SetActive(false);
        }

        void OnDestroy() { if (IsOpen) { IsOpen = false; Time.timeScale = 1f; } if (inst == this) inst = null; }

        void Show(ShopNPC npc)
        {
            shop = npc;
            if (npc != null && npc.stock.Count == 0) npc.Restock();
            tab = 0;
            openFrame = Time.frameCount;
            IsOpen = true;
            root.SetActive(true);
            Time.timeScale = 0f;
            PlayerInputHandler.SetCursorLocked(false);
            GameAudio.PlayUI("pickup_gear", 0.5f);
            Rebuild();
        }

        void Close()
        {
            IsOpen = false;
            UIFocus.LastClosedFrame = Time.frameCount;
            root.SetActive(false);
            Time.timeScale = 1f;
            PlayerInputHandler.SetCursorLocked(true);
        }

        void Update()
        {
            if (IsOpen && Time.frameCount > openFrame && (UIKit.KeyDown("escape") || InputBindings.Pressed(GameAction.Interact))) Close();
        }

        static void Tint(Button b, Color c) { var cb = b.colors; cb.normalColor = c; cb.selectedColor = c; b.colors = cb; }

        Text Txt(string s, int size, Color c, TextAnchor a, Vector2 pos, Vector2 sz)
        {
            var t = UIKit.Label(page, s, size, c, a);
            UIKit.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, sz);
            return t;
        }

        void Rebuild()
        {
            if (page != null) Destroy(page.gameObject);
            page = UIKit.Box(root.transform, "Page", UIKit.Panel, true).rectTransform;
            UIKit.Place(page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 860f));
            Txt(shop != null ? shop.merchantName.ToUpperInvariant() : "MERCHANT", 34, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(36f, -24f), new Vector2(700f, 50f));
            Txt("Gold: " + RunState.Gold, 28, new Color(1f, 0.85f, 0.35f), TextAnchor.UpperRight, new Vector2(700f, -28f), new Vector2(560f, 44f));
            var buy = UIKit.Button(page, "BUY", () => { tab = 0; Rebuild(); }, 22);
            UIKit.Place((RectTransform)buy.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, -86f), new Vector2(180f, 50f));
            var sell = UIKit.Button(page, "SELL", () => { tab = 1; Rebuild(); }, 22);
            UIKit.Place((RectTransform)sell.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, -86f), new Vector2(180f, 50f));
            var upg = UIKit.Button(page, "UPGRADE", () => { tab = 2; Rebuild(); }, 22);
            UIKit.Place((RectTransform)upg.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(424f, -86f), new Vector2(200f, 50f));
            Tint(tab == 0 ? buy : tab == 1 ? sell : upg, new Color(0.55f, 0.42f, 0.22f));
            var close = UIKit.Button(page, "LEAVE  (Esc)", Close, 20);
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -86f), new Vector2(200f, 50f));

            float y = -160f;
            if (tab == 2) UpgradeRows(ref y);
            else if (tab == 0 && shop != null)
            {
                foreach (var id in shop.stock)
                {
                    var d = FVLibrary.Item(id);
                    if (d == null) continue;
                    int left = shop.Left(d);
                    Row(d, d.price, left > 0 ? "Buy  (" + left + " left)" : "Sold out", () =>
                    {
                        if (shop.Left(d) <= 0) { PrototypeHUD.Message("Sold out", 1.2f); return; }
                        if (RunState.Gold < d.price) { PrototypeHUD.Message("Not enough gold", 1.2f); GameAudio.PlayUI("ui_click", 0.5f); return; }
                        RunState.AddGold(-d.price);
                        Inventory.Add(d.id);
                        shop.RecordSale(d.id);
                        GameAudio.PlayUI("pickup_gear", 0.7f);
                        Rebuild();
                    }, ref y, RunState.Gold >= d.price && left > 0);
                }
            }
            else if (tab == 1)
            {
                if (RunState.Items.Count == 0) Txt("Your bag is empty.", 24, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(40f, y), new Vector2(800f, 40f));
                foreach (var s in RunState.Items.ToArray())
                {
                    var d = FVLibrary.Item(s.id);
                    if (d == null) continue;
                    int value = Mathf.Max(1, Mathf.RoundToInt(d.price * 0.4f));
                    Row(d, value, "Sell (x" + s.count + ")", () =>
                    {
                        if (Inventory.Remove(d.id)) { RunState.AddGold(value); GameAudio.Play("pickup", Vector3.zero, 0.3f, 0.1f, 1.5f); }
                        Rebuild();
                    }, ref y, true);
                }
            }
        }

                /// <summary>Everything the knight owns that can be upgraded: what he's wearing first, then the bag.</summary>
        void UpgradeRows(ref float y)
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (var id in new[] { RunState.EquippedWeapon, RunState.EquippedArmor, RunState.EquippedTrinket })
                if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
            foreach (var s in RunState.Items)
            {
                var d0 = FVLibrary.Item(s.id);
                if (d0 != null && d0.IsGear && !ids.Contains(s.id)) ids.Add(s.id);
            }
            Txt("Each upgrade adds 25% to the gear's bonuses, up to +" + Inventory.MaxUpgrade + ".", 18, UIKit.Dim, TextAnchor.UpperLeft,
                new Vector2(650f, -92f), new Vector2(390f, 50f));
            if (ids.Count == 0) { Txt("You have no gear to upgrade.", 24, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(40f, y), new Vector2(800f, 40f)); return; }
            foreach (var id in ids)
            {
                var d = FVLibrary.Item(id);
                if (d == null) continue;
                int lvl = Inventory.UpgradeLevel(id);
                bool max = lvl >= Inventory.MaxUpgrade;
                int cost = Inventory.UpgradeCost(d);
                bool worn = id == RunState.EquippedWeapon || id == RunState.EquippedArmor || id == RunState.EquippedTrinket;
                string label = d.name + (lvl > 0 ? "  +" + lvl : "") + (worn ? "   (equipped)" : "");
                string info = max ? d.DescribeAt(lvl) + "   - fully upgraded"
                                  : "Now: " + d.DescribeAt(lvl).Split('(')[0].Trim() + "   >   Next: " + d.DescribeAt(lvl + 1).Split('(')[0].Trim();
                Row(d, max ? 0 : cost, max ? "Max" : "Upgrade to +" + (lvl + 1), () =>
                {
                    if (Inventory.Upgrade(id)) GameAudio.PlayUI("pickup_gear", 0.8f);
                    else { PrototypeHUD.Message("Not enough gold", 1.2f); GameAudio.PlayUI("ui_click", 0.5f); }
                    Rebuild();
                }, ref y, !max && RunState.Gold >= cost, label, info);
            }
        }

        void Row(ItemDef d, int price, string action, System.Action onClick, ref float y, bool affordable, string title = null, string info = null)
        {
            if (y < -800f) return;
            var row = UIKit.Box(page, "Row", new Color(1f, 1f, 1f, 0.05f)).rectTransform;
            UIKit.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(36f, y), new Vector2(1228f, 66f));
            var sw = UIKit.Box(page, "Swatch", d.color).rectTransform;
            UIKit.AddIcon(sw, d);
            UIKit.Place(sw, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, y - 10f), new Vector2(46f, 46f));
            Txt(title ?? d.name, 24, UIKit.Ink, TextAnchor.UpperLeft, new Vector2(110f, y - 6f), new Vector2(700f, 32f));
            Txt(info ?? d.Describe() + (d.tier > 1 ? "   tier " + d.tier : ""), 17, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(110f, y - 38f), new Vector2(700f, 26f));
            if (price > 0) Txt(price + " gold", 22, affordable ? new Color(1f, 0.85f, 0.35f) : new Color(0.7f, 0.35f, 0.3f), TextAnchor.MiddleRight, new Vector2(820f, y - 16f), new Vector2(200f, 34f));
            var b = UIKit.Button(page, action, onClick, 20);
            UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1050f, y - 8f), new Vector2(200f, 50f));
            b.interactable = affordable;
            y -= 74f;
        }
    }
}
