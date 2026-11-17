using UnityEngine;

namespace FracturedVow
{
    /// <summary>Helpers for characters from the imported packs: spawning a model at the right height and putting a weapon in its hand.</summary>
    public static class CharacterVisuals
    {
        /// <summary>Instantiates a library character under 'parent' as "Model", scaled to 'height' metres, with its Animator controller.</summary>
        public static GameObject SpawnModel(FVLibrary.Character c, Transform parent, float height)
        {
            if (c == null || c.prefab == null) return null;
            var go = Object.Instantiate(c.prefab, parent);
            go.name = "Model";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            foreach (var col in go.GetComponentsInChildren<Collider>()) Object.Destroy(col);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>()) Object.Destroy(rb);
            var anim = go.GetComponentInChildren<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            if (c.controller != null) anim.runtimeAnimatorController = c.controller;
            anim.applyRootMotion = false;
            FitHeight(go, height);
            return go;
        }

        public static void FitHeight(GameObject go, float height)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            if (b.size.y > 0.01f) go.transform.localScale *= height / b.size.y;
            b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            go.transform.position += Vector3.up * (go.transform.parent != null ? go.transform.parent.position.y - b.min.y : -b.min.y);
        }

        /// <summary>
        /// True if 'root' has no model yet, or only the blocky stand-in the Production Pass uses when the library
        /// wasn't set up (that one is removed here so the real character can take its place).
        /// </summary>
        public static bool NeedsRealModel(Transform root)
        {
            var m = root.Find("Model");
            if (m == null) return true;
            if (m.GetComponent<PrimitiveRig>() == null) return false;
            m.name = "Stand-in (removed)";
            Object.Destroy(m.gameObject);
            return true;
        }

        // ---------------- weapon grip ----------------

        /// <summary>Preset grip turns, cycled by the console's "grip" command until the blade points the right way.</summary>
        public static readonly Vector3[] GripPresets =
        {
            Vector3.zero, new Vector3(180f, 0f, 0f), new Vector3(0f, 180f, 0f), new Vector3(0f, 0f, 180f),
            new Vector3(90f, 0f, 0f), new Vector3(-90f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0f, 0f, -90f),
        };

        /// <summary>Extra turn applied to every held weapon (saved on this computer).</summary>
        public static Vector3 Grip
        {
            get
            {
                var p = PlayerPrefs.GetString("fv_grip", "180,0,0").Split(',');
                float F(int i) => p.Length > i && float.TryParse(p[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
                return new Vector3(F(0), F(1), F(2));
            }
            set
            {
                PlayerPrefs.SetString("fv_grip", value.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
                    value.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," + value.z.ToString(System.Globalization.CultureInfo.InvariantCulture));
                PlayerPrefs.Save();
                foreach (var h in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (h.name == "Held Weapon") h.localRotation = Quaternion.Euler(value);
            }
        }

        public static Transform FindHand(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant().Contains("right_hand_weapon_slot")) return t; // Polytope characters have a weapon socket
            var anim = root.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman)
            {
                var h = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (h != null) return h;
            }
            Transform best = null;
            foreach (var t in root.GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace(".", "");
                if (!n.Contains("hand")) continue;
                if (n.Contains("handr") || n.Contains("rhand") || n.Contains("righthand") || n.Contains("handright")) return t;
                if (best == null && n.EndsWith("r")) best = t;
            }
            return best;
        }

        /// <summary>Puts a weapon model in the character's right hand (replacing any previous one).</summary>
        public static void HoldWeapon(Transform root, GameObject weapon, float length = 1f)
        {
            var hand = FindHand(root);
            if (hand == null) return;
            var old = hand.Find("Held Weapon");
            if (old != null) Object.Destroy(old.gameObject);
            if (weapon == null) return;
            var w = Object.Instantiate(weapon, hand);
            w.name = "Held Weapon";
            foreach (var col in w.GetComponentsInChildren<Collider>()) Object.Destroy(col);
            foreach (var rb in w.GetComponentsInChildren<Rigidbody>()) Object.Destroy(rb);
            w.transform.localPosition = Vector3.zero;
            w.transform.localRotation = Quaternion.Euler(Grip); // see the console's "grip" command
            var rends = w.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float m = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (m > 0.001f) w.transform.localScale *= length / m;
        }
    }
}
