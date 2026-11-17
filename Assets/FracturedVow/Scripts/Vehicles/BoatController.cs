using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Arcade boat from the GDD: plain Rigidbody, tank-style controls (W/S throttle, A/D steer), solid
    /// collisions, no buoyancy simulation. F boards; F again near the shore disembarks. The camera
    /// swaps to the boat while driving. Spells still work from the boat (light combat).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BoatController : MonoBehaviour, IInteractable
    {
        public WaterZone water;
        public Transform seat;

        [Header("Handling")]
        public float acceleration = 9f;
        public float reverseAcceleration = 5f;
        public float maxSpeed = 9f;
        public float maxReverseSpeed = 4f;
        public float turnSpeed = 75f;
        [Range(0f, 1f)] public float turnAtRest = 0.45f;
        [Range(0f, 1f)] public float keelGrip = 0.9f;

        [Header("Docking & camera")]
        public float shoreReach = 4f;
        [Tooltip("Optional. If set, the knight can only step off within dockReach of one of these points.")]
        public Transform[] docks;
        public float dockReach = 6f;
        public float edgeMargin = 1.7f;
        public float cameraDistance = 8f;
        public Vector3 cameraOffset = new Vector3(0f, 1.6f, 0f);

        public bool IsOccupied => driver != null;
        public PlayerInteractor Driver => driver;
        public float Speed => rb != null ? rb.linearVelocity.magnitude : 0f;

        Rigidbody rb;
        PlayerInteractor driver;
        ThirdPersonCamera cam;
        int boardedFrame;
        float throttle, steer;

        public string InteractionPrompt => "Board boat";

        Vector3 startPos;
        Quaternion startRot;

        void Start()
        {
            startPos = transform.position;
            startRot = transform.rotation;
        }

        /// <summary>Every boat in the level (companions look for the one the knight is rowing).</summary>
        public static readonly List<BoatController> All = new List<BoatController>();

        void OnEnable() { GameManager.Respawned += ResetToStart; All.Add(this); }
        void OnDisable() { GameManager.Respawned -= ResetToStart; All.Remove(this); }

        /// <summary>Back to its mooring (after the knight respawns).</summary>
        public void ResetToStart()
        {
            if (driver != null) ForceExit();
            if (rb == null) rb = GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = startPos;
            rb.rotation = startRot;
            transform.SetPositionAndRotation(startPos, startRot);
        }
        public bool CanInteract(PlayerInteractor interactor) => driver == null;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            rb.linearDamping = 0.8f;
            rb.angularDamping = 3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            if (seat == null) seat = transform;
        }

        public void Interact(PlayerInteractor who)
        {
            if (driver != null) return;
            driver = who;
            boardedFrame = Time.frameCount;

            who.Controller.SetControlEnabled(false);
            who.transform.SetParent(seat, true);
            who.transform.localPosition = Vector3.zero;
            who.transform.localRotation = Quaternion.identity;
            if (who.Combat != null) who.Combat.combatEnabled = false;
            who.enabled = false;

            cam = Camera.main != null ? Camera.main.GetComponent<ThirdPersonCamera>() : null;
            if (cam != null) cam.SetTarget(transform, cameraOffset, cameraDistance);

            PrototypeHUD.Message("W/S throttle, A/D steer, E cast. Press F near the shore to step off.", 4f);
        }

        void Update()
        {
            if (driver == null) { throttle = steer = 0f; return; }
            if (driver.Health != null && driver.Health.IsDead) { ForceExit(); return; }

            throttle = driver.Input.Move.y;
            steer = driver.Input.Move.x;

            if (driver.Input.InteractPressed && Time.frameCount != boardedFrame) TryExit();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 v = rb.linearVelocity;
            float forwardSpeed = Vector3.Dot(v, transform.forward);

            if (throttle > 0.05f && forwardSpeed < maxSpeed)
                rb.AddForce(transform.forward * throttle * acceleration, ForceMode.Acceleration);
            else if (throttle < -0.05f && forwardSpeed > -maxReverseSpeed)
                rb.AddForce(transform.forward * throttle * reverseAcceleration, ForceMode.Acceleration);

            if (Mathf.Abs(steer) > 0.05f)
            {
                float speedFactor = Mathf.Lerp(turnAtRest, 1f, Mathf.Abs(forwardSpeed) / maxSpeed);
                float reverse = forwardSpeed < -0.2f ? -1f : 1f;
                rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, steer * turnSpeed * speedFactor * reverse * dt, 0f));
            }

            // keel: kill most sideways drift so it feels like a boat, not an ice cube
            float lateral = Vector3.Dot(rb.linearVelocity, transform.right);
            rb.linearVelocity -= transform.right * lateral * keelGrip * dt * 5f;

            ClampToWater();
        }

        void ClampToWater()
        {
            if (water == null) return;
            Bounds b = water.WorldBounds;
            Vector3 p = rb.position;
            Vector3 v = rb.linearVelocity;
            bool clamped = false;

            if (p.x < b.min.x + edgeMargin) { p.x = b.min.x + edgeMargin; v.x = Mathf.Max(0f, v.x); clamped = true; }
            if (p.x > b.max.x - edgeMargin) { p.x = b.max.x - edgeMargin; v.x = Mathf.Min(0f, v.x); clamped = true; }
            if (p.z < b.min.z + edgeMargin) { p.z = b.min.z + edgeMargin; v.z = Mathf.Max(0f, v.z); clamped = true; }
            if (p.z > b.max.z - edgeMargin) { p.z = b.max.z - edgeMargin; v.z = Mathf.Min(0f, v.z); clamped = true; }

            if (clamped) { rb.position = p; rb.linearVelocity = v; }
        }

        void TryExit()
        {
            if (docks != null && docks.Length > 0)
            {
                Transform best = null;
                float bestDist = dockReach;
                foreach (var d in docks)
                {
                    if (d == null || !d.gameObject.activeInHierarchy) continue; // a sunken platform's docks are gone
                    Vector3 off = d.position - transform.position; off.y = 0f;
                    if (off.magnitude < bestDist) { bestDist = off.magnitude; best = d; }
                }
                if (best == null)
                {
                    PrototypeHUD.Message("Steer up to a dock to step off.", 2f);
                    return;
                }
                Exit(best.position);
                return;
            }

            if (water == null) { Exit(transform.position + transform.right * 2f); return; }

            Bounds b = water.WorldBounds;
            Vector3 p = transform.position;
            float toMinX = p.x - b.min.x, toMaxX = b.max.x - p.x, toMinZ = p.z - b.min.z, toMaxZ = b.max.z - p.z;
            float nearest = Mathf.Min(Mathf.Min(toMinX, toMaxX), Mathf.Min(toMinZ, toMaxZ));

            if (nearest > shoreReach)
            {
                PrototypeHUD.Message("Too far from shore - steer to the edge of the water to step off.", 2f);
                return;
            }

            Vector3 exit = p;
            if (nearest == toMinX) exit.x = b.min.x - 1.2f;
            else if (nearest == toMaxX) exit.x = b.max.x + 1.2f;
            else if (nearest == toMinZ) exit.z = b.min.z - 1.2f;
            else exit.z = b.max.z + 1.2f;

            Exit(exit);
        }

        void Exit(Vector3 position)
        {
            float groundY = 0f;
            if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;
            position.y = groundY + 0.05f;

            Vector3 face = position - transform.position;
            face.y = 0f;
            Quaternion rot = face.sqrMagnitude > 0.01f ? Quaternion.LookRotation(face) : transform.rotation;

            var who = driver;
            ReleaseDriver();
            who.Controller.Teleport(position, rot);
            PrototypeHUD.Message("Stepped ashore", 1.5f);
        }

        /// <summary>Used on death/respawn: frees the player without moving them.</summary>
        public void ForceExit()
        {
            if (driver == null) return;
            ReleaseDriver();
        }

        void ReleaseDriver()
        {
            var who = driver;
            driver = null;
            throttle = steer = 0f;
            who.transform.SetParent(null, true);
            who.Controller.SetControlEnabled(true);
            if (who.Combat != null) who.Combat.combatEnabled = true;
            who.enabled = true;
            if (cam != null) cam.ResetTarget();
        }
    }
}
