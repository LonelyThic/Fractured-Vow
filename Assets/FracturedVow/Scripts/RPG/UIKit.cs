using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace FracturedVow
{
    /// <summary>True while any menu or the console has the keyboard, so gameplay input stops.</summary>
    public static class UIFocus
    {
        public static bool Any => GameMenu.IsOpen || CharacterMenu.IsOpen || DevConsole.IsOpen || ShopUI.IsOpen || LoreNote.Reading || CheatMenu.IsOpen;
        /// <summary>Frame a menu or the console was last closed, so the same Esc press doesn't open the pause menu.</summary>
        public static int LastClosedFrame = -10;
    }

    /// <summary>Shared look and builders for the code-built uGUI screens (HUD, character menu, console).</summary>
    public static class UIKit
    {
        public static readonly Color Gold = new Color(0.95f, 0.78f, 0.42f);
        public static readonly Color Ink = new Color(0.93f, 0.9f, 0.85f);
        public static readonly Color Dim = new Color(0.62f, 0.6f, 0.58f);
        public static readonly Color Panel = new Color(0.05f, 0.045f, 0.05f, 0.9f);
        public static readonly Color ButtonColor = new Color(0.16f, 0.13f, 0.12f, 1f);

        static Font font;
        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        public static RectTransform Canvas(string name, int order, Transform parent)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920f, 1080f);
            s.matchWidthOrHeight = 0.5f;
            return (RectTransform)go.transform;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Places a rect by anchor point, pivot, position and size.</summary>
        public static RectTransform Place(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor; r.pivot = pivot; r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        public static void Stretch(RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset); r.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Box(Transform parent, string name, Color color, bool outline = false)
        {
            var r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.color = color;
            if (outline)
            {
                var o = r.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.55f);
                o.effectDistance = new Vector2(2f, -2f);
            }
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align)
        {
            var r = Rect("Text", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var sh = r.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.8f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }

        public static UnityEngine.UI.Button Button(Transform parent, string label, Action onClick, int fontSize = 22)
        {
            var r = Rect("Button " + label, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.color = Color.white;
            var b = r.gameObject.AddComponent<UnityEngine.UI.Button>();
            var cb = b.colors;
            cb.normalColor = ButtonColor; cb.highlightedColor = new Color(0.38f, 0.28f, 0.16f);
            cb.selectedColor = new Color(0.32f, 0.24f, 0.14f); cb.pressedColor = new Color(0.55f, 0.4f, 0.2f);
            cb.disabledColor = new Color(0.12f, 0.11f, 0.11f, 0.8f);
            b.colors = cb;
            var o = r.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(Gold.r, Gold.g, Gold.b, 0.35f);
            var t = Label(r, label, fontSize, Ink, TextAnchor.MiddleCenter);
            Stretch((RectTransform)t.transform, 4f);
            b.onClick.AddListener(() => { GameAudio.PlayUI("ui_click"); onClick(); });
            return b;
        }

        /// <summary>A bar: dark back, a delayed "ghost" fill and the real fill. Set its value with SetBar.</summary>
        public static RectTransform Bar(Transform parent, string name, Color fill, out RectTransform fillRect, out RectTransform ghostRect, out Text label)
        {
            var back = Box(parent, name, new Color(0f, 0f, 0f, 0.7f)).rectTransform;
            var o = back.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.9f);
            var ghost = Box(back, "Ghost", new Color(1f, 0.85f, 0.55f, 0.55f)).rectTransform;
            ghost.anchorMin = Vector2.zero; ghost.anchorMax = new Vector2(1f, 1f); ghost.pivot = new Vector2(0f, 0.5f);
            ghost.offsetMin = new Vector2(2f, 2f); ghost.offsetMax = new Vector2(-2f, -2f);
            var f = Box(back, "Fill", fill).rectTransform;
            f.anchorMin = Vector2.zero; f.anchorMax = new Vector2(1f, 1f); f.pivot = new Vector2(0f, 0.5f);
            f.offsetMin = new Vector2(2f, 2f); f.offsetMax = new Vector2(-2f, -2f);
            label = Label(back, "", 16, Ink, TextAnchor.MiddleCenter);
            Stretch((RectTransform)label.transform);
            fillRect = f; ghostRect = ghost;
            return back;
        }

        /// <summary>Puts a picture of the item's 3D model into a swatch box (keeps the color swatch if it has no model).</summary>
        public static void AddIcon(RectTransform swatch, ItemDef d)
        {
            var tex = ItemIcons.Get(d);
            if (tex == null) return;
            var bg = swatch.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.13f, 0.12f, 0.11f, 1f);
            var r = Rect("Icon", swatch);
            Stretch(r, 2f);
            var img = r.gameObject.AddComponent<RawImage>();
            img.texture = tex;
            img.raycastTarget = false;
        }

        public static void SetFill(RectTransform fill, float t)
        {
            t = Mathf.Clamp01(t);
            fill.anchorMax = new Vector2(t, 1f);
        }

        public static bool KeyDown(string key)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            switch (key)
            {
                case "tab": return kb.tabKey.wasPressedThisFrame;
                case "escape": return kb.escapeKey.wasPressedThisFrame;
                case "console": return kb.backquoteKey.wasPressedThisFrame || kb.f1Key.wasPressedThisFrame;
                case "enter": return kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;
                case "up": return kb.upArrowKey.wasPressedThisFrame;
                default: return false;
            }
#else
            switch (key)
            {
                case "tab": return Input.GetKeyDown(KeyCode.Tab);
                case "escape": return Input.GetKeyDown(KeyCode.Escape);
                case "console": return Input.GetKeyDown(KeyCode.BackQuote) || Input.GetKeyDown(KeyCode.F1);
                case "enter": return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                case "up": return Input.GetKeyDown(KeyCode.UpArrow);
                default: return false;
            }
#endif
        }
    }
}
