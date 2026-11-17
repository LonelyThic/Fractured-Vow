using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Pictures of items for the inventory and the shop: each item's 3D model is photographed once by a hidden
    /// camera far below the level (transparent background, its own light) and the picture is kept for reuse.
    /// Items without a model get none (the UI shows a colored swatch instead).
    /// </summary>
    public static class ItemIcons
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Vector3 Studio = new Vector3(0f, -2000f, 0f);
        const int Size = 128;

        public static Texture2D Get(ItemDef def)
        {
            if (def == null || def.model == null) return null;
            if (cache.TryGetValue(def.id, out var tex) && tex != null) return tex;
            tex = Render(def);
            cache[def.id] = tex;
            return tex;
        }

        static Texture2D Render(ItemDef def)
        {
            var root = new GameObject("Icon Studio");
            root.transform.position = Studio;
            GameObject model = null;
            Camera cam = null;
            RenderTexture rt = null;
            try
            {
                model = Object.Instantiate(def.model, root.transform);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.Euler(-20f, 35f, -35f); // a lively three-quarter angle
                foreach (var c in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                foreach (var b in model.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
                var rends = model.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) return null;
                Bounds bounds = rends[0].bounds;
                foreach (var r in rends) bounds.Encapsulate(r.bounds);

                var light = new GameObject("Icon Light").AddComponent<Light>();
                light.transform.SetParent(root.transform, false);
                light.type = LightType.Directional;
                light.intensity = 1.4f;
                light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
                var fill = new GameObject("Icon Fill").AddComponent<Light>();
                fill.transform.SetParent(root.transform, false);
                fill.type = LightType.Point;
                fill.range = bounds.extents.magnitude * 8f + 2f;
                fill.intensity = 2f;
                fill.transform.position = bounds.center + new Vector3(-1f, 0.5f, -1.5f) * (bounds.extents.magnitude * 2f + 0.5f);

                cam = new GameObject("Icon Camera").AddComponent<Camera>();
                cam.transform.SetParent(root.transform, false);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.15f + 0.01f;
                cam.transform.position = bounds.center + Vector3.back * (bounds.extents.magnitude * 3f + 1f);
                cam.transform.rotation = Quaternion.identity;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = bounds.extents.magnitude * 8f + 5f;
                cam.enabled = false;

                rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                return tex;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Fractured Vow] Couldn't draw an icon for " + def.name + ": " + e.Message);
                return null;
            }
            finally
            {
                if (cam != null) cam.targetTexture = null;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(root);
            }
        }
    }
}
