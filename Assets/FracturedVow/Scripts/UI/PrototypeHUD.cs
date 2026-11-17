using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Zero-setup prototype HUD drawn with OnGUI (no Canvas or TextMeshPro needed): health/mana/stamina
    /// bars, equipped spell, interaction prompt, messages, memory pop-ups, enemy health bars, floating
    /// damage numbers and a controls panel (H). Replace with a proper UI Toolkit/Canvas HUD later.
    /// </summary>
    public class PrototypeHUD : MonoBehaviour
    {
        public bool showControls = false;
        public float referenceHeight = 900f;

        static PrototypeHUD instance;

        /// <summary>Hides the whole HUD (used during cutscenes).</summary>
        public static bool Hidden;

        PlayerController player;
        Health health;
        PlayerStats stats;
        PlayerMagic magic;
        PlayerInteractor interactor;
        PlayerInputHandler input;
        Camera cam;

        struct Msg { public string text; public float until; }
        struct Num { public Vector3 pos; public string text; public Color color; public float start; }

        readonly List<Msg> messages = new List<Msg>();
        readonly List<Num> numbers = new List<Num>();
        string bigTitle, bigBody;
        float bigUntil;

        GUIStyle label, small, center, bigTitleStyle, bigBodyStyle, prompt;
        float scale = 1f;

        const string ControlsText =
            "WASD  move            Shift  sprint\n" +
            "Space  jump            Mouse  look / Scroll zoom\n" +
            "LMB  sword (3-hit combo)\n" +
            "RMB  raise shield (block)\n" +
            "E  cast spell     Q / 1-3  swap spell\n" +
            "F  interact / board boat / step off\n" +
            "H  hide help    R  restart    Esc  free mouse";

        void Awake() { instance = this; }

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                health = player.GetComponent<Health>();
                stats = player.GetComponent<PlayerStats>();
                magic = player.GetComponent<PlayerMagic>();
                interactor = player.GetComponent<PlayerInteractor>();
                input = player.GetComponent<PlayerInputHandler>();
            }
        }

        void Update()
        {
            if (input != null && input.HelpPressed) showControls = !showControls;
            messages.RemoveAll(m => Time.time > m.until);
            numbers.RemoveAll(n => Time.time - n.start > 0.9f);
        }

        public static void Message(string text, float duration = 2.5f)
        {
            if (instance == null) return;
            var list = instance.messages;
            for (int i = 0; i < list.Count; i++)
                if (list[i].text == text) { list[i] = new Msg { text = text, until = Time.time + duration }; return; }
            list.Add(new Msg { text = text, until = Time.time + duration });
            if (list.Count > 4) list.RemoveAt(0);
        }

        public static void BigMessage(string title, string body, float duration = 6f)
        {
            if (instance == null) return;
            instance.bigTitle = title;
            instance.bigBody = body;
            instance.bigUntil = Time.time + duration;
        }

        /// <summary>Floating text in the world (e.g. "+36 shards").</summary>
        public static void FloatText(Vector3 worldPos, string text, Color color)
        {
            if (instance == null) return;
            instance.numbers.Add(new Num { pos = worldPos, text = text, color = color, start = Time.time });
        }

        public static void DamageNumber(Vector3 worldPos, float amount, Color color)
        {
            if (instance == null) return;
            instance.numbers.Add(new Num
            {
                pos = worldPos + Random.insideUnitSphere * 0.3f,
                text = Mathf.RoundToInt(amount).ToString(),
                color = color,
                start = Time.time
            });
        }

        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            label.normal.textColor = Color.white;
            small = new GUIStyle(label) { fontSize = 13, fontStyle = FontStyle.Normal };
            center = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };
            prompt = new GUIStyle(center) { fontSize = 20 };
            bigTitleStyle = new GUIStyle(center) { fontSize = 34 };
            bigTitleStyle.normal.textColor = new Color(0.85f, 0.7f, 1f);
            bigBodyStyle = new GUIStyle(center) { fontSize = 18, fontStyle = FontStyle.Italic, wordWrap = true };
        }

        void OnGUI()
        {
            if (UIFocus.Any || Hidden) return;
            Styles();
            if (cam == null) cam = Camera.main;
            scale = Screen.height / referenceHeight;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;

            DrawWorldBars();
            DrawNumbers();

            // player bars, spells, shards and flasks now live in GameHUD (the RPG HUD)

            if (interactor != null && interactor.enabled && interactor.CurrentPrompt != null)
                Shadow(new Rect(w / 2 - 250, h * 0.62f, 500, 30), interactor.CurrentPrompt, prompt);

            float my = h * 0.3f;
            foreach (var m in messages) { Shadow(new Rect(w / 2 - 300, my, 600, 26), m.text, center); my += 26; }

            if (Time.time < bigUntil)
            {
                Rect r = new Rect(w / 2 - 330, h * 0.12f, 660, 200);
                Fill(r, new Color(0f, 0f, 0f, 0.65f));
                GUI.Label(new Rect(r.x, r.y + 10, r.width, 44), bigTitle, bigTitleStyle);
                GUI.Label(new Rect(r.x + 20, r.y + 56, r.width - 40, 140), bigBody, bigBodyStyle);
            }

            if (PlayerInputHandler.CursorLocked)
                Fill(new Rect(w / 2 - 2, h / 2 - 2, 4, 4), new Color(1f, 1f, 1f, 0.8f));
            else
                Shadow(new Rect(w / 2 - 200, h / 2 - 15, 400, 30), "Click to capture the mouse", prompt);
        }

        readonly System.Collections.Generic.Dictionary<Health, (float until, bool seen)> sightCache =
            new System.Collections.Generic.Dictionary<Health, (float, bool)>();

        /// <summary>A bar shows once the foe is hurt or hunting the knight (not for everyone in the dungeon).</summary>
        static bool Engaged(Health hp)
        {
            if (Time.time - hp.LastDamageTime < 8f) return true;
            var ai = hp.GetComponent<EnemyAI>();
            return ai != null && (ai.CurrentState == EnemyAI.State.Chase || ai.CurrentState == EnemyAI.State.Attack);
        }

        /// <summary>No bars through walls (checked four times a second per foe).</summary>
        bool InSight(Health hp, Vector3 camPos)
        {
            if (sightCache.TryGetValue(hp, out var c) && Time.time < c.until) return c.seen;
            Vector3 at = hp.transform.position + Vector3.up * Mathf.Max(1f, hp.worldBarHeight * 0.6f);
            bool seen = true;
            foreach (var h in Physics.RaycastAll(camPos, (at - camPos).normalized, Vector3.Distance(camPos, at), ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.transform.IsChildOf(hp.transform) || h.collider.GetComponentInParent<Health>() != null || h.collider.attachedRigidbody != null) continue;
                seen = false;
                break;
            }
            sightCache[hp] = (Time.time + 0.25f, seen);
            return seen;
        }

        void DrawWorldBars()
        {
            if (cam == null || Event.current.type != EventType.Repaint) return; // draw once per frame, not per GUI event
            Vector3 camPos = cam.transform.position;
            foreach (var hp in Health.All)
            {
                if (hp == null || !hp.showWorldBar || hp.IsDead) continue;
                if ((hp.transform.position - camPos).sqrMagnitude > 900f) continue; // only within 30 m
                if (!Engaged(hp) || !InSight(hp, camPos)) continue;
                Vector3 sp = cam.WorldToScreenPoint(hp.transform.position + Vector3.up * hp.worldBarHeight);
                if (sp.z < 0f || sp.z > 30f) continue;
                float x = sp.x / scale, y = (Screen.height - sp.y) / scale;
                var ai = hp.GetComponent<EnemyAI>();
                bool elite = ai != null && ai.elite;
                Bar(x - (elite ? 40 : 30), y, elite ? 80 : 60, elite ? 9 : 7, hp.Normalized, elite ? new Color(1f, 0.55f, 0.15f) : new Color(0.9f, 0.2f, 0.2f), null);
            }
        }

        void DrawNumbers()
        {
            if (cam == null) return;
            foreach (var n in numbers)
            {
                float t = (Time.time - n.start) / 0.9f;
                Vector3 sp = cam.WorldToScreenPoint(n.pos + Vector3.up * t * 0.8f);
                if (sp.z < 0f) continue;
                float x = sp.x / scale, y = (Screen.height - sp.y) / scale;
                var c = n.color; c.a = 1f - t;
                var old = GUI.color;
                GUI.color = c;
                Shadow(new Rect(x - 40, y - 12, 80, 24), n.text, center);
                GUI.color = old;
            }
        }

        void Bar(float x, float y, float w, float h, float fill, Color color, string text)
        {
            Fill(new Rect(x - 1, y - 1, w + 2, h + 2), new Color(0f, 0f, 0f, 0.7f));
            Fill(new Rect(x, y, w * Mathf.Clamp01(fill), h), color);
            if (!string.IsNullOrEmpty(text)) GUI.Label(new Rect(x + 6, y - 2, w, h + 4), text, small);
        }

        static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        static void Shadow(Rect r, string text, GUIStyle style)
        {
            var old = GUI.color;
            var c = old;
            GUI.color = new Color(0f, 0f, 0f, c.a * 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            GUI.color = c;
            GUI.Label(r, text, style);
        }
    }
}
