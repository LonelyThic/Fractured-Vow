using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Serpent body for the Leviathan: the head is moved by the boss (or the cutscene); every body segment
    /// follows the path the head took, so it coils, arcs out of the water and dives naturally. The jaw opens
    /// with jawOpen (0..1).
    /// </summary>
    public class LeviathanBody : MonoBehaviour
    {
        public Transform head;
        public Transform jaw;
        public Transform[] segments;
        public float spacing = 1.8f;
        [Range(0f, 1f)] public float jawOpen;
        public float swayAmount = 0.25f;

        readonly List<Vector3> trail = new List<Vector3>();
        Quaternion jawBase;

        void Awake()
        {
            if (jaw != null) jawBase = jaw.localRotation;
            Snap();
        }

        /// <summary>Lays the body out straight behind the head (after teleporting the head).</summary>
        public void Snap()
        {
            trail.Clear();
            if (head == null) return;
            Vector3 back = -head.forward; back.y = 0f;
            if (back.sqrMagnitude < 0.01f) back = Vector3.back;
            back.Normalize();
            int n = (segments != null ? segments.Length : 0) + 2;
            for (int i = 0; i <= n * 4; i++) trail.Add(head.position + back * (i * spacing * 0.25f) + Vector3.down * (i * 0.05f));
            Place();
        }

        void LateUpdate()
        {
            if (head == null) return;
            if (trail.Count == 0 || (head.position - trail[0]).sqrMagnitude > 0.04f) trail.Insert(0, head.position);
            float need = spacing * ((segments != null ? segments.Length : 0) + 2);
            float total = 0f;
            int keep = trail.Count;
            for (int i = 1; i < trail.Count; i++)
            {
                total += Vector3.Distance(trail[i - 1], trail[i]);
                if (total > need) { keep = i + 1; break; }
            }
            if (keep < trail.Count) trail.RemoveRange(keep, trail.Count - keep);
            Place();
            if (jaw != null) jaw.localRotation = jawBase * Quaternion.Euler(Mathf.Lerp(0f, 38f, jawOpen), 0f, 0f);
        }

        void Place()
        {
            if (segments == null) return;
            for (int s = 0; s < segments.Length; s++)
            {
                var seg = segments[s];
                if (seg == null) continue;
                float want = spacing * (s + 1);
                Vector3 pos, ahead;
                Sample(want, out pos, out ahead);
                float sway = Mathf.Sin(Time.time * 2.2f - s * 0.55f) * swayAmount * (s / (float)segments.Length);
                Vector3 dir = ahead - pos;
                if (dir.sqrMagnitude < 0.0001f) dir = seg.forward;
                Vector3 side = Vector3.Cross(Vector3.up, dir.normalized);
                seg.position = pos + side * sway;
                seg.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        void Sample(float distance, out Vector3 pos, out Vector3 ahead)
        {
            float acc = 0f;
            for (int i = 1; i < trail.Count; i++)
            {
                float d = Vector3.Distance(trail[i - 1], trail[i]);
                if (acc + d >= distance)
                {
                    float t = d > 0.0001f ? (distance - acc) / d : 0f;
                    pos = Vector3.Lerp(trail[i - 1], trail[i], t);
                    ahead = trail[i - 1];
                    return;
                }
                acc += d;
            }
            pos = trail.Count > 0 ? trail[trail.Count - 1] : head.position;
            ahead = trail.Count > 1 ? trail[trail.Count - 2] : head.position + head.forward;
        }
    }
}
