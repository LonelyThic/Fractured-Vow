using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// The shards you were carrying when you died, left glowing where you fell. Walk into it to take them back.
    /// Die again before you do and they are gone for good.
    /// </summary>
    public class Remnant : MonoBehaviour
    {
        public int amount;
        Transform player;
        float baseY;

        public static Remnant Current { get; private set; }

        public static void Spawn(Vector3 at, int amount, Material template)
        {
            Clear();
            var go = RuntimeVisuals.Primitive(PrimitiveType.Cylinder, template, new Color(0.8f, 0.55f, 1f), false);
            go.name = "Lost Memories (" + amount + ")";
            go.transform.position = at + Vector3.up * 0.9f;
            go.transform.localScale = new Vector3(0.35f, 0.9f, 0.35f);
            var lt = go.AddComponent<Light>();
            lt.type = LightType.Point; lt.color = new Color(0.8f, 0.55f, 1f); lt.range = 6f; lt.intensity = 3f;
            var r = go.AddComponent<Remnant>();
            r.amount = amount;
            r.baseY = go.transform.position.y;
            Current = r;
        }

        public static void Clear()
        {
            if (Current != null) Destroy(Current.gameObject);
            Current = null;
        }

        void Update()
        {
            transform.Rotate(0f, 90f * Time.deltaTime, 0f);
            var p = transform.position;
            transform.position = new Vector3(p.x, baseY + Mathf.Sin(Time.time * 2f) * 0.15f, p.z);
            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
                return;
            }
            Vector3 d = player.position - transform.position; d.y = 0f;
            if (d.magnitude < 1.6f && Mathf.Abs(player.position.y + 0.9f - transform.position.y) < 2.5f)
            {
                RunState.AddShards(amount);
                RunState.LostShards = 0;
                RunState.LostScene = null;
                PrototypeHUD.Message("Memories recovered: +" + amount + " shards", 2.5f);
                GameAudio.PlayUI("memory_chime", 0.7f);
                GameFX.Burst(transform.position, new Color(0.8f, 0.55f, 1f), 50);
                Current = null;
                Destroy(gameObject);
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || UIFocus.Any || PrototypeHUD.Hidden || Camera.main == null) return;
            Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.2f);
            if (sp.z < 0f || sp.z > 40f) return;
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(Screen.height / 55f) };
            style.normal.textColor = new Color(0.85f, 0.65f, 1f);
            GUI.Label(new Rect(sp.x - 150f, Screen.height - sp.y - 12f, 300f, 24f), "Lost memories: " + amount, style);
        }
    }
}
