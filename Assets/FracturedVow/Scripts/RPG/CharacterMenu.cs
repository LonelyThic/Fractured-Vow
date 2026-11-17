using System;
using UnityEngine;
using UnityEngine.UI;

namespace FracturedVow
{
    /// <summary>
    /// Tab menu with three pages:
    ///  CHARACTER - spend Memory Shards to level Vigor, Endurance, Strength or Intelligence; see every derived stat.
    ///  SKILLS    - the five-branch skill tree (resets with each new run).
    ///  SPELLS    - your spell inventory: put spells in hotbar slots 1-3, or drop them as tomes on the ground.
    /// Pauses the game while open.
    /// </summary>
    public class CharacterMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        RectTransform canvas, page;
        GameObject root;
        int tab;
        SkillNode selected;
        PlayerController player;
        PlayerProgress progress;
        PlayerStats stats;
        PlayerMagic magic;
        Health health;

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) { Destroy(gameObject); return; }
            progress = player.GetComponent<PlayerProgress>();
            stats = player.GetComponent<PlayerStats>();
            magic = player.GetComponent<PlayerMagic>();
            health = player.GetComponent<Health>();
            UIKit.EnsureEventSystem();
            canvas = UIKit.Canvas("Character Menu Canvas", 90, transform);
            root = UIKit.Box(canvas, "Character Menu", new Color(0f, 0f, 0f, 0.7f)).gameObject;
            UIKit.Stretch((RectTransform)root.transform);
            root.SetActive(false);
        }

        void OnDestroy() { if (IsOpen) { IsOpen = false; Time.timeScale = 1f; } }

        void Update()
        {
            if (root == null || DevConsole.IsOpen || ShopUI.IsOpen || CutsceneDirector.Playing || LoreNote.Reading || CheatMenu.IsOpen) return;
            if (Time.frameCount - UIFocus.LastClosedFrame <= 1) return; // the key that closed a page doesn't also close this
            if (!IsOpen && GameMenu.IsOpen) return;
            if (InputBindings.Pressed(GameAction.Character)) { if (IsOpen) Close(); else Open(); }
            else if (IsOpen && UIKit.KeyDown("escape")) Close();
        }

        void Open()
        {
            if (health != null && health.IsDead) return;
            IsOpen = true;
            root.SetActive(true);
            Time.timeScale = 0f;
            PlayerInputHandler.SetCursorLocked(false);
            Rebuild();
        }

        void Close()
        {
            IsOpen = false;
            UIFocus.LastClosedFrame = Time.frameCount;
            root.SetActive(false);
            if (!GameMenu.IsOpen) { Time.timeScale = 1f; PlayerInputHandler.SetCursorLocked(true); }
        }

        void Rebuild()
        {
            if (page != null) Destroy(page.gameObject);
            page = UIKit.Box(root.transform, "Page", UIKit.Panel, true).rectTransform;
            UIKit.Place(page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 860f));

            string[] names = { "CHARACTER", "INVENTORY", "SKILLS", "SPELLS", "COMPANIONS", "JOURNAL" };
            for (int i = 0; i < names.Length; i++)
            {
                int t = i;
                var b = UIKit.Button(page, names[i], () => { tab = t; Rebuild(); }, 22);
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f + i * 206f, -24f), new Vector2(196f, 54f));
                if (i == tab) Tint(b, new Color(0.55f, 0.42f, 0.22f));
            }
            var close = UIKit.Button(page, "CLOSE", Close, 20);
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(160f, 54f));

            if (tab == 0) BuildCharacter();
            else if (tab == 1) BuildInventory();
            else if (tab == 2) BuildSkills();
            else if (tab == 3) BuildSpells();
            else if (tab == 4) BuildCompanions();
            else BuildJournal();
        }

        Text Txt(string s, int size, Color c, TextAnchor a, Vector2 pos, Vector2 sizeRect)
        {
            var t = UIKit.Label(page, s, size, c, a);
            UIKit.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), pos, sizeRect);
            return t;
        }

        static void Tint(Button b, Color c)
        {
            var cb = b.colors;
            cb.normalColor = c; cb.selectedColor = c;
            b.colors = cb;
        }

        // ---------------- JOURNAL ----------------

        int journalPick;

        /// <summary>Every note picked up this run: titles on the left, the page on the right.</summary>
        void BuildJournal()
        {
            Txt("JOURNAL", 30, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(40f, -100f), new Vector2(600f, 40f));
            int total = 16;
            Txt(RunState.Notes.Count + " of " + total + " pages found. Notes lie near checkpoints, merchants, chests and boats.", 18, UIKit.Dim,
                TextAnchor.UpperLeft, new Vector2(40f, -140f), new Vector2(1300f, 30f));
            if (RunState.Notes.Count == 0)
            {
                Txt("No pages yet. Look for loose parchment glowing on the floor, and press F to pick it up.", 24, UIKit.Ink,
                    TextAnchor.UpperLeft, new Vector2(40f, -200f), new Vector2(1300f, 40f));
                return;
            }
            journalPick = Mathf.Clamp(journalPick, 0, RunState.Notes.Count - 1);
            for (int i = 0; i < RunState.Notes.Count && i < 14; i++)
            {
                int idx = i;
                var b = UIKit.Button(page, RunState.Notes[i], () => { journalPick = idx; Rebuild(); }, 18);
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -190f - i * 46f), new Vector2(500f, 40f));
                if (i == journalPick) Tint(b, new Color(0.55f, 0.42f, 0.22f));
            }
            string title = RunState.Notes[journalPick];
            var paper = UIKit.Box(page, "Parchment", new Color(0.86f, 0.79f, 0.62f), true).rectTransform;
            UIKit.Place(paper, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(580f, -190f), new Vector2(860f, 620f));
            var t = UIKit.Label(paper, title.ToUpperInvariant(), 28, new Color(0.3f, 0.18f, 0.08f), TextAnchor.UpperCenter);
            UIKit.Place((RectTransform)t.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(780f, 44f));
            t.fontStyle = FontStyle.Bold;
            var body = UIKit.Label(paper, LoreNote.BodyOf(title), 24, new Color(0.18f, 0.12f, 0.06f), TextAnchor.UpperLeft);
            UIKit.Place((RectTransform)body.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(760f, 500f));
            body.fontStyle = FontStyle.Italic;
            body.lineSpacing = 1.15f;
        }

        // ---------------- CHARACTER ----------------

        void BuildCharacter()
        {
            int cost = RunState.LevelCost;
            Txt("LEVEL " + RunState.Level, 44, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(40f, -110f), new Vector2(600f, 60f));
            Txt("Memory Shards: " + RunState.Shards.ToString("N0") + "      Next level: " + cost, 24, UIKit.Ink, TextAnchor.UpperLeft, new Vector2(40f, -172f), new Vector2(700f, 34f));
            Txt("Each level raises one attribute and earns a skill point.", 18, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(40f, -208f), new Vector2(700f, 30f));

            string[] attr = { "VIGOR", "ENDURANCE", "STRENGTH", "INTELLIGENCE" };
            string[] desc = { "+12 max health", "+10 stamina, faster recovery", "+6% sword damage", "+8 mana, +7% spell damage" };
            int[] val = { RunState.Vigor, RunState.Endurance, RunState.Strength, RunState.Intelligence };
            for (int i = 0; i < 4; i++)
            {
                int a = i;
                float y = -270f - i * 92f;
                var row = UIKit.Box(page, "Attr", new Color(1f, 1f, 1f, 0.04f)).rectTransform;
                UIKit.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(700f, 80f));
                Txt(attr[i], 28, UIKit.Ink, TextAnchor.MiddleLeft, new Vector2(60f, y - 6f), new Vector2(300f, 40f));
                Txt(desc[i], 18, UIKit.Dim, TextAnchor.MiddleLeft, new Vector2(60f, y - 42f), new Vector2(400f, 30f));
                Txt(val[i].ToString(), 34, UIKit.Gold, TextAnchor.MiddleRight, new Vector2(440f, y - 18f), new Vector2(120f, 44f));
                var b = UIKit.Button(page, "+", () => { if (progress != null && progress.LevelUp(a)) Rebuild(); }, 34);
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(600f, y - 12f), new Vector2(110f, 56f));
                b.interactable = RunState.Shards >= cost;
            }

            var lines = new[]
            {
                "Max health      " + Mathf.RoundToInt(health.maxHealth),
                "Stamina         " + Mathf.RoundToInt(stats.maxStamina) + "  (+" + stats.staminaRegenPerSecond.ToString("0") + "/s)",
                "Mana            " + Mathf.RoundToInt(stats.maxMana),
                "Armor           " + Mathf.RoundToInt(health.armor * 100f) + "%",
                "Sword damage    x" + stats.damageMultiplier.ToString("0.00"),
                "Spell damage    x" + stats.spellMultiplier.ToString("0.00"),
                "Block           " + Mathf.RoundToInt(stats.blockReduction * 100f) + "%   parry window " + stats.parryWindow.ToString("0.00") + " s",
                "Flasks          " + Inventory.Count("hp_flask") + " health, " + Inventory.Count("mp_flask") + " mana  (heal x" + stats.flaskHealMult.ToString("0.0") + ")",
                "Gold            " + RunState.Gold,
                "",
                "Run " + RunState.RunNumber + "   seed " + RunState.Seed + "   kills " + RunState.Kills,
                "Gear: " + (stats.equippedGear.Count > 0 ? string.Join(", ", stats.equippedGear) : "none"),
            };
            Txt("STATS", 28, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(820f, -110f), new Vector2(600f, 40f));
            Txt(string.Join("\n", lines), 21, UIKit.Ink, TextAnchor.UpperLeft, new Vector2(820f, -160f), new Vector2(640f, 600f));
        }

        // ---------------- INVENTORY ----------------

        void BuildInventory()
        {
            Txt("EQUIPPED", 28, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(40f, -100f), new Vector2(400f, 40f));
            var kinds = new[] { ItemKind.Weapon, ItemKind.Armor, ItemKind.Trinket };
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                float y = -150f - i * 96f;
                string id = Inventory.Equipped(kind);
                var d = string.IsNullOrEmpty(id) ? null : FVLibrary.Item(id);
                var box = UIKit.Box(page, "Slot", new Color(1f, 1f, 1f, 0.05f)).rectTransform;
                UIKit.Place(box, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(440f, 84f));
                Txt(kind.ToString().ToUpperInvariant(), 16, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(54f, y - 6f), new Vector2(200f, 22f));
                Txt(d != null ? d.name : "(empty)", 22, d != null ? UIKit.Ink : UIKit.Dim, TextAnchor.UpperLeft, new Vector2(54f, y - 28f), new Vector2(230f, 28f));
                if (d != null && d.model != null)
                {
                    var ic = UIKit.Box(page, "Equipped Icon", d.color).rectTransform;
                    UIKit.Place(ic, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(292f, y - 8f), new Vector2(68f, 68f));
                    UIKit.AddIcon(ic, d);
                }
                if (d != null)
                {
                    Txt(d.Describe(), 15, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(54f, y - 56f), new Vector2(300f, 22f));
                    var un = UIKit.Button(page, "Remove", () => { Inventory.Unequip(kind); Rebuild(); }, 16);
                    UIKit.Place((RectTransform)un.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(370f, y - 20f), new Vector2(100f, 44f));
                }
            }
            Txt("Gold: " + RunState.Gold, 24, new Color(1f, 0.85f, 0.4f), TextAnchor.UpperLeft, new Vector2(40f, -450f), new Vector2(400f, 34f));
            Txt("Flasks: " + InputBindings.Name(GameAction.HealthFlask) + " health, " + InputBindings.Name(GameAction.ManaFlask) + " mana.\nGive weapons and armor to companions with the Give buttons.",
                17, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(40f, -490f), new Vector2(440f, 80f));

            Txt("BAG", 28, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(520f, -100f), new Vector2(400f, 40f));
            if (RunState.Items.Count == 0) { Txt("Empty.", 22, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(520f, -150f), new Vector2(400f, 30f)); return; }
            float ry = -150f;
            foreach (var s in RunState.Items.ToArray())
            {
                var d = FVLibrary.Item(s.id);
                if (d == null || ry < -800f) continue;
                string id = s.id;
                var row = UIKit.Box(page, "Item", new Color(1f, 1f, 1f, 0.05f)).rectTransform;
                UIKit.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(520f, ry), new Vector2(940f, 62f));
                var sw = UIKit.Box(page, "Swatch", d.color).rectTransform;
                UIKit.Place(sw, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(528f, ry - 4f), new Vector2(54f, 54f));
                UIKit.AddIcon(sw, d);
                Txt(d.name + (s.count > 1 ? "  x" + s.count : ""), 20, UIKit.Ink, TextAnchor.UpperLeft, new Vector2(586f, ry - 6f), new Vector2(380f, 26f));
                Txt(d.Describe(), 15, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(586f, ry - 34f), new Vector2(380f, 22f));
                float bx = 970f;
                var use = UIKit.Button(page, d.IsGear ? "Equip" : "Use", () => { Inventory.Use(id, player.gameObject); Rebuild(); }, 16);
                UIKit.Place((RectTransform)use.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(bx, ry - 9f), new Vector2(100f, 44f));
                bx += 108f;
                if (d.kind == ItemKind.Weapon || d.kind == ItemKind.Armor)
                    for (int c = 0; c < RunState.Companions.Count; c++)
                    {
                        int ci = c;
                        string who = RunState.Companions[c].name.Split(' ')[0];
                        var give = UIKit.Button(page, "Give " + who, () => { GiveToCompanion(ci, id); Rebuild(); }, 15);
                        UIKit.Place((RectTransform)give.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(bx, ry - 9f), new Vector2(120f, 44f));
                        bx += 128f;
                    }
                var drop = UIKit.Button(page, "Drop", () => { DropItem(id); Rebuild(); }, 16);
                UIKit.Place((RectTransform)drop.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1356f, ry - 9f), new Vector2(96f, 44f));
                Tint(drop, new Color(0.5f, 0.16f, 0.14f));
                ry -= 70f;
            }
        }

        void DropItem(string id)
        {
            var d = FVLibrary.Item(id);
            if (d == null || !Inventory.Remove(id)) return;
            ItemPickup.Spawn(player.transform.position + player.transform.forward * 1.4f, d);
        }

        static void GiveToCompanion(int index, string id)
        {
            var d = FVLibrary.Item(id);
            if (d == null || index >= RunState.Companions.Count || !Inventory.Remove(id)) return;
            var save = RunState.Companions[index];
            string old = d.kind == ItemKind.Weapon ? save.weapon : save.armor;
            if (!string.IsNullOrEmpty(old)) Inventory.Add(old, 1, false);
            if (d.kind == ItemKind.Weapon) save.weapon = id; else save.armor = id;
            foreach (var c in CompanionAI.All) if (c.saveIndex == index) c.ApplyGear();
            PrototypeHUD.Message(save.name + " now carries " + d.name, 2f);
            GameAudio.PlayUI("pickup_gear", 0.6f);
        }

        // ---------------- COMPANIONS ----------------

        void BuildCompanions()
        {
            Txt("COMPANIONS  (" + RunState.Companions.Count + " / " + RunState.MaxCompanions + ")", 28, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(40f, -100f), new Vector2(800f, 40f));
            Txt("Hire sellswords for gold where you find them. Give them weapons and armor from the Inventory page.", 18, UIKit.Dim,
                TextAnchor.UpperLeft, new Vector2(40f, -140f), new Vector2(1300f, 30f));
            if (RunState.Companions.Count == 0) { Txt("Nobody fights at your side yet.", 24, UIKit.Ink, TextAnchor.UpperLeft, new Vector2(40f, -200f), new Vector2(800f, 40f)); return; }
            for (int i = 0; i < RunState.Companions.Count; i++)
            {
                int ci = i;
                var s = RunState.Companions[i];
                CompanionAI live = null;
                foreach (var c in CompanionAI.All) if (c.saveIndex == i) live = c;
                float y = -200f - i * 290f;
                var card = UIKit.Box(page, "Companion", new Color(1f, 1f, 1f, 0.05f)).rectTransform;
                UIKit.Place(card, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(1420f, 270f));
                Txt(s.name + (s.dead ? "   (down)" : ""), 28, UIKit.Ink, TextAnchor.UpperLeft, new Vector2(64f, y - 14f), new Vector2(800f, 40f));
                if (live != null)
                    Txt("Health " + Mathf.CeilToInt(live.Health.Current) + " / " + Mathf.CeilToInt(live.Health.maxHealth) +
                        "    Armor " + Mathf.RoundToInt(live.Health.armor * 100f) + "%    Damage +" + Mathf.RoundToInt(live.WeaponBonus * 100f) + "%",
                        19, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(64f, y - 58f), new Vector2(900f, 30f));
                string[] slots = { "WEAPON", "ARMOR" };
                for (int k = 0; k < 2; k++)
                {
                    int slot = k;
                    string id = k == 0 ? s.weapon : s.armor;
                    var d = string.IsNullOrEmpty(id) ? null : FVLibrary.Item(id);
                    float sy = y - 104f - k * 72f;
                    Txt(slots[k], 16, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(64f, sy), new Vector2(200f, 24f));
                    Txt(d != null ? d.name + "   " + d.Describe() : "(nothing)", 21, d != null ? UIKit.Ink : UIKit.Dim, TextAnchor.UpperLeft, new Vector2(200f, sy - 2f), new Vector2(800f, 30f));
                    if (d != null)
                    {
                        var back = UIKit.Button(page, "Take back", () =>
                        {
                            var sv = RunState.Companions[ci];
                            string cur = slot == 0 ? sv.weapon : sv.armor;
                            if (!string.IsNullOrEmpty(cur)) Inventory.Add(cur, 1, false);
                            if (slot == 0) sv.weapon = null; else sv.armor = null;
                            foreach (var c in CompanionAI.All) if (c.saveIndex == ci) c.ApplyGear();
                            Rebuild();
                        }, 16);
                        UIKit.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1060f, sy + 6f), new Vector2(150f, 44f));
                    }
                }
                var dismiss = UIKit.Button(page, "Dismiss", () =>
                {
                    var sv = RunState.Companions[ci];
                    if (!string.IsNullOrEmpty(sv.weapon)) Inventory.Add(sv.weapon, 1, false);
                    if (!string.IsNullOrEmpty(sv.armor)) Inventory.Add(sv.armor, 1, false);
                    CompanionAI liveC = null;
                    foreach (var c in CompanionAI.All) if (c.saveIndex == ci) liveC = c;
                    if (liveC != null) liveC.Dismiss(); else RunState.Companions.RemoveAt(ci);
                    Rebuild();
                }, 18);
                UIKit.Place((RectTransform)dismiss.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1260f, y - 14f), new Vector2(180f, 50f));
                Tint(dismiss, new Color(0.5f, 0.16f, 0.14f));
            }
        }

        // ---------------- SKILLS ----------------

        void BuildSkills()
        {
            Txt("SKILL POINTS: " + Skills.Points, 30, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(40f, -100f), new Vector2(600f, 40f));
            Txt("Earn a skill point every level. Starting a new run resets the tree.", 18, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(40f, -140f), new Vector2(900f, 30f));
            const float colW = 284f, rowH = 112f, x0 = 40f, y0 = -230f;
            for (int c = 0; c < Skills.Branches.Length; c++)
                Txt(Skills.Branches[c], 22, UIKit.Gold, TextAnchor.MiddleCenter, new Vector2(x0 + c * colW, y0 + 36f), new Vector2(260f, 30f));

            foreach (var n in Skills.All)
            {
                var node = n;
                float x = x0 + n.column * colW, y = y0 - n.row * rowH;
                if (n.row > 0)
                {
                    var link = UIKit.Box(page, "Link", Skills.Has(n.requires.Length > 0 ? n.requires[0] : n.id) ? UIKit.Gold : new Color(0.3f, 0.28f, 0.26f)).rectTransform;
                    UIKit.Place(link, new Vector2(0f, 1f), new Vector2(0.5f, 0f), new Vector2(x + 130f, y), new Vector2(4f, rowH - 76f));
                }
                bool has = Skills.Has(n.id), can = Skills.CanUnlock(n);
                var b = UIKit.Button(page, n.name + "\n<size=15>" + (has ? "learned" : n.cost + " pt") + "</size>", () => { selected = node; Rebuild(); }, 20);
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(260f, 76f));
                b.GetComponentInChildren<Text>().supportRichText = true;
                Tint(b, has ? new Color(0.55f, 0.42f, 0.18f) : can ? new Color(0.2f, 0.42f, 0.24f) : new Color(0.17f, 0.16f, 0.16f));
                if (selected != null && selected.id == n.id) b.GetComponent<Outline>().effectColor = Color.white;
            }

            var info = UIKit.Box(page, "Details", new Color(1f, 1f, 1f, 0.05f)).rectTransform;
            UIKit.Place(info, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1420f, 120f));
            if (selected == null)
            {
                var t = UIKit.Label(info, "Select a skill to see what it does. Green skills can be learned now.", 22, UIKit.Dim, TextAnchor.MiddleCenter);
                UIKit.Stretch((RectTransform)t.transform);
                return;
            }
            var title = UIKit.Label(info, selected.branch + "  -  " + selected.name, 26, UIKit.Gold, TextAnchor.UpperLeft);
            UIKit.Stretch((RectTransform)title.transform, 16f);
            var d = UIKit.Label(info, selected.description + "\nCost: " + selected.cost + " skill point" + (selected.cost > 1 ? "s" : "") +
                (selected.requires.Length > 0 ? "   Requires: " + Skills.Get(selected.requires[0]).name : ""), 21, UIKit.Ink, TextAnchor.LowerLeft);
            UIKit.Stretch((RectTransform)d.transform, 16f);
            if (!Skills.Has(selected.id))
            {
                var learn = UIKit.Button(info, "LEARN", () => { if (Skills.Unlock(selected.id)) { GameAudio.PlayUI("spell_learn"); Rebuild(); } }, 24);
                UIKit.Place((RectTransform)learn.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(200f, 60f));
                learn.interactable = Skills.CanUnlock(selected);
            }
        }

        // ---------------- SPELLS ----------------

        void BuildSpells()
        {
            Txt("SPELLS", 30, UIKit.Gold, TextAnchor.UpperLeft, new Vector2(40f, -100f), new Vector2(600f, 40f));
            Txt("Slots 1-3 are your hotbar. Put a spell in a slot to equip it; drop a spell to leave its tome on the ground.", 18, UIKit.Dim,
                TextAnchor.UpperLeft, new Vector2(40f, -140f), new Vector2(1300f, 30f));
            if (magic == null || magic.spells.Count == 0)
            {
                Txt("You haven't learned any spells yet. Spell tomes are hidden in dead ends.", 24, UIKit.Ink, TextAnchor.UpperLeft, new Vector2(40f, -200f), new Vector2(1200f, 40f));
                return;
            }
            for (int i = 0; i < magic.spells.Count; i++)
            {
                int idx = i;
                var s = magic.spells[i];
                float y = -200f - i * 104f;
                var row = UIKit.Box(page, "Spell Row", new Color(1f, 1f, 1f, i < 3 ? 0.07f : 0.03f)).rectTransform;
                UIKit.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, y), new Vector2(1420f, 92f));
                var swatch = UIKit.Box(page, "Swatch", s.color).rectTransform;
                UIKit.Place(swatch, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(56f, y - 16f), new Vector2(60f, 60f));
                Txt((i < 3 ? "[" + (i + 1) + "]  " : "[bag]  ") + s.displayName + "   Lv " + s.level, 26, i < 3 ? UIKit.Gold : UIKit.Ink,
                    TextAnchor.UpperLeft, new Vector2(134f, y - 12f), new Vector2(600f, 36f));
                Txt(magic.Describe(s), 19, UIKit.Dim, TextAnchor.UpperLeft, new Vector2(134f, y - 52f), new Vector2(640f, 30f));
                for (int k = 0; k < 3; k++)
                {
                    int slot = k;
                    var b = UIKit.Button(page, "Slot " + (k + 1), () => { magic.MoveSpell(idx, slot); Rebuild(); }, 18);
                    UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f + k * 140f, y - 20f), new Vector2(124f, 52f));
                    b.interactable = idx != slot && slot < magic.spells.Count;
                }
                var drop = UIKit.Button(page, "Drop", () => { magic.DropSpell(idx); Rebuild(); }, 18);
                UIKit.Place((RectTransform)drop.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1240f, y - 20f), new Vector2(160f, 52f));
                Tint(drop, new Color(0.5f, 0.16f, 0.14f));
            }
        }
    }
}
