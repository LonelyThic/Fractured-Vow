using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FracturedVow
{
    /// <summary>
    /// Checkpoints, death and respawn. When the knight dies, a death screen offers an instant respawn at the
    /// last checkpoint or a full restart of the floor. Respawning resets every enemy and any boss fight that
    /// was in progress, so the knight never wakes up in the middle of a battle.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        /// <summary>Raised after every respawn so bosses, platforms and boats can reset themselves.</summary>
        public static event System.Action Respawned;
        /// <summary>The knight reached a checkpoint (companions refill their draughts).</summary>
        public static event System.Action CheckpointReached;

        public PlayerController player;
        [Tooltip("Seconds of the death animation before the death screen appears.")]
        public float deathScreenDelay = 1.2f;

        Vector3 checkpointPos;
        public Vector3 CheckpointPosition => checkpointPos;
        Quaternion checkpointRot;
        Health playerHealth;
        PlayerStats playerStats;

        void Awake()
        {
            Instance = this;
            if (player == null) player = FindFirstObjectByType<PlayerController>();
        }

        void Start()
        {
            if (player == null) return;
            playerHealth = player.GetComponent<Health>();
            playerStats = player.GetComponent<PlayerStats>();
            checkpointPos = player.transform.position;
            checkpointRot = player.transform.rotation;
            if (playerHealth != null) playerHealth.Died += OnPlayerDied;
            // the start of a floor is the first checkpoint
            StartCoroutine(FirstCheckpoint());
        }

        void OnDestroy()
        {
            if (playerHealth != null) playerHealth.Died -= OnPlayerDied;
        }

        IEnumerator FirstCheckpoint()
        {
            yield return null; yield return null; // after the run and the floor have loaded
            RunState.LostShards = 0; RunState.LostScene = null; Remnant.Clear(); // (older saves dropped shards on death)
            SaveSystem.MarkCheckpoint();
            WorldLog.Commit();
        }

        /// <summary>A boss fell: a checkpoint right where the knight stands, so the victory can't be lost.</summary>
        public void CheckpointHereSoon(float delay = 2f) => StartCoroutine(CheckpointLater(delay));

        IEnumerator CheckpointLater(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (player == null || playerHealth == null || playerHealth.IsDead) yield break;
            SetCheckpoint(player.transform.position, player.transform.rotation);
        }

        public void SetCheckpoint(Vector3 position, Quaternion rotation, bool announce = true)
        {
            // walking back through a checkpoint saves again (quietly): anything gained since then is kept
            if ((position - checkpointPos).sqrMagnitude < 0.25f) announce = false;
            checkpointPos = position;
            checkpointRot = rotation;
            SaveSystem.MarkCheckpoint();
            WorldLog.Commit();
            SaveSystem.Save(PlayerProgress.SceneName);
            var flask = player != null ? player.GetComponent<PlayerFlask>() : null;
            if (flask != null) flask.Refill();
            if (announce) PrototypeHUD.Message("Checkpoint reached");
            CheckpointReached?.Invoke();
        }

        void OnPlayerDied()
        {
            // nothing is dropped: respawning puts the knight and the world back to the last checkpoint
            LastLost = 0;
            StartCoroutine(DeathScreen());
        }

        /// <summary>Shards dropped by the latest death (shown on the death screen).</summary>
        public static int LastLost;

        void SpawnRemnantIfHere()
        {
            if (RunState.LostShards <= 0 || RunState.LostScene != PlayerProgress.SceneName) return;
            Remnant.Spawn(RunState.LostAt, RunState.LostShards, Resources.Load<Material>("FVFX/FVP_FX_Additive"));
        }

        public void TeleportToCheckpoint()
        {
            if (player == null) return;
            var boat = player.GetComponentInParent<BoatController>();
            if (boat != null) boat.ForceExit();
            player.Teleport(checkpointPos, checkpointRot);
        }

        /// <summary>Starts a brand-new run from Floor 1: level, skills, skill points, items and gold all reset.</summary>
        public static void RestartRun()
        {
            RunState.NewRun();
            SaveSystem.Delete();
            Time.timeScale = 1f;
            LoadingScreen.Load(0);
        }

        IEnumerator DeathScreen()
        {
            yield return new WaitForSecondsRealtime(deathScreenDelay);
            GameMenu.ShowDeath();
        }

        /// <summary>
        /// Respawn at the last checkpoint, exactly as things were there: the character (level, shards, gold, items,
        /// gear, spells, skills, companions, notes) and the world (enemies, chests, pickups, loot, hired sellswords).
        /// Whatever happened before the checkpoint stays done; whatever happened after it is undone.
        /// </summary>
        public void Respawn()
        {
            if (player == null) return;
            var boat = player.GetComponentInParent<BoatController>();
            if (boat != null) boat.ForceExit();

            SaveSystem.RewindToCheckpoint();
            var progress = player.GetComponent<PlayerProgress>();
            if (progress != null) progress.SyncWithRun();
            EnemyAI.ResetAll();      // (asks the world log which deaths to undo)
            WorldLog.Rewind();
            CompanionAI.SyncWithRun(checkpointPos);
            foreach (var w in FindObjectsByType<WardenBoss>(FindObjectsSortMode.None)) w.ResetFight();
            foreach (var o in FindObjectsByType<StoneWall>(FindObjectsSortMode.None)) Destroy(o.gameObject);

            player.Teleport(checkpointPos, checkpointRot);
            playerHealth.Revive();
            if (playerStats != null) playerStats.RefillAll();
            var flaskR = player.GetComponent<PlayerFlask>();
            if (flaskR != null) flaskR.Refill();
            SpawnRemnantIfHere();
            var bridge = player.GetComponent<AnimatorBridge>();
            if (bridge != null) bridge.ResetAnimator();

            GameAudio.PlayMusic("music_dungeon", 1f);
            PrototypeHUD.Message("Back at the last checkpoint", 2f);
            var lib = FVLibrary.Get;
            if (lib != null && lib.respawn != null) MagicProjectile.PlayEffect(lib.respawn, player.transform.position, 1f);
            Respawned?.Invoke();
        }

        /// <summary>
        /// Replays the floor from its start: the character goes back to how they were when they entered it
        /// (level, skills, skill points, items, gold, companions), and the level reloads.
        /// </summary>
        public static void RestartFloor()
        {
            SaveSystem.RewindToFloorStart();
            Time.timeScale = 1f;
            LoadingScreen.Load(SceneManager.GetActiveScene().name);
        }
    }
}
