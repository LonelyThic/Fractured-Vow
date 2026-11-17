using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>Floor vent: smokes as a warning, then erupts in a column of fire that hurts anything standing on it.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class FlameVent : MonoBehaviour
    {
        enum Phase { Idle, Warning, Active }

        public float damage = 18f;
        public float knockback = 3f;
        public float activeTime = 1.4f;
        public float idleTime = 3f;
        public float warningTime = 0.8f;
        public float startDelay = 0f;
        public float hitCooldown = 0.6f;

        public ParticleSystem fire;
        public ParticleSystem warnSmoke;
        public Light glow;
        public float glowIntensity = 4f;

        readonly HashSet<IDamageable> occupants = new HashSet<IDamageable>();
        readonly Dictionary<IDamageable, float> lastHit = new Dictionary<IDamageable, float>();
        readonly List<IDamageable> scratch = new List<IDamageable>();
        Phase phase = Phase.Idle;
        float timer;

        void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            timer = -startDelay;
            SetEmit(fire, false);
            SetEmit(warnSmoke, false);
            if (glow != null) glow.intensity = 0f;
        }

        void Update()
        {
            timer += Time.deltaTime;
            switch (phase)
            {
                case Phase.Idle:
                    if (timer >= idleTime) { phase = Phase.Warning; timer = 0f; SetEmit(warnSmoke, true); }
                    break;
                case Phase.Warning:
                    if (glow != null) glow.intensity = Mathf.Lerp(0f, glowIntensity * 0.3f, timer / warningTime);
                    if (timer >= warningTime)
                    {
                        phase = Phase.Active; timer = 0f; SetEmit(warnSmoke, false); SetEmit(fire, true);
                        GameAudio.Play("vent_fire", transform.position + Vector3.up, 0.9f);
                    }
                    break;
                case Phase.Active:
                    if (glow != null) glow.intensity = glowIntensity * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 12f, 0f));
                    Burn();
                    if (timer >= activeTime) { phase = Phase.Idle; timer = 0f; SetEmit(fire, false); if (glow != null) glow.intensity = 0f; }
                    break;
            }
        }

        void Burn()
        {
            scratch.Clear();
            foreach (var d in occupants) if ((d as Object) == null || d.IsDead) scratch.Add(d);
            foreach (var d in scratch) occupants.Remove(d);
            foreach (var d in occupants)
            {
                if (lastHit.TryGetValue(d, out float t) && Time.time - t < hitCooldown) continue;
                lastHit[d] = Time.time;
                d.TakeDamage(new DamageInfo(damage, transform.position, gameObject, knockback));
            }
        }

        static void SetEmit(ParticleSystem ps, bool on)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.enabled = on;
            if (on && !ps.isPlaying) ps.Play();
        }

        void OnTriggerEnter(Collider other)
        {
            var d = other.GetComponentInParent<IDamageable>();
            if (d != null && other.GetComponentInParent<WardenBoss>() == null) occupants.Add(d);
        }

        void OnTriggerExit(Collider other)
        {
            var d = other.GetComponentInParent<IDamageable>();
            if (d != null) occupants.Remove(d);
        }
    }
}
