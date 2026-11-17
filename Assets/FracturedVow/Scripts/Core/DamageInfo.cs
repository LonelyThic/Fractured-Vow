using UnityEngine;

namespace FracturedVow
{
    public struct DamageInfo
    {
        public float amount;
        public Vector3 sourcePosition;
        public GameObject attacker;
        public float knockback;

        public DamageInfo(float amount, Vector3 sourcePosition, GameObject attacker, float knockback = 0f)
        {
            this.amount = amount;
            this.sourcePosition = sourcePosition;
            this.attacker = attacker;
            this.knockback = knockback;
        }
    }

    public interface IDamageable
    {
        void TakeDamage(DamageInfo info);
        bool IsDead { get; }
    }

    public interface IBlocker
    {
        bool TryBlock(ref DamageInfo info);
    }

    public interface ISlowable
    {
        void ApplySlow(float speedMultiplier, float duration);
    }
}
