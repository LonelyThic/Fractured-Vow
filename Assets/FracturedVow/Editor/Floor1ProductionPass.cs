using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FracturedVow.EditorTools
{
    /// <summary>
    /// Tools > Fractured Vow > Floor 1: Production Pass
    /// Run on a freshly built Floor1_WardensPit scene. Replaces every placeholder with working gameplay:
    /// animated husk enemies, the Warden boss fight, real pickups, spike/flame/pit traps, lever + portcullis,
    /// sealed boss gate, memory fragment, checkpoints, level exit, a dressed-up boat, torch fire and embers,
    /// dust, mist, drips, ash, and URP post-processing (bloom, vignette, color grading).
    /// </summary>
    public class Floor1ProductionPass : EditorWindow
    {
        GameObject enemyPrefab;
        GameObject enemyModel;
        bool hideTourSigns = true;
        bool setDressing = true;
        bool postProcessing = true;
        string report = "";

        string root, prodDir, matDir, texDir;
        System.Random rng;
        Texture2D softTex;

        Material mSkin, mDrownedSkin, mBossSkin, mIron, mRustIron, mCloth, mDrownedCloth, mBossCloth, mEyes, mBossEyes, mBone;
        Material mRedGlass, mBlueGlass, mCork, mLeather, mPage, mRuneFrost, mRuneFire, mBrass, mCrystal, mTelegraph, mFlameCore, mLantern, mDoorWood;
        Material mWood, mStone;
        Material fxAdd, fxAlpha;

        Transform gGameplay, gEnemies, gPickups, gTraps, gInteract, gFx, gTriggers;
        Transform markers;
        bool floor2;
        FVLibrary lib;
        GameObject bigChestObj;
        System.Random variantRng;
        Material mRobe, mOrbGlow, mWave, mRuneStone, mGhost;
        Material mLevSkin, mLevBelly, mLevFin, mLevEyes, mLevMouth, mBannerRed, mBannerTeal, mClay;

        [MenuItem("Tools/Fractured Vow/Production Pass (Floor 1 or 2)")]
        static void Open()
        {
            var w = GetWindow<Floor1ProductionPass>("Production Pass");
            w.minSize = new Vector2(380, 320);
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Production pass - Floor 1 or Floor 2", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Build the floor fresh with 'Build Level: Floor 1' or 'Build Level: Floor 2'.\n" +
                "2. With that scene open, click Run Production Pass. It detects which floor it is.\n" +
                "Enemy prefab: your team's finished enemy (e.g. Prefabs/Husk Sentinel), used for every regular mob as-is, " +
                "with its own model, Animator and settings. Bosses keep their own models.\n" +
                "Enemy model is only used when no prefab is set: a bare rigged model gets an Animator built from its pack's clips.",
                MessageType.Info);
            enemyPrefab = (GameObject)EditorGUILayout.ObjectField("Enemy prefab (recommended)", enemyPrefab, typeof(GameObject), false);
            using (new EditorGUI.DisabledScope(enemyPrefab != null))
                enemyModel = (GameObject)EditorGUILayout.ObjectField("Enemy model (optional)", enemyModel, typeof(GameObject), false);
            hideTourSigns = EditorGUILayout.Toggle("Hide numbered tour signs", hideTourSigns);
            setDressing = EditorGUILayout.Toggle("Add set dressing (pilasters, banners, beams, props)", setDressing);
            postProcessing = EditorGUILayout.Toggle("Add post-processing", postProcessing);
            EditorGUILayout.Space();
            if (GUILayout.Button("Run Production Pass", GUILayout.Height(36))) EditorApplication.delayCall += Run;
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.None);
        }

        // =====================================================================================

        void Run()
        {
            if (EditorApplication.isPlaying) { Done("Exit Play mode first."); return; }
            GameObject level = null;
            foreach (var go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (go.transform.Find("Structure") != null && go.transform.Find("Torches") != null) { level = go; break; }
            if (level == null) { Done("Couldn't find a level in the open scene. Open a scene built with 'Build Level: Floor 1' or 'Floor 2' and make sure its 'Structure' and 'Torches' groups haven't been renamed."); return; }
            if (GameObject.Find("Production Pass (applied)") != null) { Done("Already applied. Rebuild the floor, then run the pass again."); return; }
            markers = level.transform.Find("Markers");
            floor2 = markers != null;
            lib = AssetDatabase.LoadAssetAtPath<FVLibrary>(root + "/Resources/FVLibrary.asset");
            variantRng = new System.Random(floor2 ? 202 : 101);
            bigChestObj = null;

            ResolvePaths();
            rng = new System.Random(11);
            MakeMaterials();
            enemyCtrl = null;

            gGameplay = new GameObject("Gameplay (production)").transform;
            gEnemies = Group("Enemies");
            gPickups = Group("Pickups");
            gTraps = Group("Traps");
            gInteract = Group("Interactables");
            gFx = Group("Atmosphere FX");
            gTriggers = Group("Triggers & Checkpoints");

            Transform L = level.transform;
            UpgradeTorches(L.Find("Torches"));
            UpgradeWater(L);
            ReplacePickups(L.Find("Items (placeholders)"));
            ReplaceTraps(L.Find("Traps (placeholders)"));

            Door eastGate, bossGate;
            GameObject fragment;
            ReplaceInteractables(L.Find("Interactables (placeholders)"), out eastGate, out bossGate, out fragment);
            var boss = ReplaceEnemies(L.Find("Enemies (static placeholders)"), bossGate, fragment);
            if (!floor2) BuildPrisoner(new Vector3(4.6f, 0f, 9f));

            AddTriggers(boss);
            AddAtmosphere();
            if (floor2) { BuildCutscene(); DressArena(); }
            PackDressing();
            if (setDressing) Dress(L);
            ApplyLighting();
            if (postProcessing) PostProcessing();

            var signs = L.Find("Tour Signs");
            if (signs != null && hideTourSigns) signs.gameObject.SetActive(false);
            var ceilings = L.Find("Ceilings (hide for top-down screenshots)");
            if (ceilings != null) ceilings.gameObject.SetActive(true);

            new GameObject("Production Pass (applied)").transform.SetParent(gGameplay);

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (!floor2) MakeFirstInBuild(scene.path); // the game starts on Floor 1's title screen
            AssetDatabase.SaveAssets();
            Done((floor2 ? "Floor 2" : "Floor 1") + " production pass applied and saved. Press Play.\nCheck the new Levels/Floor1/Production folder and scripts into Unity Version Control.");
        }

        // =====================================================================================
        // SET DRESSING: pilasters and banners on every wall, beams under ceilings, clutter along the walls
        // =====================================================================================

        void Dress(Transform level)
        {
            var structure = level.Find("Structure");
            if (structure == null) return;
            Physics.SyncTransforms();
            var g = Group("Set Dressing");
            var rnd = new System.Random(4);
            Material banner = floor2 ? mBannerTeal : mBannerRed;

            foreach (var r in structure.GetComponentsInChildren<Renderer>())
            {
                if (r.name != "Wall") continue;
                Transform w = r.transform;
                Vector3 s = w.lossyScale;
                bool alongX = s.x >= s.z;
                float len = alongX ? s.x : s.z, thick = alongX ? s.z : s.x, h = s.y;
                if (len < 3.5f || h < 3f) continue;
                string grp = w.parent != null ? w.parent.name : "";
                bool narrow = grp.Contains("Corridor") || grp.Contains("Channel") || grp.Contains("Window") || grp.Contains("Dock") ||
                              grp.Contains("Stair") || grp.Contains("Hallway") || grp.Contains("Boathouse");
                Vector3 axis = alongX ? Vector3.right : Vector3.forward;
                Vector3 normal = alongX ? Vector3.forward : Vector3.right;
                float bottom = w.position.y - h / 2f, top = w.position.y + h / 2f;

                foreach (float side in new[] { 1f, -1f })
                {
                    Vector3 face = w.position + normal * side * (thick / 2f);
                    Vector3 probe = face + normal * side * 0.9f;
                    if (!Physics.Raycast(new Vector3(probe.x, top - 0.2f, probe.z), Vector3.down, out var hit, h + 1f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!hit.collider.name.StartsWith("Floor") || hit.point.y < -0.05f) continue; // dry floor only, never water
                    float floorY = hit.point.y;
                    float usable = Mathf.Min(top, floorY + 6.5f) - floorY;
                    if (usable < 2.8f) continue;
                    int n = Mathf.Max(1, Mathf.FloorToInt((len - 2f) / 4f));
                    float step = (len - 2f) / n;
                    Vector3 outward = normal * side;
                    for (int i = 0; i <= n; i++)
                    {
                        Vector3 p = w.position + axis * (-len / 2f + 1f + i * step) + outward * (thick / 2f + 0.15f);
                        p.y = floorY;
                        Vector3 sz = alongX ? new Vector3(0.55f, usable, 0.3f) : new Vector3(0.3f, usable, 0.55f);
                        DBox("Pilaster", g, p + Vector3.up * usable / 2f, sz, mStone, true);
                        DBox("Pilaster Base", g, p + Vector3.up * 0.15f + outward * 0.05f, sz + new Vector3(0.2f, 0f, 0.2f) - Vector3.up * (usable - 0.3f), mStone, false);
                        DBox("Pilaster Capital", g, p + Vector3.up * (usable - 0.15f) + outward * 0.05f, sz + new Vector3(0.2f, 0f, 0.2f) - Vector3.up * (usable - 0.3f), mStone, false);
                    }
                    if (narrow) continue;
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 p = w.position + axis * (-len / 2f + 1f + (i + 0.5f) * step) + outward * (thick / 2f);
                        p.y = floorY;
                        double roll = rnd.NextDouble();
                        if (roll < 0.25 && usable > 3.4f) Banner(g, p + outward * 0.04f + Vector3.up * 2.7f, axis, outward, banner);
                        else if (roll < 0.6) Clutter(g, p + outward * 0.55f, rnd);
                    }
                }
            }

            var ceilings = level.Find("Ceilings (hide for top-down screenshots)");
            if (ceilings == null) return;
            foreach (var r in ceilings.GetComponentsInChildren<Renderer>())
            {
                if (r.name != "Ceiling") continue;
                Vector3 s = r.transform.lossyScale;
                float shortSide = Mathf.Min(s.x, s.z), longSide = Mathf.Max(s.x, s.z);
                if (shortSide < 5f || shortSide > 30f) continue;
                bool beamsAcrossX = s.x <= s.z;
                float y = r.transform.position.y - s.y / 2f - 0.2f;
                for (float t = -longSide / 2f + 2f; t <= longSide / 2f - 2f; t += 3.5f)
                {
                    Vector3 c = r.transform.position + (beamsAcrossX ? Vector3.forward : Vector3.right) * t;
                    c.y = y;
                    DBox("Ceiling Beam", r.transform.parent, c, beamsAcrossX ? new Vector3(shortSide - 0.6f, 0.35f, 0.3f) : new Vector3(0.3f, 0.35f, shortSide - 0.6f), mWood, false);
                }
            }
        }

        void Banner(Transform g, Vector3 pos, Vector3 along, Vector3 outward, Material cloth)
        {
            var rot = Quaternion.LookRotation(outward);
            var b = DBox("Banner", g, pos, new Vector3(1.1f, 2.2f, 0.04f), cloth, false);
            b.transform.rotation = rot;
            var rod = DBox("Banner Rod", g, pos + Vector3.up * 1.15f, new Vector3(1.4f, 0.07f, 0.07f), mBrass, false);
            rod.transform.rotation = rot;
            var trim = DBox("Banner Trim", g, pos - Vector3.up * 1.05f + outward * 0.01f, new Vector3(1.1f, 0.1f, 0.03f), mBrass, false);
            trim.transform.rotation = rot;
        }

        static readonly string[] ClutterCats = { "barrel", "crate", "pot", "bones", "candle", "sack", "misc", "chair" };

        void Clutter(Transform g, Vector3 p, System.Random rnd)
        {
            // props from the imported dungeon / medieval packs when available
            if (lib != null && lib.props.Count > 0 && rnd.NextDouble() < 0.8)
            {
                var prefab = LibProp(ClutterCats[rnd.Next(ClutterCats.Length)], rnd);
                if (prefab == null) prefab = lib.props[rnd.Next(lib.props.Count)].prefab;
                if (prefab != null) { PlaceProp(prefab, g, p, rnd.Next(0, 360), 1.4f); return; }
            }
            switch (rnd.Next(5))
            {
                case 0:
                    var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    barrel.name = "Barrel"; barrel.transform.SetParent(g, false);
                    barrel.transform.position = p + Vector3.up * 0.5f; barrel.transform.localScale = new Vector3(0.75f, 0.5f, 0.75f);
                    barrel.GetComponent<Renderer>().sharedMaterial = mWood; barrel.isStatic = true;
                    break;
                case 1:
                    var crate = DBox("Crate", g, p + Vector3.up * 0.4f, Vector3.one * 0.8f, mWood, true);
                    crate.transform.rotation = Quaternion.Euler(0f, rnd.Next(0, 40), 0f);
                    break;
                case 2:
                    for (int i = 0; i < 2; i++)
                    {
                        var urn = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        urn.name = "Urn"; urn.transform.SetParent(g, false);
                        urn.transform.position = p + new Vector3(i * 0.5f - 0.25f, 0.35f, 0f); urn.transform.localScale = new Vector3(0.45f, 0.7f, 0.45f);
                        urn.GetComponent<Renderer>().sharedMaterial = mClay; urn.isStatic = true;
                    }
                    break;
                case 3:
                    for (int i = 0; i < 4; i++)
                        Part(g, PrimitiveType.Cylinder, p + new Vector3((float)rnd.NextDouble() - 0.5f, 0.04f, (float)rnd.NextDouble() - 0.5f) - g.position,
                            new Vector3(0.07f, 0.25f, 0.07f), mBone, new Vector3(90f, rnd.Next(0, 180), 0f));
                    Part(g, PrimitiveType.Sphere, p + new Vector3(0.2f, 0.11f, 0.1f) - g.position, Vector3.one * 0.22f, mBone);
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                    {
                        float hh = 0.18f + i * 0.08f;
                        Part(g, PrimitiveType.Cylinder, p + new Vector3(-0.12f + i * 0.12f, hh / 2f, (i % 2) * 0.08f) - g.position, new Vector3(0.07f, hh / 2f, 0.07f), mBone);
                        Part(g, PrimitiveType.Sphere, p + new Vector3(-0.12f + i * 0.12f, hh + 0.03f, (i % 2) * 0.08f) - g.position, Vector3.one * 0.06f, mFlameCore);
                    }
                    break;
            }
        }

        static GameObject DBox(string name, Transform parent, Vector3 pos, Vector3 size, Material m, bool collider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        static void MakeFirstInBuild(string path)
        {
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(x => x.path == path);
            list.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        void Done(string msg)
        {
            report = msg;
            Debug.Log("[Fractured Vow] " + msg);
            Repaint();
        }

        void ResolvePaths()
        {
            string me = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)).Replace('\\', '/');
            string editorDir = me.Substring(0, me.LastIndexOf('/'));
            root = editorDir.EndsWith("/Editor") ? editorDir.Substring(0, editorDir.LastIndexOf('/')) : "Assets/FracturedVow";
            prodDir = root + "/Levels/Floor1/Production";
            matDir = prodDir + "/Materials";
            texDir = prodDir + "/Textures";
            EnsureFolder(matDir);
            EnsureFolder(texDir);
            EnsureFolder(root + "/Resources/FVFX");
        }

        Transform Group(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(gGameplay, false);
            return t;
        }

        // =====================================================================================
        // ENEMIES + BOSS
        // =====================================================================================

        enum HuskKind { Sentinel, Sleeper, Drowned }

        /// <summary>
        /// Swaps a placeholder for a creature from the imported packs: the Floor 1 catacombs get skeletons, casters
        /// and monsters by name; on Floor 2 most husks become skeletons, monsters or necromancers. Returns false to
        /// keep the regular husk.
        /// </summary>
        bool TryLibraryEnemy(string n, Vector3 pos, Quaternion rot, Vector3[] patrol)
        {
            if (lib == null) return false;
            List<FVLibrary.Character> list = null;
            string role = "melee";
            if (n.Contains("Necromancer")) { list = lib.enemyRanged; role = "ranged"; }
            else if (n.Contains("Ghoul") || n.Contains("Brute")) { list = lib.enemyBrute; role = "brute"; }
            else if (n.Contains("Skeleton")) list = lib.enemyMelee;
            else if (floor2 && !n.Contains("Drowned"))
            {
                double r = variantRng.NextDouble();
                if (r < 0.32) list = lib.enemyMelee;
                else if (r < 0.52) { list = lib.enemyBrute; role = "brute"; }
                else if (r < 0.68) { list = lib.enemyRanged; role = "ranged"; }
            }
            if (list == null || list.Count == 0)
            {
                // the pack for this role isn't imported: fall back to any imported creature, else the husk
                if (n.Contains("Skeleton") || n.Contains("Necromancer") || n.Contains("Ghoul"))
                    list = lib.enemyMelee.Count > 0 ? lib.enemyMelee : lib.enemyBrute.Count > 0 ? lib.enemyBrute : null;
                if (list == null || list.Count == 0) return false;
            }
            BuildLibraryEnemy(list[variantRng.Next(list.Count)], role, pos, rot, patrol);
            return true;
        }

        void BuildLibraryEnemy(FVLibrary.Character c, string role, Vector3 pos, Quaternion rot, Vector3[] patrol)
        {
            bool brute = role == "brute", caster = role == "ranged";
            var go = new GameObject(c.name + (caster ? " (caster)" : brute ? " (brute)" : ""));
            go.transform.SetParent(gEnemies, false);
            go.transform.SetPositionAndRotation(pos, rot);
            float height = brute ? 2.5f : 1.85f;
            var cc = go.AddComponent<CharacterController>();
            cc.height = height; cc.radius = brute ? 0.6f : 0.4f; cc.center = new Vector3(0f, height / 2f, 0f);
            var model = LibModel(c, go.transform, height);
            var anim = model.GetComponentInChildren<Animator>();
            var h = go.AddComponent<Health>();
            h.maxHealth = brute ? 130f : caster ? 45f : 60f;
            h.showWorldBar = true; h.worldBarHeight = height + 0.3f;
            var bridge = go.AddComponent<AnimatorBridge>();
            bridge.animator = anim;
            var ai = go.AddComponent<EnemyAI>();
            Renderer body = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (body == null) body = model.GetComponentInChildren<MeshRenderer>(); // unrigged Monster Pack models
            ai.bodyRenderer = body;
            ai.baseColor = Color.white;
            if (brute) { ai.attackDamage = 18f; ai.chaseSpeed = 3f; ai.patrolSpeed = 1.4f; ai.attackRange = 2.5f; ai.attackWindup = 0.75f; ai.attackKnockback = 8f; ai.dodgeChance = 0.05f; }
            else if (caster) { ai.ranged = true; ai.attackDamage = 11f; ai.chaseSpeed = 3.2f; ai.attackWindup = 0.8f; ai.dodgeChance = 0.3f; }
            else { ai.attackDamage = 11f; ai.chaseSpeed = 3.9f; ai.dodgeChance = 0.25f; }
            if (c.stomp)
            {
                // Monster Pack models have no skeleton: a code-driven stomping walk, swings and flinches instead
                var pm = go.AddComponent<ProceduralMotion>();
                pm.model = model.transform;
                pm.stomp = true;
                pm.pivotHeight = height * 0.5f;
            }
            if (caster) GiveStaff(model);
            if (patrol != null)
            {
                var pts = new List<Transform>();
                for (int i = 0; i < patrol.Length; i++)
                {
                    var wp = new GameObject(go.name + " Waypoint " + (i + 1)).transform;
                    wp.SetParent(gEnemies, false);
                    wp.position = patrol[i];
                    pts.Add(wp);
                }
                ai.patrolPoints = pts.ToArray();
            }
        }

        /// <summary>The Feyloom necromancer's staff (URP version) in a caster's right hand.</summary>
        void GiveStaff(GameObject model)
        {
            GameObject staff = null;
            foreach (var guid in AssetDatabase.FindAssets("SM_Staff t:Prefab"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.Contains("/URP/")) { staff = AssetDatabase.LoadAssetAtPath<GameObject>(p); break; }
            }
            var anim = model.GetComponentInChildren<Animator>();
            if (staff == null || anim == null || !anim.isHuman) return;
            var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null) return;
            var s = (GameObject)PrefabUtility.InstantiatePrefab(staff, hand);
            s.transform.localPosition = Vector3.zero;
            s.transform.localRotation = Quaternion.identity;
            foreach (var col in s.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
        }

        /// <summary>Places a library character's model under 'parent' (unpacked, no colliders), scaled to 'height'.</summary>
        GameObject LibModel(FVLibrary.Character c, Transform parent, float height)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(c.prefab, parent);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (var col in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            foreach (var rb in model.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(rb);
            var anim = model.GetComponentInChildren<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            if (c.controller != null) anim.runtimeAnimatorController = c.controller;
            anim.applyRootMotion = false;
            FitTo(model, height, true);
            return model;
        }

        /// <summary>Scales a model so its height (or largest side) is 'size', standing on its parent's position.</summary>
        static void FitTo(GameObject go, float size, bool byHeight)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float m = byHeight ? b.size.y : Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (m > 0.001f) go.transform.localScale *= size / m;
            b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float floorY = go.transform.parent != null ? go.transform.parent.position.y : 0f;
            go.transform.position += Vector3.up * (floorY - b.min.y);
        }

        GameObject LibProp(string category, System.Random rnd)
        {
            if (lib == null) return null;
            var pool = lib.props.FindAll(p => p != null && p.prefab != null && p.category == category);
            if (pool.Count == 0) return null;
            return pool[rnd != null ? rnd.Next(pool.Count) : Random.Range(0, pool.Count)].prefab;
        }

        GameObject PlaceProp(GameObject prefab, Transform parent, Vector3 pos, float yaw, float size)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float m = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (m > size && m > 0.001f) go.transform.localScale *= size / m;
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                go.transform.position += Vector3.up * (pos.y - b.min.y);
            }
            go.isStatic = true;
            return go;
        }

        WardenBoss ReplaceEnemies(Transform group, Door bossGate, GameObject fragment)
        {
            WardenBoss boss = null;
            if (group == null) return null;
            var drownedPatrols = new Queue<Vector3[]>(new[]
            {
                new[] { new Vector3(56f, 0f, 31f), new Vector3(56f, 0f, 39f) },
                new[] { new Vector3(56f, 0f, 41f), new Vector3(56f, 0f, 48f) },
            });

            foreach (Transform p in Children(group))
            {
                string n = p.name;
                Vector3 pos = p.position; Quaternion rot = p.rotation;
                Vector3[] wps = Waypoints(p);
                if (n.Contains("WARDEN")) boss = BuildBoss(pos, rot, bossGate, fragment);
                else if (n.Contains("LEVIATHAN")) BuildLeviathan(pos, rot, bossGate, fragment);
                else if (TryLibraryEnemy(n, pos, rot, wps)) { }
                else if (n.Contains("Drowned")) BuildHusk("Drowned Husk", pos, rot, HuskKind.Drowned, wps ?? (!floor2 && drownedPatrols.Count > 0 ? drownedPatrols.Dequeue() : null));
                else if (n.Contains("sleeping")) BuildHusk("Husk (sleeping)", pos, rot, HuskKind.Sleeper, wps);
                else BuildHusk(n.Contains("add") ? "Husk Sentinel (arena)" : "Husk Sentinel", pos, rot, HuskKind.Sentinel, wps);
            }
            Object.DestroyImmediate(group.gameObject);
            return boss;
        }

        /// <summary>Floor 1's talking NPC: a ghostly knight chained in the east cell, reached through the bars.</summary>
        void BuildPrisoner(Vector3 pos)
        {
            var go = new GameObject("The Chained Prisoner (NPC)");
            go.transform.SetParent(gInteract, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, -90f, 0f));
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 1.4f; sc.center = new Vector3(0f, 0.8f, 0f);

            if (lib != null && lib.heroes.Count > 0)
            {
                // a pack character, turned into a pale glowing ghost
                var m = LibModel(lib.heroes[0], go.transform, 1.8f);
                foreach (var r in m.GetComponentsInChildren<Renderer>())
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = mGhost;
                    r.sharedMaterials = mats;
                }
            }
            else
            {
                var rig = BuildRig(go.transform, 1f, mGhost, mRustIron, mGhost, mRuneFrost, false, false, false);
                var hand = rig.armR.Find("Hand R");
                if (hand != null) Object.DestroyImmediate(hand.gameObject); // a prisoner has no sword
            }
            foreach (float x in new[] { -0.4f, 0.4f })
                Part(go.transform, PrimitiveType.Cylinder, new Vector3(x, 1.1f, -0.7f), new Vector3(0.04f, 0.6f, 0.04f), mRustIron, new Vector3(70f, 0f, 0f));

            var lt = new GameObject("Ghost Light").AddComponent<Light>();
            lt.transform.SetParent(go.transform, false);
            lt.transform.localPosition = new Vector3(0f, 1.4f, 0.6f);
            lt.type = LightType.Point; lt.color = new Color(0.6f, 0.8f, 1f); lt.range = 4.5f; lt.intensity = 1.6f;
            Sparkle(go.transform, new Color(0.6f, 0.8f, 1f));
            go.AddComponent<DialogueNPC>();
        }

        static Vector3[] Waypoints(Transform placeholder)
        {
            var list = new List<Vector3>();
            foreach (Transform c in placeholder) if (c.name.StartsWith("Waypoint")) list.Add(c.position);
            return list.Count > 0 ? list.ToArray() : null;
        }

        /// <summary>Floor 2's final boss: the Leviathan, a sea serpent fought from platforms and a boat.</summary>
        void BuildLeviathan(Vector3 pos, Quaternion rot, Door gate, GameObject fragment)
        {
            var go = new GameObject("The Leviathan (final boss)");
            go.transform.SetParent(gEnemies, false);
            go.transform.SetPositionAndRotation(new Vector3(pos.x, 0f, pos.z), rot);
            var h = go.AddComponent<Health>();
            h.maxHealth = 900f; h.showWorldBar = false; h.worldBarHeight = 2.5f;
            var body = BuildSerpent(go.transform, "Serpent", true);
            h.anchor = body.head;

            var boss = go.AddComponent<LeviathanBoss>();
            boss.body = body;
            var lake = GameObject.Find("Lake Water Zone");
            boss.lake = lake != null ? lake.GetComponent<WaterZone>() : null;
            boss.telegraphMaterial = mTelegraph;
            boss.orbMaterial = mOrbGlow;
            boss.waveMaterial = mWave;
            boss.unlockOnDeath = gate != null ? new[] { gate } : new Door[0];
            boss.activateOnDeath = fragment != null ? new[] { fragment } : new GameObject[0];
            boss.wake = LeviathanWake(go.transform);
            boss.splash = Splash(go.transform);
        }

        /// <summary>A sea serpent made of parts: a horned head with a hinged jaw, glowing eyes and 16 tapering body segments with fins.</summary>
        LeviathanBody BuildSerpent(Transform parent, string name, bool hitbox)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var head = Pivot("Head", root, Vector3.zero);
            Part(head, PrimitiveType.Cube, new Vector3(0f, 0.3f, 0.6f), new Vector3(2f, 1.3f, 3f), mLevSkin);
            Part(head, PrimitiveType.Cube, new Vector3(0f, 0.8f, 0.3f), new Vector3(1.6f, 0.45f, 2.3f), mLevSkin);
            Part(head, PrimitiveType.Cube, new Vector3(0f, 0.1f, 2.4f), new Vector3(1.4f, 0.75f, 1.5f), mLevSkin);
            Part(head, PrimitiveType.Cube, new Vector3(0f, -0.2f, 1.3f), new Vector3(1.2f, 0.15f, 1.9f), mLevMouth);
            Part(head, PrimitiveType.Sphere, new Vector3(-0.85f, 0.7f, 1.2f), new Vector3(0.34f, 0.26f, 0.34f), mLevEyes);
            Part(head, PrimitiveType.Sphere, new Vector3(0.85f, 0.7f, 1.2f), new Vector3(0.34f, 0.26f, 0.34f), mLevEyes);
            Part(head, PrimitiveType.Cylinder, new Vector3(-0.7f, 1.15f, -0.5f), new Vector3(0.18f, 0.9f, 0.18f), mBone, new Vector3(-55f, 0f, 15f));
            Part(head, PrimitiveType.Cylinder, new Vector3(0.7f, 1.15f, -0.5f), new Vector3(0.18f, 0.9f, 0.18f), mBone, new Vector3(-55f, 0f, -15f));
            Part(head, PrimitiveType.Cube, new Vector3(-1.15f, 0.3f, -0.3f), new Vector3(0.08f, 1.2f, 1.7f), mLevFin, new Vector3(0f, -22f, 0f));
            Part(head, PrimitiveType.Cube, new Vector3(1.15f, 0.3f, -0.3f), new Vector3(0.08f, 1.2f, 1.7f), mLevFin, new Vector3(0f, 22f, 0f));
            for (int i = 0; i < 5; i++) Part(head, PrimitiveType.Cube, new Vector3(-0.5f + i * 0.25f, -0.32f, 2.75f), new Vector3(0.08f, 0.32f, 0.08f), mBone);
            var jaw = Pivot("Jaw", head, new Vector3(0f, -0.35f, -0.4f));
            Part(jaw, PrimitiveType.Cube, new Vector3(0f, -0.25f, 1.45f), new Vector3(1.6f, 0.5f, 2.9f), mLevBelly);
            for (int i = 0; i < 5; i++) Part(jaw, PrimitiveType.Cube, new Vector3(-0.5f + i * 0.25f, 0.12f, 2.65f), new Vector3(0.08f, 0.26f, 0.08f), mBone);
            var eye = new GameObject("Eye Glow").AddComponent<Light>();
            eye.transform.SetParent(head, false);
            eye.transform.localPosition = new Vector3(0f, 0.8f, 1.8f);
            eye.type = LightType.Point; eye.color = new Color(0.4f, 1f, 0.9f); eye.range = 9f; eye.intensity = 3f;
            if (hitbox)
            {
                var cap = head.gameObject.AddComponent<CapsuleCollider>();
                cap.direction = 2; cap.radius = 1.3f; cap.height = 4.8f; cap.center = new Vector3(0f, 0.2f, 0.9f);
            }

            // 24 overlapping segments: armoured scale plates on top, a pale belly, a row of dorsal spikes,
            // paired side fins every few segments and glowing spots running down the flanks
            const int count = 24;
            var segs = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                var seg = Pivot("Segment " + (i + 1), root, Vector3.back * (i + 1) * 1.45f);
                float k = i / (float)(count - 1);
                float sz = Mathf.Lerp(2.1f, 0.45f, Mathf.Pow(k, 0.85f)) * (1f + Mathf.Sin(k * Mathf.PI) * 0.12f);
                Part(seg, PrimitiveType.Sphere, Vector3.zero, new Vector3(sz * 1.05f, sz * 0.95f, sz * 1.45f), mLevSkin);
                Part(seg, PrimitiveType.Sphere, new Vector3(0f, -sz * 0.24f, 0.05f * sz), new Vector3(sz * 0.86f, sz * 0.62f, sz * 1.35f), mLevBelly);
                Part(seg, PrimitiveType.Sphere, new Vector3(0f, sz * 0.22f, -sz * 0.15f), new Vector3(sz * 0.95f, sz * 0.55f, sz * 0.9f), mLevSkin, new Vector3(-12f, 0f, 0f));
                Part(seg, PrimitiveType.Cylinder, new Vector3(0f, sz * 0.62f, -sz * 0.1f), new Vector3(0.12f * sz, sz * 0.42f, 0.12f * sz), mBone, new Vector3(-35f, 0f, 0f));
                Part(seg, PrimitiveType.Cube, new Vector3(0f, sz * 0.5f, -sz * 0.35f), new Vector3(0.06f, sz * 0.5f, sz * 0.8f), mLevFin, new Vector3(-25f, 0f, 0f));
                if (i % 4 == 1 && i < count - 4)
                {
                    Part(seg, PrimitiveType.Cube, new Vector3(-sz * 0.55f, -sz * 0.15f, 0f), new Vector3(sz * 0.9f, 0.05f, sz * 0.6f), mLevFin, new Vector3(0f, 25f, -25f));
                    Part(seg, PrimitiveType.Cube, new Vector3(sz * 0.55f, -sz * 0.15f, 0f), new Vector3(sz * 0.9f, 0.05f, sz * 0.6f), mLevFin, new Vector3(0f, -25f, 25f));
                }
                if (i % 2 == 0)
                {
                    Part(seg, PrimitiveType.Sphere, new Vector3(-sz * 0.48f, 0f, 0f), Vector3.one * sz * 0.12f, mLevEyes);
                    Part(seg, PrimitiveType.Sphere, new Vector3(sz * 0.48f, 0f, 0f), Vector3.one * sz * 0.12f, mLevEyes);
                }
                if (i == count - 1)
                {
                    Part(seg, PrimitiveType.Cube, new Vector3(0f, 0.4f, -sz * 1.4f), new Vector3(0.07f, 1.6f, 1.4f), mLevFin, new Vector3(-30f, 0f, 0f));
                    Part(seg, PrimitiveType.Cube, new Vector3(0f, -0.4f, -sz * 1.4f), new Vector3(0.07f, 1.6f, 1.4f), mLevFin, new Vector3(30f, 0f, 0f));
                }
                segs[i] = seg;
            }
            var body = root.gameObject.AddComponent<LeviathanBody>();
            body.head = head;
            body.jaw = jaw;
            body.segments = segs;
            body.spacing = 1.45f;
            return body;
        }

        ParticleSystem LeviathanWake(Transform parent)
        {
            var ps = NewPS("Leviathan Wake", parent, Vector3.zero, fxAlpha, 160);
            var main = ps.main;
            main.startLifetime = Curve(1f, 1.8f);
            main.startSpeed = Curve(0.5f, 1.6f);
            main.startSize = Curve(0.6f, 1.4f);
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.45f);
            main.startRotation = Curve(0f, Mathf.PI * 2f);
            var em = ps.emission; em.rateOverTime = 45f; em.enabled = false;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 1.6f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f);
            SizeCurve(ps, 0.6f, 1.6f);
            return ps;
        }

        /// <summary>
        /// Pack details: chandeliers in every big room, the Warden's throne, ground fog over the water and
        /// fireflies around the piers (BrokenVector Low Poly Dungeon + Particle Pack).
        /// </summary>
        void PackDressing()
        {
            if (lib == null) return;
            var g = Group("Pack Details");
            var ceilings = GameObject.Find("Ceilings (hide for top-down screenshots)");
            if (lib.chandelier != null && ceilings != null)
                foreach (var r in ceilings.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name != "Ceiling") continue;
                    Vector3 s = r.transform.lossyScale;
                    if (Mathf.Min(s.x, s.z) < 15f || Mathf.Max(s.x, s.z) > 30f) continue;
                    float y = r.transform.position.y - s.y / 2f;
                    var ch = PlaceProp(lib.chandelier, r.transform.parent, new Vector3(r.transform.position.x, y - 1.6f, r.transform.position.z), 0f, 1.8f);
                    // hang it from the ceiling (PlaceProp sets the bottom at the given height)
                    var lt = new GameObject("Chandelier Light").AddComponent<Light>();
                    lt.transform.SetParent(ch.transform, true);
                    lt.transform.position = new Vector3(r.transform.position.x, y - 1.2f, r.transform.position.z);
                    lt.type = LightType.Point; lt.color = new Color(1f, 0.75f, 0.45f); lt.range = 11f; lt.intensity = 2.2f;
                    if (lib.candles != null)
                    {
                        var c = (GameObject)PrefabUtility.InstantiatePrefab(lib.candles, ch.transform);
                        c.transform.position = lt.transform.position;
                        c.transform.localScale = Vector3.one * 0.5f;
                    }
                }
            if (!floor2 && lib.throne != null) PlaceProp(lib.throne, g, new Vector3(50f, 0f, 90.4f), 180f, 2.6f);
            if (lib.groundFog != null)
            {
                var lake = GameObject.Find("Lake Water Zone");
                var water = lake != null ? lake.GetComponent<BoxCollider>() : null;
                if (water != null)
                {
                    Bounds b = water.bounds;
                    for (float x = b.min.x + 15f; x < b.max.x; x += 30f)
                        for (float z = b.min.z + 15f; z < b.max.z; z += 30f)
                        {
                            var f = (GameObject)PrefabUtility.InstantiatePrefab(lib.groundFog, g);
                            f.transform.position = new Vector3(x, 0.1f, z);
                        }
                }
                else if (!floor2)
                {
                    var f = (GameObject)PrefabUtility.InstantiatePrefab(lib.groundFog, g); // the cistern
                    f.transform.position = new Vector3(46f, -0.3f, 35f);
                }
            }
            if (lib.fireflies != null)
                foreach (var pier in new[] { "Western Pier", "Eastern Pier", "North Pier", "Flooded Landing Dock" })
                {
                    var t = GameObject.Find(pier);
                    if (t == null) continue;
                    var f = (GameObject)PrefabUtility.InstantiatePrefab(lib.fireflies, g);
                    f.transform.position = t.transform.position + Vector3.up * 1.5f;
                }
        }

        /// <summary>Harpoon-hunter props (Deep Sea Hunter's Station) on the lake platforms and piers. They sink with their platform.</summary>
        void DressArena()
        {
            if (lib == null || lib.arenaProps.Count == 0) return;
            var rnd = new System.Random(9);
            GameObject Pick() => lib.arenaProps[rnd.Next(lib.arenaProps.Count)];
            foreach (var bp in Object.FindObjectsByType<BreakablePlatform>(FindObjectsSortMode.None))
            {
                Vector3 c = bp.transform.position;
                foreach (var v in new[] { new Vector2(-3.6f, 3.6f), new Vector2(3.6f, -3.6f) })
                    PlaceProp(Pick(), bp.transform, new Vector3(c.x + v.x, 0.4f, c.z + v.y), rnd.Next(0, 360), 2.4f).isStatic = false;
            }
            foreach (var pier in new[] { "Western Pier", "Eastern Pier", "North Pier" })
            {
                var t = GameObject.Find(pier);
                if (t != null) PlaceProp(Pick(), gInteract, t.transform.position + t.transform.right * 1.5f, rnd.Next(0, 360), 2f);
            }
        }

        /// <summary>Floor 2's opening: the Leviathan smashes the wall at the end of the hallway.</summary>
        void BuildCutscene()
        {
            if (markers == null) return;
            var wallMarker = markers.Find("Cutscene Wall");
            var rise = markers.Find("Cutscene Rise");
            var strike = markers.Find("Cutscene Strike");
            var wall = GameObject.Find("Breakable Wall");
            if (wallMarker == null || rise == null || strike == null || wall == null) return;
            var g = Group("Opening Cutscene");
            var cameo = BuildSerpent(g, "Leviathan (cutscene)", false);
            cameo.head.position = rise.position;
            var dir = g.gameObject.AddComponent<CutsceneDirector>();
            dir.wall = wall.transform;
            dir.cameo = cameo;
            dir.risePoint = rise;
            dir.strikePoint = strike;
        }

        ParticleSystem Splash(Transform parent)
        {
            var ps = NewPS("Lake Splash", parent, new Vector3(0f, 0f, 0f), fxAlpha, 200);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.startLifetime = Curve(0.8f, 1.4f);
            main.startSpeed = Curve(6f, 11f);
            main.startSize = Curve(0.4f, 1.1f);
            main.startColor = new Color(0.8f, 0.92f, 1f, 0.7f);
            main.gravityModifier = 1.2f;
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 140) });
            ShapeUpCone(ps, 35f, 2.2f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f);
            return ps;
        }

        void BuildHusk(string name, Vector3 pos, Quaternion rot, HuskKind kind, Vector3[] patrol)
        {
            if (enemyPrefab != null) { HuskFromPrefab(name, pos, rot, kind, patrol); return; }
            var go = new GameObject(name);
            go.transform.SetParent(gEnemies, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.4f; cc.center = new Vector3(0f, 0.9f, 0f);
            var h = go.AddComponent<Health>();
            h.maxHealth = kind == HuskKind.Drowned ? 50f : 60f;
            h.showWorldBar = true; h.worldBarHeight = 2.2f;
            var bridge = go.AddComponent<AnimatorBridge>();
            var ai = go.AddComponent<EnemyAI>();

            if (kind == HuskKind.Sleeper) { ai.sightRange = 5f; ai.hearingRange = 2.5f; ai.fieldOfView = 90f; }
            if (kind == HuskKind.Drowned) { ai.patrolSpeed = 1.4f; ai.chaseSpeed = 3.2f; ai.attackDamage = 10f; }

            if (patrol != null)
            {
                var pts = new List<Transform>();
                for (int i = 0; i < patrol.Length; i++)
                {
                    var wp = new GameObject(name + " Waypoint " + (i + 1)).transform;
                    wp.SetParent(gEnemies, false);
                    wp.position = patrol[i];
                    pts.Add(wp);
                }
                ai.patrolPoints = pts.ToArray();
            }

            Material skin = kind == HuskKind.Drowned ? mDrownedSkin : mSkin;
            Material cloth = kind == HuskKind.Drowned ? mDrownedCloth : mCloth;
            if (enemyModel != null)
            {
                Color modelTint = kind == HuskKind.Drowned ? new Color(0.8f, 0.95f, 1f) : Color.white;
                var r = AttachModel(go.transform, 1.8f, modelTint, bridge);
                ai.bodyRenderer = r; ai.baseColor = modelTint;
            }
            else
            {
                var rig = BuildRig(go.transform, 1f, skin, kind == HuskKind.Drowned ? mRustIron : mIron, cloth, mEyes,
                    kind != HuskKind.Drowned, false, false);
                ai.bodyRenderer = rig.torso.GetComponentInChildren<Renderer>();
                ai.baseColor = kind == HuskKind.Drowned ? new Color(0.4f, 0.55f, 0.55f) : new Color(0.42f, 0.4f, 0.38f);
                var ha = go.AddComponent<HuskAnimator>();
                ha.rig = rig;
                ha.sleepWhenIdle = kind == HuskKind.Sleeper;
            }
        }

        /// <summary>Places the team's enemy prefab (kept linked, so later prefab edits show up) and tunes it for its kind.</summary>
        void HuskFromPrefab(string name, Vector3 pos, Quaternion rot, HuskKind kind, Vector3[] patrol)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
            go.name = name;
            go.transform.SetParent(gEnemies, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var ai = go.GetComponent<EnemyAI>();
            var h = go.GetComponent<Health>();
            if (ai == null || h == null)
            {
                Debug.LogWarning("[Fractured Vow] The enemy prefab needs Health and EnemyAI components; '" + enemyPrefab.name + "' is missing one.");
                return;
            }
            h.showWorldBar = true;
            if (kind == HuskKind.Sleeper) { ai.sightRange = 5f; ai.hearingRange = 2.5f; ai.fieldOfView = 90f; }
            if (kind == HuskKind.Drowned)
            {
                h.maxHealth = 50f;
                ai.patrolSpeed = 1.4f; ai.chaseSpeed = 3.2f; ai.attackDamage = 10f;
                ai.baseColor = new Color(0.8f, 0.95f, 1f);
            }
            if (patrol != null)
            {
                var pts = new List<Transform>();
                for (int i = 0; i < patrol.Length; i++)
                {
                    var wp = new GameObject(name + " Waypoint " + (i + 1)).transform;
                    wp.SetParent(gEnemies, false);
                    wp.position = patrol[i];
                    pts.Add(wp);
                }
                ai.patrolPoints = pts.ToArray();
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
            PrefabUtility.RecordPrefabInstancePropertyModifications(h);
        }

        WardenBoss BuildBoss(Vector3 pos, Quaternion rot, Door bossGate, GameObject fragment)
        {
            var go = new GameObject("The Warden of the Pit (boss)");
            go.transform.SetParent(gEnemies, false);
            go.transform.SetPositionAndRotation(pos, rot);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 4f; cc.radius = 1.1f; cc.center = new Vector3(0f, 2f, 0f);
            var h = go.AddComponent<Health>();
            h.maxHealth = 450f; h.showWorldBar = false; h.worldBarHeight = 4.4f;
            var bridge = go.AddComponent<AnimatorBridge>();
            PrimitiveRig rig = null;
            // the Warden always keeps his built-in model; the enemy model slot is only for regular mobs
            rig = BuildRig(go.transform, 2.2f, mBossSkin, mRustIron, mBossCloth, mBossEyes, true, true, true);

            var boss = go.AddComponent<WardenBoss>();
            boss.rig = rig;
            boss.telegraphMaterial = mTelegraph;
            boss.unlockOnDeath = bossGate != null ? new[] { bossGate } : new Door[0];
            var onDeath = new List<GameObject>();
            if (fragment != null) onDeath.Add(fragment);
            if (bigChestObj != null) onDeath.Add(bigChestObj);
            boss.activateOnDeath = onDeath.ToArray();
            boss.aura = Aura(go.transform);
            boss.slamDust = SlamDust(go.transform);

            var eyeLight = new GameObject("Eye Glow").AddComponent<Light>();
            eyeLight.transform.SetParent(go.transform, false);
            eyeLight.transform.localPosition = new Vector3(0f, 3.8f, 0.8f);
            eyeLight.type = LightType.Point; eyeLight.color = new Color(1f, 0.25f, 0.1f); eyeLight.range = 4f; eyeLight.intensity = 2f;
            return boss;
        }

        /// <summary>Builds a jointed character from parts and returns its rig. Faces +Z, feet at the root.</summary>
        PrimitiveRig BuildRig(Transform parent, float scale, Material skin, Material armor, Material cloth, Material eyes,
            bool helmet, bool greatAxe, bool boss)
        {
            var model = new GameObject("Model").transform;
            model.SetParent(parent, false);
            model.localScale = Vector3.one * scale;
            var rig = model.gameObject.AddComponent<PrimitiveRig>();
            if (boss) rig.strideFrequency = 0.9f;

            var hips = Pivot("Hips", model, new Vector3(0f, 0.95f, 0f));
            Transform Leg(string n, float x)
            {
                var l = Pivot(n, hips, new Vector3(x, 0f, 0f));
                Part(l, PrimitiveType.Cube, new Vector3(0f, -0.42f, 0f), new Vector3(0.2f, 0.86f, 0.22f), cloth);
                Part(l, PrimitiveType.Cube, new Vector3(0f, -0.86f, 0.05f), new Vector3(0.23f, 0.14f, 0.34f), armor);
                Part(l, PrimitiveType.Cube, new Vector3(0f, -0.5f, 0.1f), new Vector3(0.22f, 0.3f, 0.06f), armor);
                return l;
            }
            var legL = Leg("Leg L", -0.14f);
            var legR = Leg("Leg R", 0.14f);

            var torso = Pivot("Torso", hips, new Vector3(0f, 0.02f, 0f));
            Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f), new Vector3(0.54f, 0.66f, 0.3f), skin);
            Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.47f, 0.02f), new Vector3(0.62f, 0.38f, 0.36f), armor);
            Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.6f, 0.1f, 0.36f), mRustIron);
            Part(torso, PrimitiveType.Cube, new Vector3(0f, -0.18f, 0.17f), new Vector3(0.42f, 0.46f, 0.04f), cloth);
            Part(torso, PrimitiveType.Cube, new Vector3(0f, -0.18f, -0.17f), new Vector3(0.42f, 0.46f, 0.04f), cloth);
            if (boss) Part(torso, PrimitiveType.Cube, new Vector3(0f, 0.05f, -0.22f), new Vector3(0.64f, 1.1f, 0.04f), cloth, new Vector3(8f, 0f, 0f));

            var head = Pivot("Head", torso, new Vector3(0f, 0.72f, 0f));
            Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.16f, 0f), new Vector3(0.3f, 0.34f, 0.3f), skin);
            Part(head, PrimitiveType.Cube, new Vector3(-0.065f, 0.18f, 0.135f), new Vector3(0.07f, 0.03f, 0.03f), eyes);
            Part(head, PrimitiveType.Cube, new Vector3(0.065f, 0.18f, 0.135f), new Vector3(0.07f, 0.03f, 0.03f), eyes);
            if (helmet)
            {
                Part(head, PrimitiveType.Cylinder, new Vector3(0f, 0.28f, 0f), new Vector3(0.36f, 0.08f, 0.36f), armor);
                Part(head, PrimitiveType.Cube, new Vector3(0f, 0.2f, 0.15f), new Vector3(0.04f, 0.16f, 0.04f), armor);
            }
            if (boss)
            {
                Part(head, PrimitiveType.Cylinder, new Vector3(-0.2f, 0.4f, 0f), new Vector3(0.06f, 0.18f, 0.06f), mBone, new Vector3(0f, 0f, 35f));
                Part(head, PrimitiveType.Cylinder, new Vector3(0.2f, 0.4f, 0f), new Vector3(0.06f, 0.18f, 0.06f), mBone, new Vector3(0f, 0f, -35f));
            }

            Transform Arm(string n, float x)
            {
                var a = Pivot(n, torso, new Vector3(x, 0.6f, 0f));
                Part(a, PrimitiveType.Cube, new Vector3(0f, -0.3f, 0f), new Vector3(0.15f, 0.62f, 0.15f), skin);
                Part(a, PrimitiveType.Cube, new Vector3(0f, -0.42f, 0f), new Vector3(0.18f, 0.24f, 0.18f), armor);
                Part(a, PrimitiveType.Sphere, new Vector3(0f, 0.02f, 0f), boss ? new Vector3(0.36f, 0.26f, 0.36f) : new Vector3(0.26f, 0.2f, 0.26f), armor);
                return a;
            }
            var armL = Arm("Arm L", -0.37f);
            var armR = Arm("Arm R", 0.37f);

            var hand = Pivot("Hand R", armR, new Vector3(0f, -0.6f, 0.02f));
            if (greatAxe)
            {
                Part(hand, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.45f), new Vector3(0.06f, 0.75f, 0.06f), mWood, new Vector3(90f, 0f, 0f));
                Part(hand, PrimitiveType.Cube, new Vector3(0f, 0.18f, 1.05f), new Vector3(0.05f, 0.42f, 0.34f), mRustIron);
                Part(hand, PrimitiveType.Cube, new Vector3(0f, -0.12f, 1.05f), new Vector3(0.05f, 0.22f, 0.26f), mRustIron);
            }
            else
            {
                Part(hand, PrimitiveType.Cube, new Vector3(0f, 0f, 0.55f), new Vector3(0.05f, 0.025f, 0.95f), mIron);
                Part(hand, PrimitiveType.Cube, new Vector3(0f, 0f, 0.07f), new Vector3(0.24f, 0.04f, 0.05f), mRustIron);
            }

            rig.hips = hips; rig.torso = torso; rig.head = head;
            rig.armL = armL; rig.armR = armR; rig.legL = legL; rig.legR = legR;
            return rig;
        }

        Renderer AttachModel(Transform parent, float height, Color tint, AnimatorBridge bridge)
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(enemyModel);
            if (PrefabUtility.IsPartOfPrefabInstance(model))
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Model";
            model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var rb in model.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);

            var rends = model.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                if (b.size.y > 0.01f) model.transform.localScale *= height / b.size.y;
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                model.transform.position += Vector3.up * (parent.position.y - b.min.y);
            }
            foreach (var r in rends)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) if (mats[i] != null) mats[i] = TintedCopy(mats[i], tint);
                r.sharedMaterials = mats;
            }

            var anim = model.GetComponentInChildren<Animator>();
            if (anim == null) anim = model.AddComponent<Animator>();
            var ctrl = EnemyController();
            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            bridge.animator = anim;
            return rends.Length > 0 ? rends[0] : null;
        }

        AnimatorController enemyCtrl;

        /// <summary>Builds FV_Enemy.controller from the enemy model's own pack clips (idle, walk, run, attack, hit, death).</summary>
        AnimatorController EnemyController()
        {
            if (enemyCtrl != null) return enemyCtrl;
            string path = AssetDatabase.GetAssetPath(enemyModel);
            string[] parts = path.Split('/');
            string folder = parts.Length > 2 ? parts[0] + "/" + parts[1] : "Assets";

            var clips = new List<AnimationClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (a is AnimationClip c && !c.name.StartsWith("__preview__") && !c.legacy && !clips.Contains(c)) clips.Add(c);

            AnimationClip Pick(params string[] keys)
            {
                foreach (var k in keys)
                    foreach (var c in clips)
                        if (c.name.ToLowerInvariant().Contains(k)) return c;
                return null;
            }

            var idle = Pick("idle");
            var walk = Pick("walk");
            var run = Pick("run", "charge");
            var attack = Pick("attack", "slash", "swing");
            var hurt = Pick("hit", "hurt", "damage");
            var death = Pick("death", "die");
            if (idle == null && walk == null)
            {
                Debug.LogWarning("[Fractured Vow] No animation clips found in " + folder + "; enemies will not animate.");
                return null;
            }
            Debug.Log("[Fractured Vow] Enemy clips: idle=" + idle + " walk=" + walk + " run=" + run +
                      " attack=" + attack + " hit=" + hurt + " death=" + death);

            EnsureFolder(prodDir + "/Animation");
            string cpath = prodDir + "/Animation/FV_Enemy.controller";
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(cpath) != null) AssetDatabase.DeleteAsset(cpath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(cpath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Hurt", AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            var baseIdle = idle != null ? idle : walk;
            var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(baseIdle, 0f);
            if (walk != null) tree.AddChild(walk, 0.45f);
            if (run != null) tree.AddChild(run, 1f);
            sm.defaultState = loco;

            AnimatorState NewState(string n, Motion m)
            {
                var s = sm.AddState(n);
                s.motion = m != null ? m : baseIdle;
                return s;
            }
            var sAttack = NewState("Attack", attack);
            var sHurt = NewState("Hurt", hurt);
            var sDeath = NewState("Death", death);

            void FromAny(AnimatorState to, string trigger)
            {
                var t = sm.AddAnyStateTransition(to);
                t.hasExitTime = false; t.duration = 0.08f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0, trigger);
                t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
            }
            FromAny(sAttack, "Attack");
            FromAny(sHurt, "Hurt");
            var td = sm.AddAnyStateTransition(sDeath);
            td.hasExitTime = false; td.duration = 0.1f; td.canTransitionToSelf = false;
            td.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            foreach (var s in new[] { sAttack, sHurt })
            {
                var back = s.AddTransition(loco);
                back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.15f;
            }

            AssetDatabase.SaveAssets();
            enemyCtrl = ac;
            return ac;
        }

        Material TintedCopy(Material src, Color tint)
        {
            string path = matDir + "/Enemy_" + src.name.Replace(" (Instance)", "") + "_" + ColorUtility.ToHtmlStringRGB(tint) + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) return m;
            m = new Material(src);
            Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
            SetColor(m, c * tint);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        // =====================================================================================
        // PICKUPS
        // =====================================================================================

        void ReplacePickups(Transform group)
        {
            if (group == null) return;
            foreach (Transform p in Children(group))
            {
                Vector3 pos = p.position;
                string n = p.name;
                if (n.StartsWith("Health Potion"))
                    MakePickup("Health Potion", pos, PickupType.Health, 35f, true, new Color(1f, 0.25f, 0.2f), HealthPotionModel);
                else if (n.StartsWith("Mana Vial"))
                    MakePickup("Mana Vial", pos, PickupType.Mana, 40f, true, new Color(0.3f, 0.5f, 1f), ManaVialModel);
                else if (n.StartsWith("Frost Shard"))
                {
                    var pk = MakePickup("Frost Shard tome", pos, PickupType.Spell, 0f, false, new Color(0.5f, 0.85f, 1f), t => TomeModel(t, mRuneFrost));
                    pk.spell = SpellType.FrostShard;
                }
                else if (n.StartsWith("Firebolt"))
                {
                    var pk = MakePickup("Firebolt tome", pos, PickupType.Spell, 0f, false, new Color(1f, 0.55f, 0.2f), t => TomeModel(t, mRuneFire));
                    pk.spell = SpellType.Firebolt;
                }
                else if (n.StartsWith("Stone Wall"))
                {
                    var pk = MakePickup("Stone Wall tome", pos, PickupType.Spell, 0f, false, new Color(0.9f, 0.75f, 0.45f), t => TomeModel(t, mRuneStone));
                    pk.spell = SpellType.StoneWall;
                }
                else if (n.StartsWith("Sharpened Blade"))
                {
                    var pk = MakePickup("Sharpened Blade", pos, PickupType.Gear, 0.15f, false, new Color(0.85f, 0.9f, 1f), BladeModel);
                    pk.gearStat = GearStat.Damage;
                }
                else if (n.StartsWith("Knight's Sigil"))
                {
                    var pk = MakePickup("Knight's Sigil", pos, PickupType.Gear, 25f, false, new Color(1f, 0.4f, 0.3f), SigilModel);
                    pk.gearStat = GearStat.MaxHealth;
                }
                else if (n.StartsWith("Iron Pauldron"))
                {
                    var pk = MakePickup("Iron Pauldron", pos, PickupType.Gear, 0.1f, false, new Color(1f, 0.85f, 0.5f), PauldronModel);
                    pk.gearStat = GearStat.Armor;
                }
            }
            Object.DestroyImmediate(group.gameObject);
        }

        Pickup MakePickup(string name, Vector3 pos, PickupType type, float amount, bool auto, Color glow, System.Action<Transform> model)
        {
            var go = new GameObject(name);
            go.transform.SetParent(gPickups, false);
            go.transform.position = pos;
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 0.8f;
            model(go.transform);
            var lt = new GameObject("Glow").AddComponent<Light>();
            lt.transform.SetParent(go.transform, false);
            lt.type = LightType.Point; lt.color = glow; lt.range = 2.5f; lt.intensity = 1.2f;
            Sparkle(go.transform, glow);
            var pk = go.AddComponent<Pickup>();
            pk.type = type; pk.displayName = name; pk.amount = amount; pk.autoPickup = auto;
            pk.spinSpeed = auto ? 90f : 30f;
            return pk;
        }

        void HealthPotionModel(Transform t)
        {
            Part(t, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.3f, mRedGlass);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.19f, 0f), new Vector3(0.1f, 0.07f, 0.1f), mRedGlass);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.27f, 0f), new Vector3(0.11f, 0.04f, 0.11f), mCork);
        }

        void ManaVialModel(Transform t)
        {
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.18f, 0.18f, 0.18f), mBlueGlass);
            Part(t, PrimitiveType.Sphere, new Vector3(0f, -0.15f, 0f), new Vector3(0.18f, 0.12f, 0.18f), mBlueGlass);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0.23f, 0f), new Vector3(0.1f, 0.04f, 0.1f), mCork);
        }

        void TomeModel(Transform t, Material rune)
        {
            Part(t, PrimitiveType.Cube, Vector3.zero, new Vector3(0.42f, 0.1f, 0.32f), mLeather);
            Part(t, PrimitiveType.Cube, new Vector3(0.015f, 0f, 0f), new Vector3(0.4f, 0.08f, 0.3f), mPage);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0.052f, 0f), new Vector3(0.14f, 0.006f, 0.14f), rune, new Vector3(0f, 45f, 0f));
            Part(t, PrimitiveType.Cube, new Vector3(-0.2f, 0f, 0f), new Vector3(0.03f, 0.11f, 0.33f), mBrass);
        }

        void BladeModel(Transform t)
        {
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0f, 0.1f), new Vector3(0.06f, 0.025f, 0.85f), mIron);
            Part(t, PrimitiveType.Cube, new Vector3(0f, 0f, -0.33f), new Vector3(0.3f, 0.05f, 0.05f), mBrass);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.45f), new Vector3(0.05f, 0.1f, 0.05f), mLeather, new Vector3(90f, 0f, 0f));
        }

        void SigilModel(Transform t)
        {
            Part(t, PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.45f, 0.03f, 0.45f), mBrass);
            Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0f), Vector3.one * 0.13f, mRedGlass);
        }

        void PauldronModel(Transform t)
        {
            Part(t, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.45f, 0.3f, 0.45f), mIron);
            Part(t, PrimitiveType.Cylinder, new Vector3(0f, -0.03f, 0f), new Vector3(0.47f, 0.02f, 0.47f), mBrass);
            Part(t, PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0f), new Vector3(0.08f, 0.08f, 0.08f), mBrass);
        }

        // =====================================================================================
        // TRAPS
        // =====================================================================================

        void ReplaceTraps(Transform group)
        {
            if (group == null) return;
            int plate = 0, vent = 0;
            bool pitDone = false;
            foreach (Transform p in Children(group))
            {
                Vector3 pos = p.position;
                if (p.name.StartsWith("Pit Spikes"))
                {
                    if (!pitDone) { BuildPit(); pitDone = true; }
                }
                else if (p.name.StartsWith("Spike Plate")) BuildSpikePlate(new Vector3(pos.x, 0f, pos.z), plate++ * 1.1f);
                else if (p.name.StartsWith("Flame Vent")) BuildVent(new Vector3(pos.x, 0f, pos.z), vent++ * 1.3f);
            }
            Object.DestroyImmediate(group.gameObject);
        }

        void BuildPit()
        {
            var pit = new GameObject("Spiked Pit");
            pit.transform.SetParent(gTraps, false);
            pit.transform.position = new Vector3(20.25f, -1.2f, 32f);
            for (int x = 0; x < 4; x++)
                for (int z = 0; z < 7; z++)
                {
                    float h = 0.45f + (float)rng.NextDouble() * 0.3f;
                    var s = Part(pit.transform, PrimitiveType.Cylinder, new Vector3(-0.95f + x * 0.63f, h / 2f, -2.1f + z * 0.7f),
                        new Vector3(0.06f, h / 2f, 0.06f), mRustIron, new Vector3(rng.Next(-8, 8), 0f, rng.Next(-8, 8)));
                    s.name = "Spike";
                }
            var box = pit.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.4f, 0f); box.size = new Vector3(2.5f, 0.8f, 5f);
            pit.AddComponent<DamageZone>().damage = 20f;
        }

        void BuildSpikePlate(Vector3 pos, float delay)
        {
            var root = new GameObject("Spike Plate Trap");
            root.transform.SetParent(gTraps, false);
            root.transform.position = pos;
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = new Vector3(2f, 1.5f, 5f); box.center = new Vector3(0f, 0.75f, 0f);
            var plate = Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.015f, 0f), new Vector3(2f, 0.03f, 5f), mIron);
            var spikes = new GameObject("Spikes").transform;
            spikes.SetParent(root.transform, false);
            spikes.localPosition = new Vector3(0f, -0.85f, 0f);
            for (int x = -1; x <= 1; x++)
                for (int z = 0; z < 7; z++)
                    Part(spikes, PrimitiveType.Cylinder, new Vector3(x * 0.6f, 0.35f, -2.1f + z * 0.7f), new Vector3(0.06f, 0.35f, 0.06f), mRustIron);
            var trap = root.AddComponent<SpikeTrap>();
            trap.mode = SpikeTrap.TrapMode.Cycling;
            trap.startDelay = delay;
            trap.spikes = spikes;
            trap.plateRenderer = plate.GetComponent<Renderer>();
            trap.idleColor = new Color(0.32f, 0.3f, 0.29f);
            trap.warnColor = new Color(0.9f, 0.45f, 0.1f);
            trap.activeColor = new Color(0.7f, 0.12f, 0.05f);
            trap.damage = 15f;
        }

        void BuildVent(Vector3 pos, float delay)
        {
            var root = new GameObject("Flame Vent");
            root.transform.SetParent(gTraps, false);
            root.transform.position = pos;
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = new Vector3(1.8f, 2.5f, 1.8f); box.center = new Vector3(0f, 1.25f, 0f);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.02f, 0f), new Vector3(2f, 0.04f, 2f), mRustIron);
            for (int i = -2; i <= 2; i++)
                Part(root.transform, PrimitiveType.Cube, new Vector3(i * 0.35f, 0.045f, 0f), new Vector3(0.06f, 0.02f, 1.8f), mIron);
            var vent = root.AddComponent<FlameVent>();
            vent.startDelay = delay;
            vent.fire = VentFire(root.transform);
            vent.warnSmoke = Smoke(root.transform, new Vector3(0f, 0.1f, 0f));
            var lt = new GameObject("Vent Glow").AddComponent<Light>();
            lt.transform.SetParent(root.transform, false);
            lt.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            lt.type = LightType.Point; lt.color = new Color(1f, 0.5f, 0.15f); lt.range = 7f; lt.intensity = 0f;
            vent.glow = lt;
        }

        // =====================================================================================
        // INTERACTABLES
        // =====================================================================================

        void ReplaceInteractables(Transform group, out Door eastGate, out Door bossGate, out GameObject fragment)
        {
            if (!floor2)
            {
                eastGate = Portcullis("Portcullis (east gate)", new Vector3(10f, 1.6f, 32f), Quaternion.identity,
                    "A heavy portcullis. There must be a lever in this room.");
                bossGate = Portcullis("Warden's Gate (north)", new Vector3(44f, 1.6f, 92f), Quaternion.Euler(0f, 90f, 0f),
                    "The Warden's seal holds this gate shut.");
            }
            else
            {
                eastGate = null;
                var m = markers.Find("BossGate");
                bossGate = m != null ? Portcullis("Inquisitor's Seal (north gate)", m.position, m.rotation,
                    "The Leviathan's seal holds this gate shut. Defeat it on the lake.") : null;
            }
            fragment = null;
            if (group == null) return;

            foreach (Transform p in Children(group))
            {
                string n = p.name;
                if (n.StartsWith("Lever")) BuildLever(new Vector3(-5.72f, 1.2f, 34f), eastGate);
                else if (n.StartsWith("Memory Fragment")) fragment = BuildFragment(p.position);
                else if (n.StartsWith("Stairs to Floor 2") || n.StartsWith("Surface Gate")) BuildExit(p.position);
                else if (n.StartsWith("Boat")) { DressBoat(p); continue; }
                else if (n.StartsWith("Big Chest")) { bigChestObj = BuildChest(p.position, p.rotation, 3); bigChestObj.SetActive(false); }
                else if (n.StartsWith("Chest")) BuildChest(p.position, p.rotation, n.Contains("rich") ? 2 : 1);
                else if (n.StartsWith("Merchant")) BuildMerchant(p.position, p.rotation);
                else if (n.StartsWith("Mercenary")) BuildMercenary(p.position, p.rotation, n.Contains(":") ? n.Substring(n.IndexOf(':') + 1).Trim() : "Bram the Sellsword");
                else if (!n.StartsWith("Portcullis")) continue;
                Object.DestroyImmediate(p.gameObject);
            }
        }

        Door Portcullis(string name, Vector3 pos, Quaternion rot, string lockedMsg)
        {
            var root = new GameObject(name);
            root.transform.SetParent(gInteract, false);
            root.transform.SetPositionAndRotation(pos, rot);
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.25f, 3.2f, 3f);
            for (int i = 0; i < 10; i++)
                Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, -1.35f + i * 0.3f), new Vector3(0.09f, 1.6f, 0.09f), mIron);
            foreach (float y in new[] { -1.2f, 0f, 1.2f })
                Part(root.transform, PrimitiveType.Cube, new Vector3(0f, y, 0f), new Vector3(0.12f, 0.1f, 3f), mRustIron);
            for (int i = 0; i < 10; i++)
                Part(root.transform, PrimitiveType.Cube, new Vector3(0f, -1.68f, -1.35f + i * 0.3f), new Vector3(0.05f, 0.16f, 0.05f), mRustIron, new Vector3(0f, 0f, 45f));
            var door = root.AddComponent<Door>();
            door.locked = true;
            door.lockedMessage = lockedMsg;
            door.openOffset = new Vector3(0f, 3.1f, 0f);
            door.speed = 1.6f;
            return door;
        }

        void BuildLever(Vector3 pos, Door target)
        {
            var root = new GameObject("Gate Lever");
            root.transform.SetParent(gInteract, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, 90f, 0f));
            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.6f, 0.9f, 0.5f); box.center = new Vector3(0f, 0f, 0.2f);
            Part(root.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(0.45f, 0.65f, 0.08f), mIron);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0f, 0.05f), new Vector3(0.12f, 0.3f, 0.08f), mRustIron);
            var pivot = Pivot("Handle Pivot", root.transform, new Vector3(0f, 0f, 0.1f));
            pivot.localRotation = Quaternion.Euler(40f, 0f, 0f);
            Part(pivot, PrimitiveType.Cylinder, new Vector3(0f, 0.28f, 0f), new Vector3(0.05f, 0.28f, 0.05f), mIron);
            Part(pivot, PrimitiveType.Sphere, new Vector3(0f, 0.58f, 0f), Vector3.one * 0.1f, mBrass);
            var lever = root.AddComponent<Lever>();
            lever.targets = new[] { target };
            lever.handle = pivot;
        }

        GameObject BuildFragment(Vector3 pos)
        {
            var root = new GameObject("Memory Fragment I");
            root.transform.SetParent(gInteract, false);
            root.transform.position = pos;
            var sc = root.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 1.2f;
            Part(root.transform, PrimitiveType.Cube, Vector3.zero, Vector3.one * 0.42f, mCrystal, new Vector3(45f, 0f, 45f));
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.28f, 0f), Vector3.one * 0.16f, mCrystal, new Vector3(30f, 10f, 45f));
            Part(root.transform, PrimitiveType.Cube, new Vector3(0.05f, -0.26f, 0f), Vector3.one * 0.14f, mCrystal, new Vector3(10f, 30f, 45f));
            var lt = new GameObject("Fragment Light").AddComponent<Light>();
            lt.transform.SetParent(root.transform, false);
            lt.type = LightType.Point; lt.color = new Color(0.75f, 0.45f, 1f); lt.range = 7f; lt.intensity = 3.5f;
            Sparkle(root.transform, new Color(0.8f, 0.55f, 1f));
            var mf = root.AddComponent<MemoryFragment>();
            mf.title = "Memory Fragment I - The Oath";
            if (floor2)
            {
                mf.title = "Final Memory - The Broken Vow";
                mf.memoryText = "Torchlight on a throne room floor. Your blade raised against your own commanders. " +
                    "You chose your conscience over their corruption, and they buried you for it. You remember your name.";
                mf.maxHealthBonus = 20f;
            }
            root.SetActive(false); // revealed when the Warden dies
            return root;
        }

        void BuildExit(Vector3 doorPos)
        {
            var root = new GameObject(floor2 ? "Surface Gate" : "Stair Door to Floor 2");
            root.transform.SetParent(gInteract, false);
            root.transform.position = doorPos;
            Part(root.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(2.6f, 2.8f, 0.2f), mDoorWood);
            foreach (float y in new[] { -0.9f, 0.9f })
                Part(root.transform, PrimitiveType.Cube, new Vector3(0f, y, -0.12f), new Vector3(2.6f, 0.12f, 0.05f), mRustIron);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0f, -0.12f), new Vector3(0.04f, 2.8f, 0.05f), mRustIron);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.5f, -0.05f), new Vector3(2.9f, 0.3f, 0.3f), mStone);
            var lt = new GameObject("Light Under Door").AddComponent<Light>();
            lt.transform.SetParent(root.transform, false);
            lt.transform.localPosition = new Vector3(0f, -1.1f, -0.6f);
            lt.type = LightType.Point; lt.color = new Color(1f, 0.85f, 0.6f); lt.range = 5f; lt.intensity = 2.5f;
            Motes(root.transform, new Vector3(0f, -0.4f, -1.2f), new Vector3(3f, 2f, 2f), new Color(1f, 0.9f, 0.7f, 0.35f), 6f);

            var trig = new GameObject("Floor Exit Trigger");
            trig.transform.SetParent(gTriggers, false);
            trig.transform.position = doorPos + new Vector3(0f, 0f, -1.6f);
            var box = trig.AddComponent<BoxCollider>();
            box.size = new Vector3(3.5f, 3f, 2.5f);
            var exit = trig.AddComponent<FloorExit>();
            if (floor2)
            {
                exit.title = "THE VOW IS RESTORED";
                exit.body = "Sunlight. Wind. A name remembered.\nThe knight walks out of the dungeon with his mind, blade and honor restored.";
                exit.nextSceneName = "";
            }
            else exit.nextSceneName = "Floor2_SunkenCathedral";
        }

        GameObject BuildChest(Vector3 pos, Quaternion rot, int tier)
        {
            var go = new GameObject(tier >= 3 ? "Great Chest (Warden's hoard)" : "Treasure Chest");
            go.transform.SetParent(gInteract, false);
            go.transform.SetPositionAndRotation(new Vector3(pos.x, pos.y < 0.5f ? 0f : pos.y - 0.4f, pos.z), rot);
            var prefab = lib != null ? (tier >= 3 && lib.bigChest != null ? lib.bigChest : lib.chest) : null;
            Transform lid = null;
            if (prefab != null)
            {
                var m = (GameObject)PrefabUtility.InstantiatePrefab(prefab, go.transform);
                PrefabUtility.UnpackPrefabInstance(m, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
                FitTo(m, tier >= 3 ? 2f : 1.2f, false);
            }
            else
            {
                float k = tier >= 3 ? 1.6f : 1f;
                Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 0.38f * k, 0f), new Vector3(1.2f, 0.76f, 0.8f) * k, mDoorWood);
                Part(go.transform, PrimitiveType.Cube, new Vector3(0f, 0.38f * k, 0.41f * k), new Vector3(1.22f, 0.1f, 0.02f) * k, mBrass);
                var hinge = Pivot("Lid", go.transform, new Vector3(0f, 0.76f * k, -0.4f * k));
                Part(hinge, PrimitiveType.Cube, new Vector3(0f, 0.08f * k, 0.4f * k), new Vector3(1.24f, 0.16f, 0.82f) * k, tier >= 3 ? mBrass : mDoorWood);
                Part(hinge, PrimitiveType.Cube, new Vector3(0f, 0.08f * k, 0.81f * k), new Vector3(0.18f, 0.2f, 0.04f) * k, mBrass);
                lid = hinge;
            }
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 1.5f; sc.center = Vector3.up * 0.5f;
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(1.2f, 0.9f, 0.8f) * (tier >= 3 ? 1.6f : 1f); box.center = Vector3.up * 0.45f * (tier >= 3 ? 1.6f : 1f);
            var chest = go.AddComponent<Chest>();
            chest.tier = tier;
            chest.lid = lid;
            if (tier >= 3)
            {
                var lt = new GameObject("Hoard Glow").AddComponent<Light>();
                lt.transform.SetParent(go.transform, false);
                lt.transform.localPosition = new Vector3(0f, 1.6f, 0.8f);
                lt.type = LightType.Point; lt.color = new Color(1f, 0.8f, 0.4f); lt.range = 6f; lt.intensity = 3f;
                Sparkle(go.transform, new Color(1f, 0.85f, 0.4f));
            }
            return go;
        }

        GameObject NpcBase(string name, Vector3 pos, Quaternion rot, FVLibrary.Character who, Color fallback)
        {
            var go = new GameObject(name);
            go.transform.SetParent(gInteract, false);
            go.transform.SetPositionAndRotation(new Vector3(pos.x, 0f, pos.z), rot);
            var sc = go.AddComponent<SphereCollider>();
            sc.isTrigger = true; sc.radius = 1.6f; sc.center = Vector3.up;
            var col = go.AddComponent<CapsuleCollider>();
            col.height = 1.8f; col.radius = 0.35f; col.center = Vector3.up * 0.9f;
            if (who != null && who.prefab != null)
            {
                var m = LibModel(who, go.transform, 1.8f);
                ModularOutfit.Apply(m, "cloth_00", name.GetHashCode());
            }
            else
            {
                var rig = BuildRig(go.transform, 1f, mGhost, mRustIron, mDoorWood, mRuneFrost, true, false, false);
                rig.pose = PrimitiveRig.Pose.Idle;
                var r = rig.GetComponentsInChildren<Renderer>();
                foreach (var rr in r) rr.sharedMaterial = Lit("NPC_" + ColorUtility.ToHtmlStringRGB(fallback), fallback, 0.2f);
            }
            return go;
        }

        void BuildMerchant(Vector3 pos, Quaternion rot)
        {
            var go = NpcBase("Merchant", pos, rot, lib != null ? lib.MerchantChar : null, new Color(0.45f, 0.32f, 0.2f));
            var shop = go.AddComponent<ShopNPC>();
            shop.model = 0;
            var lantern = new GameObject("Merchant Lantern").AddComponent<Light>();
            lantern.transform.SetParent(go.transform, false);
            lantern.transform.localPosition = new Vector3(0.6f, 2.2f, 0.6f);
            lantern.type = LightType.Point; lantern.color = new Color(1f, 0.75f, 0.4f); lantern.range = 6f; lantern.intensity = 2.5f;
            if (lib != null)
            {
                var crate = LibProp("crate", null); var barrel = LibProp("barrel", null);
                if (crate != null) PlaceProp(crate, gInteract, go.transform.position + go.transform.right * 1.3f, 20f, 1f);
                if (barrel != null) PlaceProp(barrel, gInteract, go.transform.position - go.transform.right * 1.3f, 0f, 1.1f);
            }
        }

        int mercCount;

        void BuildMercenary(Vector3 pos, Quaternion rot, string mercName)
        {
            int model = 1 + (mercCount++ % 2); // the Polytope sellsword and shieldmaiden
            var go = NpcBase("Mercenary: " + mercName, pos, rot, lib != null ? lib.CompanionChar(model) : null, new Color(0.3f, 0.35f, 0.5f));
            var post = go.AddComponent<MercenaryPost>();
            post.mercName = mercName;
            post.model = model;
            post.price = floor2 ? 220 : 140;
        }

        void DressBoat(Transform boat)
        {
            boat.name = "Boat";
            var visual = new GameObject("Visual").transform;
            visual.SetParent(boat, false);
            foreach (var n in new[] { "Hull", "Bow" })
            {
                var c = boat.Find(n);
                if (c == null) continue;
                c.SetParent(visual, true);
                c.GetComponent<Renderer>().sharedMaterial = mDoorWood;
            }
            Part(visual, PrimitiveType.Cube, new Vector3(-0.78f, 0.3f, 0f), new Vector3(0.08f, 0.15f, 3.2f), mWood);
            Part(visual, PrimitiveType.Cube, new Vector3(0.78f, 0.3f, 0f), new Vector3(0.08f, 0.15f, 3.2f), mWood);
            Part(visual, PrimitiveType.Cube, new Vector3(0f, 0.3f, -1.58f), new Vector3(1.6f, 0.15f, 0.08f), mWood);
            Part(visual, PrimitiveType.Cube, new Vector3(0f, 0.28f, 0.6f), new Vector3(1.5f, 0.06f, 0.35f), mWood);
            Part(visual, PrimitiveType.Cylinder, new Vector3(-1.05f, 0.25f, -0.2f), new Vector3(0.06f, 0.9f, 0.06f), mWood, new Vector3(70f, 0f, 30f));
            Part(visual, PrimitiveType.Cylinder, new Vector3(1.05f, 0.25f, -0.2f), new Vector3(0.06f, 0.9f, 0.06f), mWood, new Vector3(70f, 0f, -30f));
            Part(visual, PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 1.45f), new Vector3(0.05f, 0.6f, 0.05f), mWood);
            Part(visual, PrimitiveType.Cube, new Vector3(0f, 1.42f, 1.45f), new Vector3(0.18f, 0.24f, 0.18f), mLantern);
            var lt = new GameObject("Lantern Light").AddComponent<Light>();
            lt.transform.SetParent(visual, false);
            lt.transform.localPosition = new Vector3(0f, 1.45f, 1.45f);
            lt.type = LightType.Point; lt.color = new Color(1f, 0.7f, 0.35f); lt.range = 7f; lt.intensity = 2f;
            lt.gameObject.AddComponent<TorchFlicker>().amount = 0.15f;

            if (lib != null && lib.boat != null)
            {
                // the imported boat model replaces the built-in hull (the collider and controls stay the same)
                foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.enabled = false;
                var m = (GameObject)PrefabUtility.InstantiatePrefab(lib.boat, visual);
                PrefabUtility.UnpackPrefabInstance(m, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                foreach (var col in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
                foreach (var rb in m.GetComponentsInChildren<Rigidbody>()) Object.DestroyImmediate(rb);
                m.transform.localPosition = Vector3.zero; m.transform.localRotation = Quaternion.identity;
                var rends = m.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    if (b.size.x > b.size.z) { m.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); }
                    float len = Mathf.Max(b.size.x, b.size.z);
                    if (len > 0.01f) m.transform.localScale *= 3.6f / len;
                    b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    m.transform.position += new Vector3(boat.position.x - b.center.x, boat.position.y - 0.3f - b.min.y, boat.position.z - b.center.z);
                }
                foreach (var l in visual.GetComponentsInChildren<Light>()) l.enabled = true;
            }
            var wake = Wake(boat, new Vector3(0f, -0.1f, -1.8f));
            var bw = boat.gameObject.AddComponent<BoatWake>();
            bw.boat = boat.GetComponent<BoatController>();
            bw.wake = wake;
            bw.visual = visual;
            boat.SetParent(gInteract, true);

            if (!floor2)
            {
                var docks = new GameObject("Boat Docks").transform;
                docks.SetParent(gInteract, false);
                Transform D(string n, Vector3 p) { var t = new GameObject(n).transform; t.SetParent(docks, false); t.position = p; return t; }
                boat.GetComponent<BoatController>().docks = new[]
                {
                    D("West Dock", new Vector3(39.2f, 0.05f, 32f)),
                    D("East Dock", new Vector3(52.8f, 0.05f, 42f)),
                    D("Island Shore", new Vector3(46f, 0.05f, 38.8f)),
                };
            }
        }

        // =====================================================================================
        // TRIGGERS, ATMOSPHERE, LIGHTING
        // =====================================================================================

        void AddTriggers(WardenBoss boss)
        {
            if (floor2)
            {
                foreach (Transform m in markers)
                    if (m.name == "Checkpoint") Checkpoint("Checkpoint", m.position, m.localScale);
                return;
            }
            Checkpoint("Checkpoint - Cistern", new Vector3(36f, 1f, 32f), new Vector3(3f, 2f, 3f));
            Checkpoint("Checkpoint - Smuggler's Den", new Vector3(-24f, 1f, 32f), new Vector3(8f, 2f, 6f));
            Motes(gFx, new Vector3(-57f, 2f, 32f), new Vector3(80f, 4f, 60f), new Color(1f, 0.92f, 0.8f, 0.3f), 30f);
            Checkpoint("Checkpoint - Gallery", new Vector3(52f, 4f, 68f), new Vector3(4f, 2f, 3f));
            Brazier(new Vector3(52f, 3f, 67f));

            if (boss != null)
            {
                var t = new GameObject("Boss Arena Trigger");
                t.transform.SetParent(gTriggers, false);
                t.transform.position = new Vector3(44f, 1.25f, 81f);
                var box = t.AddComponent<BoxCollider>();
                box.size = new Vector3(27f, 2.5f, 21f);
                t.AddComponent<BossArenaTrigger>().boss = boss;
            }
        }

        void Checkpoint(string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(gTriggers, false);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = size;
            go.AddComponent<FracturedVow.Checkpoint>();
        }

        void Brazier(Vector3 pos)
        {
            var root = new GameObject("Brazier (rest point)");
            root.transform.SetParent(gInteract, false);
            root.transform.position = pos;
            Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 0.45f, 0.12f), mIron);
            Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.95f, 0f), new Vector3(0.7f, 0.1f, 0.7f), mRustIron);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 1.02f, 0f), new Vector3(0.5f, 0.15f, 0.5f), mFlameCore);
            Fire(root.transform, new Vector3(0f, 1.05f, 0f), 2.2f);
            Embers(root.transform, new Vector3(0f, 1.2f, 0f));
            var lt = new GameObject("Brazier Light").AddComponent<Light>();
            lt.transform.SetParent(root.transform, false);
            lt.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            lt.type = LightType.Point; lt.color = new Color(1f, 0.6f, 0.25f); lt.range = 9f; lt.intensity = 3f;
            lt.gameObject.AddComponent<TorchFlicker>();
        }

        void UpgradeTorches(Transform torches)
        {
            if (torches == null) return;
            foreach (Transform t in torches)
            {
                var flame = t.Find("Flame");
                if (flame != null && lib != null && lib.torchModel != null && t.parent == torches)
                {
                    // the BrokenVector wall torch replaces the plain sconce; Particle Pack flames burn on top
                    var sconce = t.Find("Sconce");
                    if (sconce != null) sconce.GetComponent<Renderer>().enabled = false;
                    var model = PlaceProp(lib.torchModel, t, t.position + t.forward * 0.05f + Vector3.down * 0.35f, t.eulerAngles.y + 180f, 0.8f);
                    model.isStatic = false;
                    flame.GetComponent<Renderer>().enabled = false;
                    if (lib.torchFlame != null)
                    {
                        var f = (GameObject)PrefabUtility.InstantiatePrefab(lib.torchFlame, t);
                        f.transform.position = flame.position;
                        f.transform.localScale = Vector3.one * 0.35f;
                    }
                    else Fire(t, flame.localPosition + Vector3.up * 0.02f, 1f);
                    Embers(t, flame.localPosition + Vector3.up * 0.15f);
                }
                else if (flame != null)
                {
                    flame.localScale = new Vector3(0.1f, 0.16f, 0.1f);
                    flame.GetComponent<Renderer>().sharedMaterial = mFlameCore;
                    Fire(t, flame.localPosition + Vector3.up * 0.02f, 1f);
                    Embers(t, flame.localPosition + Vector3.up * 0.15f);
                }
                var lt = t.Find("Torch Light");
                if (lt != null)
                {
                    var light = lt.GetComponent<Light>();
                    light.intensity = 3.5f;
                    light.range = 12f;
                    if (lt.GetComponent<TorchFlicker>() == null) lt.gameObject.AddComponent<TorchFlicker>();
                }
            }
        }

        void UpgradeWater(Transform level)
        {
            foreach (var r in level.GetComponentsInChildren<Renderer>(true))
                if (r.name == "Water Surface" && r.GetComponent<WaterSurface>() == null) r.gameObject.AddComponent<WaterSurface>();
        }

        void AddAtmosphere()
        {
            var dust = new Color(1f, 0.92f, 0.8f, 0.3f);
            if (floor2)
            {
                foreach (Transform m in markers)
                {
                    Vector3 s = m.localScale;
                    if (m.name == "FX Dust") Motes(gFx, m.position, s, dust, Mathf.Clamp(s.x * s.z * 0.06f, 6f, 30f));
                    else if (m.name == "FX Mist") Mist(gFx, m.position, s);
                    else if (m.name == "FX Drips") Drips(gFx, m.position, s);
                    else if (m.name == "FX Ash") Ash(gFx, m.position, s);
                }
                return;
            }
            Motes(gFx, new Vector3(0f, 2f, 10f), new Vector3(12f, 4f, 20f), dust, 10f);
            Motes(gFx, new Vector3(2f, 2.5f, 32f), new Vector3(16f, 5f, 12f), dust, 12f);
            Motes(gFx, new Vector3(22f, 2f, 32f), new Vector3(24f, 4f, 5f), dust, 14f);
            Motes(gFx, new Vector3(46f, 3f, 35f), new Vector3(24f, 6f, 30f), new Color(0.8f, 0.95f, 1f, 0.25f), 16f);
            Motes(gFx, new Vector3(44f, 4f, 81f), new Vector3(28f, 8f, 22f), dust, 18f);
            Mist(gFx, new Vector3(46f, 0.05f, 35f), new Vector3(16f, 0.2f, 30f));
            Drips(gFx, new Vector3(46f, 5.8f, 35f), new Vector3(14f, 0.1f, 28f));
            Ash(gFx, new Vector3(44f, 7.6f, 81f), new Vector3(26f, 0.2f, 20f));
            // dust falling where the hall ceiling collapsed
            Ash(gFx, new Vector3(20.25f, 4.4f, 32f), new Vector3(3f, 0.2f, 4.5f));
        }

        void ApplyLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            // dark and moody by default; players can raise it with the Brightness setting (Esc > Settings)
            RenderSettings.ambientSkyColor = new Color(0.2f, 0.2f, 0.27f);
            RenderSettings.ambientEquatorColor = new Color(0.15f, 0.14f, 0.14f);
            RenderSettings.ambientGroundColor = new Color(0.08f, 0.08f, 0.08f);
            RenderSettings.fogDensity = 0.016f;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.07f);

            // no shadows, so the moonlight works as a soft fill light under the ceilings
            var moon = GameObject.Find("Moonlight (dim)");
            if (moon != null)
            {
                var l = moon.GetComponent<Light>();
                l.intensity = 0.35f;
                l.color = new Color(0.8f, 0.85f, 1f);
                l.shadows = LightShadows.None;
            }
        }

        void PostProcessing()
        {
            string path = prodDir + "/Floor1_PostFX.asset";
            var old = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f); bloom.intensity.Override(0.9f); bloom.scatter.Override(0.72f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f); vignette.smoothness.Override(0.45f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f); color.contrast.Override(14f); color.saturation.Override(-4f);
            color.colorFilter.Override(new Color(1f, 0.96f, 0.9f));
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(0.15f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);

            var vol = new GameObject("Post-processing Volume");
            vol.transform.SetParent(gFx, false);
            var v = vol.AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;

            var cam = Camera.main;
            if (cam != null)
            {
                cam.allowHDR = true;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                if (cam.GetComponent<CameraShake>() == null) cam.gameObject.AddComponent<CameraShake>();
            }
        }

        // =====================================================================================
        // PARTICLES
        // =====================================================================================

        ParticleSystem NewPS(string name, Transform parent, Vector3 localPos, Material mat, int max, bool world = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.maxParticles = max; main.loop = true; main.playOnAwake = true;
            main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return ps;
        }

        static ParticleSystem.MinMaxGradient Grad(Color a, Color b, float peak)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.15f), new GradientAlphaKey(peak * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        static ParticleSystem.MinMaxCurve Curve(float a, float b) => new ParticleSystem.MinMaxCurve(a, b);

        static void ShapeUpCone(ParticleSystem ps, float angle, float radius)
        {
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = angle; sh.radius = radius;
            sh.rotation = new Vector3(-90f, 0f, 0f);
        }

        static void ShapeBox(ParticleSystem ps, Vector3 size)
        {
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Box;
            sh.scale = size;
        }

        static void SizeCurve(ParticleSystem ps, float start, float end)
        {
            var s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, start, 1f, end));
        }

        static void Noise(ParticleSystem ps, float strength, float freq)
        {
            var n = ps.noise;
            n.enabled = true; n.strength = strength; n.frequency = freq;
        }

        void Fire(Transform parent, Vector3 localPos, float scale)
        {
            var ps = NewPS("Fire", parent, localPos, fxAdd, 80);
            var main = ps.main;
            main.startLifetime = Curve(0.35f, 0.7f);
            main.startSpeed = Curve(0.4f * scale, 0.9f * scale);
            main.startSize = Curve(0.12f * scale, 0.28f * scale);
            main.startColor = new Color(1f, 0.75f, 0.4f);
            main.startRotation = Curve(0f, Mathf.PI * 2f);
            var em = ps.emission; em.rateOverTime = 28f * Mathf.Sqrt(scale);
            ShapeUpCone(ps, 8f, 0.04f * scale);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(new Color(1f, 0.9f, 0.5f), new Color(0.8f, 0.15f, 0.05f), 1f);
            SizeCurve(ps, 1f, 0.2f);
            Noise(ps, 0.2f, 1.2f);
        }

        void Embers(Transform parent, Vector3 localPos)
        {
            var ps = NewPS("Embers", parent, localPos, fxAdd, 30);
            var main = ps.main;
            main.startLifetime = Curve(1.5f, 2.5f);
            main.startSpeed = Curve(0.3f, 0.8f);
            main.startSize = Curve(0.02f, 0.045f);
            main.startColor = new Color(1f, 0.55f, 0.15f);
            main.gravityModifier = -0.05f;
            var em = ps.emission; em.rateOverTime = 4f;
            ShapeUpCone(ps, 25f, 0.05f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(new Color(1f, 0.8f, 0.3f), new Color(1f, 0.3f, 0.05f), 1f);
            Noise(ps, 0.4f, 0.8f);
        }

        void Motes(Transform parent, Vector3 localPos, Vector3 size, Color c, float rate)
        {
            var ps = NewPS("Dust Motes", parent, localPos, fxAdd, 200);
            var main = ps.main;
            main.startLifetime = Curve(6f, 10f);
            main.startSpeed = Curve(0.02f, 0.08f);
            main.startSize = Curve(0.025f, 0.06f);
            main.startColor = c;
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = rate;
            ShapeBox(ps, size);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f);
            Noise(ps, 0.08f, 0.2f);
        }

        void Mist(Transform parent, Vector3 localPos, Vector3 size)
        {
            var ps = NewPS("Water Mist", parent, localPos, fxAlpha, 80);
            var main = ps.main;
            main.startLifetime = Curve(7f, 10f);
            main.startSpeed = Curve(0.05f, 0.15f);
            main.startSize = Curve(2.5f, 4.5f);
            main.startColor = new Color(0.7f, 0.85f, 0.9f, 0.1f);
            main.startRotation = Curve(0f, Mathf.PI * 2f);
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 6f;
            ShapeBox(ps, size);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f);
        }

        void Drips(Transform parent, Vector3 localPos, Vector3 size)
        {
            var ps = NewPS("Ceiling Drips", parent, localPos, fxAlpha, 60);
            var main = ps.main;
            main.startLifetime = Curve(1f, 1.2f);
            main.startSpeed = 0f;
            main.startSize = Curve(0.025f, 0.04f);
            main.startColor = new Color(0.75f, 0.9f, 1f, 0.7f);
            main.gravityModifier = 1f;
            var em = ps.emission; em.rateOverTime = 6f;
            ShapeBox(ps, size);
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.04f;
            r.lengthScale = 1f;
        }

        void Ash(Transform parent, Vector3 localPos, Vector3 size)
        {
            var ps = NewPS("Falling Ash", parent, localPos, fxAlpha, 150);
            var main = ps.main;
            main.startLifetime = Curve(9f, 13f);
            main.startSpeed = 0f;
            main.startSize = Curve(0.02f, 0.05f);
            main.startColor = new Color(0.55f, 0.52f, 0.5f, 0.6f);
            main.gravityModifier = 0.015f;
            main.prewarm = true;
            var em = ps.emission; em.rateOverTime = 10f;
            ShapeBox(ps, size);
            Noise(ps, 0.15f, 0.3f);
        }

        void Sparkle(Transform parent, Color c)
        {
            var ps = NewPS("Sparkle", parent, Vector3.zero, fxAdd, 30, false);
            var main = ps.main;
            main.startLifetime = Curve(0.6f, 1.1f);
            main.startSpeed = Curve(0.05f, 0.2f);
            main.startSize = Curve(0.03f, 0.07f);
            main.startColor = c;
            var em = ps.emission; em.rateOverTime = 8f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.35f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f);
            SizeCurve(ps, 1f, 0f);
        }

        ParticleSystem Smoke(Transform parent, Vector3 localPos)
        {
            var ps = NewPS("Warning Smoke", parent, localPos, fxAlpha, 60);
            var main = ps.main;
            main.startLifetime = Curve(1f, 1.6f);
            main.startSpeed = Curve(0.6f, 1.2f);
            main.startSize = Curve(0.3f, 0.7f);
            main.startColor = new Color(0.16f, 0.14f, 0.13f, 0.55f);
            main.startRotation = Curve(0f, Mathf.PI * 2f);
            var em = ps.emission; em.rateOverTime = 14f;
            ShapeUpCone(ps, 15f, 0.6f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f);
            SizeCurve(ps, 0.6f, 1.6f);
            return ps;
        }

        ParticleSystem VentFire(Transform parent)
        {
            var ps = NewPS("Vent Fire", parent, new Vector3(0f, 0.05f, 0f), fxAdd, 250);
            var main = ps.main;
            main.startLifetime = Curve(0.5f, 0.8f);
            main.startSpeed = Curve(3.5f, 5.5f);
            main.startSize = Curve(0.35f, 0.8f);
            main.startColor = new Color(1f, 0.7f, 0.35f);
            main.startRotation = Curve(0f, Mathf.PI * 2f);
            var em = ps.emission; em.rateOverTime = 90f;
            ShapeUpCone(ps, 12f, 0.5f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(new Color(1f, 0.95f, 0.6f), new Color(0.7f, 0.1f, 0.02f), 1f);
            SizeCurve(ps, 0.6f, 1.2f);
            Noise(ps, 0.5f, 1.5f);
            return ps;
        }

        ParticleSystem Aura(Transform parent)
        {
            var ps = NewPS("Warden Aura", parent, new Vector3(0f, 2f, 0f), fxAlpha, 120);
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = Curve(1.2f, 2f);
            main.startSpeed = Curve(0.2f, 0.5f);
            main.startSize = Curve(0.5f, 1.1f);
            main.startColor = new Color(0.35f, 0.03f, 0.03f, 0.45f);
            main.startRotation = Curve(0f, Mathf.PI * 2f);
            var em = ps.emission; em.rateOverTime = 16f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 1.2f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(0.8f);
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f);
            SizeCurve(ps, 0.7f, 1.5f);
            return ps;
        }

        ParticleSystem SlamDust(Transform parent)
        {
            var ps = NewPS("Slam Dust", parent, new Vector3(0f, 0.15f, 0f), fxAlpha, 120);
            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.startLifetime = Curve(0.6f, 1f);
            main.startSpeed = Curve(5f, 8f);
            main.startSize = Curve(0.5f, 1.2f);
            main.startColor = new Color(0.45f, 0.4f, 0.33f, 0.6f);
            main.startRotation = Curve(0f, Mathf.PI * 2f);
            var em = ps.emission; em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = 0.8f; sh.rotation = new Vector3(-90f, 0f, 0f);
            var lim = ps.limitVelocityOverLifetime; lim.enabled = true; lim.limit = 0.5f; lim.dampen = 0.15f;
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f);
            SizeCurve(ps, 0.6f, 1.8f);
            return ps;
        }

        ParticleSystem Wake(Transform parent, Vector3 localPos)
        {
            var ps = NewPS("Wake", parent, localPos, fxAlpha, 120);
            var main = ps.main;
            main.startLifetime = Curve(1f, 1.6f);
            main.startSpeed = Curve(0.2f, 0.5f);
            main.startSize = Curve(0.3f, 0.7f);
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.35f);
            var em = ps.emission; em.rateOverTime = 0f;
            ShapeBox(ps, new Vector3(1.2f, 0.05f, 0.2f));
            var col = ps.colorOverLifetime; col.enabled = true; col.color = Grad(Color.white, Color.white, 1f);
            SizeCurve(ps, 0.6f, 1.6f);
            return ps;
        }

        // =====================================================================================
        // MATERIALS
        // =====================================================================================

        void MakeMaterials()
        {
            mSkin = Lit("Husk_Skin", new Color(0.42f, 0.4f, 0.38f), 0.15f);
            mDrownedSkin = Lit("Husk_DrownedSkin", new Color(0.36f, 0.48f, 0.48f), 0.45f);
            mBossSkin = Lit("Warden_Skin", new Color(0.3f, 0.22f, 0.22f), 0.2f);
            mIron = Lit("Iron", new Color(0.32f, 0.32f, 0.34f), 0.55f, 0.6f);
            mRustIron = Lit("RustIron", new Color(0.33f, 0.22f, 0.16f), 0.3f, 0.4f);
            mCloth = Lit("Cloth_Crimson", new Color(0.35f, 0.06f, 0.06f), 0.05f);
            mDrownedCloth = Lit("Cloth_Kelp", new Color(0.12f, 0.22f, 0.16f), 0.3f);
            mBossCloth = Lit("Cloth_Black", new Color(0.08f, 0.06f, 0.06f), 0.05f);
            mEyes = Glow("Eyes_Ember", new Color(1f, 0.45f, 0.1f), 4f);
            mBossEyes = Glow("Eyes_Warden", new Color(1f, 0.15f, 0.05f), 6f);
            mBone = Lit("Bone", new Color(0.82f, 0.78f, 0.68f), 0.2f);
            mRedGlass = Glow("Glass_Red", new Color(0.9f, 0.1f, 0.12f), 1.4f, 0.9f);
            mBlueGlass = Glow("Glass_Blue", new Color(0.15f, 0.35f, 1f), 1.4f, 0.9f);
            mCork = Lit("Cork", new Color(0.55f, 0.4f, 0.25f), 0.1f);
            mLeather = Lit("Leather", new Color(0.3f, 0.16f, 0.08f), 0.25f);
            mPage = Lit("Pages", new Color(0.88f, 0.84f, 0.72f), 0.05f);
            mRuneFrost = Glow("Rune_Frost", new Color(0.4f, 0.85f, 1f), 5f);
            mRuneFire = Glow("Rune_Fire", new Color(1f, 0.45f, 0.1f), 5f);
            mBrass = Lit("Brass", new Color(0.75f, 0.58f, 0.25f), 0.6f, 0.8f);
            mCrystal = Glow("Memory_Crystal", new Color(0.7f, 0.4f, 1f), 4f, 0.9f);
            mTelegraph = Glow("Boss_Telegraph", new Color(0.9f, 0.08f, 0.04f), 2.5f);
            mFlameCore = Glow("Flame_Core", new Color(1f, 0.6f, 0.2f), 8f);
            mLantern = Glow("Lantern", new Color(1f, 0.7f, 0.35f), 4f);
            mDoorWood = Lit("DarkWood", new Color(0.25f, 0.15f, 0.08f), 0.2f);
            mRobe = Lit("Robe_Inquisitor", new Color(0.12f, 0.1f, 0.16f), 0.1f);
            mOrbGlow = Glow("Inquisitor_Orb", new Color(0.5f, 1f, 0.7f), 4f);
            mWave = Glow("Lake_Wave", new Color(0.25f, 0.6f, 0.65f), 0.4f, 0.9f);
            mRuneStone = Glow("Rune_Stone", new Color(0.85f, 0.7f, 0.45f), 4f);
            mGhost = Glow("Ghost_Prisoner", new Color(0.55f, 0.75f, 0.95f), 1.2f);
            mLevSkin = Lit("Leviathan_Scales", new Color(0.07f, 0.2f, 0.22f), 0.65f, 0.1f);
            mLevBelly = Lit("Leviathan_Belly", new Color(0.5f, 0.55f, 0.45f), 0.4f);
            mLevFin = Glow("Leviathan_Fin", new Color(0.1f, 0.38f, 0.38f), 0.6f, 0.5f);
            mLevEyes = Glow("Leviathan_Eyes", new Color(0.4f, 1f, 0.9f), 7f);
            mLevMouth = Glow("Leviathan_Mouth", new Color(0.5f, 0.05f, 0.04f), 1.5f);
            mBannerRed = Lit("Banner_Crimson", new Color(0.42f, 0.06f, 0.07f), 0.05f);
            mBannerTeal = Lit("Banner_Teal", new Color(0.06f, 0.26f, 0.27f), 0.05f);
            mClay = Lit("Clay", new Color(0.42f, 0.27f, 0.18f), 0.15f);

            string lvlMats = root + "/Levels/Floor1/Materials";
            mWood = AssetDatabase.LoadAssetAtPath<Material>(lvlMats + "/FV1_WoodPlanks.mat") ?? mDoorWood;
            mStone = AssetDatabase.LoadAssetAtPath<Material>(lvlMats + "/FV1_StoneBrick.mat") ?? mIron;

            softTex = SoftCircle();
            fxAdd = ParticleMat("FX_Additive", true);
            fxAlpha = ParticleMat("FX_AlphaBlend", false);
        }

        static Shader LitShader()
        {
            Shader s = null;
            if (GraphicsSettings.currentRenderPipeline != null) s = Shader.Find("Universal Render Pipeline/Lit");
            return s != null ? s : Shader.Find("Standard");
        }

        Material LoadOrNew(string name, Shader shader, string folder = null)
        {
            string path = (folder ?? matDir) + "/FVP_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            return m;
        }

        Material Lit(string name, Color c, float smooth, float metal = 0f)
        {
            var m = LoadOrNew(name, LitShader());
            SetColor(m, c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            EditorUtility.SetDirty(m);
            return m;
        }

        Material Glow(string name, Color c, float intensity, float smooth = 0.3f)
        {
            var m = Lit(name, c, smooth);
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", c * intensity);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        Material ParticleMat(string name, bool additive)
        {
            Shader sh = GraphicsSettings.currentRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : null;
            if (sh == null) sh = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
            // saved under Resources so runtime effects (GameFX: hit sparks, dust, pickups) can load them
            var m = LoadOrNew(name, sh, root + "/Resources/FVFX");
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", softTex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", softTex);
            SetColor(m, Color.white);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", additive ? 2f : 0f);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (m.HasProperty("_SrcBlendAlpha")) m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                if (m.HasProperty("_DstBlendAlpha")) m.SetFloat("_DstBlendAlpha", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        Texture2D SoftCircle()
        {
            const int size = 64;
            string path = texDir + "/FVP_SoftParticle.png";
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), tex.EncodeToPNG());
            DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // =====================================================================================
        // HELPERS
        // =====================================================================================

        static List<Transform> Children(Transform t)
        {
            var list = new List<Transform>();
            foreach (Transform c in t) list.Add(c);
            return list;
        }

        static Transform Pivot(string name, Transform parent, Vector3 localPos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material m, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
