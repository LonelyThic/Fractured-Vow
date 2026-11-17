using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace FracturedVow.EditorTools
{
    /// <summary>
    /// Tools > Fractured Vow > Build Level: Floor 2 (The Drowned Deep)
    /// 50 rooms on a 10 x 5 grid: loops, twelve dead ends with rewards, seven hubs, and two separate routes
    /// (the Western and Eastern Boathouses) to the Leviathan's lake. The lake holds five breakable stone
    /// platforms and two boats. The floor opens in a pillared antechamber whose bricked-up arch the Leviathan smashes through.
    /// Reuses Floor 1's generated materials (build Floor 1 first). Props from a prop pack folder (for example
    /// LOW POLY Dark Fantasy Dungeon) are scattered on top of the built-in furniture.
    /// Run "Production Pass" on the result to turn placeholders into gameplay, the boss, the cutscene and effects.
    /// </summary>
    public class Floor2LevelBuilder : EditorWindow
    {
        GameObject playerPrefab;
        DefaultAsset packFolder;
        bool ceilings = true;
        int layoutSeed = 25024; // the team's chosen layout
        string report = "";

        string root, genDir, meshDir, matDir, scenePath;
        Mesh unitCube;
        readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();
        System.Random rng;

        Material mWall, mMossWall, mFloor, mRubble, mWood, mWater, mMetal, mPillar, mBone;
        Material mEnemy, mBoss, mPowerUp, mTool, mTrap, mInteract, mBoat, mFlame;
        Material mCrack, mFungus, mBannerA, mBannerB, mGold, mBookA, mBookB, mBookC, mCloth;

        Transform gStructure, gCeilings, gProps, gLights, gItems, gEnemies, gTraps, gInteract, gTour, gMarkers, gDocks;

        readonly List<GameObject> packProps = new List<GameObject>();
        readonly List<Transform> docks = new List<Transform>();
        WaterZone lakeZone;

        const float P = 26f;
        const float LX0 = 40f, LX1 = 196f, LZ0 = 122f, LZ1 = 200f;
        static readonly Vector3 LeviathanHome = new Vector3(118f, 0f, 160f);
        static readonly Vector2[] Platforms = { new Vector2(78, 142), new Vector2(118, 138), new Vector2(158, 146), new Vector2(98, 176), new Vector2(140, 178) };

        // id | name | column | row | width | depth | kind | enemies | loot | trap | patrol   (generated layout, matches Floor2_Map.png)
        static readonly string[] RoomData =
        {
            "R00|Inner Barracks|0|0|14|12|DeadEnd|4|Mana Vial|spike|0",
            "R10|Lower Library|1|0|14|12|Dry|3|Mana Vial||0",
            "R20|East Dormitory|2|0|14|16|DeadEnd|3|Health Potion||0",
            "R30|Upper Bell Tower|3|0|14|14|Dry|0|Mana Vial||0",
            "R40|Breach Hall|4|0|20|20|Start|0|||0",
            "R50|Upper Cloister|5|0|16|14|Dry|0|Health Potion||0",
            "R60|Great Cloister|6|0|14|14|Dry|1|Health Potion||0",
            "R70|Inner Scriptorium|7|0|16|14|DeadEnd|4|Iron Pauldron|spike|0",
            "R80|North Guardroom|8|0|12|14|Dry|2|||1",
            "R90|Sunken Refectory|9|0|14|14|DeadEnd|3|Knight's Sigil||0",
            "R01|Great Refectory|0|1|14|16|Dry|3|Mana Vial||0",
            "R11|Upper Library|1|1|18|18|Hub|3|Mana Vial|vent|1",
            "R21|Old Armory|2|1|12|14|DeadEnd|3|Mana Vial|spike|0",
            "R31|Broken Scriptorium|3|1|14|16|Dry|0|||0",
            "R41|Silent Barracks|4|1|18|18|Hub|1|Mana Vial|vent|0",
            "R51|Silent Armory|5|1|12|16|Dry|0|Mana Vial||0",
            "R61|East Refectory|6|1|20|20|Hub|2|Health Potion|vent|0",
            "R71|Hollow Choir|7|1|14|14|Dry|2|||1",
            "R81|Old Reliquary|8|1|20|20|Hub|3|Mana Vial|vent|1",
            "R91|East Armory|9|1|14|16|DeadEnd|2|Stone Wall tome|spike|0",
            "R02|Broken Bone Gallery|0|2|14|12|Dry|3|Health Potion|spike|0",
            "R12|Sunken Tomb|1|2|14|16|Dry|2|||1",
            "R22|Sunken Undercroft|2|2|12|16|Dry|2|||0",
            "R32|Little Tomb|3|2|14|14|Dry|1|||1",
            "R42|Outer Bone Gallery|4|2|16|14|Dry|0|Health Potion||0",
            "R52|Broken Tomb|5|2|14|16|Dry|0|||0",
            "R62|Outer Mausoleum|6|2|16|14|Dry|2|Mana Vial||1",
            "R72|Sunken Charnel House|7|2|16|14|Dry|2|Health Potion||1",
            "R82|North Crypt|8|2|18|18|Hub|3|Health Potion|vent|0",
            "R92|North Undercroft|9|2|16|14|Dry|3|Mana Vial||1",
            "R03|Lower Bone Gallery|0|3|14|12|Dry|3|||0",
            "R13|Old Catacomb|1|3|12|14|DeadEnd|3|Firebolt tome|spike|0",
            "R23|Silent Sepulchre|2|3|14|16|Dry|2|Mana Vial||1",
            "R33|Great Crypt|3|3|16|14|Dry|1|||1",
            "R43|Hollow Shrine|4|3|14|14|Dry|1|Mana Vial||1",
            "R53|Old Mausoleum|5|3|20|20|Hub|3|Health Potion|vent|1",
            "R63|Old Bone Gallery|6|3|18|18|Hub|3|Health Potion|vent|1",
            "R73|Old Charnel House|7|3|12|16|DeadEnd|2|Sharpened Blade|spike|0",
            "R83|Sunken Vault|8|3|14|14|Dry|2|||1",
            "R93|Little Undercroft|9|3|16|16|Dry|3|Mana Vial||1",
            "R04|Old Drowned Hall|0|4|14|14|Dry|3|Health Potion|spike|0",
            "R14|Old Cistern|1|4|14|12|DeadEnd|4|Health Potion|spike|0",
            "R24|Western Boathouse|2|4|16|16|Dock|2|||0",
            "R34|Lower Aqueduct|3|4|16|16|Dry|2|||0",
            "R44|Sunken Reservoir|4|4|14|12|Dry|2|||1",
            "R54|Outer Pumphouse|5|4|16|16|DeadEnd|2|Frost Shard tome||0",
            "R64|Sunken Cistern|6|4|14|14|Dry|1|Health Potion||1",
            "R74|Eastern Boathouse|7|4|16|16|Dock|2|Mana Vial||0",
            "R84|Little Drowned Hall|8|4|12|14|DeadEnd|4|Mana Vial|spike|0",
            "R94|Great Sluice Gate|9|4|16|14|DeadEnd|3|Health Potion|spike|0",
        };

        static readonly string[] LinkData =
        {
            "R22|R32",
            "R30|R40",
            "R43|R53",
            "R10|R11",
            "R30|R31",
            "R82|R83",
            "R80|R81",
            "R64|R74",
            "R53|R63",
            "R61|R71",
            "R22|R23",
            "R63|R64",
            "R12|R22",
            "R53|R54",
            "R62|R72",
            "R51|R61",
            "R42|R43",
            "R61|R62",
            "R83|R84",
            "R32|R33",
            "R33|R34",
            "R93|R94",
            "R71|R72",
            "R34|R44",
            "R02|R03",
            "R63|R73",
            "R12|R13",
            "R80|R90",
            "R52|R53",
            "R42|R52",
            "R81|R91",
            "R01|R02",
            "R31|R32",
            "R51|R52",
            "R50|R60",
            "R60|R61",
            "R01|R11",
            "R43|R44",
            "R00|R10",
            "R40|R50",
            "R92|R93",
            "R72|R82",
            "R03|R04",
            "R23|R33",
            "R31|R41",
            "R81|R82",
            "R11|R12",
            "R10|R20",
            "R40|R41",
            "R04|R14",
            "R11|R21",
            "R41|R42",
            "R83|R93",
            "R41|R51",
            "R82|R92",
            "R70|R80",
            "R24|R34",
            "R62|R63",
            "R71|R81",
            "R23|R24",
        };

        class Room
        {
            public string id, name, kind, item, trap;
            public int c, r, mobs;
            public bool patrol;
            public float w, d;
            public float X => c * P;
            public float Z => r * P;
            public float W0 => X - w / 2f;
            public float W1 => X + w / 2f;
            public float S0 => Z - d / 2f;
            public float S1 => Z + d / 2f;
            public float Height => kind == "Start" ? 9f : kind == "Hub" ? 7f : 5.5f;
            public readonly List<Vector2> gapN = new List<Vector2>(), gapS = new List<Vector2>(),
                                          gapE = new List<Vector2>(), gapW = new List<Vector2>();
        }

        readonly Dictionary<string, Room> rooms = new Dictionary<string, Room>();
        readonly List<KeyValuePair<Room, Room>> links = new List<KeyValuePair<Room, Room>>();

        [MenuItem("Tools/Fractured Vow/Build Level: Floor 2 (The Drowned Deep)")]
        static void Open()
        {
            var w = GetWindow<Floor2LevelBuilder>("Floor 2 Builder");
            w.minSize = new Vector2(380, 320);
        }

        /// <summary>Player (Hero) if Set Up Imported Packs has made it, otherwise Player (Knight).</summary>
        static GameObject DefaultPlayer()
        {
            foreach (var n in new[] { "Player (Hero)", "Player (Knight)" })
                foreach (var guid in AssetDatabase.FindAssets(n + " t:Prefab"))
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (go != null && go.name == n) return go;
                }
            return null;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Floor 2 - The Drowned Deep (50 rooms)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Build Floor 1 first: Floor 2 reuses its stone, wood and water materials.\n" +
                "1. Drag Prefabs/Player (Knight) into the slot.\n" +
                "2. Optional: drag the 'Dark Fantasy Dungeon DEMO' folder into 'Prop pack folder' to scatter its props too.\n" +
                "3. Click Build, then run Tools > Fractured Vow > Production Pass on the new scene.",
                MessageType.Info);
            if (playerPrefab == null) playerPrefab = DefaultPlayer();
            playerPrefab = (GameObject)EditorGUILayout.ObjectField("Player prefab", playerPrefab, typeof(GameObject), false);
            packFolder = (DefaultAsset)EditorGUILayout.ObjectField("Prop pack folder", packFolder, typeof(DefaultAsset), false);
            ceilings = EditorGUILayout.Toggle("Build ceilings", ceilings);
            layoutSeed = EditorGUILayout.IntField(new GUIContent("Layout seed", "0 = the designed map (Floor2_Map.png). Any other number generates a brand-new 50-room layout: new paths, loops, dead ends, room names, enemies and loot."), layoutSeed);
            if (GUILayout.Button("Random seed")) layoutSeed = Random.Range(1, 99999);
            EditorGUILayout.Space();
            if (GUILayout.Button("Build Floor 2", GUILayout.Height(36))) EditorApplication.delayCall += Build;
            if (!string.IsNullOrEmpty(report)) EditorGUILayout.HelpBox(report, MessageType.None);
        }

        // =====================================================================================
        // BUILD
        // =====================================================================================

        void Build()
        {
            if (EditorApplication.isPlaying) { Done("Exit Play mode first."); return; }
            if (playerPrefab == null) { Done("Drag the Player (Knight) prefab into the slot first."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ResolvePaths();
            if (!LoadMaterials()) { Done("Couldn't find Floor 1's materials in " + root + "/Levels/Floor1/Materials. Build Floor 1 first."); return; }

            rng = new System.Random(2026);
            meshCache.Clear(); packProps.Clear(); docks.Clear(); rooms.Clear(); links.Clear();
            LoadPackProps();
            if (layoutSeed != 0) GenerateLayout(layoutSeed);
            else ParseLayout(RoomData, LinkData);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            unitCube = temp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(temp);

            var level = new GameObject("Floor 2 - The Drowned Deep").transform;
            gStructure = Group("Structure", level);
            gCeilings = Group("Ceilings (hide for top-down screenshots)", level);
            gProps = Group("Props", level);
            gLights = Group("Torches", level);
            gItems = Group("Items (placeholders)", level);
            gEnemies = Group("Enemies (static placeholders)", level);
            gTraps = Group("Traps (placeholders)", level);
            gInteract = Group("Interactables (placeholders)", level);
            gTour = Group("Tour Signs", level);
            gMarkers = Group("Markers", level);
            gDocks = Group("Docks", gStructure);

            SetupLighting();
            foreach (var r in rooms.Values) BuildRoom(r);
            foreach (var l in links) BuildLink(l.Key, l.Value);
            BuildAntechamber();
            BuildLake();
            BuildSurfaceExit();
            PickNpcRooms();
            foreach (var r in rooms.Values) Populate(r);
            foreach (var boat in gInteract.GetComponentsInChildren<BoatController>())
            {
                boat.water = lakeZone;
                boat.docks = docks.ToArray();
            }

            gCeilings.gameObject.SetActive(ceilings);
            var start = rooms["R40"];
            PlacePlayer(new Vector3(start.X, 0.05f, start.S0 - 17f), Quaternion.identity); // in the antechamber, facing the arch

            var systems = new GameObject("Systems").transform;
            new GameObject("GameManager").AddComponent<GameManager>().transform.SetParent(systems);
            var hud = new GameObject("PrototypeHUD").AddComponent<PrototypeHUD>();
            hud.showControls = false;
            hud.transform.SetParent(systems);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            AddToBuildSettings(scenePath);
            AssetDatabase.SaveAssets();
            Done("Built " + scenePath + " (" + rooms.Count + " rooms, " + links.Count + " corridors).\nNext: Tools > Fractured Vow > Production Pass.");
        }

        void Done(string msg) { report = msg; Debug.Log("[Fractured Vow] " + msg); Repaint(); }

        void ResolvePaths()
        {
            string me = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)).Replace('\\', '/');
            string editorDir = me.Substring(0, me.LastIndexOf('/'));
            root = editorDir.EndsWith("/Editor") ? editorDir.Substring(0, editorDir.LastIndexOf('/')) : "Assets/FracturedVow";
            genDir = root + "/Levels/Floor2";
            meshDir = genDir + "/Meshes";
            matDir = genDir + "/Materials";
            scenePath = root + "/Scenes/Floor2_SunkenCathedral.unity"; // same name Floor 1's exit already loads
            EnsureFolder(meshDir); EnsureFolder(matDir); EnsureFolder(root + "/Scenes");
        }

        bool LoadMaterials()
        {
            string d = root + "/Levels/Floor1/Materials/FV1_";
            Material Mat(string n) => AssetDatabase.LoadAssetAtPath<Material>(d + n + ".mat");
            mWall = Mat("StoneBrick"); mMossWall = Mat("MossyBrick"); mFloor = Mat("Flagstone"); mRubble = Mat("RubbleDirt");
            mWood = Mat("WoodPlanks"); mWater = Mat("Water"); mMetal = Mat("DarkIron"); mPillar = Mat("PillarStone"); mBone = Mat("Bone");
            mEnemy = Mat("Enemy_Red"); mBoss = Mat("Boss_DarkRed"); mPowerUp = Mat("PowerUp_Green"); mTool = Mat("Tool_Blue");
            mTrap = Mat("Trap_Yellow"); mInteract = Mat("Interactable_Purple"); mBoat = Mat("Boat_Cyan"); mFlame = Mat("Flame");
            if (mWall == null || mFloor == null || mWater == null || mWood == null || mMossWall == null || mRubble == null) return false;
            mCrack = NewMat("Crack_Glow", new Color(1f, 0.25f, 0.08f), 4f);
            mFungus = NewMat("Fungus_Glow", new Color(0.3f, 0.95f, 0.85f), 2.5f);
            mBannerA = NewMat("Banner_Crimson", new Color(0.4f, 0.06f, 0.07f), 0f);
            mBannerB = NewMat("Banner_Teal", new Color(0.06f, 0.25f, 0.26f), 0f);
            mGold = NewMat("Gold_Trim", new Color(0.75f, 0.58f, 0.25f), 0f, 0.7f);
            mBookA = NewMat("Book_Red", new Color(0.45f, 0.12f, 0.1f), 0f);
            mBookB = NewMat("Book_Green", new Color(0.12f, 0.3f, 0.18f), 0f);
            mBookC = NewMat("Book_Brown", new Color(0.35f, 0.22f, 0.12f), 0f);
            mCloth = NewMat("Cloth_Dark", new Color(0.12f, 0.1f, 0.1f), 0f);
            return true;
        }

        Material NewMat(string name, Color c, float glow, float metal = 0f)
        {
            string path = matDir + "/FV2_" + name + ".mat";
            Shader sh = GraphicsSettings.currentRenderPipeline != null ? Shader.Find("Universal Render Pipeline/Lit") : null;
            if (sh == null) sh = Shader.Find("Standard");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
            if (glow > 0f && m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                m.SetColor("_EmissionColor", c * glow);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        void LoadPackProps()
        {
            if (packFolder == null) return;
            string path = AssetDatabase.GetAssetPath(packFolder);
            if (!AssetDatabase.IsValidFolder(path)) return;
            string[] skip = { "wall", "floor", "ceiling", "door", "stair", "tile", "ground", "roof", "column", "arch", "corner" };
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { path }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (go == null) continue;
                string n = go.name.ToLowerInvariant();
                bool bad = false;
                foreach (var s in skip) if (n.Contains(s)) { bad = true; break; }
                // only plain scenery: no demo players, cameras or scripts (they'd take over the camera)
                string pth = AssetDatabase.GUIDToAssetPath(guid).ToLowerInvariant();
                if (pth.Contains("demo scene") || pth.Contains("/demo/") || go.GetComponentInChildren<Camera>(true) != null ||
                    go.GetComponentInChildren<MonoBehaviour>(true) != null || go.GetComponentInChildren<CharacterController>(true) != null) bad = true;
                if (!bad && go.GetComponentInChildren<Renderer>() != null) packProps.Add(go);
            }
        }

        void ParseLayout(IEnumerable<string> roomLines, IEnumerable<string> linkLines)
        {
            foreach (var line in roomLines)
            {
                var f = line.Split('|');
                rooms[f[0]] = new Room
                {
                    id = f[0], name = f[1], c = int.Parse(f[2]), r = int.Parse(f[3]),
                    w = float.Parse(f[4], System.Globalization.CultureInfo.InvariantCulture),
                    d = float.Parse(f[5], System.Globalization.CultureInfo.InvariantCulture),
                    kind = f[6], mobs = int.Parse(f[7]), item = f[8], trap = f[9], patrol = f[10] == "1"
                };
            }
            foreach (var line in linkLines)
            {
                var f = line.Split('|');
                Room a = rooms[f[0]], b = rooms[f[1]];
                if ((a.r == b.r && a.c > b.c) || (a.c == b.c && a.r > b.r)) { var t = a; a = b; b = t; }
                links.Add(new KeyValuePair<Room, Room>(a, b));
                if (a.r == b.r) { var g = new Vector2(a.Z - 1.5f, a.Z + 1.5f); a.gapE.Add(g); b.gapW.Add(g); }
                else { var g = new Vector2(a.X - 1.5f, a.X + 1.5f); a.gapN.Add(g); b.gapS.Add(g); }
            }
            var s = rooms["R40"];
            s.gapS.Add(new Vector2(s.X - 4f, s.X + 4f));                  // the great arch to the antechamber (sealed by the breakable wall)
            foreach (var id in new[] { "R24", "R74" })
                rooms[id].gapN.Add(new Vector2(rooms[id].X - 2f, rooms[id].X + 2f)); // boathouse corridors to the lake
        }

        /// <summary>
        /// Procedural layout: a random spanning tree over the 10 x 5 grid (every room reachable), plus extra
        /// corridors for loops and alternate routes. Leaves of the tree become dead ends with the floor's best
        /// loot; rooms get harder the further they are from the start. The start, the two boathouses and the
        /// lake stay where they are.
        /// </summary>
        void GenerateLayout(int seed)
        {
            var rnd = new System.Random(seed);
            const int cols = 10, rowsN = 5;
            var S = new Vector2Int(4, 0); var W = new Vector2Int(2, 4); var E = new Vector2Int(7, 4);
            var cells = new List<Vector2Int>();
            for (int r = 0; r < rowsN; r++) for (int c = 0; c < cols; c++) cells.Add(new Vector2Int(c, r));
            var adj = new Dictionary<Vector2Int, HashSet<Vector2Int>>();
            foreach (var c in cells) adj[c] = new HashSet<Vector2Int>();

            var all = new List<KeyValuePair<Vector2Int, Vector2Int>>();
            foreach (var c in cells)
            {
                if (c.x + 1 < cols) all.Add(new KeyValuePair<Vector2Int, Vector2Int>(c, new Vector2Int(c.x + 1, c.y)));
                if (c.y + 1 < rowsN) all.Add(new KeyValuePair<Vector2Int, Vector2Int>(c, new Vector2Int(c.x, c.y + 1)));
            }
            for (int i = all.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); var t = all[i]; all[i] = all[j]; all[j] = t; }

            var parent = new Dictionary<Vector2Int, Vector2Int>();
            foreach (var c in cells) parent[c] = c;
            Vector2Int Find(Vector2Int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            var spare = new List<KeyValuePair<Vector2Int, Vector2Int>>();
            foreach (var e in all)
            {
                var ra = Find(e.Key); var rb = Find(e.Value);
                if (ra != rb) { parent[ra] = rb; adj[e.Key].Add(e.Value); adj[e.Value].Add(e.Key); }
                else spare.Add(e);
            }
            int loops = 0;
            foreach (var e in spare)
            {
                if (loops >= 11) break;
                int da = adj[e.Key].Count, db = adj[e.Value].Count;
                if (da == 1 && rnd.NextDouble() < 0.75) continue;
                if (db == 1 && rnd.NextDouble() < 0.75) continue;
                if (da >= 4 || db >= 4) continue;
                adj[e.Key].Add(e.Value); adj[e.Value].Add(e.Key); loops++;
            }

            var depth = new Dictionary<Vector2Int, int> { [S] = 0 };
            var queue = new Queue<Vector2Int>(); queue.Enqueue(S);
            while (queue.Count > 0)
            {
                var x = queue.Dequeue();
                foreach (var y in adj[x]) if (!depth.ContainsKey(y)) { depth[y] = depth[x] + 1; queue.Enqueue(y); }
            }

            var leaves = cells.FindAll(x => adj[x].Count == 1 && x != S && x != W && x != E);
            leaves.Sort((p, q) => depth[p].CompareTo(depth[q]));
            string[] lootPool = { "Frost Shard tome", "Stone Wall tome", "Firebolt tome", "Sharpened Blade", "Iron Pauldron", "Knight's Sigil" };
            var loot = new Dictionary<Vector2Int, string>();
            for (int i = 0; i < leaves.Count; i++) loot[leaves[i]] = i < lootPool.Length ? lootPool[i] : (rnd.NextDouble() < 0.5 ? "Health Potion" : "Mana Vial");

            string[] upper = { "Cloister", "Chapel", "Chapter House", "Refectory", "Dormitory", "Scriptorium", "Library", "Sacristy", "Vestry", "Bell Tower", "Armory", "Barracks", "Guardroom", "Almonry", "Choir", "Reliquary" };
            string[] lower = { "Ossuary", "Catacomb", "Crypt", "Charnel House", "Tomb", "Bone Gallery", "Sepulchre", "Vault", "Undercroft", "Mausoleum", "Shrine", "Barracks" };
            string[] water = { "Cistern", "Sluice Gate", "Pumphouse", "Aqueduct", "Drowned Hall", "Reservoir" };
            string[] prefix = { "North", "South", "East", "West", "Old", "Upper", "Lower", "Inner", "Outer", "Great", "Little", "Broken", "Sunken", "Silent", "Hollow" };
            var used = new HashSet<string>();
            int[] sizes = { 12, 14, 14, 16 };

            var roomLines = new List<string>();
            foreach (var c in cells)
            {
                int d = depth[c], deg = adj[c].Count;
                string kind = c == S ? "Start" : (c == W || c == E) ? "Dock" : leaves.Contains(c) ? "DeadEnd" : deg >= 4 ? "Hub" : "Dry";
                int w, dd;
                if (kind == "Start") w = dd = 20;
                else if (kind == "Hub") w = dd = rnd.NextDouble() < 0.5 ? 18 : 20;
                else if (kind == "Dock") w = dd = 16;
                else { w = sizes[rnd.Next(sizes.Length)]; dd = sizes[rnd.Next(sizes.Length)]; }
                string name = kind == "Start" ? "Breach Hall" : c == W ? "Western Boathouse" : c == E ? "Eastern Boathouse" : null;
                if (name == null)
                {
                    var pool = c.y <= 1 ? upper : c.y <= 3 ? lower : water;
                    for (int k = 0; k < 200; k++)
                    {
                        string n = prefix[rnd.Next(prefix.Length)] + " " + pool[rnd.Next(pool.Length)];
                        if (used.Add(n)) { name = n; break; }
                    }
                    if (name == null) name = "Hall " + c.x + "-" + c.y;
                }
                int mobs = 0;
                if (kind != "Start")
                {
                    mobs = (d <= 1 ? 0 : d <= 3 ? 1 : d <= 6 ? 2 : 3) + (kind == "DeadEnd" || kind == "Hub" ? 1 : 0) - (rnd.NextDouble() < 0.25 ? 1 : 0);
                    mobs = Mathf.Clamp(mobs, 0, 4);
                }
                string trap = kind == "Hub" ? "vent" : ((kind == "DeadEnd" && rnd.NextDouble() < 0.5) || rnd.NextDouble() < 0.12) ? "spike" : "";
                if (kind == "Start" || kind == "Dock") trap = ""; // the Breach Hall's pool and the boathouse piers stay clear
                string item;
                if (!loot.TryGetValue(c, out item)) item = rnd.NextDouble() < 0.35 ? "Health Potion" : (rnd.NextDouble() < 0.3 ? "Mana Vial" : "");
                if (kind == "Start") item = "";
                int patrol = mobs >= 1 && kind != "DeadEnd" && kind != "Dock" && rnd.NextDouble() < 0.5 ? 1 : 0;
                roomLines.Add("R" + c.x + c.y + "|" + name + "|" + c.x + "|" + c.y + "|" + w + "|" + dd + "|" + kind + "|" + mobs + "|" + item + "|" + trap + "|" + patrol);
            }
            var linkLines = new List<string>();
            var done = new HashSet<string>();
            foreach (var c in cells)
                foreach (var n in adj[c])
                {
                    string key = c.x < n.x || (c.x == n.x && c.y < n.y) ? c + "-" + n : n + "-" + c;
                    if (done.Add(key)) linkLines.Add("R" + c.x + c.y + "|R" + n.x + n.y);
                }
            ParseLayout(roomLines, linkLines);
            Debug.Log("[Fractured Vow] Generated layout from seed " + seed + ": " + linkLines.Count + " corridors, " + leaves.Count + " dead ends, " + loops + " loops.");
        }

        Vector3 At(Room r, float dx, float dz, float y = 0f) => new Vector3(r.X + dx, y, r.Z + dz);

        // =====================================================================================
        // GEOMETRY
        // =====================================================================================

        void BuildRoom(Room r)
        {
            var g = Group(r.id + " " + r.name, gStructure);
            float h = r.Height;
            Material wall = r.r == 4 ? mMossWall : (r.kind == "Hub" || r.kind == "Start" ? mPillar : mWall);

            if (r.kind == "Start")
            {
                // walkways around a deep pool: this is where the Leviathan first appears
                float px = 4f, pz = 5f;
                Floor(g, r.W0, r.W1, r.S0, r.Z - pz, 0f, mFloor, 4f);
                Floor(g, r.W0, r.W1, r.Z + pz, r.S1, 0f, mFloor, 4f);
                Floor(g, r.W0, r.X - px, r.Z - pz, r.Z + pz, 0f, mFloor, 4f);
                Floor(g, r.X + px, r.W1, r.Z - pz, r.Z + pz, 0f, mFloor, 4f);
                Floor(g, r.X - px, r.X + px, r.Z - pz, r.Z + pz, -4f, mRubble);
                Box("Water Surface", g, new Vector3(r.X, -0.12f, r.Z), new Vector3(px * 2f, 0.04f, pz * 2f), mWater, Quaternion.identity, false);
                MakeWaterZone("Pool Water Zone", g, r.X - px, r.X + px, r.Z - pz, r.Z + pz);
                for (int i = 0; i < 4; i++)
                {
                    float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                    Pillar(g, At(r, sx * (px + 1.2f), sz * (pz + 1.2f)), 0.7f, h);
                }
                Marker("FX Mist", new Vector3(r.X, 0.05f, r.Z), new Vector3(px * 2f, 0.2f, pz * 2f));
            }
            else Floor(g, r.W0, r.W1, r.S0, r.S1, 0f, mFloor);

            float keepLintel = lintelY;
            if (r.kind == "Start") lintelY = 6.4f; // the Breach Hall's south arch is twice as tall as a doorway
            WallX(g, r.S0, r.W0, r.W1, r.kind == "Start" ? -4f : 0f, h, wall, r.gapS.ToArray());
            lintelY = keepLintel;
            WallX(g, r.S1, r.W0, r.W1, r.kind == "Start" ? -4f : 0f, h, wall, r.gapN.ToArray());
            WallZ(g, r.W0, r.S0, r.S1, r.kind == "Start" ? -4f : 0f, h, wall, r.gapW.ToArray());
            WallZ(g, r.W1, r.S0, r.S1, r.kind == "Start" ? -4f : 0f, h, wall, r.gapE.ToArray());
            Ceiling(r.W0, r.W1, r.S0, r.S1, h);

            float ty = r.kind == "Hub" || r.kind == "Start" ? 3.2f : 2.6f;
            foreach (float s in new[] { -0.25f, 0.25f })
            {
                float x = r.X + r.w * s, z = r.Z + r.d * s;
                if (Clear(r.gapS, x)) Torch(new Vector3(x, ty, r.S0 + 0.3f), 0f);
                if (Clear(r.gapN, x)) Torch(new Vector3(x, ty, r.S1 - 0.3f), 180f);
                if (r.w >= 16f)
                {
                    if (Clear(r.gapW, z)) Torch(new Vector3(r.W0 + 0.3f, ty, z), 90f);
                    if (Clear(r.gapE, z)) Torch(new Vector3(r.W1 - 0.3f, ty, z), 270f);
                }
            }
            Sign(r.name.ToUpperInvariant(), new Vector3(r.X, Mathf.Min(h - 1.2f, 4f), r.Z));
            Marker("FX Dust", new Vector3(r.X, 2f, r.Z), new Vector3(r.w, 4f, r.d));
            if (r.kind == "Hub" || r.kind == "Dock" || r.kind == "Start")
                Marker("Checkpoint", At(r, 0f, r.kind == "Start" ? -r.d / 2f + 2.5f : 0f, 1f), new Vector3(Mathf.Min(8f, r.w - 2f), 2f, 4f));
        }

        void BuildLink(Room a, Room b)
        {
            var g = Group("Corridor " + a.id + "-" + b.id, gStructure);
            const float h = 4.5f;
            if (a.r == b.r)
            {
                float x0 = a.W1, x1 = b.W0, z0 = a.Z - 1.5f, z1 = a.Z + 1.5f;
                Floor(g, x0, x1, z0, z1, 0f, mFloor);
                WallX(g, z0, x0, x1, 0f, h, mWall);
                WallX(g, z1, x0, x1, 0f, h, mWall);
                Ceiling(x0, x1, z0, z1, h);
            }
            else
            {
                float z0 = a.S1, z1 = b.S0, x0 = a.X - 1.5f, x1 = a.X + 1.5f;
                Floor(g, x0, x1, z0, z1, 0f, mFloor);
                WallZ(g, x0, z0, z1, 0f, h, mWall);
                WallZ(g, x1, z0, z1, 0f, h, mWall);
                Ceiling(x0, x1, z0, z1, h);
            }
        }

        static bool Clear(List<Vector2> gaps, float v)
        {
            foreach (var g in gaps) if (v > g.x - 0.6f && v < g.y + 0.6f) return false;
            return true;
        }

        /// <summary>
        /// Floor 2's opening room: a big pillared antechamber south of the Breach Hall. Its north wall has a huge
        /// stone archway (8 m wide, 6.4 m high) bricked up with loose blocks, which the Leviathan smashes in the cutscene.
        /// </summary>
        void BuildAntechamber()
        {
            var s = rooms["R40"];
            var g = Group("Breach Antechamber", gStructure);
            const float half = 13f, depth = 28f, H = 9f;
            float x0 = s.X - half, x1 = s.X + half, z1 = s.S0, z0 = s.S0 - depth;
            Floor(g, x0, x1, z0, z1, 0f, mFloor);
            WallZ(g, x0, z0, z1, 0f, H, mWall);
            WallZ(g, x1, z0, z1, 0f, H, mWall);
            WallX(g, z0, x0, x1, 0f, H, mWall);
            WallX(g, z1, x0, s.W0, 0f, H, mWall);   // beside the Breach Hall
            WallX(g, z1, s.W1, x1, 0f, H, mWall);
            Ceiling(x0, x1, z0, z1, H);

            // two rows of great pillars leading to the bricked-up arch
            for (int i = 0; i < 3; i++)
            {
                float z = z0 + 5f + i * 7f;
                Pillar(g, new Vector3(s.X - 7.5f, 0f, z), 0.9f, H);
                Pillar(g, new Vector3(s.X + 7.5f, 0f, z), 0.9f, H);
                Chains(new Vector3(s.X - 7.5f, H - 1.2f, z + 1.2f));
                Chains(new Vector3(s.X + 7.5f, H - 1.2f, z - 1.2f));
            }
            // torches down both long walls, banners between them
            for (float z = z0 + 3.5f; z < z1 - 2f; z += 6f)
            {
                Torch(new Vector3(x0 + 0.3f, 3.2f, z), 90f);
                Torch(new Vector3(x1 - 0.3f, 3.2f, z), -90f);
            }
            Banner(new Vector3(x0 + 0.25f, 5.2f, z0 + 10f), 90f);
            Banner(new Vector3(x1 - 0.25f, 5.2f, z0 + 10f), -90f);
            Banner(new Vector3(x0 + 0.25f, 5.2f, z0 + 22f), 90f);
            Banner(new Vector3(x1 - 0.25f, 5.2f, z0 + 22f), -90f);
            // torches flanking the arch, so the wall is lit when it breaks
            Torch(new Vector3(s.X - 5.2f, 3.4f, z1 - 0.3f), 180f);
            Torch(new Vector3(s.X + 5.2f, 3.4f, z1 - 0.3f), 180f);
            for (int i = 0; i < 6; i++)
                Bones(new Vector3(s.X + ((i % 2 == 0) ? -3.2f : 3.6f) + (i - 3) * 0.5f, 0f, z0 + 4f + i * 3.6f));

            // the archway: a grid of stone blocks that the cutscene blows apart
            var wall = Group("Breakable Wall", g);
            const int cols = 10, rowsN = 8;
            const float archW = 8f, archH = 6.4f;
            float bw = archW / cols, bh = archH / rowsN;
            for (int ix = 0; ix < cols; ix++)
                for (int iy = 0; iy < rowsN; iy++)
                {
                    var c = Box("Wall Chunk", wall, new Vector3(s.X - archW / 2f + bw * (ix + 0.5f), bh * (iy + 0.5f), s.S0),
                                new Vector3(bw * 0.98f, bh * 0.98f, 0.7f), mWall);
                    c.isStatic = false;
                }
            Marker("Cutscene Wall", new Vector3(s.X, 3.2f, s.S0), Vector3.one);
            Marker("Cutscene Rise", new Vector3(s.X, -3.5f, s.Z - 2.5f), Vector3.one);
            Marker("Cutscene Strike", new Vector3(s.X, 3.4f, s.S0 - 4.5f), Vector3.one);
        }

        void BuildLake()
        {
            var g = Group("Lake - The Leviathan's Deep", gStructure);
            const float H = 20f;
            Floor(g, LX0, LX1, LZ0, LZ1, -4f, mRubble);
            Box("Water Surface", g, new Vector3((LX0 + LX1) / 2f, -0.12f, (LZ0 + LZ1) / 2f), new Vector3(LX1 - LX0, 0.04f, LZ1 - LZ0), mWater, Quaternion.identity, false);
            lakeZone = MakeWaterZone("Lake Water Zone", g, LX0, LX1, LZ0, LZ1);
            WallX(g, LZ0, LX0, LX1, -4f, H, mMossWall, new Vector2(50f, 54f), new Vector2(180f, 184f));
            WallX(g, LZ1, LX0, LX1, -4f, H, mMossWall, new Vector2(116.5f, 119.5f));
            WallZ(g, LX0, LZ0, LZ1, -4f, H, mMossWall);
            WallZ(g, LX1, LZ0, LZ1, -4f, H, mMossWall);
            Ceiling(LX0, LX1, LZ0, LZ1, H);

            // boathouse corridors and piers (one boat at each: the two routes to the boss)
            foreach (var id in new[] { "R24", "R74" })
            {
                var r = rooms[id];
                float x0 = r.X - 2f, x1 = r.X + 2f;
                var cg = Group("Boathouse Corridor " + id, gStructure);
                Floor(cg, x0, x1, r.S1, LZ0, 0f, mFloor);
                WallZ(cg, x0, r.S1, LZ0, 0f, 4.5f, mMossWall);
                WallZ(cg, x1, r.S1, LZ0, 0f, 4.5f, mMossWall);
                Ceiling(x0, x1, r.S1, LZ0, 4.5f);
                Floor(cg, x0, x1, LZ0, LZ0 + 6f, 0f, mWood, 4f);
                for (int i = 0; i < 2; i++) Box("Pier Post", gProps, new Vector3(i == 0 ? x0 + 0.2f : x1 - 0.2f, -0.6f, LZ0 + 5.8f), new Vector3(0.3f, 2f, 0.3f), mWood);
                Dock(id == "R24" ? "Western Pier" : "Eastern Pier", new Vector3(r.X, 0f, LZ0 + 4f), 0f, gDocks);
                BuildBoat(new Vector3(r.X, 0.05f, LZ0 + 8.3f), Quaternion.identity); // moored right against the pier's end
            }

            // five breakable platforms
            for (int i = 0; i < Platforms.Length; i++) BuildPlatform(i + 1, Platforms[i]);

            // north pier: the final memory, then the sealed gate to the surface
            Floor(g, 116f, 120f, LZ1 - 6f, LZ1, 0f, mWood, 4f);
            Dock("North Pier", new Vector3(118f, 0f, LZ1 - 3.5f), 180f, gDocks);
            Interact("Memory Fragment (appears after boss)", PrimitiveType.Cube, new Vector3(118f, 1.7f, LZ1 - 4.5f), Vector3.one * 0.5f, false);
            Marker("BossGate", new Vector3(118f, 1.6f, LZ1 + 2f), Vector3.one, 90f);
            Marker("Checkpoint", new Vector3(118f, 1f, LZ1 - 3f), new Vector3(3f, 2f, 3f));

            // rock spires to steer around, stalactites overhead, glowing fungus on the walls
            foreach (var p in new[] { new Vector2(60, 160), new Vector2(66, 192), new Vector2(178, 168), new Vector2(186, 132), new Vector2(132, 152), new Vector2(104, 150), new Vector2(160, 188) })
            {
                float h = 8f + (float)rng.NextDouble() * 8f;
                Box("Rock Spire", g, new Vector3(p.x, -4f + h / 2f, p.y), new Vector3(2.4f, h, 2.2f), mRubble, Quaternion.Euler(0f, rng.Next(0, 90), rng.Next(-6, 6)));
                Box("Rock Spire Base", g, new Vector3(p.x, -2f, p.y), new Vector3(3.6f, 4.2f, 3.4f), mRubble, Quaternion.Euler(0f, rng.Next(0, 90), 0f));
            }
            for (int i = 0; i < 60; i++)
            {
                float x = LX0 + 4f + (float)rng.NextDouble() * (LX1 - LX0 - 8f), z = LZ0 + 4f + (float)rng.NextDouble() * (LZ1 - LZ0 - 8f);
                float len = 2f + (float)rng.NextDouble() * 5f;
                Box("Stalactite", gProps, new Vector3(x, H - len / 2f, z), new Vector3(0.4f + len * 0.08f, len, 0.4f + len * 0.08f), mRubble,
                    Quaternion.Euler(rng.Next(-4, 4), rng.Next(0, 90), rng.Next(-4, 4)), false);
            }
            for (int i = 0; i < 40; i++)
            {
                bool south = rng.NextDouble() < 0.5;
                float x = LX0 + 3f + (float)rng.NextDouble() * (LX1 - LX0 - 6f);
                float z = south ? LZ0 + 0.35f : LZ1 - 0.35f;
                float y = 0.3f + (float)rng.NextDouble() * 4f;
                for (int k = 0; k < 3; k++)
                    Child(gProps, PrimitiveType.Sphere, "Glow Fungus", Vector3.zero, Vector3.one * (0.15f + (float)rng.NextDouble() * 0.2f), mFungus)
                        .transform.position = new Vector3(x + k * 0.25f, y + k * 0.15f, z);
            }
            for (float x = LX0 + 9f; x < LX1 - 4f; x += 18f)
            {
                if (Mathf.Abs(x - 52f) > 4f && Mathf.Abs(x - 182f) > 4f) Torch(new Vector3(x, 4f, LZ0 + 0.3f), 0f);
                if (Mathf.Abs(x - 118f) > 4f) Torch(new Vector3(x, 4f, LZ1 - 0.3f), 180f);
            }

            Enemy("LEVIATHAN (final boss)", LeviathanHome, 180f, true);
            Sign("THE LEVIATHAN'S DEEP", new Vector3(118f, 9f, LZ0 + 6f));
            Marker("FX Mist", new Vector3((LX0 + LX1) / 2f, 0.05f, (LZ0 + LZ1) / 2f), new Vector3(LX1 - LX0, 0.2f, LZ1 - LZ0));
            Marker("FX Drips", new Vector3((LX0 + LX1) / 2f, H - 0.5f, (LZ0 + LZ1) / 2f), new Vector3(LX1 - LX0 - 8f, 0.1f, LZ1 - LZ0 - 8f));
        }

        void BuildPlatform(int n, Vector2 c)
        {
            var root = new GameObject("Platform P" + n);
            root.transform.SetParent(gStructure, false);
            root.transform.position = new Vector3(c.x, 0f, c.y);
            var slab = Box("Platform Stone", root.transform, new Vector3(c.x, -1.8f, c.y), new Vector3(12f, 4.4f, 12f), mPillar);
            slab.isStatic = false;
            foreach (var v in new[] { new Vector2(-5.6f, -5.6f), new Vector2(5.6f, -5.6f), new Vector2(-5.6f, 5.6f), new Vector2(5.6f, 5.6f) })
            {
                var post = Box("Corner Stone", root.transform, new Vector3(c.x + v.x, 0.8f, c.y + v.y), new Vector3(0.8f, 0.8f, 0.8f), mWall);
                post.isStatic = false;
            }
            var brazier = Box("Brazier Stand", root.transform, new Vector3(c.x, 0.9f, c.y), new Vector3(0.5f, 1f, 0.5f), mMetal);
            brazier.isStatic = false;
            var cracks = new GameObject("Cracks");
            cracks.transform.SetParent(root.transform, false);
            for (int i = 0; i < 7; i++)
            {
                var k = Box("Crack", cracks.transform, new Vector3(c.x + (float)rng.NextDouble() * 9f - 4.5f, 0.42f, c.y + (float)rng.NextDouble() * 9f - 4.5f),
                    new Vector3(0.12f, 0.03f, 2f + (float)rng.NextDouble() * 3f), mCrack, Quaternion.Euler(0f, rng.Next(0, 180), 0f), false);
                k.isStatic = false;
            }
            cracks.SetActive(false);
            var pts = new List<Transform>();
            foreach (var v in new[] { new Vector3(0f, 0f, -5.3f), new Vector3(0f, 0f, 5.3f), new Vector3(-5.3f, 0f, 0f), new Vector3(5.3f, 0f, 0f) })
                pts.Add(Dock("P" + n + " Edge", new Vector3(c.x, 0.45f, c.y) + v, 0f, root.transform));
            var bp = root.AddComponent<BreakablePlatform>();
            bp.dockPoints = pts.ToArray();
            bp.cracks = cracks;
            Torch(new Vector3(c.x, 1.7f, c.y), 0f);
            bp.riders = new[] { gLights.GetChild(gLights.childCount - 1) }; // the brazier's fire sinks with the platform
        }

        void BuildSurfaceExit()
        {
            var g = Group("Surface Stair", gStructure);
            float x0 = 116.5f, x1 = 119.5f;
            Floor(g, x0, x1, LZ1, LZ1 + 4f, 0f, mFloor);
            for (int i = 0; i < 12; i++)
                Box("Step " + (i + 1), g, new Vector3(118f, (i + 1) * 0.125f, LZ1 + 4.5f + i), new Vector3(3f, (i + 1) * 0.25f, 1f), mFloor);
            Box("Surface Landing", g, new Vector3(118f, 1.5f, LZ1 + 23f), new Vector3(3f, 3f, 14f), mWall);
            Box("Surface Landing Floor", g, new Vector3(118f, 3.02f, LZ1 + 23f), new Vector3(3f, 0.04f, 14f), mFloor, Quaternion.identity, false);
            WallZ(g, x0, LZ1, LZ1 + 30f, 0f, 9f, mWall);
            WallZ(g, x1, LZ1, LZ1 + 30f, 0f, 9f, mWall);
            WallX(g, LZ1 + 30f, x0, x1, 0f, 9f, mWall);
            Ceiling(x0, x1, LZ1, LZ1 + 30f, 9f);
            Interact("Surface Gate (final exit)", PrimitiveType.Cube, new Vector3(118f, 4.4f, LZ1 + 29.6f), new Vector3(2.6f, 2.8f, 0.2f), false);
            var sun = new GameObject("Sunlight (surface)").AddComponent<Light>();
            sun.transform.SetParent(gLights, false);
            sun.transform.position = new Vector3(118f, 7f, LZ1 + 26f);
            sun.type = LightType.Point; sun.color = new Color(1f, 0.9f, 0.7f); sun.range = 22f; sun.intensity = 6f;
        }

        WaterZone MakeWaterZone(string name, Transform parent, float x0, float x1, float z0, float z1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3((x0 + x1) / 2f, -2.75f, (z0 + z1) / 2f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(x1 - x0, 4.5f, z1 - z0);
            return go.AddComponent<WaterZone>();
        }

        Transform Dock(string name, Vector3 pos, float yaw, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            docks.Add(t);
            return t;
        }

        void BuildBoat(Vector3 pos, Quaternion rot)
        {
            var boat = new GameObject("Boat (cyan)");
            boat.transform.SetParent(gInteract);
            boat.transform.SetPositionAndRotation(pos, rot);
            var col = boat.AddComponent<BoxCollider>();
            col.size = new Vector3(1.6f, 0.5f, 3.2f);
            var rb = boat.AddComponent<Rigidbody>();
            rb.mass = 200f;
            Child(boat.transform, PrimitiveType.Cube, "Hull", Vector3.zero, new Vector3(1.6f, 0.5f, 3.2f), mBoat);
            var bow = Child(boat.transform, PrimitiveType.Cube, "Bow", new Vector3(0f, 0f, 1.6f), new Vector3(1.13f, 0.5f, 1.13f), mBoat);
            bow.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            var seat = new GameObject("Seat").transform;
            seat.SetParent(boat.transform, false);
            seat.localPosition = new Vector3(0f, 0.25f, -0.4f);
            var bc = boat.AddComponent<BoatController>();
            bc.seat = seat;
            bc.dockReach = 7f;
        }

        void Marker(string name, Vector3 pos, Vector3 size, float yaw = 0f)
        {
            var t = new GameObject(name).transform;
            t.SetParent(gMarkers, false);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            t.localScale = size;
        }

        // =====================================================================================
        // CONTENT
        // =====================================================================================

        string npcRoomA, npcRoomB;

        /// <summary>Against the middle of a wall with no doorway, so a chest never blocks the way.</summary>
        Vector3 ChestSpot(Room r)
        {
            if (r.gapN.Count == 0) return At(r, 0f, r.d / 2f - 1.6f, 0.4f);
            if (r.gapS.Count == 0) return At(r, 0f, -r.d / 2f + 1.6f, 0.4f);
            if (r.gapW.Count == 0) return At(r, -r.w / 2f + 1.6f, 0f, 0.4f);
            return At(r, r.w / 2f - 1.6f, 0f, 0.4f);
        }

        void PickNpcRooms()
        {
            npcRoomA = npcRoomB = null;
            foreach (var l in links)
            {
                Room other = l.Key.id == "R40" ? l.Value : l.Value.id == "R40" ? l.Key : null;
                if (other == null || other.kind == "DeadEnd") continue;
                if (npcRoomA == null) npcRoomA = other.id; else if (npcRoomB == null) npcRoomB = other.id;
            }
            if (npcRoomB == null) npcRoomB = npcRoomA;
        }

        void Populate(Room r)
        {
            Furnish(r);

            // enemies
            var spots = new[]
            {
                new Vector2[0],
                new[] { new Vector2(0f, 1f) },
                new[] { new Vector2(-3f, 1.5f), new Vector2(3f, -1.5f) },
                new[] { new Vector2(-3f, 2f), new Vector2(3f, 2f), new Vector2(0f, -2.5f) },
                new[] { new Vector2(-3.5f, 3f), new Vector2(3.5f, 3f), new Vector2(-3.5f, -3f), new Vector2(3.5f, -3f) },
            };
            var list = spots[Mathf.Clamp(r.mobs, 0, 4)];
            for (int i = 0; i < list.Length; i++)
            {
                string kind = r.kind == "Dock" ? "Drowned Husk" : (r.kind == "DeadEnd" && i == 0 ? "Husk (sleeping in cell)" : "Husk Sentinel");
                Enemy(kind, At(r, list[i].x, list[i].y), rng.Next(0, 360), false);
                if (i == 0 && r.patrol)
                {
                    var e = gEnemies.GetChild(gEnemies.childCount - 1);
                    float half = r.w / 2f - 2.5f;
                    foreach (var p in new[] { new Vector2(-half, 0f), new Vector2(half, 0f) })
                    {
                        var wp = new GameObject("Waypoint").transform;
                        wp.SetParent(e, true);
                        wp.position = At(r, p.x, p.y);
                    }
                }
            }

            // loot: tomes and gear sit on a pedestal against the north wall
            if (!string.IsNullOrEmpty(r.item))
            {
                bool pickup = r.item == "Health Potion" || r.item == "Mana Vial";
                var spot = At(r, 0f, r.d / 2f - 2.2f);
                if (!pickup) Box("Pedestal", gProps, spot + Vector3.up * 0.45f, new Vector3(0.9f, 0.9f, 0.9f), mPillar);
                var type = r.item.Contains("Potion") ? PrimitiveType.Sphere : r.item.Contains("Vial") || r.item.Contains("Sigil") ? PrimitiveType.Cylinder : PrimitiveType.Cube;
                Item(r.item, type, spot + Vector3.up * (pickup ? 0.5f : 1.15f), Vector3.one * 0.45f, pickup ? mPowerUp : mTool);
            }
            if (r.kind == "Hub" || r.kind == "Dock") Item(rng.NextDouble() < 0.5 ? "Health Potion" : "Mana Vial", PrimitiveType.Sphere, At(r, r.w / 2f - 2f, -r.d / 2f + 2f, 0.5f), Vector3.one * 0.45f, mPowerUp);

            // treasure: every dead end has a chest, hubs sometimes
            if (r.kind == "DeadEnd" || (r.kind == "Hub" && rng.NextDouble() < 0.5))
                Interact(r.kind == "DeadEnd" && rng.NextDouble() < 0.5 ? "Chest (rich)" : "Chest", PrimitiveType.Cube, ChestSpot(r), new Vector3(1.2f, 0.8f, 0.8f), false);

            // the merchant and a sellsword wait in the rooms next to the Breach Hall; another in the Eastern Boathouse
            if (r.id == npcRoomA) Interact("Merchant", PrimitiveType.Capsule, At(r, -r.w / 4f, r.d / 4f, 1f), new Vector3(0.6f, 1f, 0.6f), false);
            if (r.id == npcRoomB) Interact("Mercenary: Corvin the Exile", PrimitiveType.Capsule, At(r, r.w / 4f, r.d / 4f, 1f), new Vector3(0.6f, 1f, 0.6f), false);
            if (r.id == "R74") Interact("Mercenary: Maren the Boatwright", PrimitiveType.Capsule, At(r, -r.w / 4f, -r.d / 4f, 1f), new Vector3(0.6f, 1f, 0.6f), false);

            if (r.kind == "Start") return; // nothing else in the cutscene room: the Leviathan rises from its pool
            if (r.trap == "spike") Trap("Spike Plate", At(r, 0f, -1.5f, 0.03f), new Vector3(2f, 0.06f, 5f));
            if (r.trap == "vent") { Trap("Flame Vent", At(r, -4f, 0f, 0.03f), new Vector3(2f, 0.06f, 2f)); Trap("Flame Vent", At(r, 4f, 0f, 0.03f), new Vector3(2f, 0.06f, 2f)); }
        }

        /// <summary>Room furniture chosen by the room's name, kept to the walls so the middle stays clear for fights.</summary>
        void Furnish(Room r)
        {
            string n = r.name;
            float ix = r.w / 2f - 1.3f, iz = r.d / 2f - 1.3f;
            if (Has(n, "Library", "Scriptorium"))
            {
                for (int i = -1; i <= 1; i++) if (Clear(r.gapN, r.X + i * 3.2f)) Shelf(At(r, i * 3.2f, iz - 0.1f), 0f);
                if (Clear(r.gapW, r.Z)) Shelf(At(r, -ix + 0.1f, 0f), 90f);
                Table(At(r, ix - 1.5f, -iz + 1.5f));
            }
            else if (Has(n, "Chapel", "Choir", "Shrine", "Sacristy", "Vestry", "Reliquary"))
            {
                for (int i = 0; i < 3; i++) { Pew(At(r, -2.6f, -iz + 2f + i * 1.8f)); Pew(At(r, 2.6f, -iz + 2f + i * 1.8f)); }
                Box("Altar", gProps, At(r, 0f, iz - 0.6f, 0.55f), new Vector3(2.4f, 1.1f, 1f), mPillar);
                Box("Altar Cloth", gProps, At(r, 0f, iz - 0.6f, 1.12f), new Vector3(2.5f, 0.04f, 1.05f), mBannerA, Quaternion.identity, false);
                Candles(At(r, -0.8f, iz - 0.6f, 1.12f)); Candles(At(r, 0.8f, iz - 0.6f, 1.12f));
            }
            else if (Has(n, "Ossuary", "Catacomb", "Bone Gallery", "Charnel", "Crypt", "Tomb", "Sepulchre", "Mausoleum", "Undercroft", "Vault"))
            {
                if (Clear(r.gapW, r.Z)) Sarcophagus(At(r, -ix + 0.5f, 0f), 0f);
                if (Clear(r.gapE, r.Z)) Sarcophagus(At(r, ix - 0.5f, 0f), 0f);
                Bones(At(r, -ix + 1f, iz - 1f)); Bones(At(r, ix - 1f, -iz + 1f)); Bones(At(r, ix - 1.5f, iz - 1f));
                Candles(At(r, -ix + 0.6f, -iz + 0.6f));
            }
            else if (Has(n, "Barracks", "Dormitory", "Guardroom"))
            {
                for (int i = -1; i <= 1; i++) if (Clear(r.gapN, r.X + i * 3f)) Box("Bunk", gProps, At(r, i * 3f, iz - 0.4f, 0.35f), new Vector3(1.1f, 0.7f, 2.1f), mWood);
                Box("Weapon Rack", gProps, At(r, ix - 0.2f, -iz + 2f, 1.1f), new Vector3(0.35f, 2.2f, 2.4f), mWood);
                Barrel(At(r, -ix + 0.6f, -iz + 0.6f));
            }
            else if (Has(n, "Refectory", "Almonry"))
            {
                Table(At(r, -3f, 2f)); Table(At(r, 3f, 2f)); Table(At(r, -3f, -2.5f)); Table(At(r, 3f, -2.5f));
                Barrel(At(r, ix - 0.5f, iz - 0.5f)); Barrel(At(r, ix - 1.5f, iz - 0.6f));
            }
            else if (Has(n, "Armory"))
            {
                Box("Weapon Rack", gProps, At(r, -ix + 0.2f, 1f, 1.1f), new Vector3(0.35f, 2.2f, 2.6f), mWood);
                Box("Shield Rack", gProps, At(r, ix - 0.2f, 1f, 1f), new Vector3(0.35f, 2f, 2.2f), mWood);
                Box("Anvil", gProps, At(r, 0f, -iz + 1.2f, 0.4f), new Vector3(0.9f, 0.8f, 0.5f), mMetal);
                Crate(At(r, ix - 0.7f, -iz + 0.7f), 1f);
            }
            else if (Has(n, "Cistern", "Sluice", "Pumphouse", "Aqueduct", "Drowned Hall", "Reservoir"))
            {
                Box("Pipe", gProps, At(r, 0f, iz + 0.9f, 3.6f), new Vector3(r.w - 1f, 0.5f, 0.5f), mMetal, Quaternion.identity, false);
                Box("Grate", gProps, At(r, 0f, 0f, 0.02f), new Vector3(2.4f, 0.04f, 2.4f), mMetal, Quaternion.identity, false);
                Barrel(At(r, -ix + 0.6f, iz - 0.6f)); Crate(At(r, ix - 0.6f, iz - 0.6f), 0.9f);
            }
            else if (Has(n, "Cloister", "Chapter House", "Bell Tower"))
            {
                foreach (var v in new[] { new Vector2(-ix + 1.2f, -iz + 1.2f), new Vector2(ix - 1.2f, -iz + 1.2f), new Vector2(-ix + 1.2f, iz - 1.2f), new Vector2(ix - 1.2f, iz - 1.2f) })
                    Pillar(gProps, At(r, v.x, v.y), 0.45f, r.Height);
                if (n.Contains("Bell")) Box("Fallen Bell", gProps, At(r, ix - 2f, 0f, 0.7f), new Vector3(1.4f, 1.4f, 1.4f), mGold, Quaternion.Euler(0f, 20f, 70f));
            }
            if (r.kind == "Hub")
            {
                foreach (var v in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                    Pillar(gStructure, At(r, v.x * (r.w / 2f - 3f), v.y * (r.d / 2f - 3f)), 0.75f, r.Height);
                Box("Statue Plinth", gProps, At(r, 0f, 0f, 0.4f), new Vector3(1.6f, 0.8f, 1.6f), mPillar);
                Box("Statue", gProps, At(r, 0f, 0f, 1.9f), new Vector3(0.7f, 2.2f, 0.5f), mWall);
            }
            if (r.kind == "Start")
            {
                Banner(new Vector3(r.X - 5f, 4.2f, r.S1 - 0.3f), 180f); Banner(new Vector3(r.X + 5f, 4.2f, r.S1 - 0.3f), 180f);
            }

            // pack props (or barrels and crates) in two free corners
            var corners = new List<Vector2> { new Vector2(-ix, -iz), new Vector2(ix, -iz), new Vector2(-ix, iz), new Vector2(ix, iz) };
            for (int k = 0; k < 2; k++)
            {
                var c = corners[rng.Next(corners.Count)];
                corners.Remove(c);
                Prop(At(r, c.x, c.y));
            }
        }

        static bool Has(string n, params string[] keys)
        {
            foreach (var k in keys) if (n.Contains(k)) return true;
            return false;
        }

        void Shelf(Vector3 pos, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Box("Bookshelf", gProps, pos + Vector3.up * 1.3f, new Vector3(2.4f, 2.6f, 0.5f), mWood, rot);
            var mats = new[] { mBookA, mBookB, mBookC };
            for (int s = 0; s < 3; s++)
                Box("Books", gProps, pos + Vector3.up * (0.55f + s * 0.8f) + rot * new Vector3(0f, 0f, -0.05f), new Vector3(2.1f, 0.42f, 0.4f), mats[Mathf.Abs(s + (int)pos.x) % 3], rot, false);
        }

        void Pew(Vector3 pos)
        {
            Box("Pew Seat", gProps, pos + Vector3.up * 0.45f, new Vector3(2.6f, 0.12f, 0.5f), mWood);
            Box("Pew Back", gProps, pos + new Vector3(0f, 0.85f, -0.22f), new Vector3(2.6f, 0.7f, 0.08f), mWood, Quaternion.identity, false);
        }

        void Table(Vector3 pos)
        {
            Box("Table", gProps, pos + Vector3.up * 0.45f, new Vector3(2.6f, 0.9f, 1.2f), mWood);
            Candles(pos + Vector3.up * 0.9f);
        }

        void Sarcophagus(Vector3 pos, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Box("Sarcophagus", gProps, pos + Vector3.up * 0.5f, new Vector3(1.1f, 1f, 2.3f), mPillar, rot);
            Box("Sarcophagus Lid", gProps, pos + Vector3.up * 1.07f, new Vector3(1.2f, 0.14f, 2.4f), mWall, rot, false);
        }

        void Candles(Vector3 pos)
        {
            for (int i = 0; i < 3; i++)
            {
                float h = 0.15f + i * 0.07f;
                var c = Child(gProps, PrimitiveType.Cylinder, "Candle", Vector3.zero, new Vector3(0.06f, h / 2f, 0.06f), mBone);
                c.transform.position = pos + new Vector3(-0.12f + i * 0.12f, h / 2f, (i % 2) * 0.08f);
                var f = Child(gProps, PrimitiveType.Sphere, "Candle Flame", Vector3.zero, Vector3.one * 0.05f, mFlame);
                f.transform.position = pos + new Vector3(-0.12f + i * 0.12f, h + 0.03f, (i % 2) * 0.08f);
            }
        }

        void Banner(Vector3 pos, float yaw)
        {
            var rot = Quaternion.Euler(0f, yaw, 0f);
            Box("Banner", gProps, pos, new Vector3(1.2f, 2.4f, 0.04f), mBannerB, rot, false);
            Box("Banner Rod", gProps, pos + Vector3.up * 1.25f, new Vector3(1.5f, 0.07f, 0.07f), mGold, rot, false);
        }

        void Prop(Vector3 pos)
        {
            if (packProps.Count == 0)
            {
                if (rng.NextDouble() < 0.5) Barrel(pos); else Crate(pos, 0.9f);
                return;
            }
            var prefab = packProps[rng.Next(packProps.Count)];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(gProps, true);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, rng.Next(0, 360), 0f));
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            foreach (var rr in rends) b.Encapsulate(rr.bounds);
            float k = Mathf.Min(1f, 1.8f / Mathf.Max(Mathf.Max(b.size.x, b.size.z), 0.01f), 2.5f / Mathf.Max(b.size.y, 0.01f));
            go.transform.localScale *= k;
            b = rends[0].bounds;
            foreach (var rr in rends) b.Encapsulate(rr.bounds);
            go.transform.position += Vector3.up * (pos.y - b.min.y);
        }

        void PlacePlayer(Vector3 pos, Quaternion rot)
        {
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            player.transform.SetPositionAndRotation(pos, rot);
            var cam = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera));
            cam.tag = "MainCamera";
            cam.transform.position = pos + new Vector3(0f, 2.5f, -4f);
            var tpc = cam.AddComponent<ThirdPersonCamera>();
            tpc.target = player.transform;
            tpc.input = player.GetComponent<PlayerInputHandler>();
            tpc.maxDistance = 9f;
            tpc.distance = 4.5f;
            Selection.activeGameObject = player;
        }

        void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.16f, 0.2f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.07f);
            RenderSettings.fogDensity = 0.018f;
            var sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.name = "Moonlight (dim)";
                sun.intensity = 0.25f;
                sun.color = new Color(0.6f, 0.7f, 1f);
                sun.transform.rotation = Quaternion.Euler(65f, 30f, 0f);
            }
            if (Camera.main != null)
            {
                Camera.main.clearFlags = CameraClearFlags.SolidColor;
                Camera.main.backgroundColor = new Color(0.03f, 0.03f, 0.05f);
            }
        }
        // =====================================================================================
        // GEOMETRY HELPERS
        // =====================================================================================

        const float WallT = 0.5f;
        float lintelY = 3.2f; // doorway height; a lintel fills the wall above it

        void Floor(Transform p, float x0, float x1, float z0, float z1, float topY, Material m, float thickness = 0.5f)
        {
            Box("Floor", p, new Vector3((x0 + x1) / 2f, topY - thickness / 2f, (z0 + z1) / 2f), new Vector3(x1 - x0, thickness, z1 - z0), m);
        }

        void Ceiling(float x0, float x1, float z0, float z1, float y)
        {
            Box("Ceiling", gCeilings, new Vector3((x0 + x1) / 2f, y + 0.25f, (z0 + z1) / 2f), new Vector3(x1 - x0 + WallT, 0.5f, z1 - z0 + WallT), mWall);
        }

        /// <summary>Wall running along X at a fixed z, with optional door gaps (x ranges). Doorways get a lintel above 3 m.</summary>
        void WallX(Transform p, float z, float x0, float x1, float y0, float y1, Material m, params Vector2[] gaps)
        {
            foreach (var seg in Segments(x0 - WallT / 2f, x1 + WallT / 2f, gaps))
                Box("Wall", p, new Vector3((seg.x + seg.y) / 2f, (y0 + y1) / 2f, z), new Vector3(seg.y - seg.x, y1 - y0, WallT), m);
            foreach (var g in gaps)
                if (y1 - lintelY > 0.2f && y0 < lintelY)
                    Box("Lintel", p, new Vector3((g.x + g.y) / 2f, (lintelY + y1) / 2f, z), new Vector3(g.y - g.x, y1 - lintelY, WallT), m);
        }

        /// <summary>Wall running along Z at a fixed x, with optional door gaps (z ranges).</summary>
        void WallZ(Transform p, float x, float z0, float z1, float y0, float y1, Material m, params Vector2[] gaps)
        {
            foreach (var seg in Segments(z0 - WallT / 2f, z1 + WallT / 2f, gaps))
                Box("Wall", p, new Vector3(x, (y0 + y1) / 2f, (seg.x + seg.y) / 2f), new Vector3(WallT, y1 - y0, seg.y - seg.x), m);
            foreach (var g in gaps)
                if (y1 - lintelY > 0.2f && y0 < lintelY)
                    Box("Lintel", p, new Vector3(x, (lintelY + y1) / 2f, (g.x + g.y) / 2f), new Vector3(WallT, y1 - lintelY, g.y - g.x), m);
        }

        static List<Vector2> Segments(float a, float b, Vector2[] gaps)
        {
            var list = new List<Vector2>();
            var sorted = new List<Vector2>(gaps);
            sorted.Sort((u, v) => u.x.CompareTo(v.x));
            float cur = a;
            foreach (var g in sorted)
            {
                if (g.x - cur > 0.05f) list.Add(new Vector2(cur, g.x));
                cur = Mathf.Max(cur, g.y);
            }
            if (b - cur > 0.05f) list.Add(new Vector2(cur, b));
            return list;
        }

        GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material m) =>
            Box(name, parent, center, size, m, Quaternion.identity, true);

        GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material m, Quaternion rot, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, rot);
            go.transform.localScale = size;
            go.GetComponent<MeshFilter>().sharedMesh = BoxMesh(size);
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) DestroyImmediate(go.GetComponent<Collider>());
            go.isStatic = true;
            return go;
        }

        /// <summary>Cube mesh with world-scaled box-mapped UVs, so textures tile at a constant 2 m regardless of size.</summary>
        Mesh BoxMesh(Vector3 size)
        {
            string key = string.Format("box_{0:0.##}_{1:0.##}_{2:0.##}", size.x, size.y, size.z).Replace(',', '.');
            if (meshCache.TryGetValue(key, out var cached)) return cached;
            string path = meshDir + "/" + key + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) { meshCache[key] = existing; return existing; }

            var m = Object.Instantiate(unitCube);
            m.name = key;
            var v = m.vertices; var n = m.normals; var uv = new Vector2[v.Length];
            const float tile = 2f;
            for (int i = 0; i < v.Length; i++)
            {
                Vector3 w = Vector3.Scale(v[i], size);
                Vector3 an = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
                if (an.x > 0.5f) uv[i] = new Vector2(w.z, w.y) / tile;
                else if (an.y > 0.5f) uv[i] = new Vector2(w.x, w.z) / tile;
                else uv[i] = new Vector2(w.x, w.y) / tile;
            }
            m.uv = uv;
            AssetDatabase.CreateAsset(m, path);
            meshCache[key] = m;
            return m;
        }

        GameObject Child(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        void Pillar(Transform p, Vector3 basePos, float radius, float height)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Pillar";
            go.transform.SetParent(p, false);
            go.transform.position = basePos + Vector3.up * height / 2f;
            go.transform.localScale = new Vector3(radius * 2f, height / 2f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = mPillar;
            go.isStatic = true;
            Box("Pillar Base", p, basePos + Vector3.up * 0.2f, new Vector3(radius * 2.4f, 0.4f, radius * 2.4f), mWall);
        }

        void Bars(Transform p, float x, float z0, float z1, Vector2? broken)
        {
            for (float z = z0 + 0.3f; z < z1 - 0.1f; z += 0.35f)
            {
                if (broken.HasValue && z > broken.Value.x && z < broken.Value.y) continue;
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bar.name = "Bar";
                bar.transform.SetParent(p, false);
                bar.transform.position = new Vector3(x, 2.25f, z);
                bar.transform.localScale = new Vector3(0.08f, 2.25f, 0.08f);
                bar.GetComponent<Renderer>().sharedMaterial = mMetal;
                bar.isStatic = true;
            }
            if (broken.HasValue)
            {
                var bent = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bent.name = "Bent Bar";
                bent.transform.SetParent(p, false);
                bent.transform.position = new Vector3(x + 0.6f, 0.4f, (broken.Value.x + broken.Value.y) / 2f);
                bent.transform.rotation = Quaternion.Euler(0f, 0f, 80f);
                bent.transform.localScale = new Vector3(0.08f, 1.1f, 0.08f);
                bent.GetComponent<Renderer>().sharedMaterial = mMetal;
                DestroyImmediate(bent.GetComponent<Collider>());
            }
            Box("Bar Rail", p, new Vector3(x, 4.3f, (z0 + z1) / 2f), new Vector3(0.15f, 0.15f, z1 - z0), mMetal);
        }

        void Torch(Vector3 pos, float yaw)
        {
            var t = new GameObject("Torch").transform;
            t.SetParent(gLights, false);
            t.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var stick = Child(t, PrimitiveType.Cylinder, "Sconce", new Vector3(0f, -0.15f, 0.15f), new Vector3(0.1f, 0.3f, 0.1f), mWood);
            stick.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
            Child(t, PrimitiveType.Sphere, "Flame", new Vector3(0f, 0.2f, 0.25f), new Vector3(0.18f, 0.3f, 0.18f), mFlame);
            var light = new GameObject("Torch Light").AddComponent<Light>();
            light.transform.SetParent(t, false);
            light.transform.localPosition = new Vector3(0f, 0.3f, 0.6f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.3f);
            light.range = 9f;
            light.intensity = 2.2f;
            light.shadows = LightShadows.None;
        }

        void Barrel(Vector3 pos)
        {
            var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            b.name = "Barrel";
            b.transform.SetParent(gProps, false);
            b.transform.position = pos + Vector3.up * 0.55f;
            b.transform.localScale = new Vector3(0.8f, 0.55f, 0.8f);
            b.GetComponent<Renderer>().sharedMaterial = mWood;
        }

        void Crate(Vector3 pos, float s) =>
            Box("Crate", gProps, pos + Vector3.up * s / 2f, Vector3.one * s, mWood, Quaternion.Euler(0f, rng.Next(0, 30), 0f));

        void Bones(Vector3 pos)
        {
            for (int i = 0; i < 4; i++)
            {
                var b = Child(gProps, PrimitiveType.Cylinder, "Bone", Vector3.zero, new Vector3(0.07f, 0.25f, 0.07f), mBone);
                b.transform.position = pos + new Vector3((float)rng.NextDouble() - 0.5f, 0.04f, (float)rng.NextDouble() - 0.5f);
                b.transform.rotation = Quaternion.Euler(90f, rng.Next(0, 180), 0f);
            }
            var skull = Child(gProps, PrimitiveType.Sphere, "Skull", Vector3.zero, Vector3.one * 0.22f, mBone);
            skull.transform.position = pos + new Vector3(0.3f, 0.11f, 0.2f);
        }

        void Chains(Vector3 pos)
        {
            for (int i = 0; i < 5; i++)
            {
                var link = Child(gProps, PrimitiveType.Cube, "Chain Link", Vector3.zero, new Vector3(0.06f, 0.18f, 0.12f), mMetal);
                link.transform.position = pos + Vector3.down * i * 0.17f;
                link.transform.rotation = Quaternion.Euler(0f, i % 2 == 0 ? 0f : 90f, 0f);
            }
        }

        // ---- placeholders (colour code from the movement milestone) ----

        void Item(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
        {
            var go = Child(gItems, type, name, Vector3.zero, scale, m);
            go.transform.position = pos;
        }

        void Enemy(string name, Vector3 pos, float yaw, bool boss)
        {
            var e = new GameObject(name).transform;
            e.SetParent(gEnemies, false);
            e.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            float s = boss ? 2.2f : 1f;
            var body = GameObject.CreatePrimitive(boss ? PrimitiveType.Cube : PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(e, false);
            body.transform.localPosition = new Vector3(0f, boss ? 1.8f : 0.9f, 0f);
            body.transform.localScale = boss ? new Vector3(2.4f, 3.6f, 1.8f) : new Vector3(0.8f, 0.9f, 0.8f);
            body.GetComponent<Renderer>().sharedMaterial = boss ? mBoss : mEnemy;
            Child(e, PrimitiveType.Cube, "Eyes (front)", new Vector3(0f, boss ? 3.1f : 1.5f, boss ? 0.95f : 0.4f), new Vector3(0.5f * s, 0.1f * s, 0.1f), mMetal);
            if (boss) Child(e, PrimitiveType.Cube, "Great Axe", new Vector3(1.6f, 2f, 0.6f), new Vector3(0.25f, 0.25f, 2.6f), mMetal);
        }

        void Trap(string name, Vector3 pos, Vector3 scale)
        {
            var go = Child(gTraps, PrimitiveType.Cube, name, Vector3.zero, scale, mTrap);
            go.transform.position = pos;
        }

        GameObject Interact(string name, PrimitiveType type, Vector3 pos, Vector3 scale, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(gInteract, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mInteract;
            if (!collider) DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        void Sign(string text, Vector3 pos)
        {
            var go = new GameObject("Sign - " + text);
            go.transform.SetParent(gTour, false);
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = 0.06f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(1f, 0.85f, 0.55f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) { tm.font = font; go.GetComponent<MeshRenderer>().sharedMaterial = font.material; }
            go.AddComponent<Billboard>();
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        // =====================================================================================
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

        static void AddToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path)) return;
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
