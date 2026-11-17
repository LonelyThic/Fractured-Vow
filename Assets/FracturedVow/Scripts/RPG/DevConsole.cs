using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FracturedVow
{
    /// <summary>
    /// Cheat console: press ` (backquote) or F1, type a command, press Enter. Type "help" for the list.
    /// The game keeps running while it's open; Up arrow repeats the last command.
    /// </summary>
    public class DevConsole : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        static DevConsole inst;

        /// <summary>Runs a console command from code (the cheat menu); returns the last line it printed.</summary>
        public static string Exec(string cmd)
        {
            if (inst == null) inst = FindFirstObjectByType<DevConsole>();
            if (inst == null) return "The console isn't loaded.";
            int before = inst.lines.Count;
            inst.Run(cmd);
            return inst.lines.Count > before ? inst.lines[inst.lines.Count - 1] : "";
        }

        void Awake() { inst = this; }

        RectTransform panel;
        Text log;
        InputField field;
        readonly List<string> lines = new List<string>();
        string last = "";
        int openedFrame = -1;
        string endText = "";
        int endFrame = -10;
        float baseWalk = -1f, baseRun = -1f;

        PlayerController player;
        Health health;
        PlayerStats stats;
        PlayerMagic magic;
        PlayerProgress progress;
        PlayerFlask flask;

        static readonly string Help =
            "help                     this list\n" +
            "cheatmenu                open the cheat menu (buttons for everything here, items, spawning)\n" +
            "spawn <enemy> [count]    copy an enemy of that kind in front of you (names are in the cheat menu)\n" +
            "restock                  merchants' sold-out stock comes back\n" +
            "god                      toggle invincibility\n" +
            "heal / refill            full health / full health, mana, stamina, flasks\n" +
            "shards <n>               add Memory Shards\n" +
            "level <n>                gain n levels\n" +
            "points <n>               add skill points\n" +
            "skills all | reset       unlock the whole tree / refund it\n" +
            "spell fire|frost|wall|all [level]\n" +
            "gear damage|armor|health add a piece of gear\n" +
            "killall                  kill every enemy on this floor\n" +
            "boss                     jump to the boss\n" +
            "tp checkpoint | tp <x> <y> <z>\n" +
            "speed <x>                movement speed multiplier (1 = normal)\n" +
            "timescale <x>            game speed (1 = normal)\n" +
            "gold <n>                 add gold\n" +
            "give <item> [count]      add an item to the bag (type 'items' for the names)\n" +
            "items                    list every item id\n" +
            "flasks <n>               add health and mana flasks\n" +
            "chest [1-3]              place a chest in front of you (3 = boss chest)\n" +
            "hire [name]              recruit a free companion\n" +
            "shop                     open the merchant's shop here\n" +
            "revive                   bring fallen companions back\n" +
            "keys reset               restore the default controls\n" +
            "trailer                  cinematic tour of this floor with title cards (record it for a trailer)\n" +
            "unstuck                  back to the last dry, solid ground (also in the Esc menu)\n" +
            "grip                     turn held swords to the next orientation (repeat until it looks right)\n" +
            "grip <x> <y> <z>         set the sword turn exactly (degrees)\n" +
            "wipe                     erase ALL progress (skill tree, skill points, saved run)\n" +
            "respawn | newrun | fps | seed | clear";

        void Start()
        {
            UIKit.EnsureEventSystem();
            var canvas = UIKit.Canvas("Console Canvas", 200, transform);
            panel = UIKit.Box(canvas, "Console", new Color(0.02f, 0.02f, 0.03f, 0.92f), true).rectTransform;
            panel.anchorMin = new Vector2(0f, 0.55f); panel.anchorMax = new Vector2(1f, 1f);
            panel.offsetMin = Vector2.zero; panel.offsetMax = Vector2.zero;

            log = UIKit.Label(panel, "", 18, new Color(0.75f, 0.95f, 0.8f), TextAnchor.LowerLeft);
            var lr = (RectTransform)log.transform;
            lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
            lr.offsetMin = new Vector2(16f, 56f); lr.offsetMax = new Vector2(-16f, -10f);
            log.verticalOverflow = VerticalWrapMode.Truncate;

            var bg = UIKit.Box(panel, "Input", new Color(0.12f, 0.12f, 0.14f, 1f)).rectTransform;
            bg.anchorMin = new Vector2(0f, 0f); bg.anchorMax = new Vector2(1f, 0f); bg.pivot = new Vector2(0.5f, 0f);
            bg.offsetMin = new Vector2(10f, 8f); bg.offsetMax = new Vector2(-10f, 48f);
            var text = UIKit.Label(bg, "", 20, Color.white, TextAnchor.MiddleLeft);
            UIKit.Stretch((RectTransform)text.transform, 8f);
            text.supportRichText = false;
            var ph = UIKit.Label(bg, "type a command, e.g. god, shards 500, level 5, help", 20, new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleLeft);
            UIKit.Stretch((RectTransform)ph.transform, 8f);
            field = bg.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            field.placeholder = ph;
            field.lineType = InputField.LineType.SingleLine;
            field.onEndEdit.AddListener(s => { endText = s; endFrame = Time.frameCount; });
            bg.offsetMax = new Vector2(-130f, 48f);
            var run = UIKit.Button(panel, "RUN", Submit, 18);
            var rr = (RectTransform)run.transform;
            rr.anchorMin = new Vector2(1f, 0f); rr.anchorMax = new Vector2(1f, 0f); rr.pivot = new Vector2(1f, 0f);
            rr.anchoredPosition = new Vector2(-10f, 8f); rr.sizeDelta = new Vector2(110f, 40f);

            panel.gameObject.SetActive(false);
            Print("Fractured Vow console. Type 'help'.");
        }

        void FindPlayer()
        {
            if (player != null) return;
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) return;
            health = player.GetComponent<Health>();
            stats = player.GetComponent<PlayerStats>();
            magic = player.GetComponent<PlayerMagic>();
            progress = player.GetComponent<PlayerProgress>();
            flask = player.GetComponent<PlayerFlask>();
        }

        void Update()
        {
            if (panel == null) return;
            if (UIKit.KeyDown("console")) { Toggle(); return; }
            if (!IsOpen) return;
            if (UIKit.KeyDown("escape")) { Toggle(); return; }
            if (UIKit.KeyDown("up") && !string.IsNullOrEmpty(last)) { field.text = last; field.caretPosition = last.Length; }
            if (UIKit.KeyDown("enter")) Submit();
        }

        void LateUpdate()
        {
            // the key that opened the console must not end up in the text box
            if (IsOpen && Time.frameCount <= openedFrame + 1 && field.text.Contains("`")) field.text = field.text.Replace("`", "");
        }

        void Toggle()
        {
            IsOpen = !IsOpen;
            panel.gameObject.SetActive(IsOpen);
            if (IsOpen)
            {
                openedFrame = Time.frameCount;
                field.text = "";
                field.ActivateInputField();
                PlayerInputHandler.SetCursorLocked(false);
            }
            else
            {
                UIFocus.LastClosedFrame = Time.frameCount;
                if (!GameMenu.IsOpen && !CharacterMenu.IsOpen) PlayerInputHandler.SetCursorLocked(true);
            }
        }

        /// <summary>Runs whatever is typed (Enter or the RUN button).</summary>
        void Submit()
        {
            if (!IsOpen) return;
            string s = field.text;
            if (string.IsNullOrWhiteSpace(s) && Time.frameCount - endFrame <= 2) s = endText;
            s = (s ?? "").Replace("`", "").Trim();
            endText = "";
            field.text = "";
            if (s.Length > 0) { last = s; Print("> " + s); Run(s); }
            field.ActivateInputField();
        }

        void Print(string s)
        {
            foreach (var l in s.Split('\n')) lines.Add(l);
            while (lines.Count > 60) lines.RemoveAt(0);
            if (log != null) log.text = string.Join("\n", lines);
        }

        static float Num(string[] a, int i, float fallback) =>
            a.Length > i && float.TryParse(a[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : fallback;

        void Run(string cmd)
        {
            FindPlayer();
            var a = cmd.ToLowerInvariant().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (a.Length == 0) return;
            if (player == null && a[0] != "help" && a[0] != "clear" && a[0] != "timescale") { Print("No player in this scene."); return; }
            switch (a[0])
            {
                case "help": Print(Help); break;
                case "clear": lines.Clear(); Print(""); break;
                case "god":
                    health.godMode = a.Length > 1 ? a[1] == "on" || a[1] == "1" : !health.godMode;
                    Print("God mode " + (health.godMode ? "ON" : "OFF"));
                    break;
                case "heal": health.Heal(99999f); Print("Healed."); break;
                case "refill":
                    health.Heal(99999f); stats.RefillAll(); if (flask != null) flask.Refill();
                    Print("Health, mana, stamina and flasks refilled.");
                    break;
                case "shards": RunState.AddShards((int)Num(a, 1, 1000)); Print("Shards: " + RunState.Shards); break;
                case "level":
                    if (progress != null) progress.GrantLevels(Mathf.Max(1, (int)Num(a, 1, 1)));
                    Print("Level " + RunState.Level + ", skill points " + Skills.Points);
                    break;
                case "points": Skills.Points += (int)Num(a, 1, 5); Print("Skill points: " + Skills.Points); break;
                case "skills":
                    if (a.Length > 1 && a[1] == "reset") { Skills.ResetTree(); Print("Skill tree refunded."); }
                    else { Skills.UnlockAll(); Print("Every skill unlocked."); }
                    break;
                case "spell":
                {
                    int lvl = Mathf.Max(1, (int)Num(a, 2, 1));
                    string w = a.Length > 1 ? a[1] : "all";
                    if (w == "fire" || w == "all") magic.LearnSpell(SpellType.Firebolt, lvl);
                    if (w == "frost" || w == "all") magic.LearnSpell(SpellType.FrostShard, lvl);
                    if (w == "wall" || w == "stone" || w == "all") magic.LearnSpell(SpellType.StoneWall, lvl);
                    Print("Spells: " + magic.spells.Count);
                    break;
                }
                case "gear":
                {
                    string w = a.Length > 1 ? a[1] : "damage";
                    if (w == "armor") stats.ApplyGear(GearStat.Armor, 0.15f, "Console Plate", health);
                    else if (w == "health") stats.ApplyGear(GearStat.MaxHealth, 25f, "Console Sigil", health);
                    else stats.ApplyGear(GearStat.Damage, 0.15f, "Console Blade", health);
                    Print("Gear added.");
                    break;
                }
                case "killall":
                {
                    int n = 0;
                    foreach (var e in FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
                    {
                        var h = e.GetComponent<Health>();
                        if (h != null && !h.IsDead) { h.Kill(); n++; }
                    }
                    Print("Killed " + n + " enemies.");
                    break;
                }
                case "boss":
                {
                    var w = FindFirstObjectByType<WardenBoss>();
                    if (w != null) { Teleport(w.transform.position + w.transform.forward * 9f + Vector3.up * 0.2f); Print("Teleported to the Warden."); break; }
                    var pier = GameObject.Find("Western Pier");
                    if (pier != null) { Teleport(pier.transform.position + Vector3.up * 0.2f); Print("Teleported to the Leviathan's lake. Take the boat."); break; }
                    Print("No boss on this floor.");
                    break;
                }
                case "tp":
                    if (a.Length > 1 && a[1] == "checkpoint") { GameManager.Instance?.TeleportToCheckpoint(); Print("Back at the checkpoint."); }
                    else if (a.Length >= 4) { Teleport(new Vector3(Num(a, 1, 0), Num(a, 2, 0), Num(a, 3, 0))); Print("Teleported."); }
                    else Print("Usage: tp checkpoint | tp x y z");
                    break;
                case "speed":
                    if (baseWalk < 0f) { baseWalk = player.walkSpeed; baseRun = player.runSpeed; }
                    float mult = Mathf.Clamp(Num(a, 1, 1f), 0.2f, 5f);
                    player.walkSpeed = baseWalk * mult; player.runSpeed = baseRun * mult;
                    Print("Speed x" + mult);
                    break;
                case "timescale": Time.timeScale = Mathf.Clamp(Num(a, 1, 1f), 0.05f, 5f); Print("Time scale " + Time.timeScale); break;
                case "respawn": GameManager.Instance?.Respawn(); Print("Respawned."); break;
                case "newrun": RunState.NewRun(); Print("New run started (run " + RunState.RunNumber + ")."); break;
                case "wipe":
                    GameMenu.WipeAllProgress();
                    RunState.NewRun();
                    Print("All progress erased: skill tree, skill points, saved run. A fresh run has started.");
                    break;
                case "fps": GameHUD.ShowFps = !GameHUD.ShowFps; Print("FPS counter " + (GameHUD.ShowFps ? "on" : "off")); break;
                case "quality":
                {
                    string w = a.Length > 1 ? a[1] : "";
                    int qv = w.StartsWith("l") ? 0 : w.StartsWith("m") ? 1 : w.StartsWith("h") ? 2 : (GameSettings.Quality + 1) % 3;
                    GameSettings.Quality = qv;
                    Print("Graphics quality: " + GameSettings.QualityNames[qv]);
                    break;
                }
                case "gold": RunState.AddGold((int)Num(a, 1, 500)); Print("Gold: " + RunState.Gold); break;
                case "items":
                {
                    var names = new List<string>();
                    foreach (var it in FVLibrary.Catalog) names.Add(it.id + " (" + it.name + ")");
                    Print(string.Join("\n", names));
                    break;
                }
                case "give":
                {
                    if (a.Length < 2) { Print("Usage: give <item id or name> [count]"); break; }
                    string want = string.Join(" ", a, 1, a.Length - 1);
                    int count = 1;
                    if (a.Length > 2 && int.TryParse(a[a.Length - 1], out int n)) { count = n; want = string.Join(" ", a, 1, a.Length - 2); }
                    ItemDef found = null;
                    foreach (var it in FVLibrary.Catalog)
                        if (it.id == want || it.name.ToLowerInvariant() == want || it.name.ToLowerInvariant().Contains(want)) { found = it; break; }
                    if (found == null) { Print("No item '" + want + "'. Type 'items'."); break; }
                    Inventory.Add(found.id, Mathf.Max(1, count));
                    Print("Added " + count + " x " + found.name);
                    break;
                }
                case "flasks": { int n = Mathf.Max(1, (int)Num(a, 1, 5)); Inventory.Add("hp_flask", n); Inventory.Add("mp_flask", n); Print("Added " + n + " of each flask."); break; }
                case "chest":
                {
                    var c = new GameObject("Console Chest");
                    c.transform.position = player.transform.position + player.transform.forward * 2.5f;
                    c.transform.rotation = Quaternion.LookRotation(-player.transform.forward);
                    var body = RuntimeVisuals.Primitive(PrimitiveType.Cube, null, new Color(0.45f, 0.28f, 0.12f), true);
                    body.transform.SetParent(c.transform, false); body.transform.localPosition = Vector3.up * 0.4f; body.transform.localScale = new Vector3(1.2f, 0.8f, 0.8f);
                    var lid = RuntimeVisuals.Primitive(PrimitiveType.Cube, null, new Color(0.55f, 0.35f, 0.15f), false);
                    lid.name = "Lid"; lid.transform.SetParent(c.transform, false); lid.transform.localPosition = new Vector3(0f, 0.85f, 0f); lid.transform.localScale = new Vector3(1.25f, 0.15f, 0.85f);
                    var sc = c.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 1.4f; sc.center = Vector3.up * 0.5f;
                    var ch = c.AddComponent<Chest>(); ch.tier = Mathf.Clamp((int)Num(a, 1, 1), 1, 3); ch.lid = lid.transform;
                    Print("Chest placed (tier " + ch.tier + "). Open it with " + InputBindings.Name(GameAction.Interact) + ".");
                    break;
                }
                case "hire":
                {
                    if (RunState.Companions.Count >= RunState.MaxCompanions) { Print("Already two companions. Dismiss one in the character menu."); break; }
                    string nm = a.Length > 1 ? char.ToUpper(a[1][0]) + a[1].Substring(1) : (RunState.Companions.Count == 0 ? "Bram" : "Isolde");
                    RunState.Companions.Add(new CompanionSave { name = nm, model = 1 + RunState.Companions.Count % 2 });
                    CompanionAI.Spawn(RunState.Companions.Count - 1, player.transform.position - player.transform.forward * 2f, player.transform.rotation);
                    Print(nm + " joins you.");
                    break;
                }
                case "shop":
                {
                    var m = new GameObject("Console Merchant");
                    m.transform.position = player.transform.position + player.transform.forward * 2.5f;
                    m.transform.rotation = Quaternion.LookRotation(-player.transform.forward);
                    var sc = m.AddComponent<SphereCollider>(); sc.isTrigger = true; sc.radius = 1.5f; sc.center = Vector3.up;
                    var shop = m.AddComponent<ShopNPC>();
                    Toggle();
                    ShopUI.Open(shop);
                    break;
                }
                case "revive": foreach (var c in CompanionAI.All) if (c.Health.IsDead) c.Revive(); Print("Companions revived."); break;
                case "trailer": if (IsOpen) Toggle(); TrailerDirector.Play(); break;
                case "cheatmenu":
                case "cheats":
                    if (IsOpen) Toggle();
                    CheatMenu.Toggle();
                    break;
                case "restock": RunState.ShopBought.Clear(); Print("Every merchant is fully stocked again."); break;
                case "spawn":
                {
                    string rest = cmd.Trim().Length > 5 ? cmd.Trim().Substring(5).Trim() : "";
                    int count = 1;
                    int sp = rest.LastIndexOf(' ');
                    if (sp > 0 && int.TryParse(rest.Substring(sp + 1), out int nParsed)) { count = Mathf.Clamp(nParsed, 1, 20); rest = rest.Substring(0, sp); }
                    rest = rest.Trim().Trim('"');
                    if (rest.Length == 0) { Print("Enemies here: " + string.Join(", ", CheatMenu.EnemyTypes())); break; }
                    int made = CheatMenu.Spawn(rest, count);
                    Print(made > 0 ? "Spawned " + made + " " + rest + "." : "No enemy called '" + rest + "' on this floor. Try: " + string.Join(", ", CheatMenu.EnemyTypes()));
                    break;
                }
                case "unstuck": SafeGround.Unstuck(); Print("Moved to the last safe ground."); break;
                case "grip":
                {
                    Vector3 g;
                    if (a.Length >= 4) g = new Vector3(Num(a, 1, 0), Num(a, 2, 0), Num(a, 3, 0));
                    else
                    {
                        int cur = System.Array.IndexOf(CharacterVisuals.GripPresets, CharacterVisuals.Grip);
                        g = CharacterVisuals.GripPresets[(cur + 1) % CharacterVisuals.GripPresets.Length];
                    }
                    CharacterVisuals.Grip = g;
                    Print("Sword grip: " + g.x + " " + g.y + " " + g.z + "   (saved; type grip again for the next one)");
                    break;
                }
                case "keys":
                    if (a.Length > 1 && a[1] == "reset") { InputBindings.ResetAll(); Print("Controls reset to defaults."); }
                    else Print("Usage: keys reset");
                    break;
                case "seed": Print("Run " + RunState.RunNumber + ", seed " + RunState.Seed + ", scene " + SceneManager.GetActiveScene().name); break;
                default: Print("Unknown command '" + a[0] + "'. Type help."); break;
            }
        }

        void Teleport(Vector3 p)
        {
            var boat = player.GetComponentInParent<BoatController>();
            if (boat != null) boat.ForceExit();
            player.Teleport(p, player.transform.rotation);
        }
    }
}
