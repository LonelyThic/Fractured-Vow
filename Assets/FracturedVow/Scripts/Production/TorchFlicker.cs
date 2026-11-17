using UnityEngine;

namespace FracturedVow
{
    /// <summary>Organic flicker for torch, brazier and lantern lights.</summary>
    [RequireComponent(typeof(Light))]
    public class TorchFlicker : MonoBehaviour
    {
        [Range(0f, 1f)] public float amount = 0.3f;
        public float speed = 8f;
        public float jitter = 0.03f;

        Light lt;
        float baseIntensity, seed;
        Vector3 basePos;

        void Awake()
        {
            lt = GetComponent<Light>();
            baseIntensity = lt.intensity;
            basePos = transform.localPosition;
            seed = Random.value * 100f;
            GameAudio.AttachLoop(gameObject, "loop_torch", 0.35f, 9f);
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed);
            lt.intensity = baseIntensity * (1f - amount + amount * 2f * n);
            transform.localPosition = basePos + new Vector3(n - 0.5f, Mathf.PerlinNoise(seed + 3f, Time.time * speed) - 0.5f, 0f) * jitter;
        }
    }
}
