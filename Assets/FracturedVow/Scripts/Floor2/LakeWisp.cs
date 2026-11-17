using UnityEngine;

namespace FracturedVow
{
    /// <summary>Floating memory wisp on the boss lake. Sail through it to restore mana and a little health.</summary>
    public class LakeWisp : MonoBehaviour
    {
        float mana, heal, life = 14f, baseY;
        Transform player;
        Material template;
        static readonly Color WispColor = new Color(0.45f, 0.65f, 1f);

        public static void Spawn(Vector3 pos, float mana, float heal, Material template)
        {
            var go = RuntimeVisuals.Primitive(PrimitiveType.Sphere, template, WispColor, false);
            go.name = "Memory Wisp";
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.6f;
            var lt = go.AddComponent<Light>();
            lt.type = LightType.Point; lt.color = WispColor; lt.range = 5f; lt.intensity = 2.5f;
            var w = go.AddComponent<LakeWisp>();
            w.mana = mana; w.heal = heal; w.template = template; w.baseY = pos.y;
        }

        void Update()
        {
            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
            }
            var p = transform.position;
            transform.position = new Vector3(p.x, baseY + Mathf.Sin(Time.time * 2f) * 0.25f, p.z);
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }
            if (player == null) return;

            Vector3 d = player.position - transform.position; d.y = 0f;
            if (d.magnitude < 2.8f)
            {
                var stats = player.GetComponent<PlayerStats>();
                var health = player.GetComponent<Health>();
                if (stats != null) stats.AddMana(mana);
                if (health != null) health.Heal(heal);
                PrototypeHUD.Message("+" + Mathf.RoundToInt(mana) + " mana, +" + Mathf.RoundToInt(heal) + " health", 1.5f);
                ImpactFlash.Spawn(transform.position, WispColor, template, 1.2f);
                Destroy(gameObject);
            }
        }
    }
}
