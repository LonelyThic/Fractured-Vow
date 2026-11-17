using UnityEngine;

namespace FracturedVow
{
    /// <summary>Slow glowing orb fired by the Inquisitor. Steer the boat out of its path.</summary>
    public class InquisitorOrb : MonoBehaviour
    {
        Vector3 velocity;
        float damage;
        float life = 7f;
        IBossHitter boss;
        Transform player;
        Material template;
        static readonly Color OrbColor = new Color(0.55f, 1f, 0.7f);

        public static void Launch(Vector3 pos, Vector3 velocity, float damage, Material template, IBossHitter boss, Transform player)
        {
            var go = RuntimeVisuals.Primitive(PrimitiveType.Sphere, template, OrbColor, false);
            go.name = "Inquisitor Orb";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.9f;
            var lt = go.AddComponent<Light>();
            lt.type = LightType.Point; lt.color = OrbColor; lt.range = 6f; lt.intensity = 2.5f;
            var o = go.AddComponent<InquisitorOrb>();
            o.velocity = velocity; o.damage = damage; o.boss = boss; o.player = player; o.template = template;
        }

        void Update()
        {
            transform.position += velocity * Time.deltaTime;
            life -= Time.deltaTime;
            if (player != null && (player.position + Vector3.up * 0.8f - transform.position).magnitude < 1.4f)
            {
                if ((boss as Object) != null) boss.HitPlayer(damage, velocity, 4f);
                Explode();
                return;
            }
            if (transform.position.y < -0.3f || life <= 0f) Explode();
        }

        void Explode()
        {
            ImpactFlash.Spawn(transform.position, OrbColor, template, 1.6f);
            Destroy(gameObject);
        }
    }
}
