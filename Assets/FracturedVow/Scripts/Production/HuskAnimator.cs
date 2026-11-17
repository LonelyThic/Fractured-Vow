using UnityEngine;

namespace FracturedVow
{
    /// <summary>Drives a PrimitiveRig from EnemyAI's state: walk, wind-up, strike, stagger, death, sleeping.</summary>
    [RequireComponent(typeof(EnemyAI))]
    public class HuskAnimator : MonoBehaviour
    {
        public PrimitiveRig rig;
        [Tooltip("Kneel while idle until the knight is spotted.")]
        public bool sleepWhenIdle;

        EnemyAI ai;
        Vector3 lastPos;
        EnemyAI.State lastState;
        float stateStart;

        void Awake()
        {
            ai = GetComponent<EnemyAI>();
            lastPos = transform.position;
        }

        void Update()
        {
            if (rig == null) return;
            var s = ai.CurrentState;
            if (s != lastState) { lastState = s; stateStart = Time.time; }
            float t = Time.time - stateStart;

            float speed = (transform.position - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPos = transform.position;
            rig.moveSpeed01 = Mathf.Clamp01(speed / Mathf.Max(0.1f, ai.chaseSpeed));

            switch (s)
            {
                case EnemyAI.State.Idle: rig.pose = sleepWhenIdle ? PrimitiveRig.Pose.Kneel : PrimitiveRig.Pose.Idle; break;
                case EnemyAI.State.Attack: rig.pose = t < ai.attackWindup ? PrimitiveRig.Pose.WindupSide : PrimitiveRig.Pose.StrikeSide; break;
                case EnemyAI.State.Stagger: rig.pose = PrimitiveRig.Pose.Stagger; break;
                case EnemyAI.State.Dead: rig.pose = PrimitiveRig.Pose.Dead; break;
                default: rig.pose = PrimitiveRig.Pose.Idle; break;
            }
        }
    }
}
