using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// A white ribbon of light that follows the real sword blade, so the trail traces exactly what the swing
    /// animation does. It records the blade's base and tip every frame while a swing is "emitting" and builds a
    /// fading mesh between them. Added to the held weapon automatically the first time the knight swings.
    /// </summary>
    public class WeaponTrail : MonoBehaviour
    {
        const float Life = 0.16f;          // how long a slice of the trail stays visible
        const int MaxSamples = 40;

        Transform tipPoint, basePoint;
        Mesh mesh;
        GameObject drawer;
        float emitUntil;
        Color color = Color.white;

        struct Sample { public Vector3 a, b; public float t; }
        readonly List<Sample> samples = new List<Sample>();
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> cols = new List<Color>();
        readonly List<Vector2> uvs = new List<Vector2>();
        readonly List<int> tris = new List<int>();

        /// <summary>
        /// Starts a trail on the sword the character is holding, for 'seconds'. Returns false if it holds no
        /// sword (the caller can fall back to the plain arc).
        /// </summary>
        public static bool Swing(Transform character, float seconds, Color color)
        {
            var hand = CharacterVisuals.FindHand(character);
            var weapon = hand != null ? hand.Find("Held Weapon") : null;
            if (weapon == null) return false;
            var trail = weapon.GetComponent<WeaponTrail>();
            if (trail == null) trail = weapon.gameObject.AddComponent<WeaponTrail>();
            if (!trail.FindBlade()) return false;
            trail.color = color;
            trail.emitUntil = Time.time + seconds;
            return true;
        }

        /// <summary>Puts a point at the blade's base and its tip: the far end of the weapon from the hand.</summary>
        bool FindBlade()
        {
            if (tipPoint != null) return true;
            var rends = GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return false;

            // the weapon's extent in its own space; the hand grips it at the origin
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            foreach (var r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var lb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = lb.center + Vector3.Scale(lb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = transform.InverseTransformPoint(r.transform.TransformPoint(corner));
                    min = Vector3.Min(min, p); max = Vector3.Max(max, p);
                }
            }
            if (float.IsInfinity(min.x)) return false;

            // the longest side is the blade; its far end from the grip is the tip
            Vector3 size = max - min;
            int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
            Vector3 center = (min + max) * 0.5f;
            Vector3 tip = center, far;
            tip[axis] = Mathf.Abs(max[axis]) >= Mathf.Abs(min[axis]) ? max[axis] : min[axis];
            Vector3 grip = center; grip[axis] = tip[axis] == max[axis] ? min[axis] : max[axis];
            far = Vector3.Lerp(grip, tip, 0.3f); // where the blade starts (past the handle)

            tipPoint = new GameObject("Trail Tip").transform;
            tipPoint.SetParent(transform, false);
            tipPoint.localPosition = tip;
            basePoint = new GameObject("Trail Base").transform;
            basePoint.SetParent(transform, false);
            basePoint.localPosition = far;

            // the ribbon lives in world space, so it's drawn by its own object (not moved by the hand)
            drawer = new GameObject("Sword Trail");
            mesh = new Mesh { name = "Sword Trail" };
            mesh.MarkDynamic();
            drawer.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = drawer.AddComponent<MeshRenderer>();
            mr.sharedMaterial = RuntimeVisuals.Glow;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return true;
        }

        void LateUpdate()
        {
            if (tipPoint == null) return;
            float now = Time.time;
            if (now < emitUntil)
            {
                samples.Add(new Sample { a = basePoint.position, b = tipPoint.position, t = now });
                if (samples.Count > MaxSamples) samples.RemoveAt(0);
            }
            while (samples.Count > 0 && now - samples[0].t > Life) samples.RemoveAt(0);
            Rebuild(now);
        }

        void Rebuild(float now)
        {
            mesh.Clear();
            if (samples.Count < 2) return;
            verts.Clear(); cols.Clear(); uvs.Clear(); tris.Clear();
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                float fade = 1f - Mathf.Clamp01((now - s.t) / Life);        // newest slice is brightest
                verts.Add(s.a); verts.Add(s.b);
                cols.Add(new Color(color.r, color.g, color.b, 0f));            // soft inner edge
                cols.Add(new Color(color.r, color.g, color.b, fade * 0.9f));   // bright outer edge at the tip
                uvs.Add(new Vector2(0.5f, 0.5f)); uvs.Add(new Vector2(0.5f, 0.5f)); // the bright middle of the glow texture
                if (i > 0)
                {
                    int k = i * 2;
                    tris.Add(k - 2); tris.Add(k - 1); tris.Add(k);
                    tris.Add(k); tris.Add(k - 1); tris.Add(k + 1);
                    // both faces, so it shows from either side of the swing
                    tris.Add(k); tris.Add(k - 1); tris.Add(k - 2);
                    tris.Add(k + 1); tris.Add(k - 1); tris.Add(k);
                }
            }
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (drawer != null) Destroy(drawer);
            if (mesh != null) Destroy(mesh);
        }

        void OnDisable() { samples.Clear(); if (mesh != null) mesh.Clear(); }
    }
}