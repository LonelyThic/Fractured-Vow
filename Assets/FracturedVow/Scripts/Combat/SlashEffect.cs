using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// The white arc of light that follows every sword swing: a fading trail swept around the knight on the hit
    /// frame (left to right, back again, and a wider golden arc for the finisher). The angle of each swing is set
    /// on PlayerCombat in the Inspector.
    /// </summary>
    public class SlashEffect : MonoBehaviour
    {
        float t;
        float duration = 0.2f;
        float fromAngle, toAngle;
        Quaternion baseRot;
        TrailRenderer[] trails;
        bool done;

        public static void Spawn(Transform owner, int comboIndex, Material template, float angle)
        {
            var root = new GameObject("SlashEffect");
            root.transform.SetParent(owner, false);
            root.transform.localPosition = new Vector3(0f, comboIndex == 2 ? 1.2f : 1.1f, 0.2f);
            root.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            bool finisher = comboIndex >= 2;
            Color c = finisher ? new Color(1f, 0.88f, 0.55f) : Color.white;
            var fx = root.AddComponent<SlashEffect>();
            // a wide soft arc and a thin bright edge along its outside
            fx.trails = new[]
            {
                Trail(root.transform, 1.25f, finisher ? 0.65f : 0.5f, new Color(c.r, c.g, c.b, 0.45f)),
                Trail(root.transform, 1.45f, 0.1f, new Color(1f, 1f, 1f, 0.95f)),
            };

            bool leftToRight = comboIndex % 2 == 0;
            float sweep = finisher ? 85f : 70f;
            fx.fromAngle = leftToRight ? -sweep : sweep;
            fx.toAngle = -fx.fromAngle;
            fx.duration = finisher ? 0.2f : 0.15f;
            fx.baseRot = root.transform.localRotation;
            root.transform.localRotation = fx.baseRot * Quaternion.Euler(0f, fx.fromAngle, 0f);
        }

        static TrailRenderer Trail(Transform root, float reach, float width, Color color)
        {
            var tip = new GameObject("Trail");
            tip.transform.SetParent(root, false);
            tip.transform.localPosition = new Vector3(0f, 0f, reach);
            var tr = tip.AddComponent<TrailRenderer>();
            tr.sharedMaterial = RuntimeVisuals.Glow;
            tr.time = 0.13f;
            tr.minVertexDistance = 0.04f;
            tr.widthMultiplier = width;
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            tr.alignment = LineAlignment.View;
            tr.textureMode = LineTextureMode.Stretch;
            tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tr.receiveShadows = false;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                      new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            return tr;
        }

        void Update()
        {
            if (done) return;
            t += Time.deltaTime / duration;
            float a = Mathf.Lerp(fromAngle, toAngle, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            transform.localRotation = baseRot * Quaternion.Euler(0f, a, 0f);
            if (t >= 1f)
            {
                // stop drawing and let the arc fade out where it is
                done = true;
                foreach (var tr in trails) if (tr != null) tr.emitting = false;
                transform.SetParent(null, true);
                Destroy(gameObject, 0.2f);
            }
        }
    }
}