using UnityEngine;

namespace FracturedVow
{
    /// <summary>Screen shake for impacts. Runs after the camera script so it adds on top of the follow.</summary>
    [DefaultExecutionOrder(1000)]
    public class CameraShake : MonoBehaviour
    {
        static CameraShake instance;
        float amplitude, until, duration = 1f;

        void Awake() { instance = this; }

        public static void Shake(float amp, float dur)
        {
            if (!GameSettings.CameraShake) return;
            if (instance == null)
            {
                var cam = Camera.main;
                if (cam == null) return;
                instance = cam.GetComponent<CameraShake>();
                if (instance == null) instance = cam.gameObject.AddComponent<CameraShake>();
            }
            if (Time.time < instance.until && instance.amplitude > amp) return;
            instance.amplitude = amp;
            instance.duration = Mathf.Max(0.01f, dur);
            instance.until = Time.time + dur;
        }

        void LateUpdate()
        {
            if (Time.time >= until) return;
            float k = (until - Time.time) / duration;
            transform.position += Random.insideUnitSphere * amplitude * k;
        }
    }
}
