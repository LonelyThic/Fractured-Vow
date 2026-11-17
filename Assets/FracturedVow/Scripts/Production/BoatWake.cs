using UnityEngine;

namespace FracturedVow
{
    /// <summary>Boat polish: wake spray scales with speed, and the hull rocks with waves and turns.</summary>
    public class BoatWake : MonoBehaviour
    {
        public BoatController boat;
        public ParticleSystem wake;
        public Transform visual;
        public float maxRate = 40f;
        public float rockAmount = 2.5f;

        Rigidbody rb;
        float lastYaw;

        void Awake()
        {
            if (boat == null) boat = GetComponent<BoatController>();
            rb = GetComponent<Rigidbody>();
            lastYaw = transform.eulerAngles.y;
            oars = GameAudio.AttachLoop(gameObject, "loop_oars", 0.6f, 18f);
        }

        LoopSound oars;

        void Update()
        {
            float speed = rb != null ? rb.linearVelocity.magnitude : 0f;
            if (oars != null) oars.level = Mathf.Clamp01(speed / 6f);
            if (wake != null)
            {
                var em = wake.emission;
                em.rateOverTime = Mathf.Lerp(0f, maxRate, speed / 9f);
            }
            if (visual != null)
            {
                float yaw = transform.eulerAngles.y;
                float turnRate = Mathf.DeltaAngle(lastYaw, yaw) / Mathf.Max(Time.deltaTime, 0.0001f);
                lastYaw = yaw;
                float roll = Mathf.Sin(Time.time * 1.3f) * rockAmount - Mathf.Clamp(turnRate * 0.05f, -6f, 6f);
                float pitch = Mathf.Sin(Time.time * 0.9f + 1f) * rockAmount * 0.6f - speed * 0.4f;
                visual.localRotation = Quaternion.Slerp(visual.localRotation, Quaternion.Euler(pitch, 0f, roll), 4f * Time.deltaTime);
            }
        }
    }
}
