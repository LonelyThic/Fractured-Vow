using UnityEngine;

namespace FracturedVow
{
    /// <summary>A wall of water rolling outward from the Inquisitor, with one gap. Sail through the gap.</summary>
    public class TidalWave : MonoBehaviour
    {
        Vector3 direction;
        float speed, damage, maxDistance, travelled, halfWidth, gapCenter, gapHalf;
        IBossHitter boss;
        Transform player;
        bool hasHit;

        public static void Spawn(Vector3 origin, Vector3 dir, float speed, float width, float gapCenter, float gap,
            float damage, float maxDistance, Material mat, IBossHitter boss, Transform player)
        {
            var root = new GameObject("Tidal Wave");
            root.transform.SetPositionAndRotation(new Vector3(origin.x, 0f, origin.z), Quaternion.LookRotation(dir));
            float half = width * 0.5f;
            Segment(root.transform, -half, gapCenter - gap * 0.5f, mat);
            Segment(root.transform, gapCenter + gap * 0.5f, half, mat);
            var w = root.AddComponent<TidalWave>();
            w.direction = dir; w.speed = speed; w.damage = damage; w.maxDistance = maxDistance;
            w.halfWidth = half; w.gapCenter = gapCenter; w.gapHalf = gap * 0.5f;
            w.boss = boss; w.player = player;
        }

        static void Segment(Transform root, float x0, float x1, Material mat)
        {
            if (x1 - x0 < 0.2f) return;
            var seg = RuntimeVisuals.Primitive(PrimitiveType.Cube, mat, new Color(0.35f, 0.75f, 0.8f), false);
            seg.name = "Wave Segment";
            seg.transform.SetParent(root, false);
            seg.transform.localPosition = new Vector3((x0 + x1) * 0.5f, 0.9f, 0f);
            seg.transform.localScale = new Vector3(x1 - x0, 2.2f, 1.4f);
            seg.transform.localRotation = Quaternion.Euler(-12f, 0f, 0f);
            var foam = RuntimeVisuals.Primitive(PrimitiveType.Cube, mat, new Color(0.85f, 0.95f, 1f), false);
            foam.name = "Foam";
            foam.transform.SetParent(root, false);
            foam.transform.localPosition = new Vector3((x0 + x1) * 0.5f, 2.05f, 0.25f);
            foam.transform.localScale = new Vector3(x1 - x0, 0.25f, 0.6f);
        }

        void Update()
        {
            float step = speed * Time.deltaTime;
            transform.position += direction * step;
            travelled += step;

            if (!hasHit && player != null)
            {
                Vector3 local = transform.InverseTransformPoint(player.position);
                bool inBand = Mathf.Abs(local.z) < 1.2f && Mathf.Abs(local.x) <= halfWidth;
                bool inGap = Mathf.Abs(local.x - gapCenter) <= gapHalf;
                if (inBand && !inGap)
                {
                    hasHit = true;
                    if ((boss as Object) != null) boss.HitPlayer(damage, direction, 9f);
                }
            }
            if (travelled > maxDistance) Destroy(gameObject);
        }
    }
}
