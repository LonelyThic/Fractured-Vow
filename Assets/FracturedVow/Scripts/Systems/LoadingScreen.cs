using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FracturedVow
{
    /// <summary>
    /// Every scene change goes through here: the title art fills the screen with the floor's name, a progress bar
    /// and a tip, while the next floor loads in the background. It fades away once the floor is ready.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public static bool Active { get; private set; }

        static readonly string[] Tips =
        {
            "Block just before a blow lands to parry it: no damage at all.",
            "Hold the attack button for a heavy strike that staggers husks.",
            "Shards you drop when you fall wait for you. Reach them before you fall again.",
            "Sellswords fight better in the armor and weapons you give them (Tab > Inventory > Give).",
            "Press M for the map. Places you've explored are brighter.",
            "When a platform cracks, run for the boat.",
            "Level up at any time from Tab > Character once you have enough shards.",
            "Elite husks glow red. They hit harder, but they're worth far more.",
            "Chests and merchants can sell you new spells. Equip up to three.",
            "Stuck? Esc > Unstuck puts you back on solid ground.",
        };

        RawImage art;
        RectTransform fill;
        CanvasGroup group;

        public static void Load(string scene) => Begin(scene, -1);
        public static void Load(int buildIndex) => Begin(null, buildIndex);

        static void Begin(string scene, int index)
        {
            if (Active) return;
            Active = true;
            var go = new GameObject("Loading Screen");
            DontDestroyOnLoad(go);
            go.AddComponent<LoadingScreen>().StartCoroutine(go.GetComponent<LoadingScreen>().Run(scene, index));
        }

        void Build(string scene, int index)
        {
            var root = UIKit.Canvas("Loading", 1000, transform);
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            var bg = UIKit.Box(root, "Black", Color.black).rectTransform;
            UIKit.Stretch(bg);
            var tex = Resources.Load<Texture2D>("FVUI/TitleBackground");
            if (tex != null)
            {
                art = UIKit.Rect("Art", root).gameObject.AddComponent<RawImage>();
                art.texture = tex;
                UIKit.Stretch(art.rectTransform);
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.width / (float)tex.height;
                art.color = new Color(0.75f, 0.75f, 0.75f);
            }
            var shade = UIKit.Box(root, "Shade", new Color(0f, 0f, 0f, 0.45f)).rectTransform;
            UIKit.Stretch(shade);

            string name = index == 0 || (scene != null && scene.Contains("Floor1")) ? "FLOOR 1\nTHE WARDEN'S PIT"
                        : scene != null && scene.Contains("Floor2") ? "FLOOR 2\nTHE DROWNED DEEP" : "LOADING";
            var title = UIKit.Label(root, name, 56, UIKit.Gold, TextAnchor.MiddleLeft);
            UIKit.Place((RectTransform)title.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(110f, 230f), new Vector2(1200f, 160f));
            title.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);

            var tip = UIKit.Label(root, Tips[Random.Range(0, Tips.Length)], 26, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Place((RectTransform)tip.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(110f, 150f), new Vector2(1500f, 44f));

            var barBack = UIKit.Box(root, "Bar", new Color(0f, 0f, 0f, 0.6f), true).rectTransform;
            UIKit.Place(barBack, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(110f, 90f), new Vector2(1700f, 14f));
            fill = UIKit.Box(barBack, "Fill", UIKit.Gold).rectTransform;
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }

        IEnumerator Run(string scene, int index)
        {
            Build(scene, index);
            Time.timeScale = 1f;
            for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime) { group.alpha = t / 0.25f; yield return null; }
            group.alpha = 1f;

            float shown = Time.unscaledTime;
            var op = scene != null ? SceneManager.LoadSceneAsync(scene) : SceneManager.LoadSceneAsync(index);
            if (op == null) { Active = false; Destroy(gameObject); yield break; }
            op.allowSceneActivation = false;
            float shownProgress = 0f;
            while (op.progress < 0.9f || Time.unscaledTime - shown < 1.2f) // at least a moment, so it reads as a screen
            {
                shownProgress = Mathf.MoveTowards(shownProgress, Mathf.Clamp01(op.progress / 0.9f), Time.unscaledDeltaTime * 1.5f);
                fill.anchorMax = new Vector2(shownProgress, 1f);
                Drift();
                yield return null;
            }
            fill.anchorMax = Vector2.one;
            op.allowSceneActivation = true;
            while (!op.isDone) { Drift(); yield return null; }
            yield return null; yield return null; // let the new floor settle its first frames

            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime) { group.alpha = 1f - t / 0.5f; Drift(); yield return null; }
            Active = false;
            Destroy(gameObject);
        }

        /// <summary>A slow push-in on the art, so the screen isn't frozen.</summary>
        void Drift()
        {
            if (art != null) art.rectTransform.localScale = Vector3.one * (1f + Mathf.Repeat(Time.unscaledTime * 0.01f, 0.08f));
        }
    }
}
