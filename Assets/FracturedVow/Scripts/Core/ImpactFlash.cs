using UnityEngine;

namespace FracturedVow
{
    public class ImpactFlash : MonoBehaviour
    {
        public float duration = 0.25f;
        public float endScale = 1.5f;
        float t;
        Vector3 start;

        void Start() { start = transform.localScale; }

        void Update()
        {
            t += Time.deltaTime / duration;
            transform.localScale = Vector3.Lerp(start, start * endScale, t);
            if (t >= 1f) Destroy(gameObject);
        }

        public static void Spawn(Vector3 pos, Color color, Material template, float size = 0.6f)
        {
            var go = RuntimeVisuals.Primitive(PrimitiveType.Sphere, template, color, false);
            go.name = "ImpactFlash";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<ImpactFlash>();
        }
    }
}
