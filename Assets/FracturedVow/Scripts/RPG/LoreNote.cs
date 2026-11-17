using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FracturedVow
{
    /// <summary>
    /// Notes left around both floors: scratched confessions, ledgers, prayers and warnings that tell the story of
    /// the Vow. They are placed automatically beside landmarks (checkpoints, merchants, chests, the exit), so any
    /// build of either floor gets them. F picks one up: it's read on a parchment page (the game pauses) and kept in
    /// the Journal (Tab > Journal) for the rest of the run. F, Esc or Enter puts the page down.
    /// </summary>
    public class LoreNote : MonoBehaviour, IInteractable
    {
        public static bool Reading { get; private set; }

        public string title;
        [TextArea] public string body;

        public string InteractionPrompt => "Take: " + title;
        public bool CanInteract(PlayerInteractor interactor) => !Reading;

        public void Interact(PlayerInteractor interactor)
        {
            if (!RunState.Notes.Contains(title)) RunState.Notes.Add(title);
            PrototypeHUD.Message("Added to your Journal (Tab > Journal)", 2.5f);
            Reader.Open(title, body);
            WorldLog.Remove(gameObject); // the Journal is rewound with the character, so the page comes back too
        }

        /// <summary>The text of a note, by title (for the Journal).</summary>
        public static string BodyOf(string title)
        {
            foreach (var set in new[] { Floor1, Floor2 })
                foreach (var n in set) if (n[0] == title) return n[1];
            return "";
        }

        /// <summary>Opens the parchment page for any note (used by the Journal).</summary>
        public static void Read(string title) => Reader.Open(title, BodyOf(title));

        // ---------------- the writings ----------------

        static readonly string[][] Floor1 =
        {
            new[] { "Scratched into a cell wall", "Day forty. They took my sword, my colors, my name.\nThey left me the dark and the dripping.\nIf you read this, you are not the first.\nYou will not be the last.\n\n- R." },
            new[] { "A gaoler's orders", "Do not answer the prisoners.\nDo not look into the lower pit.\nDo not wake Him before the bell.\n\nAny man found speaking of the Order of the Vow will be fed to the pit." },
            new[] { "Torn page: The Order of the Vow", "We swear upon the water and the stone: to keep the gate, to hold the deep, to never let the hunger rise.\n\nThe knight who breaks this oath shall forget his name, and wander until he remembers it." },
            new[] { "A letter, never sent", "Mira,\nThe Warden was a knight once. One of us. He swore the same oath I did.\nNow he keeps the cells for whoever pays.\nI think he forgot what the oath was for.\nI hope I never do.\n\nYour brother" },
            new[] { "The Warden's tally", "Kings: 2\nKnights: 41\nThieves: uncounted\nVowbreakers: 1, still living\n\nThat one must not reach the Deep." },
            new[] { "A merchant's ledger", "Sold: one flask, two torches, a dead man's boots.\nBought: a ring off a knight who swore he'd be back for it.\nHe wasn't.\n\nNote to self: gold spends the same down here. Stay near the sellswords." },
            new[] { "Prayer to the Drowned", "Sleeping one beneath the lake,\nkeep your mouth shut, keep your teeth still,\nand we will keep the oath.\n\n(the ink is smeared, as if by water)" },
            new[] { "Warning nailed to the arena gate", "BEYOND: THE WARDEN'S PIT.\nTURN BACK.\n\nScrawled underneath, in another hand:\nhe is slow when he swings the great blade. move when he lifts it." },
        };

        static readonly string[][] Floor2 =
        {
            new[] { "A fisherman's log", "The water went black three nights ago. The fish swim toward the lake, never away.\nSomething below is breathing. I can feel it in the boards." },
            new[] { "Fragment: the first oath", "Here, before the drowned altar, the first knights swore.\nThe beast slept because they kept their word.\n\nWhoever breaks the Vow wakes it." },
            new[] { "A boatwright's note", "Two boats left, one in each boathouse, east and west.\nNever leave them far from the platforms.\nIf the stone cracks under you, you'll want a hull beneath your feet." },
            new[] { "Hymn of the Sunken Chapel", "We sang until the water reached the bells.\nThen we sang under it.\n\nThe Leviathan does not hate us.\nIt is only hungry, and we stopped feeding it promises." },
            new[] { "A drowned knight's last words", "I was the one who broke it.\nI chose my brother over the gate, and the deep rose up to collect.\nIf you have my sword, you have my sin.\nFinish it." },
            new[] { "Cult tally, carved in driftwood", "Offerings thrown to the deep: 300\nAnswered: 0\nThe old way was a vow, not a sacrifice.\nWe forgot." },
            new[] { "Scrap of a map", "Breach Hall to the south. The boathouses lie west and east of the lake.\nThe five platforms are old stone. They will not hold forever.\n\nStrike it when it surfaces. Every wound costs a platform." },
            new[] { "On the Leviathan", "It is older than the keep and older than the Order.\nIt cannot be killed by hate, only by a promise kept.\n\nSo keep it, vowbreaker. Remember your name." },
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Spawn();
            Spawn(); // the scene the game started in has already loaded
        }

        static void Spawn()
        {
            Reading = false;
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null) return;
            string scene = PlayerProgress.SceneName;
            var texts = scene.Contains("Floor2") ? Floor2 : scene.Contains("Floor1") ? Floor1 : null;
            if (texts == null || Object.FindFirstObjectByType<LoreNote>() != null) return;

            // landmarks, nearest the start first, so the story reads roughly in order
            var anchors = new List<Vector3>();
            void Add(IEnumerable<MonoBehaviour> list) { foreach (var b in list) if (b != null) anchors.Add(b.transform.position); }
            Add(Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None));
            Add(Object.FindObjectsByType<ShopNPC>(FindObjectsSortMode.None));
            Add(Object.FindObjectsByType<MercenaryPost>(FindObjectsSortMode.None));
            Add(Object.FindObjectsByType<Chest>(FindObjectsSortMode.None));
            Add(Object.FindObjectsByType<BoatController>(FindObjectsSortMode.None));
            Add(Object.FindObjectsByType<FloorExit>(FindObjectsSortMode.None));
            Vector3 start = pc.transform.position;
            anchors.Sort((a, b) => (a - start).sqrMagnitude.CompareTo((b - start).sqrMagnitude));
            anchors.Insert(0, start); // the first note is in the knight's own cell (or the antechamber)

            var placed = new List<Vector3>();
            int next = 0;
            foreach (var a in anchors)
            {
                if (next >= texts.Length) break;
                bool tooClose = false;
                foreach (var p in placed) if ((p - a).sqrMagnitude < 14f * 14f) { tooClose = true; break; }
                if (tooClose) continue;
                while (next < texts.Length && RunState.Notes.Contains(texts[next][0])) next++; // already in the Journal
                if (next >= texts.Length) break;
                if (TryPlace(a, texts[next][0], texts[next][1], out Vector3 at)) { placed.Add(a); next++; }
            }
        }

        static bool TryPlace(Vector3 anchor, string title, string body, out Vector3 at)
        {
            at = anchor;
            for (int i = 0; i < 10; i++)
            {
                float ang = i * 37f * Mathf.Deg2Rad, dist = 1.6f + (i % 3) * 0.6f;
                Vector3 p = anchor + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * dist;
                if (!Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var ground, 3.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (ground.normal.y < 0.8f || ground.collider.attachedRigidbody != null) continue;
                Vector3 feet = ground.point;
                if (WaterZone.BlocksWalking(feet.y, feet)) continue;
                if (Physics.CheckBox(feet + Vector3.up * 0.5f, new Vector3(0.35f, 0.4f, 0.35f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                at = feet;
                Make(feet, title, body);
                return true;
            }
            return false;
        }

        static Material paperMat;

        static void Make(Vector3 at, string title, string body)
        {
            var go = new GameObject("Note: " + title);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var note = go.AddComponent<LoreNote>();
            note.title = title;
            note.body = body;
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(1.2f, 1.2f, 1.2f);
            col.center = new Vector3(0f, 0.5f, 0f);

            if (paperMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                paperMat = new Material(sh);
                paperMat.SetColor("_BaseColor", new Color(0.86f, 0.78f, 0.6f));
                paperMat.EnableKeyword("_EMISSION");
                paperMat.SetColor("_EmissionColor", new Color(0.35f, 0.28f, 0.14f));
            }
            for (int i = 0; i < 2; i++) // two loose pages
            {
                var page = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.Destroy(page.GetComponent<Collider>());
                page.name = "Page";
                page.transform.SetParent(go.transform, false);
                page.transform.localPosition = new Vector3(i * 0.12f, 0.02f + i * 0.012f, i * 0.06f);
                page.transform.localRotation = Quaternion.Euler(Random.Range(-3f, 3f), i * 24f - 8f, Random.Range(-3f, 3f));
                page.transform.localScale = new Vector3(0.36f, 0.012f, 0.48f);
                page.GetComponent<Renderer>().sharedMaterial = paperMat;
            }
            var glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.82f, 0.5f);
            glow.range = 2.6f;
            glow.intensity = 1.1f;
            glow.shadows = LightShadows.None;
        }

        // ---------------- the reading page ----------------

        class Reader : MonoBehaviour
        {
            static Reader inst;
            Text titleText, bodyText;
            GameObject root;
            float prevScale = 1f;
            int openedFrame;

            public static void Open(string title, string body)
            {
                if (inst == null)
                {
                    var go = new GameObject("Note Reader");
                    DontDestroyOnLoad(go);
                    inst = go.AddComponent<Reader>();
                    inst.Build();
                }
                inst.titleText.text = title.ToUpperInvariant();
                inst.bodyText.text = body;
                inst.root.SetActive(true);
                inst.openedFrame = Time.frameCount;
                inst.prevScale = Time.timeScale;
                Time.timeScale = 0f;
                Reading = true;
                GameAudio.PlayUI("ui_click", 0.6f);
            }

            void Build()
            {
                var canvas = UIKit.Canvas("Note", 120, transform); // above the Journal tab
                root = canvas.gameObject;
                var dim = UIKit.Box(canvas, "Dim", new Color(0f, 0f, 0f, 0.6f)).rectTransform;
                UIKit.Stretch(dim);
                var page = UIKit.Box(canvas, "Parchment", new Color(0.86f, 0.79f, 0.62f), true).rectTransform;
                UIKit.Place(page, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 760f));
                var edge = page.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(0.35f, 0.25f, 0.12f, 0.9f);
                edge.effectDistance = new Vector2(4f, -4f);
                titleText = UIKit.Label(page, "", 34, new Color(0.3f, 0.18f, 0.08f), TextAnchor.UpperCenter);
                UIKit.Place((RectTransform)titleText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 50f));
                titleText.fontStyle = FontStyle.Bold;
                bodyText = UIKit.Label(page, "", 28, new Color(0.18f, 0.12f, 0.06f), TextAnchor.UpperLeft);
                UIKit.Place((RectTransform)bodyText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(760f, 560f));
                bodyText.fontStyle = FontStyle.Italic;
                bodyText.lineSpacing = 1.15f;
                var hint = UIKit.Label(page, "F  /  Esc  to put it down", 20, new Color(0.35f, 0.25f, 0.12f), TextAnchor.LowerCenter);
                UIKit.Place((RectTransform)hint.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(800f, 30f));
                root.SetActive(false);
            }

            void Update()
            {
                if (!Reading || Time.frameCount == openedFrame) return;
                if (!ClosePressed()) return;
                root.SetActive(false);
                Reading = false;
                // back to the game, or back to the Journal (which keeps the game paused)
                Time.timeScale = CharacterMenu.IsOpen ? 0f : (prevScale <= 0f ? 1f : prevScale);
                UIFocus.LastClosedFrame = Time.frameCount; // this Esc must not also open the pause menu
            }

            static bool ClosePressed()
            {
                if (InputBindings.Pressed(GameAction.Interact)) return true;
#if ENABLE_INPUT_SYSTEM
                var k = Keyboard.current;
                return k != null && (k.escapeKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame);
#else
                return Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif
            }
        }
    }
}
