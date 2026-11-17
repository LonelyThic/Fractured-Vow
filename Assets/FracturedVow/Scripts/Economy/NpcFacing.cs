using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Merchants, sellswords and the prisoner turn to face the knight when he comes close to talk, and turn back
    /// to where they were standing when he leaves. Added automatically by those NPCs.
    /// </summary>
    public class NpcFacing : MonoBehaviour
    {
        public float range = 4.5f;
        public float turnSpeed = 5f;

        Quaternion home;
        Transform player;

        public static void On(GameObject npc)
        {
            if (npc.GetComponent<NpcFacing>() == null) npc.AddComponent<NpcFacing>();
        }

        void Start() => home = transform.rotation;

        void Update()
        {
            if (player == null)
            {
                var pc = FindFirstObjectByType<PlayerController>();
                if (pc == null) return;
                player = pc.transform;
            }
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            Quaternion want = to.sqrMagnitude < range * range && to.sqrMagnitude > 0.04f ? Quaternion.LookRotation(to) : home;
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }
    }
}
