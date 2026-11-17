using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Remembers where the knight last stood on solid, dry ground, so "Unstuck" (Esc menu, or the console's
    /// "unstuck") can put him back there if he ever gets wedged in scenery or stranded on the water.
    /// Falls back to the last checkpoint.
    /// </summary>
    public class SafeGround : MonoBehaviour
    {
        PlayerController pc;
        CharacterController cc;
        readonly List<Vector3> spots = new List<Vector3>();
        float next;

        void Awake()
        {
            pc = GetComponent<PlayerController>();
            cc = GetComponent<CharacterController>();
        }

        void Update()
        {
            if (Time.time < next || pc == null) return;
            next = Time.time + 0.5f;
            if (!pc.IsGrounded || !cc.enabled || CutsceneDirector.Playing) return;
            Vector3 p = transform.position;
            if (WaterZone.BlocksWalking(p.y, p)) return;                                     // not on the water
            if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (hit.collider.attachedRigidbody != null) return;                             // not on the boat
            if (spots.Count > 0 && (spots[spots.Count - 1] - p).sqrMagnitude < 1f) return;
            spots.Add(p);
            if (spots.Count > 30) spots.RemoveAt(0);
        }

        /// <summary>Moves the knight to dry ground he stood on a few seconds ago (or the last checkpoint).</summary>
        public static void Unstuck()
        {
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc == null) return;
            foreach (var boat in FindObjectsByType<BoatController>(FindObjectsSortMode.None)) boat.ForceExit();
            var sg = pc.GetComponent<SafeGround>();
            Vector3 here = pc.transform.position;
            Vector3? target = null;
            if (sg != null)
                for (int i = sg.spots.Count - 1; i >= 0; i--)
                    if ((sg.spots[i] - here).sqrMagnitude > 4f) { target = sg.spots[i]; break; } // a spot at least 2 m away
            if (target == null && GameManager.Instance != null) target = GameManager.Instance.CheckpointPosition;
            if (target == null) return;
            var cc = pc.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            pc.transform.position = target.Value + Vector3.up * 0.2f;
            if (cc != null) cc.enabled = true;
            PrototypeHUD.Message("Unstuck", 1.5f);
        }
    }
}
