using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FracturedVow
{
    /// <summary>
    /// A map of the current floor: a minimap in the top-right corner and a full map on M (rebindable).
    /// The layout isn't drawn by hand: when a level loads, the floor is surveyed from above in the background
    /// (walkable floor, walls, water), so it works for both floors and any Floor 2 seed. Places you've been are
    /// brighter. Icons: you (arrow), EXIT, checkpoints, merchant ($), sellswords (+), the boss (B), unopened
    /// chests, boats (~) and your companions.
    /// </summary>
    public class MapUI : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        const float MiniSize = 240f, MiniMeters = 60f, RevealRadius = 14f;

        float cell = 1.25f;
        Bounds area;
        int W, H;
        byte[] kind;        // 0 nothing, 1 floor, 2 wall, 3 water
        bool[] seen;
        Color32[] pixels;
        Texture2D tex;
        bool ready, dirty;

        Transform player;
        RectTransform root, mini, miniView, full, fullView;
        RawImage miniImg, fullImg;
        Text arrowMini, arrowFull, status;
        float nextReveal, nextMarkers, nextApply;

        class Marker
        {
            public Transform t;
            public string glyph;
            public Color color;
            public Text mini, full;
            public System.Func<bool> show;
        }
        readonly List<Marker> markers = new List<Marker>();

        static readonly Color32 FloorSeen = new Color32(92, 86, 76, 255), FloorDim = new Color32(44, 42, 39, 235);
        static readonly Color32 WallSeen = new Color32(214, 186, 128, 255), WallDim = new Color32(92, 84, 70, 235);
        static readonly Color32 WaterSeen = new Color32(48, 118, 140, 255), WaterDim = new Color32(24, 54, 64, 235);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        void Start()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null) { enabled = false; return; }
            player = pc.transform;
            BuildUI();
            StartCoroutine(Survey());
        }

        // ---------------- surveying the level ----------------

        static bool Dynamic(Collider c) =>
            c.attachedRigidbody != null || c.GetComponentInParent<CharacterController>() != null || c.GetComponentInParent<Health>() != null;

        IEnumerator Survey()
        {
            yield return null;
            bool any = false;
            foreach (var c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (c.isTrigger || !c.enabled || Dynamic(c)) continue;
                if (!any) { area = c.bounds; any = true; } else area.Encapsulate(c.bounds);
            }
            if (!any) yield break;
            Vector3 size = area.size;
            if (size.x > 700f || size.z > 700f) // something far away (a stray prop): stay around the player
                area = new Bounds(player.position, new Vector3(Mathf.Min(size.x, 700f), size.y, Mathf.Min(size.z, 700f)));
            while ((area.size.x / cell) * (area.size.z / cell) > 160000f) cell *= 1.25f;
            W = Mathf.CeilToInt(area.size.x / cell);
            H = Mathf.CeilToInt(area.size.z / cell);
            kind = new byte[W * H];
            seen = new bool[W * H];
            pixels = new Color32[W * H];

            var water = new List<Bounds>();
            foreach (var wz in FindObjectsByType<WaterZone>(FindObjectsSortMode.None))
            {
                var col = wz.GetComponent<Collider>();
                if (col != null) water.Add(col.bounds);
            }

            float top = area.max.y + 2f, depth = area.size.y + 4f;
            var hits = new RaycastHit[32];
            int done = 0;
            for (int z = 0; z < H; z++)
                for (int x = 0; x < W; x++)
                {
                    float wx = area.min.x + (x + 0.5f) * cell, wzc = area.min.z + (z + 0.5f) * cell;
                    int n = Physics.RaycastNonAlloc(new Vector3(wx, top, wzc), Vector3.down, hits, depth, ~0, QueryTriggerInteraction.Ignore);
                    float floorY = float.MaxValue;
                    for (int i = 0; i < n; i++)
                        if (hits[i].normal.y > 0.7f && !Dynamic(hits[i].collider) && hits[i].point.y < floorY) floorY = hits[i].point.y;
                    byte k = 0;
                    bool inWater = false;
                    float waterTop = float.MinValue;
                    foreach (var b in water)
                        if (wx >= b.min.x && wx <= b.max.x && wzc >= b.min.z && wzc <= b.max.z) { inWater = true; waterTop = Mathf.Max(waterTop, b.max.y); }
                    if (floorY < float.MaxValue)
                    {
                        if (inWater && floorY < waterTop - 0.3f) k = 3;
                        else
                        {
                            bool blocked = false;
                            foreach (var o in Physics.OverlapBox(new Vector3(wx, floorY + 1.2f, wzc), new Vector3(cell * 0.35f, 0.6f, cell * 0.35f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                                if (!Dynamic(o)) { blocked = true; break; }
                            k = blocked ? (byte)2 : (byte)1;
                        }
                    }
                    else if (inWater) k = 3;
                    kind[z * W + x] = k;
                    if (++done % 700 == 0) yield return null; // spread over frames: no hitch when the level starts
                }

            // walls are only drawn where they border somewhere you can stand (clean outlines)
            for (int i = 0; i < kind.Length; i++)
                if (kind[i] == 2 && !NextToOpen(i % W, i / W)) kind[i] = 0;

            tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Paint(i);
            tex.SetPixels32(pixels);
            tex.Apply();
            miniImg.texture = tex;
            fullImg.texture = tex;
            FitFullMap();
            ready = true;
            status.text = "";
            Reveal();
        }

        bool NextToOpen(int x, int z)
        {
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= W || nz >= H) continue;
                    byte k = kind[nz * W + nx];
                    if (k == 1 || k == 3) return true;
                }
            return false;
        }

        Color32 Paint(int i)
        {
            bool s = seen[i];
            switch (kind[i])
            {
                case 1: return s ? FloorSeen : FloorDim;
                case 2: return s ? WallSeen : WallDim;
                case 3: return s ? WaterSeen : WaterDim;
                default: return Clear;
            }
        }

        /// <summary>Marks the area around the knight as explored.</summary>
        void Reveal()
        {
            if (!ready || player == null) return;
            int cx = Mathf.FloorToInt((player.position.x - area.min.x) / cell), cz = Mathf.FloorToInt((player.position.z - area.min.z) / cell);
            int r = Mathf.CeilToInt(RevealRadius / cell);
            for (int z = cz - r; z <= cz + r; z++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || z < 0 || x >= W || z >= H || (x - cx) * (x - cx) + (z - cz) * (z - cz) > r * r) continue;
                    int i = z * W + x;
                    if (seen[i]) continue;
                    seen[i] = true;
                    pixels[i] = Paint(i);
                    dirty = true;
                }
        }

        // ---------------- interface ----------------

        void BuildUI()
        {
            root = UIKit.Canvas("Map", 6, transform);

            // minimap, under the FPS counter in the top-right corner
            mini = UIKit.Place(UIKit.Rect("Minimap", root), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -112f), new Vector2(MiniSize, MiniSize));
            var frame = UIKit.Box(mini, "Frame", new Color(0.05f, 0.045f, 0.04f, 0.82f), true).rectTransform;
            UIKit.Stretch(frame);
            miniView = UIKit.Rect("View", mini);
            UIKit.Stretch(miniView, 4f);
            miniView.gameObject.AddComponent<RectMask2D>();
            miniImg = UIKit.Rect("Layout", miniView).gameObject.AddComponent<RawImage>();
            UIKit.Stretch(miniImg.rectTransform);
            miniImg.raycastTarget = false;
            arrowMini = Arrow(miniView, 26);
            var key = UIKit.Label(mini, "", 14, UIKit.Dim, TextAnchor.LowerRight);
            UIKit.Place((RectTransform)key.transform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-4f, -2f), new Vector2(220f, 20f));
            key.text = InputBindings.Name(GameAction.Map) + " map";

            // full map
            full = UIKit.Rect("Full Map", root);
            UIKit.Stretch(full);
            var dim = UIKit.Box(full, "Dim", new Color(0f, 0f, 0f, 0.78f)).rectTransform;
            UIKit.Stretch(dim);
            fullView = UIKit.Place(UIKit.Rect("View", full), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(1400f, 820f));
            var fullFrame = UIKit.Box(fullView, "Frame", new Color(0.06f, 0.055f, 0.05f, 0.9f), true).rectTransform;
            UIKit.Stretch(fullFrame, -8f);
            fullImg = UIKit.Rect("Layout", fullView).gameObject.AddComponent<RawImage>();
            UIKit.Stretch(fullImg.rectTransform);
            fullImg.raycastTarget = false;
            arrowFull = Arrow(fullView, 34);
            var title = UIKit.Label(full, "MAP", 44, UIKit.Gold, TextAnchor.MiddleCenter);
            UIKit.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(900f, 60f));
            var legend = UIKit.Label(full, "", 20, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Place((RectTransform)legend.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(1700f, 34f));
            legend.supportRichText = true;
            legend.text = "<color=#ffffff>▲ You</color>     <color=#ffd25e>EXIT</color>     <color=#8fd3ff>● Checkpoint</color>     <color=#ffd25e>$ Merchant</color>     " +
                          "<color=#8dff9a>+ Sellsword</color>     <color=#ff6a5a>B Boss</color>     <color=#ffae5e>■ Chest</color>     <color=#5fe3ff>~ Boat</color>     <color=#f2d99a>? Note</color>     " +
                          "Bright = explored     " + InputBindings.Name(GameAction.Map) + " to close";
            status = UIKit.Label(fullView, "Surveying the floor...", 26, UIKit.Dim, TextAnchor.MiddleCenter);
            UIKit.Stretch((RectTransform)status.transform);
            full.gameObject.SetActive(false);
        }

        static Text Arrow(Transform parent, int size)
        {
            var t = UIKit.Label(parent, "▲", size, Color.white, TextAnchor.MiddleCenter);
            UIKit.Place((RectTransform)t.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.4f, size * 1.4f));
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.9f);
            return t;
        }

        /// <summary>Sizes the full map to the level's shape (north is up).</summary>
        void FitFullMap()
        {
            float aspect = area.size.x / Mathf.Max(1f, area.size.z);
            float w = 1400f, h = w / aspect;
            if (h > 820f) { h = 820f; w = h * aspect; }
            fullView.sizeDelta = new Vector2(w, h);
        }

        // ---------------- markers ----------------

        void FindMarkers()
        {
            foreach (var m in markers) { if (m.mini != null) Destroy(m.mini.gameObject); if (m.full != null) Destroy(m.full.gameObject); }
            markers.Clear();
            void Add(Component c, string glyph, Color color, System.Func<bool> show = null)
            {
                if (c == null) return;
                markers.Add(new Marker { t = c.transform, glyph = glyph, color = color, show = show });
            }
            foreach (var e in FindObjectsByType<FloorExit>(FindObjectsSortMode.None)) Add(e, "EXIT", new Color(1f, 0.82f, 0.37f));
            foreach (var c in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None)) Add(c, "●", new Color(0.56f, 0.83f, 1f));
            foreach (var s in FindObjectsByType<ShopNPC>(FindObjectsSortMode.None)) Add(s, "$", new Color(1f, 0.82f, 0.37f));
            foreach (var m in FindObjectsByType<MercenaryPost>(FindObjectsSortMode.None)) { var mm = m; Add(m, "+", new Color(0.55f, 1f, 0.6f), () => mm != null && mm.isActiveAndEnabled); }
            foreach (var b in FindObjectsByType<WardenBoss>(FindObjectsSortMode.None)) { var h = b.GetComponent<Health>(); Add(b, "B", new Color(1f, 0.42f, 0.35f), () => h == null || !h.IsDead); }
            foreach (var b in FindObjectsByType<LeviathanBoss>(FindObjectsSortMode.None)) { var h = b.GetComponent<Health>(); Add(b, "B", new Color(1f, 0.42f, 0.35f), () => h == null || !h.IsDead); }
            foreach (var c in FindObjectsByType<Chest>(FindObjectsSortMode.None)) { var cc = c; Add(c, "■", new Color(1f, 0.68f, 0.37f), () => cc != null && !cc.Opened); }
            foreach (var b in FindObjectsByType<BoatController>(FindObjectsSortMode.None)) Add(b, "~", new Color(0.37f, 0.89f, 1f));
            foreach (var n in FindObjectsByType<LoreNote>(FindObjectsSortMode.None)) Add(n, "?", new Color(0.95f, 0.85f, 0.6f));
            foreach (var c in CompanionAI.All) if (c != null) Add(c, "•", new Color(0.55f, 1f, 0.6f));
            foreach (var m in markers)
            {
                bool big = m.glyph.Length > 1;
                m.mini = UIKit.Label(miniView, m.glyph, big ? 13 : 20, m.color, TextAnchor.MiddleCenter);
                UIKit.Place((RectTransform)m.mini.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 24f));
                m.full = UIKit.Label(fullView, m.glyph, big ? 18 : 26, m.color, TextAnchor.MiddleCenter);
                UIKit.Place((RectTransform)m.full.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 32f));
                foreach (var t in new[] { m.mini, m.full }) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0f, 0f, 0f, 0.9f); }
            }
            arrowMini.transform.SetAsLastSibling();
            arrowFull.transform.SetAsLastSibling();
        }

        // ---------------- every frame ----------------

        void Update()
        {
            if (player == null) return;
            bool blocked = UIFocus.Any || PrototypeHUD.Hidden || DevConsole.IsOpen;
            if (!blocked && InputBindings.Pressed(GameAction.Map)) IsOpen = !IsOpen;
            if (blocked && IsOpen && UIFocus.Any) IsOpen = false;
            full.gameObject.SetActive(IsOpen && !PrototypeHUD.Hidden);
            mini.gameObject.SetActive(!IsOpen && !blocked);
            if (!ready) return;

            if (Time.time >= nextReveal) { nextReveal = Time.time + 0.25f; Reveal(); }
            if (dirty && Time.unscaledTime >= nextApply) { nextApply = Time.unscaledTime + 0.4f; tex.SetPixels32(pixels); tex.Apply(); dirty = false; }
            if (Time.time >= nextMarkers) { nextMarkers = Time.time + 15f; FindMarkers(); }

            Vector3 p = player.position;
            float yaw = player.eulerAngles.y;
            if (mini.gameObject.activeSelf)
            {
                // the minimap scrolls so the knight stays in the middle (north up)
                float uw = MiniMeters / area.size.x, uh = MiniMeters / area.size.z;
                float u = (p.x - area.min.x) / area.size.x, v = (p.z - area.min.z) / area.size.z;
                miniImg.uvRect = new Rect(u - uw / 2f, v - uh / 2f, uw, uh);
                arrowMini.rectTransform.localEulerAngles = new Vector3(0f, 0f, -yaw);
                float half = (MiniSize - 8f) / 2f;
                foreach (var m in markers)
                {
                    if (m.mini == null) continue;
                    bool show = m.t != null && (m.show == null || m.show());
                    Vector2 rel = show ? new Vector2(m.t.position.x - p.x, m.t.position.z - p.z) / MiniMeters * (MiniSize - 8f) : Vector2.zero;
                    show &= Mathf.Abs(rel.x) < half - 6f && Mathf.Abs(rel.y) < half - 6f;
                    m.mini.gameObject.SetActive(show);
                    if (show) m.mini.rectTransform.anchoredPosition = rel;
                }
            }
            if (full.gameObject.activeSelf)
            {
                Vector2 size = fullView.sizeDelta;
                Vector2 Pos(Vector3 w) => new Vector2((w.x - area.center.x) / area.size.x * size.x, (w.z - area.center.z) / area.size.z * size.y);
                arrowFull.rectTransform.anchoredPosition = Pos(p);
                arrowFull.rectTransform.localEulerAngles = new Vector3(0f, 0f, -yaw);
                foreach (var m in markers)
                {
                    if (m.full == null) continue;
                    bool show = m.t != null && (m.show == null || m.show());
                    m.full.gameObject.SetActive(show);
                    if (show) m.full.rectTransform.anchoredPosition = Pos(m.t.position);
                }
            }
        }

        void OnDestroy() { IsOpen = false; if (tex != null) Destroy(tex); }
    }
}
