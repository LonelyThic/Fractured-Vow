using UnityEngine;

namespace FracturedVow
{
    public static class RuntimeVisuals
    {
        public static void SetColor(Material m, Color c)
        {
            if (m == null) return;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static GameObject Primitive(PrimitiveType type, Material template, Color color, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            var mat = template != null ? new Material(template) : new Material(r.sharedMaterial);
            SetColor(mat, color);
            r.sharedMaterial = mat;
            return go;
        }

        public static void SetRendererColor(Renderer r, Color c, MaterialPropertyBlock block)
        {
            if (r == null) return;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }
    }
}
