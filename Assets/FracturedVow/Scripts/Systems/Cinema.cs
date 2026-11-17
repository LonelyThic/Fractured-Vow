using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FracturedVow
{
    /// <summary>
    /// Story cinematics and spoken lines.
    ///  - Cinema.Play(shots): letterboxed camera shots with subtitles (Space / Enter / click skips), used for the
    ///    Floor 1 opening and the Warden's introduction.
    ///  - Cinema.Say(speaker, line): a spoken line during play (boss taunts, the narrator), shown above the hotbar.
    /// </summary>
    public class Cinema : MonoBehaviour
    {
        public struct Shot
        {
            public Vector3 camFrom, camTo, lookFrom, lookTo;
            public float seconds, shake;
            public string speaker, line;
        }

        static Cinema inst;
        RectTransform topBar, bottomBar, lineBox;
        Text speakerText, lineText;
        CanvasGroup lineGroup;
        bool skip, running;
        float sayUntil;
        string fullLine;
        float revealStart, revealTime;

        static Cinema Get()
        {
            if (inst == null)
            {
                var go = new GameObject("Cinema");
                inst = go.AddComponent<Cinema>();
                inst.BuildUI();
            }
            return inst;
        }

        void BuildUI()
        {
            var root = UIKit.Canvas("Cinema", 40, transform);
            topBar = UIKit.Box(root, "Top Bar", Color.black).rectTransform;
            topBar.anchorMin = new Vector2(0f, 1f); topBar.anchorMax = new Vector2(1f, 1f); topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, 0f);
            bottomBar = UIKit.Box(root, "Bottom Bar", Color.black).rectTransform;
            bottomBar.anchorMin = new Vector2(0f, 0f); bottomBar.anchorMax = new Vector2(1f, 0f); bottomBar.pivot = new Vector2(0.5f, 0f);
            bottomBar.sizeDelta = new Vector2(0f, 0f);

            lineBox = UIKit.Rect("Line", root);
            lineGroup = lineBox.gameObject.AddComponent<CanvasGroup>();
            lineGroup.alpha = 0f;
            lineGroup.blocksRaycasts = false;
            UIKit.Place(lineBox, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 190f), new Vector2(1400f, 110f));
            var back = UIKit.Box(lineBox, "Back", new Color(0f, 0f, 0f, 0.55f)).rectTransform;
            UIKit.Stretch(back);
            speakerText = UIKit.Label(lineBox, "", 24, UIKit.Gold, TextAnchor.UpperCenter);
            UIKit.Place((RectTransform)speakerText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(1360f, 30f));
            lineText = UIKit.Label(lineBox, "", 28, new Color(0.95f, 0.93f, 0.88f), TextAnchor.UpperCenter);
            UIKit.Place((RectTransform)lineText.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1360f, 70f));
            foreach (var t in new[] { speakerText, lineText }) t.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
        }

        // ---------------- spoken lines ----------------

        /// <summary>Shows a spoken line (typed out) for a few seconds.</summary>
        public static void Say(string speaker, string line, float seconds = 4.5f)
        {
            var c = Get();
            c.speakerText.text = speaker != null ? speaker.ToUpperInvariant() : "";
            c.fullLine = line;
            c.lineText.text = "";
            c.revealStart = Time.unscaledTime;
            c.revealTime = Mathf.Min(1.6f, line.Length * 0.025f);
            c.sayUntil = Time.unscaledTime + seconds;
        }

        void Update()
        {
            // the line types out, then fades
            if (fullLine != null)
            {
                float k = revealTime > 0f ? Mathf.Clamp01((Time.unscaledTime - revealStart) / revealTime) : 1f;
                int n = Mathf.RoundToInt(fullLine.Length * k);
                if (lineText.text.Length != n) lineText.text = fullLine.Substring(0, n);
            }
            float target = Time.unscaledTime < sayUntil && !UIFocus.Any ? 1f : 0f;
            lineGroup.alpha = Mathf.MoveTowards(lineGroup.alpha, target, Time.unscaledDeltaTime * 4f);
            ((RectTransform)lineBox).anchoredPosition = new Vector2(0f, running ? Screen.height * 0.02f + 150f : 190f);

            float bar = running ? 120f : 0f;
            topBar.sizeDelta = new Vector2(0f, Mathf.MoveTowards(topBar.sizeDelta.y, bar, Time.unscaledDeltaTime * 400f));
            bottomBar.sizeDelta = new Vector2(0f, Mathf.MoveTowards(bottomBar.sizeDelta.y, bar, Time.unscaledDeltaTime * 400f));

            if (running && SkipPressed()) skip = true;
        }

        static bool SkipPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
                || (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
#endif
        }

        // ---------------- cinematics ----------------

        public static bool Running => inst != null && inst.running;

        /// <summary>Plays the shots (can be yielded from a coroutine); 'done' runs at the end, skipped or not.</summary>
        public static Coroutine Play(List<Shot> shots, System.Action done = null) => Get().StartCoroutine(Get().Run(shots, done));

        IEnumerator Run(List<Shot> shots, System.Action done)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            var cam = Camera.main;
            if (pc == null || cam == null || CutsceneDirector.Playing) { done?.Invoke(); yield break; }
            var input = pc.GetComponent<PlayerInputHandler>();
            var follow = cam.GetComponent<ThirdPersonCamera>();
            running = true;
            skip = false;
            CutsceneDirector.Playing = true;
            PrototypeHUD.Hidden = true;
            if (input != null) input.inputEnabled = false;
            if (follow != null) follow.enabled = false;
            float near = cam.nearClipPlane;
            cam.nearClipPlane = 0.05f;

            foreach (var s in shots)
            {
                if (skip) break;
                if (!string.IsNullOrEmpty(s.line)) Say(s.speaker, s.line, s.seconds + 0.3f);
                if (s.shake > 0f) CameraShake.Shake(s.shake, Mathf.Min(1.5f, s.seconds));
                for (float t = 0f; t < s.seconds && !skip; t += Time.deltaTime)
                {
                    float k = Mathf.SmoothStep(0f, 1f, t / s.seconds);
                    Vector3 look = Vector3.Lerp(s.lookFrom, s.lookTo, k);
                    // the camera stays in the knight's own space (never through a wall or ceiling), then keeps
                    // a clear view of what it looks at
                    Vector3 anchor = pc.transform.position + Vector3.up * 1.5f;
                    Vector3 pos = Safe(anchor, Vector3.Lerp(s.camFrom, s.camTo, k), pc.transform);
                    pos = Safe(look, pos, pc.transform, anchor);
                    cam.transform.position = pos;
                    if ((look - pos).sqrMagnitude > 0.0001f) cam.transform.rotation = Quaternion.LookRotation(look - pos);
                    yield return null;
                }
            }

            sayUntil = 0f;
            cam.nearClipPlane = near;
            if (follow != null) follow.enabled = true;
            if (input != null) input.inputEnabled = true;
            PrototypeHUD.Hidden = false;
            CutsceneDirector.Playing = false;
            running = false;
            PlayerInputHandler.SetCursorLocked(true);
            done?.Invoke();
        }

        /// <summary>Keeps the camera on this side of walls (between the subject and where it wants to be).</summary>
        /// <summary>Like Safe(look, want), but only when 'look' is in the same space as 'anchor' (else 'want' as is).</summary>
        static Vector3 Safe(Vector3 look, Vector3 want, Transform ignore, Vector3 anchor)
        {
            if (Physics.Linecast(anchor, look, out var hit, ~0, QueryTriggerInteraction.Ignore) &&
                (ignore == null || !hit.transform.IsChildOf(ignore)) && hit.collider.GetComponentInParent<Health>() == null)
                return want; // the subject is behind something (the cell door, say): don't drag the camera through it
            return Safe(look, want, ignore);
        }

        static Vector3 Safe(Vector3 look, Vector3 want, Transform ignore)
        {
            Vector3 d = want - look;
            float len = d.magnitude;
            if (len < 0.05f) return want;
            foreach (var hit in Physics.SphereCastAll(look, 0.25f, d / len, len, ~0, QueryTriggerInteraction.Ignore))
            {
                if (ignore != null && hit.transform.IsChildOf(ignore)) continue;
                if (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) continue;
                if (hit.collider.GetComponentInParent<Health>() != null) continue;
                if (hit.distance < len) { want = look + d / len * Mathf.Max(0.4f, hit.distance - 0.3f); len = hit.distance; }
            }
            return want;
        }

        // ---------------- the story's scenes ----------------

        static Shot S(Vector3 cf, Vector3 ct, Vector3 lf, Vector3 lt, float sec, string speaker = null, string line = null, float shake = 0f) =>
            new Shot { camFrom = cf, camTo = ct, lookFrom = lf, lookTo = lt, seconds = sec, speaker = speaker, line = line, shake = shake };

        /// <summary>Floor 1's opening: the camera finds the knight in the dark while the narrator speaks.</summary>
        public static void PlayFloor1Opening(System.Action done)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null) { done?.Invoke(); return; }
            Transform k = pc.transform;
            Vector3 p = k.position, f = k.forward, r = k.right, up = Vector3.up;
            Vector3 head = p + up * 1.6f;
            const string N = "Narrator";
            // every shot stays within a couple of metres of the knight, so it never leaves his cell
            Play(new List<Shot>
            {
                // low and close, rising slowly up to his face
                S(head + f * 1.3f + r * 0.5f - up * 1.0f, head + f * 1.6f + r * 0.8f - up * 0.35f, head - up * 0.6f, head, 6f, N,
                  "They say a knight who breaks his vow forgets his own name."),
                // his profile in the torchlight
                S(head + r * 2.0f + f * 0.4f, head + r * 1.4f + f * 0.5f + up * 0.1f, head, head, 5.5f, N,
                  "You woke in the dark beneath the old keep, thrown in with the condemned."),
                // over his shoulder, toward the way out
                S(head - f * 1.3f + r * 0.55f + up * 0.2f, head - f * 1.0f + r * 0.45f + up * 0.1f, head + f * 5f - up * 0.3f, head + f * 7f - up * 0.2f, 5.5f, N,
                  "Somewhere above, the Warden keeps the only way out."),
                // easing back to where the game's camera sits
                S(head - f * 1.0f + up * 0.15f, head - f * 2.6f + up * 0.8f, head, head + f * 3f, 4f, N,
                  "Remember who you were... or die down here trying."),
            }, done);
        }

        /// <summary>The Warden's introduction, the first time the knight enters his arena.</summary>
        public static Coroutine PlayWardenIntro(Transform warden)
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null || warden == null) return null;
            Vector3 b = warden.position, p = pc.transform.position;
            float h = 3.5f;
            foreach (var rend in warden.GetComponentsInChildren<Renderer>()) h = Mathf.Max(h, rend.bounds.max.y - b.y);
            Vector3 toPlayer = p - b; toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.01f) toPlayer = warden.forward;
            toPlayer.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer);
            Vector3 face = b + Vector3.up * (h * 0.85f);
            const string W = "The Warden";
            return Play(new List<Shot>
            {
                S(p + Vector3.up * 2.4f - toPlayer * 2.5f, p + Vector3.up * 2.8f - toPlayer * 1.2f, face, face, 3.5f, W,
                  "Another one... crawling up from my cells."),
                S(b + toPlayer * (h * 1.8f) + Vector3.up * (h * 0.9f) + side * 2.5f, b + toPlayer * (h * 1.5f) + Vector3.up * (h * 0.95f) + side * 1.5f, face, face, 4.5f, W,
                  "Three hundred years I have kept this pit. I have broken kings in it."),
                S(b + toPlayer * (h * 1.1f) + Vector3.up * 0.8f - side * 2f, b + toPlayer * (h * 1.2f) + Vector3.up * 1f - side * 1f, face, face + Vector3.up * 0.3f, 4.5f, W,
                  "Your order sent you down here to be forgotten. Let me finish their work."),
                S(b + toPlayer * (h * 1.6f) + Vector3.up * (h * 0.7f), b + toPlayer * (h * 2.4f) + Vector3.up * (h * 0.8f), face, face, 3f, W,
                  "Come then, vowbreaker. Kneel... or be broken!", 0.5f),
            });
        }
    }
}
