using UnityEngine;

namespace FracturedVow
{
    /// <summary>A gold coin that pops out of a defeated enemy or a chest, then flies to the knight when he comes near.</summary>
    public class GoldCoin : MonoBehaviour
    {
        public int value = 5;
        Vector3 velocity;
        Transform player;
        float age, groundY;
        bool landed;

        static Material coinMat;

        public static void Spawn(Vector3 at, int value)
        {
            GameObject go;
            var lib = FVLibrary.Get;
            if (lib != null && lib.coin != null)
            {
                go = Instantiate(lib.coin);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                FitTo(go, 0.3f);
            }
            else
            {
                if (coinMat == null) coinMat = Resources.Load<Material>("FVFX/FVP_FX_Additive");
                go = RuntimeVisuals.Primitive(PrimitiveType.Cylinder, null, new Color(1f, 0.8f, 0.25f), false);
                go.transform.localScale = new Vector3(0.22f, 0.025f, 0.22f);
            }
            go.name = "Gold Coin";
            go.transform.position = at;
            var c2 = go.AddComponent<GoldCoin>();
            c2.value = value;
            c2.velocity = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 5.5f), Random.Range(-2.5f, 2.5f));
            c2.groundY = at.y - 0.8f;
            WorldLog.Spawned(go);
        }

        static void FitTo(GameObject go, float size)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float m = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (m > 0.001f) go.transform.localScale *= size / m;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) player = p.transform;
            }
            transform.Rotate(0f, 360f * Time.deltaTime, 0f, Space.World);

            if (player != null && age > 0.6f)
            {
                Vector3 target = player.position + Vector3.up * 1f;
                float d = Vector3.Distance(transform.position, target);
                if (d < 5f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, target, (6f + (5f - d) * 4f) * Time.deltaTime);
                    if (d < 0.7f)
                    {
                        RunState.AddGold(value);
                        GameAudio.Play("pickup", transform.position, 0.25f, 0.2f, 1.6f);
                        WorldLog.Remove(gameObject);
                    }
                    return;
                }
            }
            if (landed) return;
            velocity += Physics.gravity * Time.deltaTime;
            Vector3 next = transform.position + velocity * Time.deltaTime;
            if (next.y <= groundY + 0.1f) { next.y = groundY + 0.1f; landed = true; }
            transform.position = next;
        }
    }
}
