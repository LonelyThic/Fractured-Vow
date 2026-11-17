using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FracturedVow.EditorTools
{
    /// <summary>
    /// Tools > Fractured Vow > Fix Pink Materials (URP)
    /// Asset Store packs made for the old built-in pipeline (Standard shader, or their own surface shaders) show up
    /// purple in a URP project. This switches every such material in the project to URP Lit (or URP Particles for
    /// particle materials), carrying over its texture, color, normal map, metal/smoothness, emission and
    /// cut-out or transparency. Materials that already use URP or Shader Graph are left alone.
    /// Set Up Imported Packs runs this automatically.
    /// </summary>
    public static class MaterialFixer
    {
        [MenuItem("Tools/Fractured Vow/Fix Pink Materials (URP)")]
        static void Menu()
        {
            ImportUrpPackages(installed =>
            {
                int n = FixAll(out string report);
                EditorUtility.DisplayDialog("Fix Pink Materials",
                    (installed.Count > 0 ? "Installed the packs' own URP versions:\n" + string.Join("\n", installed) + "\n\n" : "") +
                    n + " other materials converted to URP.\n\n" + report, "OK");
            });
        }

        /// <summary>
        /// Some packs ship their own URP version as a .unitypackage inside their folder (Polytope, Low Poly Medieval
        /// Props). Those look best, so they're imported first (newest URP version per folder, once per project).
        /// Calls onDone with the names of the packages it installed.
        /// </summary>
        public static void ImportUrpPackages(System.Action<List<string>> onDone)
        {
            var best = new Dictionary<string, (string path, int[] ver)>();
            foreach (var path in System.IO.Directory.GetFiles("Assets", "*.unitypackage", System.IO.SearchOption.AllDirectories))
            {
                string p = path.Replace('\\', '/');
                string file = System.IO.Path.GetFileNameWithoutExtension(p);
                if (!file.Contains("URP") || file.Contains("To_Built") || p.Contains("/FracturedVow/")) continue;
                if (EditorPrefs.GetBool("FV_urp_" + PlayerSettings.productGUID + "_" + p, false)) continue;
                string folder = System.IO.Path.GetDirectoryName(p).Replace('\\', '/');
                string tail = file.Substring(file.IndexOf("URP") + 3);
                var nums = System.Text.RegularExpressions.Regex.Matches(tail, @"\d+");
                var ver = new int[nums.Count];
                for (int i = 0; i < nums.Count; i++) ver[i] = int.Parse(nums[i].Value);
                if (!best.TryGetValue(folder, out var cur) || Newer(ver, cur.ver)) best[folder] = (p, ver);
            }
            var queue = new List<string>();
            foreach (var b in best.Values) queue.Add(b.path);
            var installed = new List<string>();
            if (queue.Count == 0) { onDone(installed); return; }

            int pending = queue.Count;
            AssetDatabase.ImportPackageCallback ok = null, cancel = null;
            AssetDatabase.ImportPackageFailedCallback fail = null;
            void Finish(string name)
            {
                pending--;
                if (pending > 0) return;
                AssetDatabase.importPackageCompleted -= ok;
                AssetDatabase.importPackageCancelled -= cancel;
                AssetDatabase.importPackageFailed -= fail;
                AssetDatabase.Refresh();
                EditorApplication.delayCall += () => onDone(installed);
            }
            ok = name =>
            {
                installed.Add(name);
                foreach (var q in queue) if (q.Contains(name)) EditorPrefs.SetBool("FV_urp_" + PlayerSettings.productGUID + "_" + q, true);
                Finish(name);
            };
            cancel = name => Finish(name);
            fail = (name, error) => { Debug.LogWarning("[Fractured Vow] Couldn't import " + name + ": " + error); Finish(name); };
            AssetDatabase.importPackageCompleted += ok;
            AssetDatabase.importPackageCancelled += cancel;
            AssetDatabase.importPackageFailed += fail;
            foreach (var q in queue) AssetDatabase.ImportPackage(q, false);
        }

        static bool Newer(int[] a, int[] b)
        {
            for (int i = 0; i < Mathf.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : -1, y = i < b.Length ? b[i] : -1;
                if (x != y) return x > y;
            }
            return false;
        }

        /// <summary>
        /// True if the shader can't draw in URP. A shader file counts as URP-ready if its source has URP passes
        /// (packs like Polytope ship URP versions under their own names); Shader Graphs are always fine.
        /// </summary>
        static bool NeedsFix(Shader s)
        {
            if (s == null) return true;
            string n = s.name;
            if (n == "Hidden/InternalErrorShader") return true;
            if (n.StartsWith("Universal Render Pipeline/") || n.StartsWith("Shader Graphs/") || n.StartsWith("Hidden/") ||
                n.StartsWith("TextMeshPro") || n.StartsWith("UI/") || n.StartsWith("Sprites/") || n.StartsWith("Skybox/") || n.StartsWith("GUI/")) return false;
            string path = AssetDatabase.GetAssetPath(s);
            if (path.EndsWith(".shadergraph") || path.EndsWith(".shadersubgraph")) return false;
            if (path.EndsWith(".shader") && System.IO.File.Exists(path))
            {
                string src = System.IO.File.ReadAllText(path);
                if (src.Contains("UniversalPipeline") || src.Contains("UniversalForward")) return false;
            }
            return true; // Standard, Legacy, Mobile, built-in Particles, and packs' built-in-only shaders
        }

        // Materials that have their own URP shader (installed from the pack's URP package). If an earlier version of
        // this tool turned them into plain URP Lit, they go back to their real shader; their colors were kept.
        static readonly (string material, string shaderGuid)[] Restore =
        {
            ("Assets/Polytope Studio/Lowpoly_Characters/Sources/Modular_Armors/Materials/PT_Armors_Material.mat", "66cf399cd52203c4685e11681d535f09"),
            ("Assets/Polytope Studio/Lowpoly_Characters/Sources/Modular_Armors/Materials/PT_Armors_Material_Toon.mat", "ad5cc8499eb3f43439c02eb1f62381e4"),
            ("Assets/Polytope Studio/Lowpoly_Props/Sources/Materials/PT_Props_mat.mat", "4e5c65c1d3e8564479e1074db8a9989f"),
            ("Assets/Polytope Studio/Lowpoly_Environments/Sources/Materials/PT_Grass_Mat.mat", "233be0623027421449b81f7423abaf79"),
            ("Assets/Polytope Studio/Lowpoly_Environments/Sources/Materials/PT_Poppy_mat.mat", "69e2e8c890f00e04e945ccd10e764372"),
            ("Assets/Polytope Studio/Lowpoly_Environments/Sources/Materials/PT_Generic_Tree_Leaves_mat.mat", "77e1a76a38ecd5a46a270afb12894dd5"),
            ("Assets/Polytope Studio/Lowpoly_Environments/Sources/Materials/PT_Generic_Tree_Trunk_mat.mat", "270965b004462ae41bd125bc783c0fa6"),
            ("Assets/Polytope Studio/Lowpoly_Environments/Sources/Materials/PT_Rocks_mat.mat", "cd18bd550ff6630468660d0150eed511"),
        };

        /// <summary>
        /// Polytope's weapon shader was only made for URP 12 and draws nothing in Unity 6, so the weapon material
        /// becomes URP Lit in its own steel color (the shader paints by color masks; it has no texture).
        /// </summary>
        static int FixPolytopeWeapons(Shader lit, List<string> log)
        {
            int n = 0;
            foreach (var path in new[] { "Assets/Polytope Studio/Lowpoly_Weapons/Sources/Materials/PT_Weapons_Material.mat",
                                         "Assets/Polytope Studio/Lowpoly_Weapons/Sources/Materials/PT_mat.mat" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null || m.shader == lit) continue;
                Color steel = m.HasProperty("_METAL1COLOR") ? m.GetColor("_METAL1COLOR") : new Color(0.73f, 0.77f, 0.75f);
                steel.a = 1f;
                float metal = m.HasProperty("_METAL1METALLIC") ? m.GetFloat("_METAL1METALLIC") : 0.65f;
                float smooth = m.HasProperty("_METAL1SMOOTHNESS") ? m.GetFloat("_METAL1SMOOTHNESS") : 0.7f;
                m.shader = lit;
                m.SetColor("_BaseColor", steel);
                if (m.HasProperty("_Color")) m.SetColor("_Color", steel);
                m.SetTexture("_BaseMap", null);
                m.SetFloat("_Metallic", metal);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Surface", 0f);
                m.SetOverrideTag("RenderType", "Opaque");
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.SetFloat("_AlphaClip", 0f);
                foreach (var k in new[] { "_SURFACE_TYPE_TRANSPARENT", "_ALPHATEST_ON", "_EMISSION", "_NORMALMAP" }) m.DisableKeyword(k);
                m.renderQueue = -1;
                EditorUtility.SetDirty(m);
                log.Add("weapons: " + m.name + " is now polished steel");
                n++;
            }
            return n;
        }

        static int RestoreCustom(List<string> log)
        {
            int n = 0;
            foreach (var r in Restore)
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(r.material);
                var sh = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(r.shaderGuid));
                if (m == null || sh == null || m.shader == sh || NeedsFix(sh)) continue;
                m.shader = sh;
                m.renderQueue = -1;
                foreach (var k in new[] { "_NORMALMAP", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP", "_ALPHATEST_ON", "_SURFACE_TYPE_TRANSPARENT" }) m.DisableKeyword(k);
                EditorUtility.SetDirty(m);
                log.Add("restored " + m.name);
                n++;
            }
            return n;
        }

        public static int FixAll(out string report)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var particle = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (lit == null || GraphicsSettings.defaultRenderPipeline == null)
            {
                report = "This project isn't using URP, so nothing was changed.";
                return 0;
            }
            int fixedCount = 0;
            var perFolder = new Dictionary<string, int>();
            var restored = new List<string>();
            int restoredCount = RestoreCustom(restored);
            restoredCount += FixPolytopeWeapons(lit, restored);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Material"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.StartsWith("Assets/") || !path.EndsWith(".mat")) continue; // materials inside FBX files can't be edited
                    var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (m == null || !NeedsFix(m.shader)) continue;
                    Convert(m, m.shader != null && m.shader.name.ToLowerInvariant().Contains("particle") ? particle : lit);
                    EditorUtility.SetDirty(m);
                    fixedCount++;
                    string folder = path.Split('/').Length > 2 ? path.Split('/')[1] : "Assets";
                    perFolder[folder] = perFolder.TryGetValue(folder, out int c) ? c + 1 : 1;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            var lines = new List<string>();
            foreach (var kv in perFolder) lines.Add(kv.Key + ": " + kv.Value);
            if (restoredCount > 0) lines.Add("Back on their own URP shaders: " + string.Join(", ", restored).Replace("restored ", ""));
            report = lines.Count > 0 ? string.Join("\n", lines) : "Everything already uses URP.";
            Debug.Log("[Fractured Vow] Converted " + fixedCount + " materials to URP. " + string.Join(", ", lines));
            return fixedCount;
        }

        static Texture FirstTexture(Material m, params string[] names)
        {
            foreach (var n in names) if (m.HasProperty(n) && m.GetTexture(n) != null) return m.GetTexture(n);
            return null;
        }

        static void Convert(Material m, Shader target)
        {
            // read everything useful before switching shaders
            Texture albedo = FirstTexture(m, "_MainTex", "_BaseMap", "_Albedo", "_AlbedoMap", "_BaseColorMap", "_Texture", "_BaseTexture", "_Diffuse", "_DiffuseMap", "_TextureSample0");
            if (albedo == null)
                foreach (var n in m.GetTexturePropertyNames())
                {
                    string l = n.ToLowerInvariant();
                    if (l.Contains("normal") || l.Contains("bump") || l.Contains("mask") || l.Contains("emiss") || l.Contains("metal") || l.Contains("occl")) continue;
                    if (m.GetTexture(n) != null) { albedo = m.GetTexture(n); break; }
                }
            Vector2 scale = Vector2.one, offset = Vector2.zero;
            if (m.HasProperty("_MainTex")) { scale = m.GetTextureScale("_MainTex"); offset = m.GetTextureOffset("_MainTex"); }
            Color color = Color.white;
            foreach (var n in new[] { "_Color", "_BaseColor", "_MainColor", "_Tint", "_AlbedoColor" })
                if (m.HasProperty(n)) { color = m.GetColor(n); break; }
            Texture normal = FirstTexture(m, "_BumpMap", "_NormalMap", "_Normal");
            float normalScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
            Texture metalMap = FirstTexture(m, "_MetallicGlossMap", "_MetallicMap");
            float metal = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            float smooth = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : 0.3f;
            Texture emissionMap = FirstTexture(m, "_EmissionMap", "_Emission");
            Color emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            bool emissive = m.IsKeywordEnabled("_EMISSION") && (emission.maxColorComponent > 0.01f || emissionMap != null);
            Texture occlusion = FirstTexture(m, "_OcclusionMap");
            float mode = m.HasProperty("_Mode") ? m.GetFloat("_Mode") : 0f; // Standard: 0 opaque, 1 cutout, 2 fade, 3 transparent
            float cutoff = m.HasProperty("_Cutoff") ? m.GetFloat("_Cutoff") : 0.5f;
            bool cutout = mode == 1f || m.IsKeywordEnabled("_ALPHATEST_ON") || m.GetTag("RenderType", false) == "TransparentCutout";
            bool transparent = mode >= 2f || m.GetTag("RenderType", false) == "Transparent" || m.renderQueue >= 3000;
            int queue = m.renderQueue;

            m.shader = target;
            if (m.HasProperty("_BaseMap")) { m.SetTexture("_BaseMap", albedo); m.SetTextureScale("_BaseMap", scale); m.SetTextureOffset("_BaseMap", offset); }
            if (m.HasProperty("_MainTex")) { m.SetTexture("_MainTex", albedo); m.SetTextureScale("_MainTex", scale); m.SetTextureOffset("_MainTex", offset); }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);

            if (target.name.Contains("Particles"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.renderQueue = (int)RenderQueue.Transparent;
                return;
            }

            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", normalScale); m.EnableKeyword("_NORMALMAP"); }
            if (metalMap != null) { m.SetTexture("_MetallicGlossMap", metalMap); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", Mathf.Clamp01(smooth));
            if (occlusion != null) { m.SetTexture("_OcclusionMap", occlusion); m.EnableKeyword("_OCCLUSIONMAP"); }
            if (emissive)
            {
                m.SetColor("_EmissionColor", emission);
                if (emissionMap != null) m.SetTexture("_EmissionMap", emissionMap);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else m.DisableKeyword("_EMISSION");

            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = queue >= 3000 ? queue : (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetOverrideTag("RenderType", cutout ? "TransparentCutout" : "Opaque");
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (cutout) { m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", cutoff); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)RenderQueue.AlphaTest; }
                else { m.SetFloat("_AlphaClip", 0f); m.DisableKeyword("_ALPHATEST_ON"); m.renderQueue = -1; }
            }
        }
    }
}
