using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>Always-dangerous area, such as the spiked pit. Damages anything inside on an interval.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class DamageZone : MonoBehaviour
    {
        public float damage = 20f;
        public float interval = 0.8f;
        public float knockback = 2f;

        readonly Dictionary<IDamageable, float> inside = new Dictionary<IDamageable, float>();
        readonly List<IDamageable> keys = new List<IDamageable>();

        void Awake() { GetComponent<BoxCollider>().isTrigger = true; }

        void Update()
        {
            keys.Clear();
            keys.AddRange(inside.Keys);
            foreach (var d in keys)
            {
                if ((d as Object) == null || d.IsDead) { inside.Remove(d); continue; }
                if (Time.time < inside[d]) continue;
                inside[d] = Time.time + interval;
                d.TakeDamage(new DamageInfo(damage, transform.position + Vector3.down, gameObject, knockback));
            }
        }

        void OnTriggerEnter(Collider other)
        {
            var d = other.GetComponentInParent<IDamageable>();
            if (d != null && !inside.ContainsKey(d)) inside[d] = Time.time;
        }

        void OnTriggerExit(Collider other)
        {
            var d = other.GetComponentInParent<IDamageable>();
            if (d != null) inside.Remove(d);
        }
    }
}
