using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Orbit camera for 3rd-person play: mouse/stick orbit, scroll zoom, smooth follow, collision so the
    /// camera never clips through walls. The boat swaps its target with SetTarget / ResetTarget.
    /// </summary>
    public class ThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public PlayerInputHandler input;

        [Header("Framing")]
        public Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);
        public Vector3 shoulderOffset = new Vector3(0.35f, 0f, 0f);
        public float distance = 4.5f;
        public float minDistance = 1.5f;
        public float maxDistance = 9f;
        public float zoomStep = 0.8f;

        [Header("Orbit")]
        public float minPitch = -35f;
        public float maxPitch = 70f;
        public float startPitch = 15f;

        [Header("Smoothing & collision")]
        public float followSharpness = 20f;
        public float collisionRadius = 0.25f;
        public LayerMask collisionMask = ~0;

        float yaw, pitch, currentDistance;

        [Header("Lock-on")]
        [Tooltip("Set by the player while locked on: the camera swings to keep the target in view.")]
        public Transform lockTarget;
        public float lockTurnSpeed = 8f;
        Vector3 smoothedPivot;
        Transform defaultTarget;
        Vector3 defaultOffset;
        float defaultDistance;

        void Start()
        {
            if (input == null && target != null) input = target.GetComponentInParent<PlayerInputHandler>();
            defaultTarget = target;
            defaultOffset = targetOffset;
            defaultDistance = distance;
            pitch = startPitch;
            currentDistance = distance;
            if (target != null)
            {
                yaw = target.eulerAngles.y;
                smoothedPivot = target.position + targetOffset;
            }
        }

        public void SetTarget(Transform newTarget, Vector3 offset, float newDistance)
        {
            target = newTarget;
            targetOffset = offset;
            distance = Mathf.Clamp(newDistance, minDistance, Mathf.Max(maxDistance, newDistance));
        }

        public void ResetTarget()
        {
            target = defaultTarget;
            targetOffset = defaultOffset;
            distance = defaultDistance;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            if (lockTarget != null && target != null)
            {
                Vector3 to = lockTarget.position - target.position; to.y = 0f;
                if (to.sqrMagnitude > 0.5f)
                {
                    float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                    yaw = Mathf.LerpAngle(yaw, wantYaw, 1f - Mathf.Exp(-lockTurnSpeed * dt));
                    pitch = Mathf.Lerp(pitch, 14f, 1f - Mathf.Exp(-4f * dt));
                }
                if (input != null) distance = Mathf.Clamp(distance - input.Zoom * zoomStep, minDistance, maxDistance);
            }
            else if (input != null)
            {
                yaw += input.Look.x;
                pitch -= input.Look.y;
                distance = Mathf.Clamp(distance - input.Zoom * zoomStep, minDistance, maxDistance);
            }
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            Vector3 pivotTarget = target.position + targetOffset;
            smoothedPivot = Vector3.Lerp(smoothedPivot, pivotTarget, 1f - Mathf.Exp(-followSharpness * dt));

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pivot = smoothedPivot + rot * shoulderOffset;
            Vector3 back = rot * Vector3.back;

            float allowed = distance;
            var hits = Physics.SphereCastAll(pivot, collisionRadius, back, distance, collisionMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.distance <= 0f || h.transform.IsChildOf(target.root)) continue;
                allowed = Mathf.Min(allowed, h.distance);
            }

            currentDistance = allowed < currentDistance
                ? allowed
                : Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-6f * dt));

            transform.SetPositionAndRotation(pivot + back * currentDistance, rot);
        }
    }
}
