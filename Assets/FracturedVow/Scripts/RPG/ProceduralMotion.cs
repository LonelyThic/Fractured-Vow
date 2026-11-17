using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Extra animation layered on top of a character's model (works with or without an Animator):
    /// dodge rolls, backsteps, hit flinches, enemy dodge hops, sprint lean, charge crouch and drinking.
    /// It only moves the model's root, so the Animator's own clips keep playing underneath.
    /// </summary>
    public class ProceduralMotion : MonoBehaviour
    {
        public Transform model;
        public float pivotHeight = 0.9f;

        Vector3 basePos;
        Quaternion baseRot;
        float rollStart = -99f, rollTime;
        bool rollBack;
        float flinch;
        Vector3 flinchAxis = Vector3.right;
        float hopStart = -99f, hopTime, hopHeight;
        float lean, leanTarget, charge, chargeTarget, drink, drinkTarget, guard, guardTarget;
        float swingStart = -99f, swingTime = 0.4f, swingDir = 1f;

        /// <summary>A body twist that follows a weapon swing (dir +1 / -1, bigger for heavy swings).</summary>
        public void Swing(float duration, float dir) { swingStart = Time.time; swingTime = Mathf.Max(0.05f, duration); swingDir = dir; }
        public bool Guard { set => guardTarget = value ? 1f : 0f; }

        /// <summary>0..1 walking speed. Models without a skeleton (static meshes) get a stepping bob and sway from it.</summary>
        public float Move { set => moveTarget = Mathf.Clamp01(value); }
        float move, moveTarget, stepPhase;
        bool rigless;

        [Tooltip("For models without a skeleton: bob and sway while moving, as a heavy stomping walk.")]
        public bool stomp;
        /// <summary>How fast the character is moving (0..1); drives the stomping walk.</summary>
        public float Move01 { get; set; }
        float stompPhase;

        public float Lean { set => leanTarget = value; }
        public float Charge { set => chargeTarget = Mathf.Clamp01(value); }
        public float Drink { set => drinkTarget = Mathf.Clamp01(value); }

        void Awake()
        {
            if (model == null) model = FindModel(transform);
            if (model != null) { basePos = model.localPosition; baseRot = model.localRotation; }
            rigless = model != null && model.GetComponentInChildren<SkinnedMeshRenderer>() == null;
        }

        static Transform FindModel(Transform root)
        {
            var anim = root.GetComponentInChildren<Animator>();
            if (anim != null && anim.transform != root) return anim.transform;
            var named = root.Find("Model");
            if (named != null) return named;
            foreach (Transform c in root) if (c.GetComponentInChildren<Renderer>() != null) return c;
            return null;
        }

        /// <summary>A forward roll (or a quick backstep when back is true).</summary>
        public void Roll(float duration, bool back = false)
        {
            rollStart = Time.time; rollTime = Mathf.Max(0.05f, duration); rollBack = back;
        }

        /// <summary>A short jolt away from a hit that came from 'from'.</summary>
        public void Flinch(Vector3 from)
        {
            Vector3 away = transform.position - from; away.y = 0f;
            if (away.sqrMagnitude < 0.001f) away = -transform.forward;
            Vector3 localAway = transform.InverseTransformDirection(away.normalized);
            flinchAxis = Vector3.Cross(Vector3.up, localAway).normalized;
            if (flinchAxis.sqrMagnitude < 0.01f) flinchAxis = Vector3.right;
            flinch = 1f;
        }

        public void Hop(float height, float duration)
        {
            hopStart = Time.time; hopHeight = height; hopTime = Mathf.Max(0.05f, duration);
        }

        void LateUpdate()
        {
            if (model == null) return;
            float dt = Time.deltaTime;
            Vector3 pos = basePos;
            Quaternion rot = baseRot;

            float rt = (Time.time - rollStart) / rollTime;
            if (rt >= 0f && rt <= 1f)
            {
                Quaternion r;
                if (rollBack)
                {
                    r = Quaternion.AngleAxis(-22f * Mathf.Sin(rt * Mathf.PI), Vector3.right);
                    pos += Vector3.up * 0.12f * Mathf.Sin(rt * Mathf.PI);
                }
                else
                {
                    r = Quaternion.AngleAxis(360f * Mathf.SmoothStep(0f, 1f, rt), Vector3.right);
                    pos += Vector3.down * 0.35f * Mathf.Sin(rt * Mathf.PI);
                }
                Vector3 pivot = Vector3.up * pivotHeight;
                pos += pivot - r * pivot;
                rot = r * rot;
            }

            float ht = (Time.time - hopStart) / hopTime;
            if (ht >= 0f && ht <= 1f) pos += Vector3.up * hopHeight * Mathf.Sin(ht * Mathf.PI);

            flinch = Mathf.MoveTowards(flinch, 0f, dt * 5f);
            if (flinch > 0f) rot = Quaternion.AngleAxis(-20f * flinch * flinch, flinchAxis) * rot;

            float st = (Time.time - swingStart) / swingTime;
            if (st >= 0f && st <= 1f)
            {
                // wind back, then whip through
                float twist = (st < 0.35f ? -Mathf.Sin(st / 0.35f * Mathf.PI * 0.5f) : Mathf.Sin((st - 0.35f) / 0.65f * Mathf.PI)) * 24f * swingDir;
                rot = Quaternion.AngleAxis(twist, Vector3.up) * rot;
                pos += Vector3.forward * 0.12f * Mathf.Sin(st * Mathf.PI);
            }
            guard = Mathf.Lerp(guard, guardTarget, 1f - Mathf.Exp(-12f * dt));
            if (guard > 0.01f) { rot = Quaternion.Euler(-5f * guard, -12f * guard, 0f) * rot; pos += Vector3.down * 0.06f * guard; }

            move = Mathf.Lerp(move, moveTarget, 1f - Mathf.Exp(-10f * dt));
            if (rigless && move > 0.02f)
            {
                stepPhase += dt * (6f + move * 6f);
                pos += Vector3.up * Mathf.Abs(Mathf.Sin(stepPhase)) * 0.09f * move;
                rot = Quaternion.Euler(4f * move, 0f, Mathf.Sin(stepPhase) * 6f * move) * rot;
            }
            else if (rigless)
            {
                // idle breathing for static models
                pos += Vector3.up * Mathf.Sin(Time.time * 2f) * 0.015f;
            }

            if (stomp)
            {
                float m = Mathf.Clamp01(Move01);
                stompPhase += dt * (5f + m * 5f);
                float step = Mathf.Abs(Mathf.Sin(stompPhase));
                pos += Vector3.up * (step * 0.14f * m + Mathf.Sin(Time.time * 1.6f) * 0.015f);
                rot = Quaternion.Euler(m * 6f, 0f, Mathf.Sin(stompPhase) * 7f * m) * rot;
            }

            lean = Mathf.Lerp(lean, leanTarget, 1f - Mathf.Exp(-8f * dt));
            charge = Mathf.Lerp(charge, chargeTarget, 1f - Mathf.Exp(-10f * dt));
            drink = Mathf.Lerp(drink, drinkTarget, 1f - Mathf.Exp(-10f * dt));
            rot = Quaternion.Euler(lean - charge * 8f + drink * -14f, 0f, 0f) * rot;
            pos += Vector3.down * (charge * 0.12f);

            model.localPosition = pos;
            model.localRotation = rot;
        }

        public static ProceduralMotion On(GameObject go)
        {
            var m = go.GetComponent<ProceduralMotion>();
            return m != null ? m : go.AddComponent<ProceduralMotion>();
        }
    }
}
