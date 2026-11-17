using UnityEngine;
using UnityEngine.SceneManagement;

namespace FracturedVow
{
    /// <summary>
    /// Adds the RPG layer to every level automatically: progression, flask and extra animation on the knight,
    /// plus the HUD, the character menu, the console and the run randomizer. Nothing to place by hand.
    /// </summary>
    public static class RPGBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.sceneLoaded += OnLoaded;
            Setup();
        }

        static void OnLoaded(Scene s, LoadSceneMode m) => Setup();

        /// <summary>
        /// Asset Store demo prefabs (like the dungeon pack's demo Player) bring their own camera and old-Input
        /// scripts. If one got into a level as a prop, this switches its camera, listener and scripts off so the
        /// knight's follow camera stays in charge and the Console stays clean.
        /// </summary>
        static void RemoveStrays(PlayerController pc)
        {
            ThirdPersonCamera follow = Object.FindFirstObjectByType<ThirdPersonCamera>();
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (follow != null && cam.gameObject == follow.gameObject) continue;
                if (cam.targetTexture != null) continue; // render-to-texture cameras are fine
                cam.enabled = false;
                if (cam.CompareTag("MainCamera")) cam.tag = "Untagged";
                var listener = cam.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
                Debug.LogWarning("[Fractured Vow] Switched off a stray camera: " + cam.name + " (from a pack's demo prefab).");
            }
            if (follow != null)
            {
                follow.gameObject.tag = "MainCamera";
                var cam = follow.GetComponent<Camera>();
                if (cam != null) cam.enabled = true;
                if (follow.target == null) follow.target = pc.transform;
            }
            string[] demoScripts = { "PlayerMovement", "MouseCamLook", "ScreenshotMaker", "SimpleCameraController", "SimpleCharacterMotor" };
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || System.Array.IndexOf(demoScripts, mb.GetType().Name) < 0) continue;
                // a pack's demo player that got placed as a prop: remove the whole thing (it has its own camera and controls)
                Transform kill = mb.transform;
                for (var t = mb.transform; t != null; t = t.parent)
                    foreach (var other in t.GetComponents<MonoBehaviour>())
                        if (other != null && other.GetType().Name == "PlayerMovement") kill = t; // the demo player's root
                if (kill.GetComponentInChildren<PlayerController>(true) != null || kill.GetComponentInChildren<ThirdPersonCamera>(true) != null) { mb.enabled = false; continue; }
                Debug.LogWarning("[Fractured Vow] Removed a pack demo object from the level: " + kill.name);
                Object.Destroy(kill.gameObject);
            }
        }

        /// <summary>Keeps checking for a few seconds after a level loads, and makes sure the game camera follows the knight.</summary>
        class StrayWatch : MonoBehaviour
        {
            PlayerController pc;
            float until;

            void Start() { pc = FindFirstObjectByType<PlayerController>(); until = Time.unscaledTime + 4f; }

            void Update()
            {
                if (pc == null) { Destroy(gameObject); return; }
                if (Time.unscaledTime < until && Time.frameCount % 10 == 0) RemoveStrays(pc);
                if (!CutsceneDirector.Playing)
                {
                    var follow = FindFirstObjectByType<ThirdPersonCamera>(FindObjectsInactive.Include);
                    if (follow != null && (!follow.enabled || !follow.gameObject.activeInHierarchy || follow.target == null))
                    {
                        follow.gameObject.SetActive(true);
                        follow.enabled = true;
                        if (follow.target == null) follow.target = pc.transform;
                        var cam = follow.GetComponent<Camera>();
                        if (cam != null) cam.enabled = true;
                        follow.gameObject.tag = "MainCamera";
                    }
                }
                if (Time.unscaledTime > until + 20f) Destroy(gameObject);
            }
        }

        /// <summary>
        /// Asset Store packs ship demo players, cameras and scripts (BrokenVector's demo Player with ScreenshotMaker,
        /// SazenGames' camera controller...). If one ever ends up in a level, it would steal the main camera and spam
        /// input errors, so they're switched off as the level loads. Our own camera (with ThirdPersonCamera) stays.
        /// </summary>
        static readonly string[] DemoScripts = { "ScreenshotMaker", "PlayerMovement", "MouseCamLook", "SimpleCameraController", "SimpleCharacterMotor" };

        static void CleanUpDemoObjects()
        {
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                foreach (var d in DemoScripts)
                    if (n == d && mb.GetType().Namespace != "FracturedVow")
                    {
                        Debug.LogWarning("[Fractured Vow] Switched off demo object '" + mb.gameObject.name + "' (" + n + ") from an Asset Store pack.");
                        mb.gameObject.SetActive(false);
                        break;
                    }
            }
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam.GetComponent<ThirdPersonCamera>() == null && cam.CompareTag("MainCamera"))
                {
                    Debug.LogWarning("[Fractured Vow] Switched off an extra main camera '" + cam.name + "'.");
                    cam.gameObject.SetActive(false);
                }
        }

        static void Setup()
        {
            CleanUpDemoObjects();
            var pc = Object.FindFirstObjectByType<PlayerController>();
            if (pc == null || Object.FindFirstObjectByType<GameHUD>() != null) return;
            var go = pc.gameObject;
            RemoveStrays(pc);
            new GameObject("Stray Watch").AddComponent<StrayWatch>();
            ProceduralMotion.On(go);
            if (go.GetComponent<PlayerProgress>() == null) go.AddComponent<PlayerProgress>();
            if (go.GetComponent<PlayerFlask>() == null) go.AddComponent<PlayerFlask>();
            if (go.GetComponent<SafeGround>() == null) go.AddComponent<SafeGround>();
            var sys = new GameObject("RPG Systems");
            sys.AddComponent<GameHUD>();
            sys.AddComponent<CharacterMenu>();
            sys.AddComponent<MapUI>();
            sys.AddComponent<DevConsole>();
            sys.AddComponent<RunRandomizer>();

            // Floor 1: the starter sword lies on the floor in front of the knight until he takes it
            if (PlayerProgress.SceneName.Contains("Floor1") && !RunState.StarterTaken && string.IsNullOrEmpty(RunState.EquippedWeapon))
            {
                var sword = FVLibrary.Item(RunState.StarterSword());
                if (sword != null)
                {
                    Vector3 at = pc.transform.position + pc.transform.forward * 1.8f;
                    if (Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
                    var p = ItemPickup.Spawn(at, sword);
                    p.name = "Starter Sword";
                    PrototypeHUD.Message("A sword lies at your feet. Press " + InputBindings.Name(GameAction.Interact) + " to take it.", 4f);
                }
            }

            // companions hired earlier in the run come along to this floor
            for (int i = 0; i < RunState.Companions.Count; i++)
            {
                Vector3 side = pc.transform.right * (i == 0 ? 1.4f : -1.4f);
                CompanionAI.Spawn(i, pc.transform.position - pc.transform.forward * 2f + side + Vector3.up * 0.1f, pc.transform.rotation);
            }
        }
    }
}
