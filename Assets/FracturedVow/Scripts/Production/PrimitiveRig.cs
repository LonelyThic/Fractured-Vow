using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Procedural animation for characters built from simple parts (husks, the Warden).
    /// A controller sets moveSpeed01 and pose; the rig blends limbs toward each pose every frame.
    /// Hierarchy: Hips -> (Leg L, Leg R, Torso -> (Head, Arm L, Arm R)). Character faces +Z.
    /// </summary>
    public class PrimitiveRig : MonoBehaviour
    {
        public enum Pose { Idle, WindupSide, StrikeSide, WindupOverhead, StrikeOverhead, Lean, Stagger, Kneel, Dead, Talk }

        public Transform hips, torso, head, armL, armR, legL, legR;
        [Tooltip("Steps per second at full speed")] public float strideFrequency = 1.5f;
        public float sharpness = 12f;

        [HideInInspector] public float moveSpeed01;
        /// <summary>Extra head turn in degrees (an NPC looking toward the knight).</summary>
        [HideInInspector] public float headYaw;
        [HideInInspector] public Pose pose = Pose.Idle;

        float phase;
        Vector3 hipsBase;

        void Awake()
        {
            if (hips != null) hipsBase = hips.localPosition;
            phase = Random.value * 10f;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float m = Mathf.Clamp01(moveSpeed01);
            phase += dt * strideFrequency * Mathf.PI * 2f * Mathf.Lerp(0.7f, 1.3f, m);
            float swing = Mathf.Sin(phase) * 38f * m;
            float breathe = Mathf.Sin(Time.time * 2f + phase * 0.1f) * 2f;

            Vector3 tLegL = new Vector3(swing, 0f, 0f);
            Vector3 tLegR = new Vector3(-swing, 0f, 0f);
            Vector3 tArmL = new Vector3(-swing * 0.7f, 0f, -6f);
            Vector3 tArmR = new Vector3(swing * 0.7f, 0f, 6f);
            Vector3 tTorso = new Vector3(6f * m + breathe * 0.5f, 0f, 0f);
            Vector3 tHead = new Vector3(-breathe * 0.5f, Mathf.Sin(Time.time * 0.5f + phase) * 6f * (1f - m), 0f);
            float hipDrop = Mathf.Abs(Mathf.Cos(phase)) * 0.04f * m;
            float k = sharpness;

            switch (pose)
            {
                case Pose.WindupSide:
                    tArmR = new Vector3(-70f, 0f, 75f); tTorso = new Vector3(0f, -35f, 0f); break;
                case Pose.StrikeSide:
                    tArmR = new Vector3(-85f, 0f, -35f); tTorso = new Vector3(12f, 40f, 0f); k = 28f; break;
                case Pose.WindupOverhead:
                    tArmL = new Vector3(-165f, 0f, -12f); tArmR = new Vector3(-165f, 0f, 12f);
                    tTorso = new Vector3(-14f, 0f, 0f); tHead = new Vector3(-15f, 0f, 0f); break;
                case Pose.StrikeOverhead:
                    tArmL = new Vector3(-45f, 0f, -8f); tArmR = new Vector3(-45f, 0f, 8f);
                    tTorso = new Vector3(38f, 0f, 0f); hipDrop = 0.18f; k = 30f; break;
                case Pose.Lean:
                    tTorso = new Vector3(32f, 0f, 0f); tArmL = new Vector3(35f, 0f, -15f); tArmR = new Vector3(35f, 0f, 15f);
                    tHead = new Vector3(-20f, 0f, 0f); break;
                case Pose.Stagger:
                    tTorso = new Vector3(-22f, 0f, 10f); tHead = new Vector3(-25f, 0f, 0f);
                    tArmL = new Vector3(-30f, 0f, -35f); tArmR = new Vector3(-30f, 0f, 35f); k = 20f; break;
                case Pose.Kneel:
                    hipDrop = 0.35f; tLegL = new Vector3(-60f, 0f, 0f); tLegR = new Vector3(40f, 0f, 0f);
                    tTorso = new Vector3(22f, 0f, 0f); tHead = new Vector3(28f + Mathf.Sin(Time.time * 0.7f + phase) * 4f, headYaw, 0f);
                    tArmL = new Vector3(-10f, 0f, -4f); tArmR = new Vector3(-10f, 0f, 4f); k = 6f; break;
                case Pose.Talk:
                {
                    // kneeling, head raised toward the listener, one hand gesturing as he speaks
                    float g = Mathf.Sin(Time.time * 3.1f + phase), g2 = Mathf.Sin(Time.time * 1.7f + phase);
                    hipDrop = 0.35f; tLegL = new Vector3(-60f, 0f, 0f); tLegR = new Vector3(40f, 0f, 0f);
                    tTorso = new Vector3(6f + g2 * 3f, headYaw * 0.3f, 0f);
                    tHead = new Vector3(-6f + g * 4f, headYaw, g2 * 4f);
                    tArmR = new Vector3(-65f + g * 18f, 0f, 22f + g2 * 8f);
                    tArmL = new Vector3(-25f + g2 * 8f, 0f, -12f);
                    k = 8f; break;
                }
                case Pose.Dead:
                    hipDrop = 0.55f; tLegL = new Vector3(-80f, 0f, -8f); tLegR = new Vector3(-80f, 0f, 8f);
                    tTorso = new Vector3(60f, 0f, 0f); tHead = new Vector3(35f, 0f, 0f);
                    tArmL = new Vector3(15f, 0f, -20f); tArmR = new Vector3(15f, 0f, 20f); k = 8f; break;
            }

            float a = 1f - Mathf.Exp(-k * dt);
            Rot(legL, tLegL, a); Rot(legR, tLegR, a);
            Rot(armL, tArmL, a); Rot(armR, tArmR, a);
            Rot(torso, tTorso, a); Rot(head, tHead, a);
            if (hips != null) hips.localPosition = Vector3.Lerp(hips.localPosition, hipsBase - Vector3.up * hipDrop, a);
        }

        static void Rot(Transform t, Vector3 euler, float a)
        {
            if (t != null) t.localRotation = Quaternion.Slerp(t.localRotation, Quaternion.Euler(euler), a);
        }
    }
}
