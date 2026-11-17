using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FracturedVow
{
    /// <summary>
    /// The developer cheat menu: type "cheatmenu" in the console (` or F1). Every button runs the matching console
    /// command, so the menu and the console always agree. Three columns: the knight (god mode, gold, levels, skills,
    /// spells, flasks, speed), the world (kill, teleport, chests, merchant, sellswords, spawning any enemy type found
    /// on this floor) and every item in the game. The game pauses while it's open. Esc or "cheatmenu" closes it.
    /// </summary>
    public class CheatMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        static CheatMenu inst;

        GameObject root;
        Text status;
        float prevScale = 1f;
        int openedFrame;

        public static void Toggle()
        {
            if (inst == null)
            {
                var go = new GameObject("Cheat Menu");
                DontDestroyOnLoad(go);
                inst = go.AddComponent<CheatMenu>();
            }
            if (IsOpen) inst.Close(); else inst.Open();
        }

        void Open()
        {
            if (root != null) Destroy(root);
            Build();
            if (!IsOpen) prevScale = Time.timeScale > 0f ? Time.timeScale : 1f; // (a rebuild keeps the remembered speed)
            IsOpen = true;
            openedFrame = Time.frameCount;
            Time.timeScale = 0f;
            PlayerInputHandler.SetCursorLocked(false);
        }

        void Close()
        {
            IsOpen = false;
            if (root != null) Destroy(root);
            Time.timeScale = prevScale;
            UIFocus.LastClosedFrame = Time.frameCount;
            if (!UIFocus.Any) PlayerInputHandler.SetCursorLocked(true);
        }

        void Update()
        {
            if (!IsOpen || Time.frameCount == openedFrame) return;
#if ENABLE_INPUT_SYSTEM
            bool esc = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            bool esc = Input.GetKeyDown(KeyCode.Escape);
#endif
            if (esc) Close();
        }

        /// <summary>Runs a console command and shows what happened at the top of the menu.</summary>
        void Do(string cmd, bool rebuild = false)
        {
            string result = DevConsole.Exec(cmd);
            GameAudio.PlayUI("ui_click", 0.5f);
            if (rebuild) { Open(); }
            if (status != null) status.text = "> " + cmd + (string.IsNullOrEmpty(result) ? "" : "    " + result);
        }

        // ---------------- layout ----------------

        void Build()
        {
            var canvas = UIKit.Canvas("Cheats", 140, transform);
            root = canvas.gameObject;
            var dim = UIKit.Box(canvas, "Dim", new Color(0f, 0f, 0f, 0.75f)).rectTransform;
            UIKit.Stretch(dim);
            var page = UIKit.Box(canvas, "Page", UIKit.Panel, true).rectTransform;
            UIKit.Place(page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1780f, 980f));

            var title = UIKit.Label(page, "CHEAT MENU", 40, UIKit.Gold, TextAnchor.UpperLeft);
            UIKit.Place((RectTransform)title.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(600f, 50f));
            status = UIKit.Label(page, "Every button runs the console command shown here.", 18, UIKit.Dim, TextAnchor.UpperLeft);
            UIKit.Place((RectTransform)status.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -70f), new Vector2(1300f, 28f));
            var close = UIKit.Button(page, "CLOSE  (Esc)", Close, 20);
            UIKit.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(220f, 50f));

            var player = Column(page, "THE KNIGHT", 30f, 560f);
            var pc = FindFirstObjectByType<PlayerController>();
            var hp = pc != null ? pc.GetComponent<Health>() : null;
            Btn(player, "God mode: " + (hp != null && hp.godMode ? "ON" : "off") + "  (no damage, endless mana and stamina)", () => Do("god", true));
            Row(player, ("Heal", () => Do("heal")), ("Refill all", () => Do("refill")));
            Row(player, ("+1,000 gold", () => Do("gold 1000")), ("+10,000 gold", () => Do("gold 10000")));
            Row(player, ("+1,000 shards", () => Do("shards 1000")), ("+10 flasks", () => Do("flasks 10")));
            Row(player, ("+1 level", () => Do("level 1")), ("+5 levels", () => Do("level 5")));
            Row(player, ("+5 skill points", () => Do("points 5")), ("Unlock all skills", () => Do("skills all")));
            Row(player, ("Reset skill tree", () => Do("skills reset")), ("All spells (level 3)", () => Do("spell all 3")));
            Row(player, ("Speed x1", () => Do("speed 1")), ("Speed x2", () => Do("speed 2")));
            Row(player, ("Time x0.5", () => { Do("timescale 0.5"); prevScale = 0.5f; Time.timeScale = 0f; }),
                        ("Time x1", () => { Do("timescale 1"); prevScale = 1f; Time.timeScale = 0f; }));
            Row(player, ("Unstuck", () => { Close(); DevConsole.Exec("unstuck"); }), ("Respawn", () => { Close(); DevConsole.Exec("respawn"); }));
            Row(player, ("Gear: +damage", () => Do("gear damage")), ("Gear: +armor", () => Do("gear armor")));

            var world = Column(page, "THE WORLD", 610f, 560f);
            Row(world, ("Kill all enemies", () => Do("killall")), ("Jump to the boss", () => { Close(); DevConsole.Exec("boss"); }));
            Row(world, ("To the checkpoint", () => { Close(); DevConsole.Exec("tp checkpoint"); }), ("Show / hide FPS", () => Do("fps")));
            Row(world, ("Chest", () => Do("chest 1")), ("Rich chest", () => Do("chest 2")));
            Row(world, ("Boss chest", () => Do("chest 3")), ("Merchant here", () => { Close(); DevConsole.Exec("shop"); }));
            Row(world, ("Hire a sellsword", () => Do("hire")), ("Revive companions", () => Do("revive")));
            Row(world, ("Trailer camera", () => { Close(); DevConsole.Exec("trailer"); }), ("Unlimited stock", () => Do("restock")));
            Head(world, "SPAWN AN ENEMY (in front of you)");
            var types = EnemyTypes();
            if (types.Count == 0) Note(world, "No enemies on this floor to copy from.");
            foreach (var t in types)
            {
                string n = t;
                Row(world, ("Spawn " + n, () => Do("spawn \"" + n + "\"")), ("x5", () => Do("spawn \"" + n + "\" 5")));
            }

            var items = Column(page, "ITEMS", 1190f, 560f, true);
            foreach (var d in FVLibrary.Catalog)
            {
                if (d == null) continue;
                string id = d.id;
                Row(items, (d.name, () => Do("give " + id)), ("x5", () => Do("give " + id + " 5")));
            }
        }

        // ---------------- helpers ----------------

        RectTransform Column(RectTransform page, string heading, float x, float width, bool scroll = false)
        {
            var h = UIKit.Label(page, heading, 24, UIKit.Gold, TextAnchor.UpperLeft);
            UIKit.Place((RectTransform)h.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -110f), new Vector2(width, 34f));
            var view = UIKit.Rect(heading + " View", page);
            UIKit.Place(view, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -150f), new Vector2(width, 810f));
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f); // catches the mouse wheel
            var content = UIKit.Rect("Content", view);
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8f; layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = view.gameObject.AddComponent<ScrollRect>();
            sr.content = content; sr.horizontal = false; sr.vertical = true; sr.scrollSensitivity = 40f;
            sr.movementType = ScrollRect.MovementType.Clamped;
            return content;
        }

        static void Fix(Component c, float h)
        {
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.minHeight = le.preferredHeight = h;
        }

        void Btn(RectTransform col, string label, System.Action a)
        {
            var b = UIKit.Button(col, label, a, 18);
            Fix(b, 46f);
        }

        void Row(RectTransform col, (string label, System.Action a) left, (string label, System.Action a) right)
        {
            var row = UIKit.Rect("Row", col);
            Fix(row, 46f);
            var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f; h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = true;
            var b1 = UIKit.Button(row, left.label, left.a, 18);
            b1.gameObject.AddComponent<LayoutElement>().flexibleWidth = 3f;
            var b2 = UIKit.Button(row, right.label, right.a, 18);
            b2.gameObject.AddComponent<LayoutElement>().flexibleWidth = right.label.Length <= 3 ? 1f : 3f;
        }

        void Head(RectTransform col, string text)
        {
            var t = UIKit.Label(col, text, 20, UIKit.Gold, TextAnchor.LowerLeft);
            Fix(t, 40f);
        }

        void Note(RectTransform col, string text)
        {
            var t = UIKit.Label(col, text, 18, UIKit.Dim, TextAnchor.MiddleLeft);
            Fix(t, 30f);
        }

        // ---------------- spawning enemies ----------------

        /// <summary>The distinct kinds of enemy on this floor (husks, skeletons, necromancers, brutes...).</summary>
        public static List<string> EnemyTypes()
        {
            var names = new List<string>();
            foreach (var e in EnemyAI.Registry)
            {
                if (e == null) continue;
                string n = TypeName(e);
                if (!names.Contains(n)) names.Add(n);
            }
            names.Sort();
            return names;
        }

        static string TypeName(EnemyAI e)
        {
            string n = e.name.Replace("(Clone)", "").Replace("(horde)", "").Replace("(spawned)", "");
            int i = n.IndexOf('(');
            if (i > 0) n = n.Substring(0, i);
            return System.Text.RegularExpressions.Regex.Replace(n, @"[\s_]*\d+$", "").Trim();
        }

        /// <summary>Copies an enemy of that kind in front of the knight. Returns how many appeared.</summary>
        public static int Spawn(string kind, int count)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null) return 0;
            EnemyAI source = null;
            foreach (var e in EnemyAI.Registry)
                if (e != null && TypeName(e).Equals(kind, System.StringComparison.OrdinalIgnoreCase)) { source = e; if (e.gameObject.activeSelf) break; }
            if (source == null) return 0;
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                float ang = (i - (count - 1) * 0.5f) * 25f;
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * pc.transform.forward;
                Vector3 p = pc.transform.position + dir * 5f;
                if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore)) p = g.point;
                var clone = Instantiate(source.gameObject, p + Vector3.up * 0.05f, Quaternion.LookRotation(-dir), source.transform.parent);
                clone.name = kind + " (spawned)";
                clone.SetActive(true);
                var ai = clone.GetComponent<EnemyAI>();
                if (ai != null) ai.ResetToSpawn();
                made++;
            }
            return made;
        }
    }
}
