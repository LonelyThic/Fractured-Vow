using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Spell bolt. Uses a sphere cast each frame (no Rigidbody) so it never tunnels through walls.
    /// Looks: a glowing core with its own light, a particle trail (fire embers or frost glitter) and a burst on impact,
    /// all made in code so they match the rest of the game.
    /// </summary>
    public class MagicProjectile : MonoBehaviour
    {
        Vector3 direction;
        float speed, damage, knockback, slowMultiplier, slowDuration, radius;
        float life = 3f;
        Transform ignoreRoot;
        GameObject owner;
        Color color;
        Material template;
        bool frost;
        GameObject impactPrefab;

        public static MagicProjectile Spawn(Vector3 pos, Vector3 dir, GameObject owner, Color color, Material template,
            float damage, float knockback, float slowMultiplier, float slowDuration, float speed, float size,
            GameObject visualPrefab = null, GameObject impactPrefab = null, GameObject trailPrefab = null)
        {
            bool frost = slowDuration > 0f;
            // the knight's own spells always use the Particle Pack effects from the library (enemies keep their bolts)
            var lib = FVLibrary.Get;
            if (lib != null && owner != null && owner.GetComponent<PlayerController>() != null)
            {
                if (!frost)
                {
                    if (visualPrefab == null) visualPrefab = lib.fireBolt;
                    if (trailPrefab == null) trailPrefab = lib.fireTrail;
                    if (impactPrefab == null) impactPrefab = lib.fireImpact;
                }
                else if (impactPrefab == null) impactPrefab = lib.frostImpact;
            }
            var go = RuntimeVisuals.Primitive(PrimitiveType.Sphere, template, Color.Lerp(color, Color.white, 0.35f), false);
            go.name = "Spell_" + (frost ? "Frost" : "Fire");
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(dir));
            go.transform.localScale = frost ? new Vector3(size * 0.7f, size * 0.7f, size * 1.8f) : Vector3.one * size;

            // an outer glow shell around the core
            var shell = RuntimeVisuals.Primitive(PrimitiveType.Sphere, template, color, false);
            shell.name = "Glow";
            shell.transform.SetParent(go.transform, false);
            shell.transform.localScale = Vector3.one * 1.6f;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = 5f;
            light.intensity = 2.5f;

            GameFX.Trail(go.transform, color, frost);
            float inv = 1f / Mathf.Max(0.05f, go.transform.localScale.x);
            if (visualPrefab != null)
            {
                // a Particle Pack effect is the bolt itself (the burning ball of FireBall); the plain spheres are hidden
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
                var fx = Instantiate(visualPrefab, go.transform);
                fx.transform.localPosition = Vector3.zero;
                fx.transform.localRotation = Quaternion.identity;
                fx.transform.localScale = Vector3.one * 0.45f * inv;
                PrepareEffect(fx, false);
            }
            if (trailPrefab != null)
            {
                // a long flame and smoke trail streaming behind (RocketTrail), so it reads as a thrown fireball
                var tr = Instantiate(trailPrefab, go.transform);
                tr.transform.localPosition = Vector3.back * 0.15f * inv;
                tr.transform.localRotation = Quaternion.identity;
                tr.transform.localScale = Vector3.one * 0.5f * inv;
                PrepareEffect(tr, true);
            }


            var p = go.AddComponent<MagicProjectile>();
            p.impactPrefab = impactPrefab;
            p.direction = dir.normalized;
            p.owner = owner;
            p.ignoreRoot = owner.transform.root;
            p.friendly = owner.GetComponentInParent<PlayerController>() != null || owner.GetComponentInParent<CompanionAI>() != null;
            p.color = color;
            p.template = template;
            p.frost = frost;
            p.damage = damage;
            p.knockback = knockback;
            p.slowMultiplier = slowMultiplier;
            p.slowDuration = slowDuration;
            p.speed = speed;
            p.radius = size * 0.5f;
            return p;
        }

        bool friendly;

        void Update()
        {
            float step = speed * Time.deltaTime;
            RaycastHit best = default;
            bool found = false;
            foreach (var h in Physics.SphereCastAll(transform.position, radius, direction, step, ~0, QueryTriggerInteraction.Ignore))
            {
                if (ignoreRoot != null && h.transform.IsChildOf(ignoreRoot)) continue;
                // the knight's and his companions' spells pass through each other
                if (friendly && (h.collider.GetComponentInParent<PlayerController>() != null || h.collider.GetComponentInParent<CompanionAI>() != null)) continue;
                if (!found || h.distance < best.distance) { best = h; found = true; }
            }

            if (found) { Impact(best); return; }

            transform.position += direction * step;
            transform.Rotate(0f, 0f, 540f * Time.deltaTime, Space.Self);
            life -= Time.deltaTime;
            if (life <= 0f) { DetachTrails(); Destroy(gameObject); }
        }

        void Impact(RaycastHit hit)
        {
            var target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && !target.IsDead)
                target.TakeDamage(new DamageInfo(damage, transform.position, owner, knockback));

            if (slowDuration > 0f)
            {
                var slowable = hit.collider.GetComponentInParent<ISlowable>();
                if (slowable != null) slowable.ApplySlow(slowMultiplier, slowDuration);
            }

            Vector3 at = hit.point == Vector3.zero ? transform.position : hit.point;
            GameAudio.Play(frost ? "frost_impact" : "fire_impact", at, 0.9f);
            ImpactFlash.Spawn(at, color, template, frost ? 0.9f : 1.2f);
            GameFX.Explosion(at, color, frost);
            if (impactPrefab != null) PlayEffect(impactPrefab, at, 0.7f);
            DetachTrails();
            Destroy(gameObject);
        }

        /// <summary>Makes a Particle Pack prefab behave on a moving bolt: scaled with it, playing, trailing in world space.</summary>
        /// <summary>
        /// Particle Pack prefabs are demo scenes: the impact effects carry a target sphere with bullet-hole decals
        /// (pink for flesh, grey for metal) and some carry scripts. Only the particles are kept.
        /// </summary>
        public static void StripDemo(GameObject fx)
        {
            foreach (var mr in fx.GetComponentsInChildren<MeshRenderer>(true)) { mr.enabled = false; Destroy(mr); }
            foreach (var sr in fx.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { sr.enabled = false; Destroy(sr); }
            foreach (var mb in fx.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
        }

        static void PrepareEffect(GameObject fx, bool trail)
        {
            StripDemo(fx);
            foreach (var c in fx.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var a in fx.GetComponentsInChildren<AudioSource>()) a.volume *= GameSettings.SfxVolume;
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.prewarm = false;
                if (trail) main.simulationSpace = ParticleSystemSimulationSpace.World;
                ps.Play(false);
            }
        }

        /// <summary>Plays a one-shot copy of a particle effect prefab and cleans it up.</summary>
        public static void PlayEffect(GameObject prefab, Vector3 at, float scale)
        {
            var fx = Instantiate(prefab, at, Quaternion.identity);
            fx.transform.localScale *= scale;
            StripDemo(fx);
            foreach (var c in fx.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>()) { var m = ps.main; m.loop = false; }
            foreach (var a in fx.GetComponentsInChildren<AudioSource>()) a.volume *= GameSettings.SfxVolume;
            Destroy(fx, 4f);
        }

        /// <summary>Lets the particle trail fade out naturally instead of vanishing with the bolt.</summary>
        void DetachTrails()
        {
            foreach (var ps in GetComponentsInChildren<ParticleSystem>())
            {
                ps.transform.SetParent(null, true);
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(ps.gameObject, 2f);
            }
        }
    }
}
