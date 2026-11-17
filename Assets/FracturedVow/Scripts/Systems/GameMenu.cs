using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace FracturedVow
{
    /// <summary>
    /// The game's menus, built in code so every level gets them automatically:
    /// title screen (first launch), Esc pause menu, settings (brightness, volumes, mouse, camera shake),
    /// controls, and the floor-cleared screen. Pauses time and audio while open.
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }

        static GameMenu inst;
        static bool titleShownThisSession;
        static bool titleRequested;

        // palette
        static readonly Color Gold = new Color(0.95f, 0.78f, 0.42f);
        static readonly Color Ink = new Color(0.93f, 0.9f, 0.85f);
        static readonly Color Dim = new Color(0.62f, 0.6f, 0.58f);
        static readonly Color PanelCol = new Color(0.05f, 0.045f, 0.05f, 0.92f);
        static readonly Color ButtonCol = new Color(0.16f, 0.13f, 0.12f, 1f);

        Font font;
        RectTransform canvasRoot;
        GameObject titlePanel, pausePanel, settingsPanel, controlsPanel, clearedPanel, deathPanel, newGamePanel, loadPanel;
        int chosenDifficulty = 2, chosenSlot = -1;
        Text deathSub;
        GameObject backTarget;
        Text clearedTitle, clearedBody;
        Button continueButton;
        string nextScene;
        readonly Dictionary<Volume, float> baseExposure = new Dictionary<Volume, float>();
        Color baseAmbient, baseSky, baseEquator, baseGround;

        // ---------------- bootstrapping ----------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Spawn();
        }

        static void OnSceneLoaded(Scene s, LoadSceneMode m) => Spawn();

        static void Spawn()
        {
            if (inst != null) return;
            if (FindFirstObjectByType<PlayerController>() == null) return;
            new GameObject("GameMenu").AddComponent<GameMenu>();
        }

        void Awake()
        {
            inst = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            baseAmbient = RenderSettings.ambientLight;
            baseSky = RenderSettings.ambientSkyColor;
            baseEquator = RenderSettings.ambientEquatorColor;
            baseGround = RenderSettings.ambientGroundColor;
            EnsureEventSystem();
            BuildCanvas();
            BuildMenuBackground();
            BuildTitle();
            BuildPause();
            BuildSettings();
            BuildVideo();
            BuildControls();
            BuildCleared();
            BuildDeath();
            HideAll();
        }

        void Start()
        {
            ApplySettings();
            GameSettings.Changed += ApplySettings;
            if (!titleShownThisSession || titleRequested)
            {
                titleShownThisSession = true;
                titleRequested = false;
                Open(titlePanel);
            }
            else ShowFloorIntro();
        }

        void OnDestroy()
        {
            GameSettings.Changed -= ApplySettings;
            if (inst == this) inst = null;
            IsOpen = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        // ---------------- open / close ----------------

        GameObject menuBg;

        /// <summary>The title art (Resources/FVUI/TitleBackground) behind the main menu and its screens.</summary>
        void BuildMenuBackground()
        {
            menuBg = Panel("Menu Background", 1f);
            var tex = Resources.Load<Texture2D>("FVUI/TitleBackground");
            if (tex != null)
            {
                var art = UI("Art", menuBg.transform).gameObject.AddComponent<RawImage>();
                art.texture = tex;
                Stretch((RectTransform)art.transform);
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.width / (float)tex.height;
                art.raycastTarget = false;
            }
            menuBg.transform.SetAsFirstSibling();
            menuBg.SetActive(false);
        }

        /// <summary>True while the main menu (or a screen opened from it) is showing.</summary>
        bool InMainMenu()
        {
            if (titlePanel != null && titlePanel.activeSelf) return true;
            if (newGamePanel != null && newGamePanel.activeSelf) return true;
            if (loadPanel != null && loadPanel.activeSelf) return true;
            bool sub = (settingsPanel != null && settingsPanel.activeSelf) || (controlsPanel != null && controlsPanel.activeSelf) ||
                       (videoPanel != null && videoPanel.activeSelf);
            return sub && backTarget == titlePanel;
        }

        void Update()
        {
            if (menuBg != null)
            {
                bool want = InMainMenu();
                if (menuBg.activeSelf != want) menuBg.SetActive(want);
                if (want) menuBg.transform.localScale = Vector3.one * (1f + Mathf.Sin(Time.unscaledTime * 0.08f) * 0.015f); // slow breathing push
            }
            if (creditsPanel != null && creditsPanel.activeSelf) { UpdateCredits(); return; }
            if (PollRebind()) return;
            if (CharacterMenu.IsOpen || DevConsole.IsOpen || ShopUI.IsOpen || LoreNote.Reading || CheatMenu.IsOpen || Time.frameCount - UIFocus.LastClosedFrame <= 1) return;
            if (!EscapePressed()) return;
            if (deathPanel.activeSelf) return;
            bool subOpen = settingsPanel.activeSelf || controlsPanel.activeSelf || (videoPanel != null && videoPanel.activeSelf) ||
                           (newGamePanel != null && newGamePanel.activeSelf) || (loadPanel != null && loadPanel.activeSelf);
            if (titlePanel.activeSelf || clearedPanel.activeSelf) return;
            if (subOpen) Back();
            else if (pausePanel.activeSelf) Resume();
            else Open(pausePanel);
        }

        static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame
                || Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        void Open(GameObject panel)
        {
            HideAll();
            panel.SetActive(true);
            // gameplay menus pause the music; the title and floor-cleared screens keep it playing
            GameAudio.SetMusicPaused(panel != titlePanel && panel != clearedPanel);
            IsOpen = true;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            PlayerInputHandler.SetCursorLocked(false);
            SelectFirst(panel);
        }

        void Resume()
        {
            HideAll();
            IsOpen = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            GameAudio.SetMusicPaused(false);
            if (!CutsceneDirector.Playing) PlayerInputHandler.SetCursorLocked(true);
        }

        void HideAll()
        {
            foreach (var p in new[] { titlePanel, pausePanel, settingsPanel, controlsPanel, clearedPanel, deathPanel, newGamePanel, loadPanel, videoPanel, creditsPanel })
                if (p != null) p.SetActive(false);
        }

        void ShowSub(GameObject sub)
        {
            backTarget = titlePanel.activeSelf ? titlePanel : clearedPanel.activeSelf ? clearedPanel : pausePanel;
            HideAll();
            sub.SetActive(true);
            SelectFirst(sub);
        }

        void Back()
        {
            HideAll();
            (backTarget != null ? backTarget : pausePanel).SetActive(true);
            SelectFirst(backTarget != null ? backTarget : pausePanel);
        }

        static void SelectFirst(GameObject panel)
        {
            var b = panel.GetComponentInChildren<Selectable>();
            if (b != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(b.gameObject);
        }

        void ShowFloorIntro()
        {
            if (FindFirstObjectByType<CutsceneDirector>() != null) return; // the cutscene shows its own title card
            bool f2 = SceneManager.GetActiveScene().name.Contains("Floor2");
            PrototypeHUD.BigMessage(f2 ? "FLOOR 2 - THE DROWNED DEEP" : "FLOOR 1 - THE WARDEN'S PIT",
                f2 ? "Find a boat. Face the Leviathan." : "Escape the cells. Find the way up.\nPress Esc for settings and controls.", 5f);
        }

        /// <summary>Called by GameManager when the knight dies: respawn at the checkpoint or restart the floor.</summary>
        public static void ShowDeath()
        {
            if (inst == null) { if (GameManager.Instance != null) GameManager.Instance.Respawn(); return; }
            int lost = GameManager.LastLost;
            inst.deathSub.text = lost > 0
                ? "You dropped " + lost + " Memory Shards where you fell. Reach them before you die again, or they are gone."
                : "The dungeon is patient. You will wake at your last checkpoint, as you were when you reached it.";
            inst.Open(inst.deathPanel);
        }

        void RespawnNow()
        {
            Resume();
            if (GameManager.Instance != null) GameManager.Instance.Respawn();
        }

        /// <summary>Called by FloorExit when the knight reaches the end of a floor.</summary>
        public static void ShowFloorCleared(string title, string body, string next)
        {
            if (inst == null) return;
            // the last door: the story ends and the credits roll
            if (string.IsNullOrEmpty(next)) { inst.RollCredits(true, title, body); return; }
            inst.nextScene = next;
            inst.clearedTitle.text = title;
            inst.clearedBody.text = body;
            bool canContinue = !string.IsNullOrEmpty(next) && Application.CanStreamedLevelBeLoaded(next);
            inst.continueButton.gameObject.SetActive(canContinue);
            inst.Open(inst.clearedPanel);
        }

        // ---------------- actions ----------------

        /// <summary>Picks up the run saved on this computer, on the floor it reached.</summary>
        void ContinueRun()
        {
            int recent = SaveSystem.MostRecentSlot();
            if (recent < 0) { ShowNewGame(true); return; }
            OpenSlot(recent);
        }

        /// <summary>Loads a save slot and opens the floor it was saved on.</summary>
        void OpenSlot(int slot)
        {
            string scene = SaveSystem.LoadSlot(slot);
            if (string.IsNullOrEmpty(scene) || !Application.CanStreamedLevelBeLoaded(scene)) { Begin(); return; }
            titleRequested = false;
            IsOpen = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            LoadingScreen.Load(scene);
        }

        void Begin()
        {
            SaveSystem.Delete(); // a fresh run replaces the one in this slot
            RunState.NewRun();
            SaveSystem.Save(SceneManager.GetActiveScene().name); // the slot is claimed right away
            Resume();
            // a new game opens with the story: the camera finds the knight while the narrator speaks
            if (PlayerProgress.SceneName.Contains("Floor1")) Cinema.PlayFloor1Opening(ShowFloorIntro);
            else ShowFloorIntro();
        }

        // ---------------- New Game / Load Game ----------------

        void Place(Component c, float x, float y)
        {
            var r = (RectTransform)c.transform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>Shows a menu screen; 'fromTitle' remembers where Back goes (rebuilding in place keeps it).</summary>
        void OpenScreen(GameObject screen, bool fromTitle)
        {
            if (fromTitle) backTarget = titlePanel;
            HideAll();
            screen.SetActive(true);
            SelectFirst(screen);
        }

        void ShowNewGame(bool fromTitle)
        {
            if (chosenSlot < 0)
            {
                chosenSlot = 0;
                for (int i = 0; i < SaveSystem.Slots; i++) if (!SaveSystem.HasSaveIn(i)) { chosenSlot = i; break; }
            }
            if (newGamePanel != null) Destroy(newGamePanel);
            newGamePanel = Panel("New Game", 0.35f);
            var card = Card(newGamePanel.transform, 1240f, 920f);
            Place(Label(card, "NEW GAME", 54, Gold, TextAnchor.MiddleCenter, 1200f, 80f), 0f, 395f);

            Place(Label(card, "DIFFICULTY", 26, Gold, TextAnchor.MiddleCenter, 1200f, 40f), 0f, 315f);
            for (int i = 0; i < Difficulty.Names.Length; i++)
            {
                int d = i;
                var b = MakeButton(card, d == chosenDifficulty ? "> " + Difficulty.Names[d] + " <" : Difficulty.Names[d],
                                   () => { chosenDifficulty = d; ShowNewGame(false); }, 216f);
                Place(b, -472f + i * 236f, 250f);
            }
            Place(Label(card, Difficulty.Blurbs[chosenDifficulty], 24, Ink, TextAnchor.MiddleCenter, 1150f, 40f), 0f, 180f);

            Place(Label(card, "SAVE SLOT", 26, Gold, TextAnchor.MiddleCenter, 1200f, 40f), 0f, 110f);
            for (int i = 0; i < SaveSystem.Slots; i++)
            {
                int slot = i;
                string info = SaveSystem.Describe(slot);
                string text = (slot == chosenSlot ? "> " : "") + "Slot " + (slot + 1) + ":  " + (info ?? "empty") +
                              (info != null && slot == chosenSlot ? "   (will be replaced)" : "") + (slot == chosenSlot ? " <" : "");
                var b = MakeButton(card, text, () => { chosenSlot = slot; ShowNewGame(false); }, 1100f);
                Place(b, 0f, 40f - i * 76f);
            }

            Place(MakeButton(card, "Begin  (" + Difficulty.Names[chosenDifficulty] + ", Slot " + (chosenSlot + 1) + ")", StartNewGame, 620f), 0f, -250f);
            Place(MakeButton(card, "Back", Back, 620f), 0f, -330f);
            OpenScreen(newGamePanel, fromTitle);
        }

        void StartNewGame()
        {
            RunState.Difficulty = chosenDifficulty;
            SaveSystem.ActiveSlot = chosenSlot;
            chosenSlot = -1;
            Begin();
        }

        void ShowLoad(bool fromTitle)
        {
            if (loadPanel != null) Destroy(loadPanel);
            loadPanel = Panel("Load Game", 0.35f);
            var card = Card(loadPanel.transform, 1240f, 720f);
            Place(Label(card, "LOAD GAME", 54, Gold, TextAnchor.MiddleCenter, 1200f, 80f), 0f, 295f);
            for (int i = 0; i < SaveSystem.Slots; i++)
            {
                int slot = i;
                float y = 170f - i * 110f;
                string info = SaveSystem.Describe(slot);
                var l = Label(card, "Slot " + (slot + 1) + "\n" + (info ?? "empty"), 24, info != null ? Ink : Dim, TextAnchor.MiddleLeft, 700f, 80f);
                Place(l, -230f, y);
                if (info == null) continue;
                Place(MakeButton(card, "Load", () => OpenSlot(slot), 170f), 280f, y);
                Button del = null;
                bool armed = false;
                del = MakeButton(card, "Delete", () =>
                {
                    if (!armed) { armed = true; del.GetComponentInChildren<Text>().text = "Sure?"; return; } // two clicks
                    SaveSystem.DeleteSlot(slot);
                    if (SaveSystem.HasSave) ShowLoad(false); else { RebuildTitle(); Back(); }
                }, 170f);
                Place(del, 470f, y);
            }
            Place(MakeButton(card, "Back", () => { RebuildTitle(); Back(); }, 620f), 0f, -260f);
            OpenScreen(loadPanel, fromTitle);
        }

        /// <summary>The title's buttons depend on the saves (Continue / Load Game), so it's rebuilt after changes.</summary>
        void RebuildTitle()
        {
            bool wasBack = backTarget == titlePanel;
            if (titlePanel != null) Destroy(titlePanel);
            BuildTitle();
            titlePanel.SetActive(false);
            if (wasBack) backTarget = titlePanel;
        }

        /// <summary>Pause menu: saves into this run's slot (Continue resumes at the start of this floor).</summary>
        void SaveNow()
        {
            SaveSystem.Save(SceneManager.GetActiveScene().name);
            PrototypeHUD.Message("Game saved to Slot " + (SaveSystem.ActiveSlot + 1), 2f);
            Resume();
        }

        void RestartFloor() => GameManager.RestartFloor();

        /// <summary>
        /// Erases everything the game remembers about playing: the skill tree and its points, the saved run
        /// and the run counter. Settings and key bindings are kept.
        /// </summary>
        public static void WipeAllProgress()
        {
            Skills.WipeAll();
            SaveSystem.DeleteAll();
            PlayerPrefs.DeleteKey("fv_runs");
            PlayerPrefs.Save();
            Debug.Log("[Fractured Vow] All progress erased.");
        }

        void QuitToTitle()
        {
            titleRequested = true;
            Time.timeScale = 1f;
            LoadingScreen.Load(SceneManager.GetActiveScene().name);
        }

        void ContinueToNext()
        {
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(nextScene)) LoadingScreen.Load(nextScene);
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---------------- settings ----------------

        void ApplySettings()
        {
            float b = GameSettings.Brightness;
            bool any = false;
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v.profile == null || !v.profile.TryGet(out ColorAdjustments ca)) continue;
                if (!baseExposure.ContainsKey(v)) baseExposure[v] = ca.postExposure.overrideState ? ca.postExposure.value : 0f;
                ca.postExposure.Override(baseExposure[v] + b * 1.5f);
                any = true;
            }
            float amb = any ? 1f + b * 0.5f : 1f + b * 1.2f;
            RenderSettings.ambientLight = baseAmbient * amb;
            RenderSettings.ambientSkyColor = baseSky * amb;
            RenderSettings.ambientEquatorColor = baseEquator * amb;
            RenderSettings.ambientGroundColor = baseGround * amb;

            foreach (var input in FindObjectsByType<PlayerInputHandler>(FindObjectsSortMode.None))
            {
                input.mouseSensitivity = GameSettings.MouseSensitivity;
                input.invertY = GameSettings.InvertY;
            }
        }

        // ---------------- UI construction ----------------

        void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        void BuildCanvas()
        {
            var go = new GameObject("Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasRoot = (RectTransform)go.transform;
        }

        GameObject Panel(string name, float backdropAlpha)
        {
            var p = UI(name, canvasRoot);
            Stretch(p);
            var img = p.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, backdropAlpha);
            return p.gameObject;
        }

        RectTransform Card(Transform parent, float width, float height)
        {
            var c = UI("Card", parent);
            c.anchorMin = c.anchorMax = new Vector2(0.5f, 0.5f);
            c.sizeDelta = new Vector2(width, height);
            var img = c.gameObject.AddComponent<Image>();
            img.color = PanelCol;
            var o = c.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
            o.effectDistance = new Vector2(2f, -2f);
            return c;
        }

        RectTransform Column(Transform parent, float yOffset, float spacing = 14f)
        {
            var col = UI("Column", parent);
            col.anchorMin = col.anchorMax = new Vector2(0.5f, 0.5f);
            col.pivot = new Vector2(0.5f, 1f);
            col.anchoredPosition = new Vector2(0f, yOffset);
            col.sizeDelta = new Vector2(760f, 0f);
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.childAlignment = TextAnchor.UpperCenter;
            v.childControlWidth = false; v.childControlHeight = false; v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            var f = col.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return col;
        }

        Text Label(Transform parent, string text, int size, Color color, TextAnchor align, float width = 760f, float height = 60f)
        {
            var t = UI("Text", parent);
            t.sizeDelta = new Vector2(width, height);
            var txt = t.gameObject.AddComponent<Text>();
            txt.font = font; txt.text = text; txt.fontSize = size; txt.color = color; txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap; txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        Button MakeButton(Transform parent, string label, Action onClick, float width = 440f)
        {
            var r = UI("Button " + label, parent);
            r.sizeDelta = new Vector2(width, 62f);
            var img = r.gameObject.AddComponent<Image>();
            img.color = Color.white;
            var b = r.gameObject.AddComponent<Button>();
            var cb = b.colors;
            cb.normalColor = ButtonCol;
            cb.highlightedColor = new Color(0.38f, 0.28f, 0.16f);
            cb.selectedColor = new Color(0.32f, 0.24f, 0.14f);
            cb.pressedColor = new Color(0.55f, 0.4f, 0.2f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            var o = r.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
            var txt = Label(r, label.ToUpperInvariant(), 26, Ink, TextAnchor.MiddleCenter, width, 62f);
            Stretch((RectTransform)txt.transform);
            b.onClick.AddListener(() => { GameAudio.PlayUI("ui_click"); onClick(); });
            var trig = r.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => GameAudio.PlayUI("ui_hover", 0.6f));
            trig.triggers.Add(enter);
            return b;
        }

        void MakeSlider(Transform parent, string label, float min, float max, float value, Action<float> onChange, Func<float, string> fmt)
        {
            var row = UI("Slider " + label, parent);
            row.sizeDelta = new Vector2(760f, 56f);
            var l = Label(row, label, 26, Ink, TextAnchor.MiddleLeft, 0f, 0f);
            Anchor((RectTransform)l.transform, 0f, 0f, 0.38f, 1f);
            var valueText = Label(row, fmt(value), 24, Gold, TextAnchor.MiddleRight, 0f, 0f);
            Anchor((RectTransform)valueText.transform, 0.86f, 0f, 1f, 1f);

            var s = UI("Slider", row);
            Anchor(s, 0.4f, 0.3f, 0.84f, 0.7f);
            var bg = UI("Background", s); Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = new Color(0.2f, 0.18f, 0.17f);
            var fillArea = UI("Fill Area", s); Stretch(fillArea);
            var fill = UI("Fill", fillArea);
            fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f); fill.sizeDelta = Vector2.zero;
            fill.gameObject.AddComponent<Image>().color = new Color(Gold.r * 0.8f, Gold.g * 0.7f, Gold.b * 0.5f);
            var handleArea = UI("Handle Area", s); Stretch(handleArea);
            var handle = UI("Handle", handleArea);
            handle.sizeDelta = new Vector2(20f, 18f);
            var hImg = handle.gameObject.AddComponent<Image>();
            hImg.color = Gold;

            var slider = s.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = hImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = min; slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(v => { onChange(v); valueText.text = fmt(v); });
        }

        void MakeToggle(Transform parent, string label, bool value, Action<bool> onChange)
        {
            var row = UI("Toggle " + label, parent);
            row.sizeDelta = new Vector2(760f, 52f);
            var l = Label(row, label, 26, Ink, TextAnchor.MiddleLeft, 0f, 0f);
            Anchor((RectTransform)l.transform, 0f, 0f, 0.7f, 1f);
            var box = UI("Box", row);
            box.anchorMin = box.anchorMax = new Vector2(1f, 0.5f);
            box.pivot = new Vector2(1f, 0.5f);
            box.sizeDelta = new Vector2(40f, 40f);
            var bg = box.gameObject.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.18f, 0.17f);
            var check = UI("Check", box);
            Stretch(check, 8f);
            var cImg = check.gameObject.AddComponent<Image>();
            cImg.color = Gold;
            var t = box.gameObject.AddComponent<Toggle>();
            t.targetGraphic = bg;
            t.graphic = cImg;
            t.isOn = value;
            t.onValueChanged.AddListener(v => { GameAudio.PlayUI("ui_click"); onChange(v); });
        }

        void BuildTitle()
        {
            titlePanel = Panel("Title", 0f);
            // a dark band down the left side keeps the menu readable over the art
            var band = UI("Band", titlePanel.transform);
            band.anchorMin = new Vector2(0f, 0f); band.anchorMax = new Vector2(0f, 1f); band.pivot = new Vector2(0f, 0.5f);
            band.sizeDelta = new Vector2(820f, 0f);
            band.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var t = Label(titlePanel.transform, "FRACTURED VOW", 104, Gold, TextAnchor.MiddleLeft, 1000f, 140f);
            var tr = (RectTransform)t.transform;
            tr.anchorMin = tr.anchorMax = new Vector2(0f, 1f); tr.pivot = new Vector2(0f, 1f);
            tr.anchoredPosition = new Vector2(100f, -110f);
            var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.9f); sh.effectDistance = new Vector2(4f, -4f);
            var sub = Label(titlePanel.transform, "A knight without a name. A dungeon without mercy.\nA vow to remember.", 28, Dim, TextAnchor.UpperLeft, 700f, 90f);
            var sr = (RectTransform)sub.transform;
            sr.anchorMin = sr.anchorMax = new Vector2(0f, 1f); sr.pivot = new Vector2(0f, 1f);
            sr.anchoredPosition = new Vector2(104f, -258f);

            var col = Column(titlePanel.transform, 0f, 12f);
            col.anchorMin = col.anchorMax = new Vector2(0f, 0.5f);
            col.pivot = new Vector2(0f, 1f);
            col.anchoredPosition = new Vector2(100f, 150f);
            col.sizeDelta = new Vector2(520f, 0f);
            col.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            int recent = SaveSystem.MostRecentSlot();
            if (recent >= 0) MakeButton(col, "Continue", ContinueRun, 520f);
            MakeButton(col, "New Game", () => ShowNewGame(true), 520f);
            if (SaveSystem.HasSave) MakeButton(col, "Load Game", () => ShowLoad(true), 520f);
            MakeButton(col, "Settings", () => ShowSub(settingsPanel), 520f);
            MakeButton(col, "Video", () => ShowSub(videoPanel), 520f);
            MakeButton(col, "Controls", () => ShowSub(controlsPanel), 520f);
            MakeButton(col, "Credits", () => RollCredits(false), 520f);
