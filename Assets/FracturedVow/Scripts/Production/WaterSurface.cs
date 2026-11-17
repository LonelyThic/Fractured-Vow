using UnityEngine;

namespace FracturedVow
{
    /// <summary>Slowly drifts the water texture and bobs the surface so the cistern feels alive.</summary>
    [RequireComponent(typeof(Renderer))]
    public class WaterSurface : MonoBehaviour
    {
        public Vector2 scrollSpeed = new Vector2(0.015f, 0.03f);
        public float bobHeight = 0.015f;

        Material mat;
        float baseY;

        void Awake()
        {
            mat = GetComponent<Renderer>().material;
            baseY = transform.position.y;
            GameAudio.AttachLoop(gameObject, "loop_water", 0.55f, 26f);
        }

        void Update()
        {
            if (mat != null) mat.mainTextureOffset = scrollSpeed * Time.time;
            var p = transform.position;
            transform.position = new Vector3(p.x, baseY + Mathf.Sin(Time.time * 0.8f) * bobHeight, p.z);
        }
    }
}
