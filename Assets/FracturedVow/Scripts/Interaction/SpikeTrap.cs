using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Floor spike trap. Cycling = fires on a timer. Pressure plate = fires shortly after something steps on it.
    /// The plate changes colour to warn the player before the spikes come up. Hurts enemies too.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SpikeTrap : MonoBehaviour
    {
        public enum TrapMode { Cycling, PressurePlate }
        enum Phase { Idle, Warning, Active }

        public TrapMode mode = TrapMode.Cycling;
        public float damage = 15f;
        public float knockback = 3f;
        public float activeTime = 1f;
        public float idleTime = 1.6f;
        public float warningTime = 0.5f;
        public float startDelay = 0f;
        public float hitCooldown = 0.75f;

        [Header("Visuals")]
        public Transform spikes;
        public float loweredY = -0.85f;
        public float raisedY = 0f;
        public Renderer plateRenderer;
        public Color idleColor = new Color(1f, 0.85f, 0.1f);
        public Color warnColor = new Color(1f, 0.55f, 0f);
        public Color activeColor = new Color(1f, 0.15f, 0.05f);

        readonly HashSet<IDamageable> occupants = new HashSet<IDamageable>();
        readonly Dictionary<IDamageable, float> lastHit = new Dictionary<IDamageable, float>();
        readonly List<IDamageable> scratch = new List<IDamageable>();
        Phase phase = Phase.Idle;
        float timer;
        MaterialPropertyBlock block;

        void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            block = new MaterialPropertyBlock();
            timer = -startDelay;
        }

        void Update()
        {
            timer += Time.deltaTime;
            CleanOccupants();

            switch (phase)
            {
                case Phase.Idle:
                    bool go = mode == TrapMode.Cycling ? timer >= idleTime - warningTime : occupants.Count > 0 && timer >= 0.5f;
                    if (go) SetPhase(Phase.Warning);
                    break;
                case Phase.Warning:
                    if (timer >= warningTime) SetPhase(Phase.Active);
                    break;
                case Phase.Active:
                    DamageOccupants();
                    if (timer >= activeTime) SetPhase(Phase.Idle);
                    break;
            }

            if (spikes != null)
            {
                float targetY = phase == Phase.Active ? raisedY : loweredY;
                var p = spikes.localPosition;
                float speed = phase == Phase.Active ? 20f : 4f;
                spikes.localPosition = new Vector3(p.x, Mathf.MoveTowards(p.y, targetY, speed * Time.deltaTime), p.z);
            }

            if (plateRenderer != null)
            {
                Color c = phase == Phase.Active ? activeColor
                        : phase == Phase.Warning ? Color.Lerp(idleColor, warnColor, Mathf.PingPong(Time.time * 10f, 1f))
                        : idleColor;
                RuntimeVisuals.SetRendererColor(plateRenderer, c, block);
            }
        }

        void SetPhase(Phase p)
        {
            if (p == Phase.Active) GameAudio.Play("spike_trap", transform.position + Vector3.up * 0.3f, 0.8f);
            phase = p;
            timer = 0f;
        }

        void DamageOccupants()
        {
            foreach (var d in occupants)
            {
                if (lastHit.TryGetValue(d, out float t) && Time.time - t < hitCooldown) continue;
                lastHit[d] = Time.time;
                d.TakeDamage(new DamageInfo(damage, transform.position, gameObject, knockback));
            }
        }

        void CleanOccupants()
        {
            scratch.Clear();
            foreach (var d in occupants)
                if ((d as Object) == null || d.IsDead) scratch.Add(d);
            foreach (var d in scratch) occupants.Remove(d);
        }

        void OnTriggerEnter(Collider other)
        {
            var d = other.GetComponentInParent<IDamageable>();
            if (d != null) occupants.Add(d);
        }

        void OnTriggerExit(Collider other)
        {
            var d = other.GetComponentInParent<IDamageable>();
            if (d != null) occupants.Remove(d);
        }
    }
}
