using UnityEngine;

namespace FracturedVow
{
   [RequireComponent(typeof(Health))]
    public class TrainingDummy : MonoBehaviour, ISlowable
    {
        public float resetAfter = 3f;
        public Transform wobbleRoot;

        Health health;
        float wobble;
        Vector3 wobbleAxis = Vector3.right;

        void Awake()
        {
            health = GetComponent<Health>();
            if (wobbleRoot == null) wobbleRoot = transform;
        }

        void OnEnable() { health.Damaged += OnHit; health.Died += OnDied; }
        void OnDisable() { health.Damaged -= OnHit; health.Died -= OnDied; }

        void OnHit(DamageInfo info)
        {
            Vector3 dir = transform.position - info.sourcePosition;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f) wobbleAxis = Vector3.Cross(Vector3.up, dir.normalized);
            wobble = 1f;
        }

        void OnDied()
        {
            PrototypeHUD.Message("Dummy destroyed! It will rebuild itself.", 2f);
            Invoke(nameof(Rebuild), 2f);
        }

        void Rebuild() { health.Revive(); }

        void Update()
        {
            wobble = Mathf.MoveTowards(wobble, 0f, Time.deltaTime * 2f);
            float angle = Mathf.Sin(Time.time * 25f) * 15f * wobble;
            wobbleRoot.localRotation = Quaternion.AngleAxis(angle, wobbleRoot.parent != null ? wobbleRoot.parent.InverseTransformDirection(wobbleAxis) : wobbleAxis);

            if (!health.IsDead && health.Current < health.maxHealth && Time.time - health.LastDamageTime > resetAfter)
                health.Heal(health.maxHealth);
        }

        public void ApplySlow(float speedMultiplier, float duration)
        {
            PrototypeHUD.Message("Frost applied (slow " + Mathf.RoundToInt((1f - speedMultiplier) * 100f) + "% for " + duration.ToString("0.0") + "s)", 1.5f);
        }
    }
}
