using UnityEngine;

namespace FracturedVow
{
    public class SlashEffect : MonoBehaviour
    {
        float t;
        float duration = 0.2f;
        float fromAngle, toAngle;
        Quaternion baseRot;
        Transform blade;

        public static void Spawn(Transform owner, int comboIndex, Material template, float angle)
        {
            var root = new GameObject("SlashEffect");
            root.transform.SetParent(owner, false);
            root.transform.localPosition = new Vector3(0f, comboIndex == 2 ? 1.2f : 1.1f, 0.2f);
            // alternate swing direction per combo hit; finisher is an overhead chop
            //root.transform.localRotation = comboIndex == 2 ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            // changed code to allow direct control of angle of each slash in Unity
            root.transform.localRotation = Quaternion.Euler(0f, 0f, angle);

            Color c = comboIndex == 2 ? new Color(1f, 0.85f, 0.3f) : new Color(0.95f, 0.95f, 1f);
            var b = RuntimeVisuals.Primitive(PrimitiveType.Cube, template, c, false);
            b.name = "Blade";
            b.transform.SetParent(root.transform, false);
            b.transform.localScale = new Vector3(0.06f, 0.06f, 1.4f);
            b.transform.localPosition = new Vector3(0f, 0f, 0.8f);

            var fx = root.AddComponent<SlashEffect>();
            fx.blade = b.transform;
            bool leftToRight = comboIndex % 2 == 0;
            fx.fromAngle = leftToRight ? -70f : 70f;
            fx.toAngle = -fx.fromAngle;
            fx.duration = comboIndex == 2 ? 0.28f : 0.2f;
            fx.baseRot = root.transform.localRotation;
            root.transform.localRotation = fx.baseRot * Quaternion.Euler(0f, fx.fromAngle, 0f);
        }

        void Update()
        {
            t += Time.deltaTime / duration;
            float a = Mathf.Lerp(fromAngle, toAngle, Mathf.SmoothStep(0f, 1f, t));
            transform.localRotation = baseRot * Quaternion.Euler(0f, a, 0f);
            if (blade != null) blade.localScale = new Vector3(0.06f, 0.06f, 1.4f) * (1f - t * 0.3f);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
