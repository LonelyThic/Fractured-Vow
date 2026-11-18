using UnityEngine;

namespace FracturedVow
{
    public static class RuntimeVisuals
    {
        static Material lit, glow;

        /// <summary>
        /// A plain URP Lit material for shapes made in code. A primitive's own default material uses the old
        /// Standard shader in a built game (the editor swaps it for URP Lit, a build doesn't), which renders
        /// bright pink, so nothing made at runtime should keep it.
        /// </summary>
        public static Material Lit
        {
            get
            {
                if (lit != null) return lit;
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (sh != null) lit = new Material(sh) { name = "FV Runtime Lit" };
                else
                {
                    // last resort: borrow the shader of a material that is in the build
                    var any = Resources.Load<Material>("FVFX/FVP_FX_AlphaBlend");
                    lit = any != null ? new Material(any) : null;
                }
                return lit;
            }
        }

        /// <summary>A soft additive glow (the particle material) for bolts, flashes and orbs.</summary>
        public static Material Glow
        {
            get
            {
                if (glow == null)
                {
                    var add = Resources.Load<Material>("FVFX/FVP_FX_Additive");
                    if (add != null) glow = new Material(add) { name = "FV Runtime Glow" };
                }
                return glow != null ? glow : Lit;
            }
        }

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
            var basis = template != null ? template : Lit;
            var mat = basis != null ? new Material(basis) : new Material(r.sharedMaterial);
            SetColor(mat, color);
            r.sharedMaterial = mat;
            return go;
        }

        /// <summary>Gives a primitive made with GameObject.CreatePrimitive a material that works in builds.</summary>
        public static void FixMaterial(GameObject go, Color color)
        {
            var r = go != null ? go.GetComponent<Renderer>() : null;
            if (r == null || Lit == null) return;
            var mat = new Material(Lit);
            SetColor(mat, color);
            r.sharedMaterial = mat;
        }

        /// <summary>Makes a shape glow in its own colour (bolts, orbs), on top of its lit colour.</summary>
        public static void Emit(GameObject go, Color color, float strength = 1.5f)
        {
            var r = go != null ? go.GetComponent<Renderer>() : null;
            if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_EmissionColor")) return;
            r.sharedMaterial.EnableKeyword("_EMISSION");
            r.sharedMaterial.SetColor("_EmissionColor", color * strength);
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