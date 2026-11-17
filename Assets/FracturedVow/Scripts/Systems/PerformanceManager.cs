using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FracturedVow
{
    /// <summary>
    /// Keeps the game smooth on weak PCs. Graphics quality (Esc > Settings) is Low, Medium or High:
    ///  - only the nearest torches and lamps are lit (the rest switch off out of sight)
    ///  - fire, embers, dust and fog far from the camera pause
    ///  - the camera stops drawing past what the fog already hides
    ///  - Low also renders at 80% resolution, turns off shadows, bloom and film grain, and caps at 60 FPS
    /// Settings changed on the render pipeline are put back when the game stops, so the project isn't altered.
    /// </summary>
    public class PerformanceManager : MonoBehaviour
    {
        static PerformanceManager inst;

        //                          Low    Medium  High
        static readonly int[] LightBudget = { 6, 12, 32 };
        static readonly float[] LightRange = { 20f, 32f, 55f };
        static readonly float[] EffectRange = { 18f, 32f, 60f };
        static readonly float[] DrawDistance = { 75f, 120f, 300f };
        static readonly float[] SoundRange = { 20f, 30f, 45f };

        UniversalRenderPipelineAsset urp;
        float origScale, origShadow;
        int origMsaa, origLights;
        bool origHdr, saved;

        readonly List<Light> lights = new List<Light>();
        readonly List<GameObject> effects = new List<GameObject>();
        readonly List<AudioSource> loops = new List<AudioSource>();
        readonly List<(Light light, float dist)> sortBuf = new List<(Light, float)>();
        Camera cam;
        float nextTick;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (inst == null)
            {
                var go = new GameObject("PerformanceManager");
                DontDestroyOnLoad(go);
                inst = go.AddComponent<PerformanceManager>();
                SceneManager.sceneLoaded += (s, m) => { if (inst != null) inst.Scan(); };
            }
            inst.Scan();
        }

        void Awake()
        {
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null)
            {
                origScale = urp.renderScale; origShadow = urp.shadowDistance; origMsaa = urp.msaaSampleCount;
                origLights = urp.maxAdditionalLightsCount; origHdr = urp.supportsHDR; saved = true;
            }
            GameSettings.Changed += Apply;
        }

        void OnDestroy()
        {
            GameSettings.Changed -= Apply;
            Restore();
        }

        void OnApplicationQuit() => Restore();

        void Restore()
        {
            if (!saved || urp == null) return;
            urp.renderScale = origScale; urp.shadowDistance = origShadow; urp.msaaSampleCount = origMsaa;
            urp.maxAdditionalLightsCount = origLights; urp.supportsHDR = origHdr;
        }

        /// <summary>Collects the level's lights and looping effects once per scene.</summary>
        void Scan()
        {
            ScanNow();
            // elites' glows and Hard/Nightmare's extra husks appear a moment after the level loads
            CancelInvoke(nameof(ScanNow));
            Invoke(nameof(ScanNow), 1.5f);
        }

        void ScanNow()
        {
            lights.Clear();
            effects.Clear();
            loops.Clear();
            // looping sounds on scenery (torch crackle, water, chains): silent and paused when far away
            foreach (var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (a.loop && a.spatialBlend > 0.5f && a.gameObject.scene.name != "DontDestroyOnLoad" &&      // never music or 2D ambience
                    a.GetComponentInParent<PlayerController>() == null && a.GetComponentInParent<Health>() == null) loops.Add(a);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.type != LightType.Point && l.type != LightType.Spot) continue;
                // torch and lamp shadows overflow URP's shadow atlas (the "shadow maps fit" warning) and cost a lot
                // of frame time; the sun/moon light keeps its shadows
                l.shadows = LightShadows.None;
                if (l.GetComponentInParent<PlayerController>() == null) lights.Add(l);
            }
            foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (!ps.main.loop || ps.GetComponentInParent<PlayerController>() != null) continue;
                var p = ps.transform.parent;
                if (p != null && p.GetComponentInParent<ParticleSystem>() != null) continue; // only the top of each effect
                if (!OnlyParticles(ps.gameObject)) continue;                              // never switch off scripted objects
                effects.Add(ps.gameObject);
            }
            cam = null;
            Apply();
        }

        static bool OnlyParticles(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
                if (!(c is Transform) && !(c is ParticleSystem) && !(c is ParticleSystemRenderer)) return false;
            return true;
        }

        /// <summary>Applies the chosen quality to the render pipeline, post effects and camera.</summary>
        void Apply()
        {
            int q = Mathf.Clamp(GameSettings.Quality, 0, 2);
            if (urp != null && saved)
            {
                urp.renderScale = q == 0 ? 0.7f : origScale;
                urp.msaaSampleCount = q == 2 ? origMsaa : 1;
                urp.shadowDistance = q == 0 ? 0f : q == 1 ? Mathf.Min(origShadow, 30f) : origShadow;
                urp.maxAdditionalLightsCount = q == 0 ? 2 : q == 1 ? Mathf.Min(origLights, 4) : origLights;
                urp.supportsHDR = q == 0 ? false : origHdr;
            }
            foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (v.profile == null) continue;
                if (v.profile.TryGet(out Bloom bloom)) bloom.active = q > 0;
                if (v.profile.TryGet(out FilmGrain grain)) grain.active = q == 2;
                if (v.profile.TryGet(out ChromaticAberration ca)) ca.active = q == 2;
                if (v.profile.TryGet(out MotionBlur mb)) mb.active = false;
            }
            GameSettings.ApplyFrameRate(); // frame limit and VSync come from Video settings
            cam = Camera.main;
            SetupCamera(q);
            nextTick = 0f;
        }

        /// <summary>Shorter draw distance, with the sky set to the fog color so the cut-off blends into the darkness.</summary>
        void SetupCamera(int q)
        {
            if (cam == null) return;
            cam.farClipPlane = DrawDistance[q];
            if (RenderSettings.fog)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = RenderSettings.fogColor;
            }
        }

        void Update()
        {
            if (Time.unscaledTime < nextTick) return;
            nextTick = Time.unscaledTime + 0.25f;
            if (cam == null || !cam.isActiveAndEnabled) { cam = Camera.main; if (cam == null) return; SetupCamera(Mathf.Clamp(GameSettings.Quality, 0, 2)); }
            int q = Mathf.Clamp(GameSettings.Quality, 0, 2);
            Vector3 c = cam.transform.position;

            // lights: the nearest few stay on
            sortBuf.Clear();
            for (int i = lights.Count - 1; i >= 0; i--)
            {
                var l = lights[i];
                if (l == null) { lights.RemoveAt(i); continue; }
                sortBuf.Add((l, (l.transform.position - c).sqrMagnitude));
            }
            sortBuf.Sort((a, b) => a.dist.CompareTo(b.dist));
            float maxD = LightRange[q] * LightRange[q];
            for (int i = 0; i < sortBuf.Count; i++)
                sortBuf[i].light.enabled = i < LightBudget[q] && sortBuf[i].dist < maxD;

            // looping sounds: paused far away
            float sd = SoundRange[q] * SoundRange[q];
            for (int i = loops.Count - 1; i >= 0; i--)
            {
                var a = loops[i];
                if (a == null) { loops.RemoveAt(i); continue; }
                bool near = (a.transform.position - c).sqrMagnitude < sd;
                if (a.enabled != near) a.enabled = near;
            }

            // looping effects: pause far away
            float fx = EffectRange[q] * EffectRange[q];
            for (int i = effects.Count - 1; i >= 0; i--)
            {
                var e = effects[i];
                if (e == null) { effects.RemoveAt(i); continue; }
                bool near = (e.transform.position - c).sqrMagnitude < fx;
                if (e.activeSelf != near) e.SetActive(near);
            }
        }
    }
}
