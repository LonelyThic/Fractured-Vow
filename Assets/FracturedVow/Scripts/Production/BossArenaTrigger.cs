using UnityEngine;

namespace FracturedVow
{
    /// <summary>Wakes the boss when the knight enters the arena floor.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class BossArenaTrigger : MonoBehaviour
    {
        public WardenBoss boss;

        void Awake() { GetComponent<BoxCollider>().isTrigger = true; }

        void OnTriggerEnter(Collider other)
        {
            if (boss != null && other.GetComponentInParent<PlayerController>() != null) boss.Activate();
        }
    }
}
