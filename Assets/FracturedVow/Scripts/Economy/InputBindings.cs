using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace FracturedVow
{
    public enum GameAction
    {
        MoveForward, MoveBack, MoveLeft, MoveRight, Sprint, Jump, Dodge, Interact, Cast, NextSpell,
        Spell1, Spell2, Spell3, HealthFlask, ManaFlask, LockOn, Character, Map
    }

    /// <summary>
    /// Rebindable keyboard controls, saved on this computer. Change them in Esc > Controls.
    /// (Mouse buttons and the gamepad keep their fixed layout.)
    /// </summary>
    public static class InputBindings
    {
        public static readonly string[] Labels =
        {
            "Move forward", "Move back", "Move left", "Move right", "Sprint", "Jump", "Dodge roll", "Interact / board boat",
            "Cast spell", "Next spell", "Spell slot 1", "Spell slot 2", "Spell slot 3", "Health flask", "Mana flask", "Lock on", "Character menu", "Map"
        };

        public static event Action Changed;

#if ENABLE_INPUT_SYSTEM
        static readonly Key[] Defaults =
        {
            Key.W, Key.S, Key.A, Key.D, Key.LeftShift, Key.Space, Key.LeftCtrl, Key.F,
            Key.E, Key.Q, Key.Digit1, Key.Digit2, Key.Digit3, Key.R, Key.V, Key.T, Key.Tab, Key.M
        };

        public static Key Get(GameAction a) => (Key)PlayerPrefs.GetInt("fv_key_" + a, (int)Defaults[(int)a]);

        public static void Set(GameAction a, Key k)
        {
            // a key can only do one thing: whatever used it before gets this action's old key
            Key old = Get(a);
            foreach (GameAction other in Enum.GetValues(typeof(GameAction)))
                if (other != a && Get(other) == k) PlayerPrefs.SetInt("fv_key_" + other, (int)old);
            PlayerPrefs.SetInt("fv_key_" + a, (int)k);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void ResetAll()
        {
            foreach (GameAction a in Enum.GetValues(typeof(GameAction))) PlayerPrefs.DeleteKey("fv_key_" + a);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static KeyControl Control(GameAction a)
        {
            var kb = Keyboard.current;
            if (kb == null) return null;
            Key k = Get(a);
            return k == Key.None ? null : kb[k];
        }

        public static bool Pressed(GameAction a) { var c = Control(a); return c != null && c.wasPressedThisFrame; }
        public static bool Held(GameAction a) { var c = Control(a); return c != null && c.isPressed; }
        public static string Name(GameAction a) => KeyName(Get(a));

        public static string KeyName(Key k)
        {
            switch (k)
            {
                case Key.LeftShift: return "Left Shift";
                case Key.RightShift: return "Right Shift";
                case Key.LeftCtrl: return "Left Ctrl";
                case Key.RightCtrl: return "Right Ctrl";
                case Key.LeftAlt: return "Left Alt";
                case Key.Space: return "Space";
                default:
                    string s = k.ToString();
                    return s.StartsWith("Digit") ? s.Substring(5) : s;
            }
        }

        /// <summary>The key pressed this frame (for rebinding), or Key.None.</summary>
        public static Key AnyKeyPressed()
        {
            var kb = Keyboard.current;
            if (kb == null) return Key.None;
            foreach (var key in kb.allKeys)
                if (key != null && key.wasPressedThisFrame) return key.keyCode;
            return Key.None;
        }
#else
        static readonly KeyCode[] Defaults =
        {
            KeyCode.W, KeyCode.S, KeyCode.A, KeyCode.D, KeyCode.LeftShift, KeyCode.Space, KeyCode.LeftControl, KeyCode.F,
            KeyCode.E, KeyCode.Q, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.R, KeyCode.V, KeyCode.T, KeyCode.Tab, KeyCode.M
        };

        public static KeyCode Get(GameAction a) => (KeyCode)PlayerPrefs.GetInt("fv_keyl_" + a, (int)Defaults[(int)a]);
        public static void Set(GameAction a, KeyCode k)
        {
            KeyCode old = Get(a);
            foreach (GameAction other in Enum.GetValues(typeof(GameAction)))
                if (other != a && Get(other) == k) PlayerPrefs.SetInt("fv_keyl_" + other, (int)old);
            PlayerPrefs.SetInt("fv_keyl_" + a, (int)k); PlayerPrefs.Save(); Changed?.Invoke();
        }
        public static void ResetAll()
        {
            foreach (GameAction a in Enum.GetValues(typeof(GameAction))) PlayerPrefs.DeleteKey("fv_keyl_" + a);
            PlayerPrefs.Save(); Changed?.Invoke();
        }
        public static bool Pressed(GameAction a) => Input.GetKeyDown(Get(a));
        public static bool Held(GameAction a) => Input.GetKey(Get(a));
        public static string Name(GameAction a) => Get(a).ToString();
        public static KeyCode AnyKeyPressed()
        {
            foreach (KeyCode k in Enum.GetValues(typeof(KeyCode)))
                if (k < KeyCode.Mouse0 && Input.GetKeyDown(k)) return k;
            return KeyCode.None;
        }
#endif
    }
}
