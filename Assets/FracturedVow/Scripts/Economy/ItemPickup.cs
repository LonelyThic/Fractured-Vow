using UnityEngine;

namespace FracturedVow
{
    /// <summary>An item lying in the world (from an enemy, a chest or a drop). Flasks are picked up by walking over them; gear and tomes with F.</summary>
    public class ItemPickup : MonoBehaviour, IInteractable
    {
        public string itemId;
        Vector3 basePos;
        float phase;
        Transform player;

        public string InteractionPrompt
        {
            get { var d = FVLibrary.Item(itemId); return d != null ? "Take " + d.name + "  (" + d.Describe() + ")" : "Take"; }
        }
        public bool CanInteract(PlayerInteractor who) => true;
        public void Interact(PlayerInteractor who) => Take();

        public static ItemPickup Spawn(Vector3 at, ItemDef def)
        {
            GameObject go;
            if (def.model != null)
            {
                go = Instantiate(def.model);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
                Fit(go, def.kind == ItemKind.Weapon ? 1.1f : 0.7f);
            }
            else
            {
                var shape = def.kind == ItemKind.HealthFlask || def.kind == ItemKind.ManaFlask ? PrimitiveType.Capsule
                          : def.kind == ItemKind.SpellTome ? PrimitiveType.Cube : PrimitiveType.Sphere;
                go = RuntimeVisuals.Primitive(shape, null, def.color, false);
                go.transform.localScale = shape == PrimitiveType.Capsule ? new Vector3(0.22f, 0.2f, 0.22f)
                                        : shape == PrimitiveType.Cube ? new Vector3(0.45f, 0.12f, 0.35f) : Vector3.one * 0.35f;
            }
            go.name = def.name;
            go.transform.position = at + Vector3.up * 0.7f;
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1f / Mathf.Max(0.05f, go.transform.lossyScale.x);
            var lt = new GameObject("Glow").AddComponent<Light>();
            lt.transform.SetParent(go.transform, false);
            lt.type = LightType.Point; lt.color = def.color; lt.range = 2.5f; lt.intensity = 1.5f;
            var p = go.AddComponent<ItemPickup>();
            p.itemId = def.id;
            WorldLog.Spawned(p.gameObject); // dropped after the checkpoint: gone again if the knight dies
            return p;
        }

        static void Fit(GameObject go, float size)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float m = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (m > 0.001f) go.transform.localScale *= size / m;
        }

        void Start() { basePos = transform.position; phase = Random.value * 6f; }

        void Update()
        {
            transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 2f + phase) * 0.12f;
            transform.Rotate(0f, 80f * Time.deltaTime, 0f, Space.World);
            var def = FVLibrary.Item(itemId);
            if (def == null || (def.kind != ItemKind.HealthFlask && def.kind != ItemKind.ManaFlask)) return;
            if (player == null) { var go = GameObject.FindGameObjectWithTag("Player"); if (go != null) player = go.transform; return; }
            Vector3 d = player.position - transform.position; d.y = 0f;
            if (d.magnitude < 1.1f) Take();
        }

        void Take()
        {
            Inventory.Add(itemId);
            var gear = FVLibrary.Item(itemId);
            if (gear != null && gear.kind == ItemKind.Weapon) RunState.StarterTaken = true;
            // gear goes straight into an empty slot, so the first sword you find is in your hand right away
            if (gear != null && gear.IsGear && string.IsNullOrEmpty(Inventory.Equipped(gear.kind)))
            {
                Inventory.Equip(itemId);
                PrototypeHUD.Message("Equipped " + gear.name, 2f);
            }
            GameAudio.Play("pickup_gear", transform.position, 0.6f);
            var def = FVLibrary.Item(itemId);
            GameFX.Burst(transform.position, def != null ? def.color : Color.white, 20);
            WorldLog.Remove(gameObject);
        }
    }
}
