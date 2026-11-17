using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FracturedVow
{
    /// <summary>
    /// Remembers what changed in the level since the last checkpoint, so dying puts the world back the way it was
    /// there: enemies killed, chests opened, items and notes picked up, sellswords hired and loot dropped after the
    /// checkpoint all come back (or go away). Everything done before the checkpoint stays done.
    /// </summary>
    public static class WorldLog
    {
        // level objects picked up since the checkpoint: hidden, not destroyed, so a death can bring them back
        static readonly HashSet<GameObject> taken = new HashSet<GameObject>();
        // loot that appeared since the checkpoint (coins, drops): removed again by a death
        static readonly HashSet<GameObject> spawned = new HashSet<GameObject>();
        // enemies killed and chests opened since the checkpoint
        static readonly HashSet<Object> changed = new HashSet<Object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => SceneManager.sceneLoaded += (s, m) => { taken.Clear(); spawned.Clear(); changed.Clear(); };

        /// <summary>An enemy died or a chest was opened.</summary>
        public static void Changed(Object o) { if (o != null) changed.Add(o); }
        public static bool ChangedSinceCheckpoint(Object o) => o != null && changed.Contains(o);

        /// <summary>New loot in the world (a coin, an item drop).</summary>
        public static void Spawned(GameObject g) { if (g != null) spawned.Add(g); }

        /// <summary>
        /// Takes something out of the world: loot dropped since the checkpoint is destroyed; anything older is only
        /// hidden until the next checkpoint (a death before then puts it back).
        /// </summary>
        public static void Remove(GameObject g)
        {
            if (g == null) return;
            if (spawned.Remove(g)) { Object.Destroy(g); return; }
            g.SetActive(false);
            taken.Add(g);
        }

        /// <summary>A checkpoint: everything so far becomes permanent.</summary>
        public static void Commit()
        {
            foreach (var g in taken) if (g != null) Object.Destroy(g);
            taken.Clear();
            spawned.Clear();
            changed.Clear();
        }

        /// <summary>A death: the world goes back to the last checkpoint.</summary>
        public static void Rewind()
        {
            foreach (var g in taken) if (g != null) g.SetActive(true);
            taken.Clear();
            foreach (var g in spawned) if (g != null) Object.Destroy(g);
            spawned.Clear();
            foreach (var o in changed)
                if (o is Chest c && c != null) c.ResetChest();
            // enemies are revived by EnemyAI.ResetAll, which asks ChangedSinceCheckpoint
            changed.Clear();
        }
    }
}
