using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Flooded area. Wading on foot is slow; the boat is confined to these bounds.
    /// Uses a BoxCollider (trigger) only to describe the area.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class WaterZone : MonoBehaviour
    {
        public static readonly List<WaterZone> All = new List<WaterZone>();

        [Range(0.1f, 1f)] public float wadeSpeedMultiplier = 0.45f;

        BoxCollider box;

        public Bounds WorldBounds => box.bounds;

        void Awake()
        {
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool Contains(Vector3 p)
        {
            var b = WorldBounds;
            return p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z && p.y < b.max.y;
        }

        public bool ContainsXZ(Vector3 p)
        {
            var b = WorldBounds;
            return p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z;
        }

        /// <summary>
        /// True if stepping to 'next' would take a walking character (knight or enemy) off dry footing into
        /// deep water. Docks, walkways and islands count as dry; only the boat may cross the water.
        /// </summary>
        public static bool BlocksWalking(float groundY, Vector3 next)
        {
            bool inWater = false;
            foreach (var w in All)
                if (w != null && w.ContainsXZ(next)) { inWater = true; break; }
            if (!inWater) return false;

            if (Physics.Raycast(new Vector3(next.x, groundY + 0.8f, next.z), Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.attachedRigidbody != null) return true; // the boat's deck is not a bridge
                return hit.point.y < groundY - 0.3f;
            }
            return true;
        }

        /// <summary>Removes the parts of a horizontal move that would step into deep water, so walkers slide along the shore.</summary>
        public static Vector3 ClampMove(Vector3 position, float groundY, Vector3 move, float radius)
        {
            if (All.Count == 0) return move;
            Vector3 flat = new Vector3(move.x, 0f, move.z);
            if (flat.sqrMagnitude < 0.000001f) return move;
            if (BlocksWalking(groundY, position)) return move; // already standing in the water: let him walk out
            Vector3 Probe(Vector3 m) => position + m.normalized * (radius + 0.2f) + m;
            if (!BlocksWalking(groundY, Probe(flat))) return move;
            Vector3 x = new Vector3(move.x, 0f, 0f), z = new Vector3(0f, 0f, move.z);
            bool xOk = x.sqrMagnitude > 0.000001f && !BlocksWalking(groundY, Probe(x));
            bool zOk = z.sqrMagnitude > 0.000001f && !BlocksWalking(groundY, Probe(z));
            return new Vector3(xOk ? move.x : 0f, move.y, zOk ? move.z : 0f);
        }

        public static float SpeedMultiplierAt(Vector3 position)
        {
            // only applies to someone actually standing down in the water (docks and walkways are dry)
            foreach (var w in All)
                if (w != null && w.Contains(position) && position.y < -0.25f) return w.wadeSpeedMultiplier;
            return 1f;
        }
    }
}
