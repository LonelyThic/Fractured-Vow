using UnityEngine;

namespace FracturedVow
{
    [RequireComponent(typeof(Collider))]
    public class Checkpoint : MonoBehaviour
    {
        void Awake() { GetComponent<Collider>().isTrigger = true; }

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null && GameManager.Instance != null)
                GameManager.Instance.SetCheckpoint(transform.position + Vector3.up * 0.1f, player.transform.rotation);
        }
    }
}
