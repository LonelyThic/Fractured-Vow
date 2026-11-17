using UnityEngine;
using UnityEngine.UI;

namespace FracturedVow
{
    /// <summary>
    /// The RPG HUD: level badge, health / mana / stamina bars (they grow as the knight levels, with a trailing
    /// "damage taken" bar), Memory Shards and the cost of the next level, healing flasks, a 3-slot spell hotbar
    /// with cooldowns, gear, a lock-on marker and run info. Built in code at runtime.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static bool ShowFps { get => GameSettings.ShowFps; set => GameSettings.ShowFps = value; }

        PlayerController player;
        Health health;
        PlayerStats stats;
        PlayerMagic magic;
        PlayerFlask flask;

        RectTransform canvas, hud;
        Text levelText, shardsText, flaskText, runText, gearText, pointsText, fpsText, goldText;
        RectTransform hpBack, hpFill, hpGhost, mpBack, mpFill, mpGhost, stBack, stFill, stGhost;
        Text hpLabel, mpLabel, stLabel;
        float hpGhostV = 1f, mpGhostV = 1f, stGhostV = 1f, fpsTimer;
        int fpsFrames;
        Slot[] slots;
        RectTransform lockMarker;
        Text hint;

        class Slot
        {
            public RectTransform root, cooldown;
            public Image fill;
            public Outline selected;
            public Text name, level, cost;
        }

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player == null) { Destroy(gameObject); return; }
            health = player.GetComponent<Health>();
            stats = player.GetComponent<PlayerStats>();
            magic = player.GetComponent<PlayerMagic>();
            flask = player.GetComponent<PlayerFlask>();
            Build();
        }

        void Build()
        {
            canvas = UIKit.Canvas("HUD Canvas", 10, transform);
            hud = UIKit.Rect("HUD", canvas);
            UIKit.Stretch(hud);

            // level badge
            var badge = UIKit.Box(hud, "Level Badge", UIKit.Panel, true).rectTransform;
            UIKit.Place(badge, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -22f), new Vector2(84f, 84f));
            var lv = UIKit.Label(badge, "LEVEL", 14, UIKit.Dim, TextAnchor.UpperCenter);
            UIKit.Stretch((RectTransform)lv.transform, 6f);
            levelText = UIKit.Label(badge, "1", 40, UIKit.Gold, TextAnchor.MiddleCenter);
            UIKit.Stretch((RectTransform)levelText.transform);
            ((RectTransform)levelText.transform).offsetMin = new Vector2(0f, -8f);

            hpBack = UIKit.Bar(hud, "Health", new Color(0.72f, 0.12f, 0.1f), out hpFill, out hpGhost, out hpLabel);
            UIKit.Place(hpBack, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -24f), new Vector2(300f, 26f));
            mpBack = UIKit.Bar(hud, "Mana", new Color(0.18f, 0.36f, 0.9f), out mpFill, out mpGhost, out mpLabel);
            UIKit.Place(mpBack, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -56f), new Vector2(260f, 18f));
            stBack = UIKit.Bar(hud, "Stamina", new Color(0.22f, 0.66f, 0.28f), out stFill, out stGhost, out stLabel);
            UIKit.Place(stBack, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(120f, -80f), new Vector2(260f, 18f));
            mpLabel.fontSize = 13; stLabel.fontSize = 13;

            var gem = UIKit.Box(hud, "Shard Icon", UIKit.Gold).rectTransform;
            UIKit.Place(gem, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(42f, -128f), new Vector2(16f, 16f));
            gem.localRotation = Quaternion.Euler(0f, 0f, 45f);
            shardsText = UIKit.Label(hud, "", 20, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Place((RectTransform)shardsText.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(60f, -128f), new Vector2(520f, 28f));

            var vial = UIKit.Box(hud, "Flask Icon", new Color(0.85f, 0.2f, 0.18f), true).rectTransform;
            UIKit.Place(vial, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(42f, -160f), new Vector2(14f, 22f));
            flaskText = UIKit.Label(hud, "", 20, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Place((RectTransform)flaskText.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(60f, -160f), new Vector2(560f, 28f));

            var coin = UIKit.Box(hud, "Gold Icon", new Color(1f, 0.8f, 0.25f), true).rectTransform;
            UIKit.Place(coin, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(42f, -192f), new Vector2(18f, 18f));
            goldText = UIKit.Label(hud, "", 20, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleLeft);
            UIKit.Place((RectTransform)goldText.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(60f, -192f), new Vector2(300f, 28f));

            pointsText = UIKit.Label(hud, "", 18, new Color(0.55f, 1f, 0.6f), TextAnchor.MiddleLeft);
            UIKit.Place((RectTransform)pointsText.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(26f, -224f), new Vector2(560f, 26f));

            runText = UIKit.Label(hud, "", 17, UIKit.Dim, TextAnchor.UpperRight);
            UIKit.Place((RectTransform)runText.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -46f), new Vector2(620f, 60f));
            fpsText = UIKit.Label(hud, "", 22, UIKit.Gold, TextAnchor.UpperRight);
            UIKit.Place((RectTransform)fpsText.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -14f), new Vector2(260f, 30f));
            fpsText.supportRichText = true;

            gearText = UIKit.Label(hud, "", 16, UIKit.Dim, TextAnchor.LowerLeft);
            UIKit.Place((RectTransform)gearText.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 24f), new Vector2(520f, 120f));

            // spell hotbar
            slots = new Slot[3];
            for (int i = 0; i < 3; i++)
            {
                var s = new Slot();
                var frame = UIKit.Box(hud, "Spell Slot " + (i + 1), UIKit.Panel).rectTransform;
                UIKit.Place(frame, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((i - 1) * 112f, 26f), new Vector2(100f, 100f));
                s.selected = frame.gameObject.AddComponent<Outline>();
                s.selected.effectDistance = new Vector2(3f, -3f);
                s.fill = UIKit.Box(frame, "Rune", Color.gray);
                UIKit.Stretch(s.fill.rectTransform, 12f);
                s.cooldown = UIKit.Box(frame, "Cooldown", new Color(0f, 0f, 0f, 0.65f)).rectTransform;
                s.cooldown.anchorMin = new Vector2(0f, 0f); s.cooldown.anchorMax = new Vector2(1f, 0f); s.cooldown.pivot = new Vector2(0.5f, 0f);
                s.cooldown.offsetMin = new Vector2(12f, 12f); s.cooldown.offsetMax = new Vector2(-12f, 12f);
                var key = UIKit.Label(frame, (i + 1).ToString(), 18, UIKit.Gold, TextAnchor.UpperLeft);
                UIKit.Stretch((RectTransform)key.transform, 6f);
                s.name = UIKit.Label(frame, "", 15, UIKit.Ink, TextAnchor.MiddleCenter);
                UIKit.Stretch((RectTransform)s.name.transform, 4f);
                s.level = UIKit.Label(frame, "", 14, UIKit.Gold, TextAnchor.UpperRight);
                UIKit.Stretch((RectTransform)s.level.transform, 6f);
                s.cost = UIKit.Label(frame, "", 14, new Color(0.6f, 0.75f, 1f), TextAnchor.LowerCenter);
                UIKit.Stretch((RectTransform)s.cost.transform, 6f);
                s.root = frame;
                slots[i] = s;
            }
            hint = UIKit.Label(hud, "", 15, UIKit.Dim, TextAnchor.MiddleCenter);
            UIKit.Place((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(900f, 22f));

            var lm = UIKit.Box(hud, "Lock Marker", new Color(1f, 0.85f, 0.4f, 0.9f), true);
            lockMarker = lm.rectTransform;
            lockMarker.sizeDelta = new Vector2(18f, 18f);
            lockMarker.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }

        void Update()
        {
            if (player == null || hud == null) return;
            bool show = !UIFocus.Any && !PrototypeHUD.Hidden;
            if (hud.gameObject.activeSelf != show) hud.gameObject.SetActive(show);
            if (!show) return;
            float dt = Time.unscaledDeltaTime;
            Bar(hpBack, hpFill, hpGhost, hpLabel, health.Current, health.maxHealth, ref hpGhostV, 300f, dt);
            Bar(mpBack, mpFill, mpGhost, mpLabel, stats.Mana, stats.maxMana, ref mpGhostV, 260f, dt);
            Bar(stBack, stFill, stGhost, stLabel, stats.Stamina, stats.maxStamina, ref stGhostV, 260f, dt);
            UpdateLockMarker();
            UpdateFps(dt);
            // the rest is text: rebuilding it every frame makes garbage (stutter), so five times a second is plenty
            textTimer -= dt;
            if (textTimer > 0f) { UpdateCooldowns(); return; }
            textTimer = 0.2f;

            levelText.text = RunState.Level.ToString();
            hint.text = InputBindings.Name(GameAction.Cast) + " cast   " + InputBindings.Name(GameAction.NextSpell) + " swap   " +
                        InputBindings.Name(GameAction.Dodge) + " dodge   " + InputBindings.Name(GameAction.LockOn) + " lock on   " +
                        InputBindings.Name(GameAction.Character) + " inventory";
            int cost = RunState.LevelCost;
            shardsText.text = RunState.Shards.ToString("N0") + " shards   " +
                (RunState.Shards >= cost ? "<color=#8dff9a>Level up ready (Tab)</color>" : "<color=#9a948c>next level " + cost + "</color>");
            shardsText.supportRichText = true;
            flaskText.text = "Health x" + Inventory.Count("hp_flask") + " <color=#9a948c>[" + InputBindings.Name(GameAction.HealthFlask) + "]</color>    " +
                             "<color=#7fa2ff>Mana x" + Inventory.Count("mp_flask") + "</color> <color=#9a948c>[" + InputBindings.Name(GameAction.ManaFlask) + "]</color>";
            goldText.text = RunState.Gold.ToString("N0") + " gold";
            flaskText.supportRichText = true;
            pointsText.text = Skills.Points > 0 ? "Skill points available: " + Skills.Points + "  (Tab)" : "";
            runText.text = Difficulty.Name + "   Kills " + RunState.Kills;
            gearText.text = stats.equippedGear.Count > 0 ? "GEAR\n" + string.Join("\n", stats.equippedGear) : "";

            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                Spell sp = magic != null && i < magic.spells.Count ? magic.spells[i] : null;
                s.fill.color = sp != null ? sp.color * 0.75f : new Color(0.15f, 0.15f, 0.15f);
                s.name.text = sp != null ? sp.displayName.Replace(" ", "\n") : "empty";
                s.level.text = sp != null ? "Lv" + sp.level : "";
                s.cost.text = sp != null ? Mathf.RoundToInt(sp.manaCost) + " mp" : "";
                bool sel = magic != null && i == magic.currentIndex;
                s.selected.effectColor = sel ? UIKit.Gold : new Color(0f, 0f, 0f, 0.6f);
            }
            UpdateCooldowns();
        }

        float textTimer;

        void UpdateCooldowns()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                Spell sp = magic != null && i < magic.spells.Count ? magic.spells[i] : null;
                float wait = sp != null ? Mathf.Clamp01((sp.readyTime - Time.time) / Mathf.Max(0.01f, sp.cooldown + sp.castTime)) : 0f;
                slots[i].cooldown.sizeDelta = new Vector2(-24f, 76f * wait);
            }
        }

        void UpdateLockMarker()
        {
            var target = player.LockTarget;
            lockMarker.gameObject.SetActive(target != null && Camera.main != null);
            if (target != null && Camera.main != null)
            {
                Vector3 sp = Camera.main.WorldToScreenPoint(target.position + Vector3.up * 1.2f);
                if (sp.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, sp, null, out var local))
                {
                    lockMarker.anchorMin = lockMarker.anchorMax = new Vector2(0.5f, 0.5f);
                    lockMarker.anchoredPosition = local;
                    lockMarker.localRotation = Quaternion.Euler(0f, 0f, 45f + Mathf.Sin(Time.unscaledTime * 4f) * 8f);
                }
            }
        }

        void UpdateFps(float dt)
        {
            fpsFrames++; fpsTimer += dt;
            if (fpsTimer >= 0.5f)
            {
                int fps = Mathf.RoundToInt(fpsFrames / fpsTimer);
                string col = fps >= 55 ? "#8dff9a" : fps >= 30 ? "#ffd27a" : "#ff7a6a"; // green / yellow / red
                fpsText.text = ShowFps ? "<color=" + col + ">" + fps + " FPS</color>" : "";
                fpsFrames = 0; fpsTimer = 0f;
            }
        }

        static void Bar(RectTransform back, RectTransform fill, RectTransform ghost, Text label, float cur, float max, ref float ghostV, float baseWidth, float dt)
        {
            float w = Mathf.Clamp(baseWidth + (max - 100f) * 1.3f, baseWidth, baseWidth + 260f);
            back.sizeDelta = new Vector2(w, back.sizeDelta.y);
            float v = max > 0f ? cur / max : 0f;
            ghostV = v > ghostV ? v : Mathf.MoveTowards(ghostV, v, dt * 0.35f);
            UIKit.SetFill(fill, v);
            UIKit.SetFill(ghost, ghostV);
            string t = Mathf.CeilToInt(cur) + " / " + Mathf.CeilToInt(max);
            if (label.text != t) label.text = t;
        }
    }
}