#if !UNITY_WEBGL
            MakeButton(col, "Quit", QuitGame, 520f); // (a browser tab can't be closed by the game)
#endif

            if (recent >= 0)
            {
                var last = Label(titlePanel.transform, "Continue:  " + SaveSystem.Describe(recent), 22, Dim, TextAnchor.MiddleLeft, 760f, 34f);
                var lr = (RectTransform)last.transform;
                lr.anchorMin = lr.anchorMax = new Vector2(0f, 0f); lr.pivot = new Vector2(0f, 0f);
                lr.anchoredPosition = new Vector2(104f, 110f);
            }
            // erasing everything is tucked away in a corner, and takes two clicks
            Button wipe = null;
            bool armed = false;
            wipe = MakeButton(titlePanel.transform, "Erase all save data", () =>
            {
                if (!armed) { armed = true; wipe.GetComponentInChildren<Text>().text = "Click again to erase"; return; }
                WipeAllProgress();
                QuitToTitle();
            }, 300f);
            var wr = (RectTransform)wipe.transform;
            wr.anchorMin = wr.anchorMax = new Vector2(0f, 0f); wr.pivot = new Vector2(0f, 0f);
            wr.anchoredPosition = new Vector2(100f, 30f);
            wr.localScale = Vector3.one * 0.8f;

            var foot = Label(titlePanel.transform, "v" + Application.version, 20, Dim, TextAnchor.LowerRight, 400f, 30f);
            var fr = (RectTransform)foot.transform;
            fr.anchorMin = fr.anchorMax = new Vector2(1f, 0f); fr.pivot = new Vector2(1f, 0f);
            fr.anchoredPosition = new Vector2(-30f, 24f);
        }

        // ---------------- video ----------------

        GameObject videoPanel;
        static readonly string[] ModeNames = { "Fullscreen (Borderless)", "Fullscreen (Exclusive)", "Maximized Window", "Windowed" };
        static readonly int[] FrameCaps = { 30, 60, 90, 120, 144, 165, 240, 0 };

        void BuildVideo()
        {
            bool wasOpen = videoPanel != null && videoPanel.activeSelf;
            if (videoPanel != null) Destroy(videoPanel);
            videoPanel = Panel("Video", 0.6f);
            var card = Card(videoPanel.transform, 900f, 760f);
            var h = Label(card, "VIDEO", 56, Gold, TextAnchor.MiddleCenter, 900f, 90f);
            ((RectTransform)h.transform).anchorMin = ((RectTransform)h.transform).anchorMax = new Vector2(0.5f, 0.92f);
            var col = Column(card, 270f, 12f);

#if UNITY_WEBGL && !UNITY_EDITOR
            // in a browser the page decides the size: only fullscreen on or off (it takes effect on the next click)
            MakeButton(col, "Fullscreen: " + (Screen.fullScreen ? "On" : "Off"),
                () => { GameSettings.DisplayMode = Screen.fullScreen ? 3 : 0; GameSettings.ApplyVideo(); BuildVideo(); }, 640f);
#else
            MakeButton(col, "Display: " + ModeNames[Mathf.Clamp(GameSettings.DisplayMode, 0, 3)],
                () => { GameSettings.DisplayMode = (GameSettings.DisplayMode + 1) % 4; GameSettings.ApplyVideo(); BuildVideo(); }, 640f);
            var res = GameSettings.Resolutions();
            int ri = GameSettings.ResolutionIndex(res);
            MakeButton(col, "Resolution: " + (res.Count > 0 ? res[ri].x + " x " + res[ri].y : Screen.width + " x " + Screen.height),
                () => { if (res.Count > 0) { var n = res[(ri + 1) % res.Count]; GameSettings.SetResolution(n.x, n.y); GameSettings.ApplyVideo(); } BuildVideo(); }, 640f);
            MakeButton(col, "VSync: " + (GameSettings.VSync ? "On" : "Off"),
                () => { GameSettings.VSync = !GameSettings.VSync; GameSettings.ApplyVideo(); BuildVideo(); }, 640f);
#endif
            int cap = GameSettings.FrameCap, ci = System.Array.IndexOf(FrameCaps, cap);
            MakeButton(col, "Frame limit: " + (GameSettings.VSync ? "VSync" : cap <= 0 ? "Unlimited" : cap + " FPS"),
                () => { GameSettings.FrameCap = FrameCaps[(ci + 1) % FrameCaps.Length]; GameSettings.ApplyVideo(); BuildVideo(); }, 640f);
            MakeButton(col, "Graphics: " + GameSettings.QualityNames[Mathf.Clamp(GameSettings.Quality, 0, 2)],
                () => { GameSettings.Quality = (GameSettings.Quality + 1) % 3; BuildVideo(); }, 640f);
            MakeToggle(col, "Show FPS counter", GameSettings.ShowFps, v => GameSettings.ShowFps = v);
            MakeButton(col, "Back", Back, 640f);
            videoPanel.SetActive(wasOpen);
            if (wasOpen) SelectFirst(videoPanel);
        }

        // ---------------- credits ----------------

        GameObject creditsPanel;
        bool creditsEnding;
        RectTransform creditsText;
        float creditsSpeed = 70f;

        /// <summary>Rolls the credits (from the title, or after the final door with a victory header).</summary>
        public void RollCredits(bool ending, string headline = null, string body = null)
        {
            if (creditsPanel != null) Destroy(creditsPanel);
            creditsEnding = ending;
            creditsPanel = Panel("Credits", 1f);
            var tex = Resources.Load<Texture2D>("FVUI/TitleBackground");
            if (tex != null)
            {
                var art = UI("Art", creditsPanel.transform).gameObject.AddComponent<RawImage>();
                art.texture = tex; art.color = new Color(0.35f, 0.35f, 0.38f);
                Stretch((RectTransform)art.transform);
                var fit = art.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.width / (float)tex.height;
            }
            var mask = UI("Mask", creditsPanel.transform);
            Stretch(mask);
            mask.gameObject.AddComponent<RectMask2D>();
            var txt = Label(mask, CreditsRichText(ending, headline, body), 30, Ink, TextAnchor.UpperCenter, 1400f, 6000f);
            txt.supportRichText = true;
            txt.lineSpacing = 1.2f;
            creditsText = (RectTransform)txt.transform;
            creditsText.anchorMin = creditsText.anchorMax = new Vector2(0.5f, 0f);
            creditsText.pivot = new Vector2(0.5f, 1f);
            creditsText.anchoredPosition = new Vector2(0f, -20f);
            var hint = Label(creditsPanel.transform, "Esc / Enter to skip", 20, Dim, TextAnchor.LowerRight, 400f, 30f);
            var hr = (RectTransform)hint.transform;
            hr.anchorMin = hr.anchorMax = new Vector2(1f, 0f); hr.pivot = new Vector2(1f, 0f);
            hr.anchoredPosition = new Vector2(-30f, 24f);
            HideAll();
            creditsPanel.SetActive(true);
            IsOpen = true;
            Time.timeScale = 0f;
            PlayerInputHandler.SetCursorLocked(false);
            GameAudio.PlayMusic(ending ? "music_dungeon" : "music_dungeon", 1.5f);
        }

        static string CreditsRichText(bool ending, string headline, string body)
        {
            var sb = new System.Text.StringBuilder();
            if (ending)
            {
                sb.Append("<size=72><color=#f2c66b>").Append(string.IsNullOrEmpty(headline) ? "THE VOW IS KEPT" : headline).Append("</color></size>\n\n");
                if (!string.IsNullOrEmpty(body)) sb.Append("<i>").Append(body).Append("</i>\n");
                sb.Append("\n\n\n\n");
            }
            var file = Resources.Load<TextAsset>("FVUI/Credits");
            string src = file != null ? file.text : "# FRACTURED VOW\n\n## MADE WITH UNITY\n\n# THANK YOU FOR PLAYING";
            foreach (var raw in src.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.StartsWith("## ")) sb.Append("\n<size=26><color=#f2c66b>").Append(line.Substring(3)).Append("</color></size>\n");
                else if (line.StartsWith("# ")) sb.Append("\n\n<size=64><color=#f2c66b>").Append(line.Substring(2)).Append("</color></size>\n\n");
                else sb.Append(line).Append("\n");
            }
            return sb.ToString();
        }

        void UpdateCredits()
        {
            if (creditsPanel == null || !creditsPanel.activeSelf) return;
            bool fast = EnterOrClick();
            creditsText.anchoredPosition += Vector2.up * creditsSpeed * (fast ? 6f : 1f) * Time.unscaledDeltaTime;
            float h = ((Text)creditsText.GetComponent<Text>()).preferredHeight;
            bool done = creditsText.anchoredPosition.y > h + canvasRoot.rect.height; // the last line has left the top
            if (done || EscapePressed()) EndCredits();
        }

        void EndCredits()
        {
            creditsPanel.SetActive(false);
            UIFocus.LastClosedFrame = Time.frameCount;
            if (creditsEnding) { QuitToTitle(); return; }
            titlePanel.SetActive(true);
            SelectFirst(titlePanel);
        }

        static bool EnterOrClick()
        {
#if ENABLE_INPUT_SYSTEM
            return (Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.spaceKey.isPressed)) ||
                   (Mouse.current != null && Mouse.current.leftButton.isPressed);
