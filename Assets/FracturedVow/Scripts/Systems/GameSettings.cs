using System;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>Player settings saved between sessions (PlayerPrefs). Change any value and Changed fires.</summary>
    public static class GameSettings
    {
        public static event Action Changed;

        public static float Brightness { get => PlayerPrefs.GetFloat("fv_brightness", 0f); set => Set("fv_brightness", Mathf.Clamp(value, -1f, 1f)); }
        public static float MasterVolume { get => PlayerPrefs.GetFloat("fv_master", 1f); set => Set("fv_master", Mathf.Clamp01(value)); }
        public static float MusicVolume { get => PlayerPrefs.GetFloat("fv_music", 0.6f); set => Set("fv_music", Mathf.Clamp01(value)); }
        public static float SfxVolume { get => PlayerPrefs.GetFloat("fv_sfx", 0.9f); set => Set("fv_sfx", Mathf.Clamp01(value)); }
        public static float MouseSensitivity { get => PlayerPrefs.GetFloat("fv_sens", 1.5f); set => Set("fv_sens", Mathf.Clamp(value, 0.2f, 5f)); }
        public static bool InvertY { get => PlayerPrefs.GetInt("fv_invert", 0) == 1; set => Set("fv_invert", value ? 1 : 0); }
        public static bool CameraShake { get => PlayerPrefs.GetInt("fv_shake", 1) == 1; set => Set("fv_shake", value ? 1 : 0); }

        public static bool ShowFps { get => PlayerPrefs.GetInt("fv_fps", 0) == 1; set => Set("fv_fps", value ? 1 : 0); }

        // ---------------- video ----------------

        /// <summary>0 Fullscreen (borderless), 1 Fullscreen (exclusive), 2 Maximized window, 3 Windowed.</summary>
        public static int DisplayMode { get => PlayerPrefs.GetInt("fv_display", 0); set => Set("fv_display", Mathf.Clamp(value, 0, 3)); }
        public static bool VSync { get => PlayerPrefs.GetInt("fv_vsync", 1) == 1; set => Set("fv_vsync", value ? 1 : 0); }
        /// <summary>Frames per second when VSync is off (0 = unlimited).</summary>
        public static int FrameCap { get => PlayerPrefs.GetInt("fv_fpscap", 60); set => Set("fv_fpscap", Mathf.Max(0, value)); }

        public static void SetResolution(int w, int h) { PlayerPrefs.SetInt("fv_resw", w); PlayerPrefs.SetInt("fv_resh", h); PlayerPrefs.Save(); }

        /// <summary>The monitor's resolutions, largest last, without refresh-rate duplicates.</summary>
        public static System.Collections.Generic.List<Vector2Int> Resolutions()
        {
            var list = new System.Collections.Generic.List<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                var v = new Vector2Int(r.width, r.height);
                if (v.x >= 1024 && !list.Contains(v)) list.Add(v);
            }
            list.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
            return list;
        }

        public static int ResolutionIndex(System.Collections.Generic.List<Vector2Int> list)
        {
            if (list.Count == 0) return 0;
            int w = PlayerPrefs.GetInt("fv_resw", 0), h = PlayerPrefs.GetInt("fv_resh", 0);
            if (w == 0) { w = Screen.currentResolution.width; h = Screen.currentResolution.height; }
            int best = list.Count - 1;
            for (int i = 0; i < list.Count; i++) if (list[i].x == w && list[i].y == h) best = i;
            return best;
        }

        /// <summary>Puts the display mode, resolution, VSync and frame limit into effect.</summary>
        public static void ApplyVideo()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Screen.fullScreen = DisplayMode != 3; // browsers allow this only after a click; Unity applies it then
            ApplyFrameRate();
            return;
#endif
            var mode = DisplayMode == 1 ? FullScreenMode.ExclusiveFullScreen : DisplayMode == 2 ? FullScreenMode.MaximizedWindow
                     : DisplayMode == 3 ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
            int w = PlayerPrefs.GetInt("fv_resw", 0), h = PlayerPrefs.GetInt("fv_resh", 0);
            if (w <= 0 || h <= 0) { w = Screen.currentResolution.width; h = Screen.currentResolution.height; }
            if (mode == FullScreenMode.Windowed) { w = Mathf.Min(w, Screen.currentResolution.width - 80); h = Mathf.Min(h, Screen.currentResolution.height - 120); }
            if (!Application.isEditor) Screen.SetResolution(w, h, mode);
            ApplyFrameRate();
        }

        /// <summary>VSync and the frame limit only (safe to call often).</summary>
        public static void ApplyFrameRate()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : (FrameCap <= 0 ? -1 : FrameCap);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyOnLaunch()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            ApplyFrameRate(); // a web page starts in its frame; the player chooses fullscreen
#else
            ApplyVideo();
#endif
        }

        /// <summary>0 Low, 1 Medium, 2 High. First launch picks Low on weak PCs, Medium otherwise.</summary>
        public static int Quality
        {
            get => PlayerPrefs.GetInt("fv_quality", SystemInfo.graphicsMemorySize < 3000 || SystemInfo.processorCount <= 4 ? 0 : 1);
            set => Set("fv_quality", Mathf.Clamp(value, 0, 2));
        }
        public static readonly string[] QualityNames = { "Low (fastest)", "Medium", "High" };

        public static void ResetToDefaults()
        {
            foreach (var k in new[] { "fv_brightness", "fv_master", "fv_music", "fv_sfx", "fv_sens", "fv_invert", "fv_shake", "fv_fps", "fv_quality" }) PlayerPrefs.DeleteKey(k);
            // display settings are kept (resetting them could leave the window the wrong size)
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        static void Set(string key, float v) { PlayerPrefs.SetFloat(key, v); PlayerPrefs.Save(); Changed?.Invoke(); }
        static void Set(string key, int v) { PlayerPrefs.SetInt(key, v); PlayerPrefs.Save(); Changed?.Invoke(); }
    }
}
