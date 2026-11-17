using UnityEngine;

namespace FracturedVow
{
    /// <summary>Stone Wall spell: a slab rises out of the floor in front of the knight, blocks enemies, then sinks.</summary>
    public class StoneWall : MonoBehaviour
    {
        public float riseTime = 0.25f;
        public float lifetime = 6f;
        float t;
        float baseY, raisedY;

        public static void Spawn(Transform caster, Material template, float lifetime)
        {
            Vector3 ahead = caster.position + caster.forward * 3f;
            float groundY = caster.position.y;
            if (Physics.Raycast(ahead + Vector3.up * 5f, Vector3.down, out var hit, 12f, ~0, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;

            var go = RuntimeVisuals.Primitive(PrimitiveType.Cube, template, new Color(0.62f, 0.58f, 0.52f), true);
            go.name = "Spell_StoneWall";
            go.transform.localScale = new Vector3(3.5f, 2.2f, 0.6f);
            go.transform.rotation = Quaternion.LookRotation(caster.forward);
            var w = go.AddComponent<StoneWall>();
            w.lifetime = lifetime;
            w.raisedY = groundY + 1.1f;
            w.baseY = groundY - 1.2f;
            go.transform.position = new Vector3(ahead.x, w.baseY, ahead.z);
            GameAudio.Play("stone_wall", new Vector3(ahead.x, groundY, ahead.z));
            GameFX.Dust(new Vector3(ahead.x, groundY + 0.2f, ahead.z), 1.6f, 30);
        }

        void Update()
        {
            t += Time.deltaTime;
            float y;
            if (t < riseTime) y = Mathf.Lerp(baseY, raisedY, t / riseTime);
            else if (t < lifetime) y = raisedY;
            else y = Mathf.Lerp(raisedY, baseY, (t - lifetime) / 0.6f);

            var p = transform.position;
            transform.position = new Vector3(p.x, y, p.z);
            if (t > lifetime + 0.6f) Destroy(gameObject);
        }
    }
}