#else
            return Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
#endif
        }

        void BuildPause()
        {
            pausePanel = Panel("Pause", 0.55f);
            var card = Card(pausePanel.transform, 640f, 940f);
            var h = Label(card, "PAUSED", 56, Gold, TextAnchor.MiddleCenter, 640f, 90f);
            ((RectTransform)h.transform).anchorMin = ((RectTransform)h.transform).anchorMax = new Vector2(0.5f, 0.9f);
            var col = Column(card, 340f, 10f);
            MakeButton(col, "Resume", Resume, 500f);
            MakeButton(col, "Save Game", SaveNow, 500f);
            MakeButton(col, "Settings", () => ShowSub(settingsPanel), 500f);
            MakeButton(col, "Video", () => ShowSub(videoPanel), 500f);
            MakeButton(col, "Controls", () => ShowSub(controlsPanel), 500f);
            MakeButton(col, "Unstuck", () => { Resume(); SafeGround.Unstuck(); }, 500f);
            MakeButton(col, "Restart Floor (keep character)", RestartFloor, 500f);
            MakeButton(col, "Start New Run (fresh character)", GameManager.RestartRun, 500f);
            MakeButton(col, "Quit to Title", QuitToTitle, 500f);
#if !UNITY_WEBGL
            MakeButton(col, "Quit Game", QuitGame, 500f);
#endif
        }

        void BuildSettings()
        {
            settingsPanel = Panel("Settings", 0.6f);
            var card = Card(settingsPanel.transform, 900f, 900f);
            var h = Label(card, "SETTINGS", 56, Gold, TextAnchor.MiddleCenter, 900f, 90f);
            ((RectTransform)h.transform).anchorMin = ((RectTransform)h.transform).anchorMax = new Vector2(0.5f, 0.95f);
            var col = Column(card, 340f, 8f);
            MakeSlider(col, "Brightness", -1f, 1f, GameSettings.Brightness, v => GameSettings.Brightness = v, v => (v >= 0 ? "+" : "") + Mathf.RoundToInt(v * 100f) + "%");
            MakeSlider(col, "Master volume", 0f, 1f, GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, Pct);
            MakeSlider(col, "Music volume", 0f, 1f, GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, Pct);
            MakeSlider(col, "Effects volume", 0f, 1f, GameSettings.SfxVolume, v => GameSettings.SfxVolume = v, Pct);
            MakeSlider(col, "Mouse sensitivity", 0.2f, 5f, GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => v.ToString("0.0"));
            MakeToggle(col, "Invert camera Y", GameSettings.InvertY, v => GameSettings.InvertY = v);
            MakeToggle(col, "Camera shake", GameSettings.CameraShake, v => GameSettings.CameraShake = v);
            MakeToggle(col, "Show FPS counter", GameSettings.ShowFps, v => GameSettings.ShowFps = v);
            MakeButton(col, "Graphics: " + GameSettings.QualityNames[Mathf.Clamp(GameSettings.Quality, 0, 2)],
                () => { GameSettings.Quality = (GameSettings.Quality + 1) % 3; RebuildSettings(); }, 520f);
            var row = UI("Buttons", card);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 0.08f);
            row.sizeDelta = new Vector2(760f, 62f);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 20f; hl.childAlignment = TextAnchor.MiddleCenter; hl.childControlWidth = false; hl.childControlHeight = false; hl.childForceExpandWidth = false;
            MakeButton(row, "Defaults", () => { GameSettings.ResetToDefaults(); RebuildSettings(); }, 300f);
            MakeButton(row, "Back", Back, 300f);
        }

        void RebuildSettings()
        {
            bool wasActive = settingsPanel.activeSelf;
            Destroy(settingsPanel);
            BuildSettings();
            settingsPanel.SetActive(wasActive);
            if (wasActive) SelectFirst(settingsPanel);
        }

        GameAction? rebinding;
        RectTransform controlsCard;

        void BuildControls()
        {
            if (controlsPanel == null) controlsPanel = Panel("Controls", 0.6f);
            if (controlsCard != null) Destroy(controlsCard.gameObject);
            controlsCard = Card(controlsPanel.transform, 1560f, 940f);
            var card = controlsCard;
            var h = Label(card, "CONTROLS", 52, Gold, TextAnchor.MiddleCenter, 1560f, 80f);
            ((RectTransform)h.transform).anchorMin = ((RectTransform)h.transform).anchorMax = new Vector2(0.5f, 0.94f);
            var sub = Label(card, rebinding.HasValue ? "Press a key for \"" + InputBindings.Labels[(int)rebinding.Value] + "\"  (Esc to cancel)" : "Click a key to change it.",
                22, rebinding.HasValue ? new Color(0.55f, 1f, 0.6f) : Dim, TextAnchor.MiddleCenter, 1400f, 34f);
            ((RectTransform)sub.transform).anchorMin = ((RectTransform)sub.transform).anchorMax = new Vector2(0.5f, 0.885f);

            var actions = (GameAction[])System.Enum.GetValues(typeof(GameAction));
            for (int i = 0; i < actions.Length; i++)
            {
                var act = actions[i];
                float y = -140f - i * 38f;
                var l = UIKit.Label(card, InputBindings.Labels[i], 22, Ink, TextAnchor.MiddleLeft);
                UIKit.Place((RectTransform)l.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, y), new Vector2(360f, 36f));
                bool waiting = rebinding.HasValue && rebinding.Value == act;
                var b = UIKit.Button(card, waiting ? "press a key..." : InputBindings.Name(act), () => { rebinding = act; BuildControls(); controlsPanel.SetActive(true); }, 20);
                UIKit.Place((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(430f, y + 2f), new Vector2(260f, 36f));
            }

            string[,] fixedRows =
            {
                { "Look / zoom", "Mouse / scroll wheel", "Right stick" },
                { "Sword (tap)", "Left click", "X / Square" },
                { "Heavy attack (hold)", "Hold left click", "Hold X / Square" },
                { "Block / parry", "Right click", "Left trigger" },
                { "Lock on", "Middle mouse (also)", "Right stick click" },
                { "Dodge", "", "B / Circle" },
                { "Jump", "", "A / Cross" },
                { "Health / mana flask", "", "D-pad up / down" },
                { "Cheat console", "`  or  F1", "-" },
                { "Pause menu", "Esc", "Start" },
            };
            var t = UIKit.Label(card, "FIXED CONTROLS", 22, Gold, TextAnchor.MiddleLeft);
            UIKit.Place((RectTransform)t.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, -140f), new Vector2(700f, 36f));
            for (int i = 0; i < fixedRows.GetLength(0); i++)
            {
                float y = -186f - i * 42f;
                var a = UIKit.Label(card, fixedRows[i, 0], 20, Ink, TextAnchor.MiddleLeft);
                UIKit.Place((RectTransform)a.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, y), new Vector2(280f, 36f));
                var k = UIKit.Label(card, fixedRows[i, 1], 20, Gold, TextAnchor.MiddleLeft);
                UIKit.Place((RectTransform)k.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1080f, y), new Vector2(220f, 36f));
                var g = UIKit.Label(card, fixedRows[i, 2], 18, Dim, TextAnchor.MiddleLeft);
                UIKit.Place((RectTransform)g.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1300f, y), new Vector2(240f, 36f));
            }
            var reset = MakeButton(card, "Reset controls", () => { InputBindings.ResetAll(); rebinding = null; BuildControls(); controlsPanel.SetActive(true); }, 300f);
            ((RectTransform)reset.transform).anchorMin = ((RectTransform)reset.transform).anchorMax = new Vector2(0.62f, 0.07f);
            var back = MakeButton(card, "Back", () => { rebinding = null; Back(); }, 300f);
            ((RectTransform)back.transform).anchorMin = ((RectTransform)back.transform).anchorMax = new Vector2(0.84f, 0.07f);
        }

        /// <summary>While waiting for a key to rebind: grab the next key press (Esc cancels).</summary>
        bool PollRebind()
        {
            if (!rebinding.HasValue || controlsPanel == null || !controlsPanel.activeSelf) return false;
#if ENABLE_INPUT_SYSTEM
            var key = InputBindings.AnyKeyPressed();
            if (key == UnityEngine.InputSystem.Key.None) return true;
            if (key != UnityEngine.InputSystem.Key.Escape) InputBindings.Set(rebinding.Value, key);
#else
            var key = InputBindings.AnyKeyPressed();
            if (key == KeyCode.None) return true;
            if (key != KeyCode.Escape) InputBindings.Set(rebinding.Value, key);
#endif
            rebinding = null;
            GameAudio.PlayUI("ui_click");
            BuildControls();
            controlsPanel.SetActive(true);
            return true;
        }

        RectTransform Row(Transform parent, string a, string b, string c, Color color)
        {
            var row = UI("Row", parent);
            row.sizeDelta = new Vector2(980f, 40f);
            var t1 = Label(row, a, 25, color, TextAnchor.MiddleLeft, 0f, 0f); Anchor((RectTransform)t1.transform, 0f, 0f, 0.36f, 1f);
            var t2 = Label(row, b, 25, color, TextAnchor.MiddleLeft, 0f, 0f); Anchor((RectTransform)t2.transform, 0.37f, 0f, 0.72f, 1f);
            var t3 = Label(row, c, 25, color == Gold ? Gold : Dim, TextAnchor.MiddleLeft, 0f, 0f); Anchor((RectTransform)t3.transform, 0.73f, 0f, 1f, 1f);
            return row;
        }

        void BuildCleared()
        {
            clearedPanel = Panel("Floor Cleared", 0.7f);
            var card = Card(clearedPanel.transform, 1000f, 560f);
            clearedTitle = Label(card, "FLOOR CLEARED", 60, Gold, TextAnchor.MiddleCenter, 1000f, 90f);
            ((RectTransform)clearedTitle.transform).anchorMin = ((RectTransform)clearedTitle.transform).anchorMax = new Vector2(0.5f, 0.84f);
            clearedBody = Label(card, "", 28, Ink, TextAnchor.UpperCenter, 860f, 160f);
            ((RectTransform)clearedBody.transform).anchorMin = ((RectTransform)clearedBody.transform).anchorMax = new Vector2(0.5f, 0.6f);
            var col = Column(card, -40f, 12f);
            continueButton = MakeButton(col, "Continue", ContinueToNext);
            MakeButton(col, "Play Floor Again", RestartFloor);
            MakeButton(col, "Quit to Title", QuitToTitle);
        }

        void BuildDeath()
        {
            deathPanel = Panel("Death", 0.75f);
            var t = Label(deathPanel.transform, "YOU FELL", 120, new Color(0.75f, 0.12f, 0.1f), TextAnchor.MiddleCenter, 1400f, 160f);
            ((RectTransform)t.transform).anchorMin = ((RectTransform)t.transform).anchorMax = new Vector2(0.5f, 0.68f);
            var sh = t.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.9f); sh.effectDistance = new Vector2(4f, -4f);
            deathSub = Label(deathPanel.transform, "", 30, Dim, TextAnchor.MiddleCenter, 1400f, 80f);
            ((RectTransform)deathSub.transform).anchorMin = ((RectTransform)deathSub.transform).anchorMax = new Vector2(0.5f, 0.56f);
            var col = Column(deathPanel.transform, -20f, 12f);
            MakeButton(col, "Respawn at Checkpoint", RespawnNow, 520f);
            MakeButton(col, "Restart Run (from Floor 1)", GameManager.RestartRun, 520f);
            MakeButton(col, "Quit to Title", QuitToTitle, 520f);
            var hint = Label(deathPanel.transform, "Respawning returns you and the dungeon to your last checkpoint. Restarting the run starts a brand-new character from Floor 1.", 22, Dim, TextAnchor.MiddleCenter, 1400f, 40f);
            ((RectTransform)hint.transform).anchorMin = ((RectTransform)hint.transform).anchorMax = new Vector2(0.5f, 0.12f);
        }

        // ---------------- rect helpers ----------------

        static RectTransform UI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        static void Anchor(RectTransform r, float x0, float y0, float x1, float y1)
        {
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";
    }
}
