using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FracturedVow
{
    /// <summary>
    /// Sound for the whole game. Starts itself, survives scene loads, and loads every clip from
    /// Resources/FVAudio. Clips named name_1, name_2... play a random variation when "name" is requested.
    /// Hits, blocks and deaths are heard automatically from Health events; other scripts call Play().
    /// </summary>
    public class GameAudio : MonoBehaviour
    {
        static GameAudio inst;
        public static float SfxVolume => GameSettings.SfxVolume;

        readonly Dictionary<string, List<AudioClip>> clips = new Dictionary<string, List<AudioClip>>();
        AudioSource[] pool;
        int next;
        AudioSource musicA, musicB, ambience, ui;
        bool aIsActive = true;
        string currentMusic;
        Coroutine musicFade;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            Ensure();
            inst.OnSceneStart(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        static void Ensure()
        {
            if (inst != null) return;
            var go = new GameObject("GameAudio");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<GameAudio>();
            inst.Init();
        }

        void Init()
        {
            foreach (var c in Resources.LoadAll<AudioClip>("FVAudio"))
            {
                Add(c.name, c);
                int us = c.name.LastIndexOf('_');
                if (us > 0 && int.TryParse(c.name.Substring(us + 1), out _)) Add(c.name.Substring(0, us), c);
            }

            pool = new AudioSource[24];
            for (int i = 0; i < pool.Length; i++)
            {
                var s = new GameObject("Sfx " + i).AddComponent<AudioSource>();
                s.transform.SetParent(transform, false);
                s.playOnAwake = false;
                s.rolloffMode = AudioRolloffMode.Linear;
                s.minDistance = 2f;
                s.maxDistance = 35f;
                s.dopplerLevel = 0f;
                pool[i] = s;
            }
            musicA = Make2D("Music A", true);
            musicB = Make2D("Music B", true);
            ambience = Make2D("Ambience", true);
            ui = Make2D("UI", false);
            musicA.ignoreListenerPause = musicB.ignoreListenerPause = ui.ignoreListenerPause = true;

            GameSettings.Changed += ApplyVolumes;
            SceneManager.sceneLoaded += OnSceneStart;
            Health.DamageCalculated += OnDamage;
            Health.AnyDied += OnDied;
            ApplyVolumes();
        }

        void OnDestroy()
        {
            GameSettings.Changed -= ApplyVolumes;
            SceneManager.sceneLoaded -= OnSceneStart;
            Health.DamageCalculated -= OnDamage;
            Health.AnyDied -= OnDied;
        }

        void Add(string key, AudioClip c)
        {
            if (!clips.TryGetValue(key, out var list)) clips[key] = list = new List<AudioClip>();
            if (!list.Contains(c)) list.Add(c);
        }

        AudioSource Make2D(string n, bool loop)
        {
            var s = new GameObject(n).AddComponent<AudioSource>();
            s.transform.SetParent(transform, false);
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        void OnSceneStart(Scene s, LoadSceneMode m)
        {
            AudioListener.pause = false;
            PlayMusic("music_dungeon", 2.5f);
            PlayAmbience("loop_ambience");
        }

        void ApplyVolumes()
        {
            AudioListener.volume = GameSettings.MasterVolume;
            if (ambience != null) ambience.volume = 0.5f * GameSettings.SfxVolume;
            float mv = GameSettings.MusicVolume;
            if (musicA != null && musicFade == null) { musicA.volume = aIsActive ? mv : 0f; musicB.volume = aIsActive ? 0f : mv; }
        }

        AudioClip Pick(string name)
        {
            if (!clips.TryGetValue(name, out var list) || list.Count == 0) return null;
            return list[Random.Range(0, list.Count)];
        }

        // ---------------- public API ----------------

        /// <summary>Plays a 3D sound at a position.</summary>
        public static void Play(string name, Vector3 position, float volume = 1f, float pitchJitter = 0.06f, float pitch = 1f)
        {
            Ensure();
            var clip = inst.Pick(name);
            if (clip == null) return;
            var s = inst.pool[inst.next];
            inst.next = (inst.next + 1) % inst.pool.Length;
            s.transform.position = position;
            s.clip = clip;
            s.spatialBlend = 1f;
            s.volume = volume * GameSettings.SfxVolume;
            s.pitch = pitch + Random.Range(-pitchJitter, pitchJitter);
            s.Play();
        }

        /// <summary>Plays a flat (non-positional) sound: UI, stingers, the knight's own voice.</summary>
        public static void PlayUI(string name, float volume = 1f)
        {
            Ensure();
            var clip = inst.Pick(name);
            if (clip != null) inst.ui.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        public static void PlayMusic(string name, float fade = 2f)
        {
            Ensure();
            if (inst.currentMusic == name) return;
            var clip = inst.Pick(name);
            if (clip == null) return;
            inst.currentMusic = name;
            if (inst.musicFade != null) inst.StopCoroutine(inst.musicFade);
            inst.musicFade = inst.StartCoroutine(inst.Crossfade(clip, fade));
        }

        IEnumerator Crossfade(AudioClip clip, float fade)
        {
            var from = aIsActive ? musicA : musicB;
            var to = aIsActive ? musicB : musicA;
            aIsActive = !aIsActive;
            to.clip = clip;
            to.volume = 0f;
            to.Play();
            float start = from.volume;
            for (float t = 0f; t < fade; t += Time.unscaledDeltaTime)
            {
                float k = t / fade, mv = GameSettings.MusicVolume;
                to.volume = k * mv;
                from.volume = (1f - k) * start;
                yield return null;
            }
            from.Stop();
            to.volume = GameSettings.MusicVolume;
            musicFade = null;
        }

        /// <summary>Pauses or resumes the music (the pause menu calls this).</summary>
        public static void SetMusicPaused(bool paused)
        {
            Ensure();
            foreach (var s in new[] { inst.musicA, inst.musicB })
            {
                if (s == null) continue;
                if (paused) s.Pause(); else s.UnPause();
            }
        }

        public static void PlayAmbience(string name)
        {
            Ensure();
            var clip = inst.Pick(name);
            if (clip == null || inst.ambience.clip == clip) return;
            inst.ambience.clip = clip;
            inst.ambience.Play();
        }

        /// <summary>Adds a looping positional sound (torch crackle, water) to an object.</summary>
        public static LoopSound AttachLoop(GameObject host, string name, float volume, float maxDistance)
        {
            Ensure();
            var clip = inst.Pick(name);
            if (clip == null) return null;
            var s = host.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 1f;
            s.maxDistance = maxDistance;
            s.dopplerLevel = 0f;
            s.time = Random.Range(0f, clip.length * 0.9f);
            s.pitch = Random.Range(0.95f, 1.05f);
            var loop = host.AddComponent<LoopSound>();
            loop.source = s;
            loop.baseVolume = volume;
            s.volume = volume * GameSettings.SfxVolume;
            s.Play();
            return loop;
        }

        // ---------------- automatic feedback ----------------

        void OnDamage(DamageReport r)
        {
            if (r.target == null) return;
            Vector3 p = r.target.transform.position + Vector3.up * 1.2f;
            bool isPlayer = r.target.GetComponent<PlayerController>() != null;
            if (r.blocked)
            {
                Play("block_clang", p, 0.9f);
                GameFX.Sparks(p + r.target.transform.forward * 0.5f, new Color(1f, 0.85f, 0.4f), 18);
                GameFX.Flash(p + r.target.transform.forward * 0.5f, new Color(1f, 0.9f, 0.6f, 0.8f), 0.7f);
                return;
            }
            Play("sword_hit", p, isPlayer ? 0.7f : 1f);
            if (isPlayer) PlayUI("player_hurt", 0.8f);
            else if (r.target.GetComponent<EnemyAI>() != null && r.after > 0f) Play("enemy_hurt", p, 0.8f);
            // a white flash and streak with a few sparks (the Particle Pack's blood and smoke burst didn't suit the style)
            Vector3 across = r.attacker != null ? Vector3.Cross(Vector3.up, (r.target.transform.position - r.attacker.transform.position).normalized) : Vector3.right;
            if (isPlayer) GameFX.Sparks(p, new Color(1f, 0.3f, 0.2f), 10);
            else if (r.final >= 1f) GameFX.SwordHit(p, across, new Color(1f, 0.75f, 0.35f));
        }

        void OnDied(Health h)
        {
            if (h == null) return;
            Vector3 p = h.transform.position + Vector3.up;
            if (h.GetComponent<PlayerController>() != null) { PlayUI("death_sting", 0.9f); return; }
            if (h.GetComponent<EnemyAI>() != null) { Play("enemy_death", p); GameFX.Dust(h.transform.position + Vector3.up * 0.3f, 0.9f, 26); }
        }
    }

    /// <summary>Keeps a looping sound's volume in step with the effects volume. Set level to fade it (0..1).</summary>
    public class LoopSound : MonoBehaviour
    {
        public AudioSource source;
        public float baseVolume = 1f;
        [Range(0f, 1f)] public float level = 1f;

        void Update()
        {
            if (source != null) source.volume = baseVolume * level * GameSettings.SfxVolume;
        }
    }
}
