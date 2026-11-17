using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FracturedVow.EditorTools
{
    /// <summary>
    /// Tools > Fractured Vow > Set Up Imported Packs
    /// Run once after importing the Asset Store packs. It searches the whole project by name and builds the
    /// game's asset library (Resources/FVLibrary): characters (with Animator controllers built from each pack's
    /// own clips), weapons and armor as items, chests, coins, a boat, props for set dressing, arena props and
    /// Particle Pack spell effects. Then rebuild the floors and run the Production Pass.
    /// Select Assets/FracturedVow/Resources/FVLibrary afterwards to see or change what it picked.
    /// </summary>
    public class AssetHookup : EditorWindow
    {
        string report = "";
        List<string> urpInstalled;
        Vector2 scroll;
        string root, genDir;

        [MenuItem("Tools/Fractured Vow/Set Up Imported Packs")]
        static void Open() { GetWindow<AssetHookup>("Set Up Packs").minSize = new Vector2(480, 420); }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Set up imported Asset Store packs", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Import the packs: Window > Package Manager > My Assets > (pack) > Download > Import.\n" +
                "2. Click the button. It finds characters, weapons, armor, chests, coins, the boat, props and spell effects by name.\n" +
                "3. Rebuild Floor 1 and Floor 2 and run the Production Pass on each.\n" +
                "Run it again any time you import more packs.", MessageType.Info);
            if (GUILayout.Button("Find and set up everything", GUILayout.Height(36))) EditorApplication.delayCall += () => MaterialFixer.ImportUrpPackages(installed => { urpInstalled = installed; Run(); });
            if (GUILayout.Button("Select the library asset")) Selection.activeObject = AssetDatabase.LoadAssetAtPath<FVLibrary>(LibPath());
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox(string.IsNullOrEmpty(report) ? "Not run yet." : report, MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        string LibPath()
        {
            Resolve();
            return root + "/Resources/FVLibrary.asset";
        }

        void Resolve()
        {
            string me = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)).Replace('\\', '/');
            string dir = me.Substring(0, me.LastIndexOf('/'));
            root = dir.EndsWith("/Editor") ? dir.Substring(0, dir.LastIndexOf('/')) : "Assets/FracturedVow";
            genDir = root + "/Generated";
        }

        // =====================================================================================

        class Found { public string path, name, lower; public GameObject go; public bool skinned, particles; public Vector3 size; }

        // ---------------- curated tables for the packs this project uses ----------------
        // (prefab name, folder hint, item name, tier). Anything not listed is still found by the generic search below.
        static readonly (string prefab, string hint, string name, int tier)[] Weapons =
        {
            ("SeaxKnife", "polyronin", "Seax Knife", 1), ("Hook", "polyronin", "Pirate's Hook", 1), ("PT_Mace_01_a", "polytope", "Iron Mace", 1),
            ("Axe", "monster pack", "Orcish Hatchet", 1), ("Spear", "viking", "Viking Spear", 1), ("PP_Theme_10_Dagger_002", "purepoly", "Rogue's Dagger", 1),
            ("Culass1", "polyronin", "Corsair Cutlass", 2), ("Culass2", "polyronin", "Captain's Cutlass", 2), ("PT_Sword_01_a", "polytope", "Knight's Arming Sword", 2),
            ("Axe", "viking", "Bearded Axe", 2), ("PP_Theme_11_Axe_One_Handed_002", "purepoly", "Woodsman's Axe", 2), ("Falchion_01", "sazengames", "Grave Falchion", 2),
            ("PP_Theme_09_Spear_005", "purepoly", "Hunter's Spear", 2), ("PT_ShortWaraxe_01_a", "polytope", "War Axe", 2), ("Sword", "viking", "Ulfberht Sword", 2),
            ("PP_Theme_11_Sword_One-Handed_003", "purepoly", "Templar Blade", 2),
            ("DoubleHeadAxe", "polyronin", "Jarl's Greataxe", 3), ("PT_Longsword_01_a", "polytope", "Oathbound Longsword", 3),
            ("PP_Theme_06_Sword_Two-Handed_001", "purepoly", "Vowbreaker Greatsword", 3), ("PP_Theme_03_Hammer_001", "purepoly", "Warden's Maul", 3),
            ("PP_Theme_01_Axe_Two_Handed_002", "purepoly", "Executioner's Axe", 3), ("PP_Theme_04_Scythe_001", "purepoly", "Reaper's Scythe", 3),
        };

        static readonly (string prefab, string hint, string name, int tier, float armor, float health, float mana)[] Trinkets =
        {
            ("Shield1", "viking", "Viking Roundshield", 1, 0.06f, 0f, 0f), ("Shield2", "viking", "Painted Roundshield", 2, 0.09f, 10f, 0f),
            ("PT_Shield_01_a", "polytope", "Heater Shield", 2, 0.1f, 10f, 0f), ("PT_Shield_06_a", "polytope", "Kite Shield", 3, 0.14f, 15f, 0f),
            ("Helmet", "viking", "Horned Helm", 2, 0.04f, 25f, 0f), ("PP_Theme_07_Scepter_001", "purepoly", "Necromancer's Scepter", 2, 0f, 0f, 35f),
            ("PP_Theme_04_Spellbook_003", "purepoly", "Grimoire of Ash", 3, 0f, 0f, 50f), ("PP_Theme_06_Potion_002", "purepoly", "Vial of Vigor", 1, 0f, 20f, 0f),
        };

        // Polytope's modular armor sets: equipping one changes what a companion actually wears.
        static readonly (string outfit, string model, string name, int tier, float armor, float health)[] ArmorSets =
        {
            ("cloth_00", "PT_Male_Armor_cloth_00_body", "Traveler's Garb", 1, 0.04f, 0f),
            ("01_A", "PT_Male_Armor_01_A_body", "Ranger's Mail", 2, 0.12f, 10f),
            ("05_C", "PT_Male_Armor_05_C_body", "Knight's Plate", 3, 0.22f, 25f),
        };

        static readonly (string cat, string[] names)[] PropTable =
        {
            ("barrel", new[] { "Barrel_Closed", "Barrel_Big", "Barrel_Open", "Barrel_01" }),
            ("crate", new[] { "Box_01", "FirewoodStack", "Firewood_01" }),
            ("table", new[] { "Table_Small", "Table_Big", "Desk", "Table3" }),
            ("chair", new[] { "Stool_round", "Stool_square", "Chair", "Bench" }),
            ("candle", new[] { "Candlestick", "Candlestick_Triple", "Candlestick_Small", "Candle_01", "Candle_02", "Candle_03" }),
            ("shelf", new[] { "Shelf", "Shelf_Wall" }),
            ("bones", new[] { "Skull", "Bone" }),
            ("pot", new[] { "Amphora", "Jar_Big", "Jar_Full", "Jug", "Bucket", "Cauldron_Empty", "Cauldron_Full", "Amphora_Stand" }),
            ("banner", new[] { "Banner" }),
            ("sack", new[] { "Bag", "Bag_01" }),
            ("rack", new[] { "WeaponRack_Big", "WeaponRack_Small" }),
            ("misc", new[] { "Book_Big_Red", "Scroll_Stack", "CrystalBall_Stand", "Scales", "Cauldron_Small_Stand", "Book_Open", "Carpet_Red", "Carpet_Green" }),
        };

        class Roles { public AnimationClip idle, walk, run, attack, hurt, death, dodge, cast; }

        void Run()
        {
            Resolve();
            Ensure(root + "/Resources");
            Ensure(genDir + "/Animators");
            var log = new List<string>();
            int converted = MaterialFixer.FixAll(out string matReport);
            log.Add("Materials: " + (urpInstalled != null && urpInstalled.Count > 0 ? "installed the packs' own URP versions (" + string.Join(", ", urpInstalled) + "), and " : "") +
                    converted + " other materials converted to URP (no more purple).");

            var all = new List<Found>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(root) || path.Contains("/Editor/") || path.StartsWith("Packages/")) continue;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;
                var f = new Found { path = path, go = go, name = go.name, lower = (path + "/" + go.name).ToLowerInvariant() };
                f.skinned = go.GetComponentInChildren<SkinnedMeshRenderer>(true) != null;
                f.particles = go.GetComponentInChildren<ParticleSystem>(true) != null;
                f.size = Size(go);
                if (f.lower.Contains("/demo scenes/") || go.GetComponentInChildren<Camera>(true) != null) continue; // pack demo players and cameras
                all.Add(f);
            }
            // every animation clip, with its file path (some packs name the clips after the file only)
            var clips = new List<(AnimationClip clip, string key)>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(root) || path.StartsWith("Packages/")) continue;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (a is AnimationClip c && !c.name.StartsWith("__preview__") && !c.legacy && c.name.ToLowerInvariant() != "tpose" && !clips.Any(x => x.clip == c))
                        clips.Add((c, (path + "/" + c.name).ToLowerInvariant()));
            }
            log.Add("Scanned " + all.Count + " prefabs and " + clips.Count + " animation clips.");

            var lib = AssetDatabase.LoadAssetAtPath<FVLibrary>(LibPath());
            if (lib == null) { lib = CreateInstance<FVLibrary>(); AssetDatabase.CreateAsset(lib, LibPath()); }
            lib.heroes.Clear(); lib.enemyMelee.Clear(); lib.enemyRanged.Clear(); lib.enemyBrute.Clear();
            lib.items.Clear(); lib.props.Clear(); lib.arenaProps.Clear();
            lib.chest = lib.bigChest = lib.coin = lib.boat = null;
            lib.fireBolt = lib.fireImpact = lib.frostBolt = lib.frostImpact = lib.castFlash = lib.wallBurst = lib.splash = null;

            Found P(string name, string hint = null) =>
                all.FirstOrDefault(f => f.name == name && (hint == null || f.lower.Contains(hint)) && !f.lower.Contains("/hdrp/") && !f.lower.Contains("/built-in/"));
            AnimationClip C(params string[] tokens)
            {
                foreach (var c in clips) if (tokens.All(t => c.key.Contains(t))) return c.clip;
                return null;
            }

            // ---------------- animator controllers from the packs' clips ----------------
            var hero = new Roles
            {
                idle = C("blink", "/idle.fbx"), run = C("blink", "/runforward.fbx"), attack = C("blink", "meleeattack_onehanded"),
                hurt = C("blink", "gethit"), death = C("blink", "/death.fbx"), dodge = C("blink", "rollforward"), cast = C("blink", "spellcast")
            };
            hero.walk = hero.run;
            var heroCtrl = Make("FV_Humanoid", hero, log);
            var sazen = new Roles
            {
                idle = C("sazengames", "skeleton_idle"), walk = C("sazengames", "skeleton_walk_forward.fbx"), run = C("sazengames", "skeleton_run_forward.fbx"),
                attack = C("sazengames", "skeleton_slash01"), hurt = C("sazengames", "skeleton_take_damage"), death = C("sazengames", "skeleton_death"),
                cast = C("sazengames", "skeleton_throw_projectiles")
            };
            var skeletonCtrl = Make("FV_Skeleton", sazen, log);
            var necro = new Roles { idle = sazen.idle, walk = sazen.walk, run = sazen.walk, attack = sazen.cast, hurt = sazen.hurt, death = sazen.death, cast = sazen.cast };
            if (necro.idle == null) necro = hero;
            var necroCtrl = Make("FV_Necromancer", necro, log);
            var ds = new Roles { idle = C("dungeoncharacters", "ds_onehand_idle"), walk = C("dungeoncharacters", "ds_onehand_walk"), attack = C("dungeoncharacters", "ds_onehand_attack") };
            ds.run = ds.walk;
            var dsCtrl = Make("FV_DungeonSkeleton", ds, log);

            void Char(List<FVLibrary.Character> list, Found f, RuntimeAnimatorController ctrl, string display)
            {
                if (f == null || list.Any(c => c.prefab == f.go)) return;
                list.Add(new FVLibrary.Character { name = display, prefab = f.go, controller = ctrl });
            }
            Char(lib.heroes, P("HumanMale_Character_FREE"), heroCtrl, "Wanderer");
            Char(lib.heroes, P("PT_Lowpoly_Armors_Male_Moduar_Free"), heroCtrl, "Sellsword");
            Char(lib.heroes, P("PT_Lowpoly_Armors_Female_Moduar_Free"), heroCtrl, "Shieldmaiden");

            // the shopkeeper: Stylized NPC - Peasant Nolant, with its own idle animation
            var nolant = P("Peasant Nolant Brown(Free Version)") ?? all.FirstOrDefault(f => f.name.StartsWith("Peasant Nolant") && f.skinned);
            lib.merchant = null;
            if (nolant != null)
            {
                var own = nolant.go.GetComponentInChildren<Animator>(true);
                lib.merchant = new FVLibrary.Character { name = "Nolant the Peddler", prefab = nolant.go, controller = own != null ? own.runtimeAnimatorController : null };
            }
            // sellswords use the same Polytope model as the player's hero (different armor and faces)
            var polyMale = P("PT_Lowpoly_Armors_Male_Moduar_Free");
            lib.companion = polyMale != null ? new FVLibrary.Character { name = "Sellsword", prefab = polyMale.go, controller = heroCtrl } : null;
            Char(lib.enemyMelee, P("Skeleton_110", "sazengames"), skeletonCtrl, "Risen Skeleton");
            Char(lib.enemyMelee, P("DungeonSkeleton_demoPrefab"), dsCtrl, "Crypt Skeleton");
            Char(lib.enemyMelee, P("Skell_Warrior", "monster pack"), null, "Skeleton Warrior");
            Char(lib.enemyMelee, P("Skeleton", "monster pack"), null, "Bone Thrall");
            Char(lib.enemyRanged, P("SKM_Skeleton_Necromancer", "/urp/"), necroCtrl, "Necromancer");
            Char(lib.enemyRanged, P("Skell_Mage", "monster pack"), null, "Skeleton Mage");
            Char(lib.enemyBrute, P("Orc", "monster pack"), null, "Orc Brute");
            Char(lib.enemyBrute, P("Minotaur", "monster pack"), null, "Minotaur");
            Char(lib.enemyBrute, P("WereWolf", "monster pack"), null, "Werewolf");

            // generic search for any other character packs
            foreach (var f in all.Where(f => f.skinned && !Ours(f) && f.size.y > 0.5f && f.size.y < 8f))
            {
                if (lib.heroes.Any(c => c.prefab == f.go) || lib.enemyMelee.Any(c => c.prefab == f.go) || lib.enemyRanged.Any(c => c.prefab == f.go)) continue;
                if (f.lower.Contains("/hdrp/") || f.lower.Contains("/built-in/") || f.lower.Contains("separate_parts")) continue;
                if (Has(f, "necromancer", "mage", "caster", "wizard", "lich", "shaman", "sorcer", "witch", "cultist")) Add(lib.enemyRanged, f, 4);
                else if (Has(f, "skeleton", "undead", "zombie", "ghoul")) Add(lib.enemyMelee, f, 6);
            }
            foreach (var list in new[] { lib.heroes, lib.enemyMelee, lib.enemyRanged, lib.enemyBrute })
                foreach (var c in list) if (c.controller == null && c.prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) c.controller = BuildController(c.prefab, log);
            log.Add("Merchant: " + (lib.merchant != null ? "Peasant Nolant" : "(Peasant Nolant not imported - using " + (lib.heroes.Count > 0 ? lib.heroes[0].name : "none") + ")") +
                    "   Companions: " + (lib.companion != null ? "the player's Polytope model" : "(none)"));
            log.Add("Characters: " + string.Join(", ", lib.heroes.Select(c => c.name)) + " (companions, merchant, prisoner)\n" +
                    "Enemies: " + string.Join(", ", lib.enemyMelee.Concat(lib.enemyRanged).Concat(lib.enemyBrute).Select(c => c.name)));

            // ---------------- items ----------------
            foreach (var w in Weapons)
            {
                var f = P(w.prefab, w.hint);
                if (f == null) continue;
                lib.items.Add(new ItemDef { id = "w_" + Id(w.name), name = w.name, kind = ItemKind.Weapon, model = f.go, tier = w.tier,
                    damage = 0.02f + w.tier * 0.11f, price = 60 + w.tier * 130, color = new Color(0.78f, 0.78f, 0.82f) });
            }
            foreach (var t in Trinkets)
            {
                var f = P(t.prefab, t.hint);
                if (f == null) continue;
                lib.items.Add(new ItemDef { id = "t_" + Id(t.name), name = t.name, kind = ItemKind.Trinket, model = f.go, tier = t.tier,
                    armor = t.armor, health = t.health, mana = t.mana, price = 70 + t.tier * 120, color = new Color(0.9f, 0.75f, 0.45f) });
            }
            foreach (var a in ArmorSets)
            {
                var f = P(a.model, "polytope");
                lib.items.Add(new ItemDef { id = "a_" + Id(a.name), name = a.name, kind = ItemKind.Armor, model = f != null ? f.go : null, tier = a.tier, outfit = a.outfit,
                    armor = a.armor, health = a.health, price = 60 + a.tier * 140, color = new Color(0.65f, 0.6f, 0.55f) });
            }
            log.Add("Items: " + lib.items.Count(i => i.kind == ItemKind.Weapon) + " weapons, " + lib.items.Count(i => i.kind == ItemKind.Trinket) +
                    " shields/helms/trinkets, " + lib.items.Count(i => i.kind == ItemKind.Armor) + " armor sets (companions wear them).");

            // ---------------- chests, coin, boat ----------------
            lib.chest = (P("ChestSmall") ?? P("PT_Chest_01"))?.go;
            lib.bigChest = (P("Chest", "brokenvector") ?? P("PT_Chest_01"))?.go;
            lib.coin = (P("Coin_01") ?? P("Coin_02"))?.go;
            lib.boat = (P("Wood_BoatV1", "1.2") ?? P("Wood_BoatV1") ?? P("Wood_BoatV2"))?.go;
            log.Add("Chest: " + Name(lib.chest) + "   big chest: " + Name(lib.bigChest) + "   coin: " + Name(lib.coin) + "   boat: " + Name(lib.boat));

            // ---------------- props ----------------
            foreach (var t in PropTable)
                foreach (var n in t.names)
                {
                    var f = P(n);
                    if (f != null) lib.props.Add(new FVLibrary.Prop { category = t.cat, prefab = f.go });
                }
            foreach (var f in all.Where(f => !f.skinned && !f.particles && Has(f, "deep sea", "deepsea", "hunter", "harpoon") && f.size.magnitude > 0.4f && f.size.magnitude < 9f).Take(30))
                lib.arenaProps.Add(f.go);
            log.Add("Props: " + lib.props.Count + " in " + lib.props.Select(p => p.category).Distinct().Count() + " categories.  Boss arena props: " +
                    (lib.arenaProps.Count > 0 ? lib.arenaProps.Count.ToString() : "none (the Deep Sea Hunter's Station pack isn't imported)"));

            // ---------------- Particle Pack ----------------
            Found Fx(string n) => all.FirstOrDefault(f => f.particles && f.name == n);
            lib.fireBolt = Fx("FireBall")?.go;
            lib.fireImpact = (Fx("SmallExplosion") ?? Fx("TinyExplosion"))?.go;
            lib.fireTrail = Fx("RocketTrail")?.go;
            lib.frostBolt = null; // the frost bolt is a glowing ice shard with a frost trail; IceLance bursts where it hits
            lib.frostImpact = (Fx("IceLance") ?? Fx("SparksEffect"))?.go;
            lib.wallBurst = (Fx("EarthShatter") ?? Fx("DustExplosion"))?.go;
            lib.splash = Fx("BigSplash")?.go;
            log.Add("Spell effects: fire " + Name(lib.fireBolt) + " -> " + Name(lib.fireImpact) + ", frost " + Name(lib.frostBolt) + " -> " + Name(lib.frostImpact) +
                    ", stone wall " + Name(lib.wallBurst) + ", Leviathan splash " + Name(lib.splash));

            // ---------------- dungeon dressing and world effects ----------------
            lib.torchModel = P("Torch_Wall", "brokenvector")?.go;
            lib.chandelier = P("Chandelier", "brokenvector")?.go;
            lib.throne = P("Throne", "brokenvector")?.go;
            lib.fireplace = P("Fireplace", "brokenvector")?.go;
            lib.carpet = P("Carpet_Red", "brokenvector")?.go;
            lib.hitFlesh = Fx("FleshImpacts")?.go;
            lib.hitMetal = Fx("MetalImpacts")?.go;
            lib.torchFlame = Fx("TinyFlames")?.go;
            lib.groundFog = Fx("GroundFog")?.go;
            lib.fireflies = Fx("FireFlies")?.go;
            lib.respawn = Fx("EllenRespawn")?.go;
            lib.candles = Fx("Candles")?.go;
            foreach (var list in new[] { lib.enemyMelee, lib.enemyRanged, lib.enemyBrute })
                foreach (var c in list) c.stomp = c.prefab != null && c.prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) == null;
            log.Add("Dressing: torch " + Name(lib.torchModel) + ", chandelier " + Name(lib.chandelier) + ", throne " + Name(lib.throne) +
                    ".  Effects: hits " + Name(lib.hitFlesh) + " / " + Name(lib.hitMetal) + ", torch flames " + Name(lib.torchFlame) +
                    ", fog " + Name(lib.groundFog) + ", fireflies " + Name(lib.fireflies) + ", respawn " + Name(lib.respawn));

            // ---------------- the player's hero ----------------
            var heroClips = new Dictionary<string, AnimationClip>
            {
                ["idle"] = C("blink", "/idle.fbx"), ["run"] = C("blink", "/runforward.fbx"), ["sprint"] = C("blink", "/sprint.fbx"),
                ["attack1"] = C("blink", "meleeattack_onehanded"), ["attack2"] = C("blink", "meleeattack_twohanded"),
                ["block"] = C("blink", "blockingloop"), ["cast"] = C("blink", "spellcast"), ["hurt"] = C("blink", "gethit"),
                ["death"] = C("blink", "/death.fbx"), ["jump"] = C("blink", "/jumps.fbx"),
                ["rollF"] = C("blink", "rollforward"), ["rollB"] = C("blink", "rollbackward"), ["rollL"] = C("blink", "rollleft"), ["rollR"] = C("blink", "rollright"),
            };
            log.Add(BuildHero(P("PT_Lowpoly_Armors_Male_Moduar_Free")?.go, (P("PT_Longsword_01_a") ?? P("PT_Sword_01_a"))?.go, P("PT_Shield_06_a")?.go, heroClips));

            log.Add(UpgradeKnight());

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            report = string.Join("\n\n", log) + "\n\nSaved " + LibPath() + ".\nNext: rebuild Floor 1 and Floor 2, then run the Production Pass on each.";
            Debug.Log("[Fractured Vow] " + report);
            Repaint();
        }

        bool Has(Found f, params string[] keys) { foreach (var k in keys) if (f.lower.Contains(k)) return true; return false; }
        bool Ours(Found f) => Has(f, "toon_rts", "fracturedvow", "/charactercontrol", "unitytechnologies", "textmesh");

        /// <summary>
        /// Builds "Player (Hero)": a copy of the Player (Knight) prefab with the same scripts, wearing the Polytope
        /// modular knight (plate armor, longsword, kite shield) and animated with the Blink humanoid clips:
        /// idle, run, sprint, one- and two-handed attacks, block, spell cast, hit, death, jump and 4-way rolls.
        /// </summary>
        string BuildHero(GameObject model, GameObject sword, GameObject shield, Dictionary<string, AnimationClip> c)
        {
            string knightPath = root + "/Prefabs/Player (Knight).prefab";
            string heroPath = root + "/Prefabs/Player (Hero).prefab";
            if (model == null) return "Hero: the Polytope modular character isn't imported, so the knight stays the player.";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(knightPath) == null) return "Hero: Player (Knight).prefab not found, skipped.";
            if (c["idle"] == null || c["run"] == null) return "Hero: the Blink animations aren't imported, skipped.";
            AnimationClip Or(AnimationClip a, AnimationClip b) => a != null ? a : b;

            string cpath = genDir + "/Animators/FV_Hero.controller";
            var ac = FreshController(cpath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            foreach (var p in new[] { "Grounded", "Blocking", "Dead" }) ac.AddParameter(p, AnimatorControllerParameterType.Bool);
            foreach (var p in new[] { "AttackIndex", "DodgeDir" }) ac.AddParameter(p, AnimatorControllerParameterType.Int);
            foreach (var p in new[] { "Attack", "Heavy", "Cast", "Hurt", "Jump", "Dodge" }) ac.AddParameter(p, AnimatorControllerParameterType.Trigger);
            var sm = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
            tree.AddChild(c["idle"], 0f);
            tree.AddChild(c["run"], 0.6f);
            tree.AddChild(Or(c["sprint"], c["run"]), 1f);
            sm.defaultState = loco;

            AnimatorState State(string n, AnimationClip clip, bool back = true)
            {
                var st = sm.AddState(n);
                st.motion = Or(clip, c["idle"]);
                if (back) { var t = st.AddTransition(loco); t.hasExitTime = true; t.exitTime = 0.88f; t.duration = 0.12f; }
                return st;
            }
            AnimatorStateTransition Any(AnimatorState to, float blend = 0.08f)
            {
                var t = sm.AddAnyStateTransition(to);
                t.hasExitTime = false; t.duration = blend; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                return t;
            }
            Any(State("Attack Light", c["attack1"])).AddCondition(AnimatorConditionMode.If, 0, "Attack");
            var a1 = sm.anyStateTransitions[sm.anyStateTransitions.Length - 1]; a1.AddCondition(AnimatorConditionMode.Less, 2, "AttackIndex");
            Any(State("Attack Heavy", Or(c["attack2"], c["attack1"]))).AddCondition(AnimatorConditionMode.If, 0, "Attack");
            var a2 = sm.anyStateTransitions[sm.anyStateTransitions.Length - 1]; a2.AddCondition(AnimatorConditionMode.Greater, 1, "AttackIndex");
            Any(State("Cast", c["cast"])).AddCondition(AnimatorConditionMode.If, 0, "Cast");
            Any(State("Hurt", c["hurt"])).AddCondition(AnimatorConditionMode.If, 0, "Hurt");
            Any(State("Jump", c["jump"])).AddCondition(AnimatorConditionMode.If, 0, "Jump");
            string[] rollKeys = { "rollF", "rollB", "rollL", "rollR" };
            for (int i = 0; i < 4; i++)
            {
                var t = Any(State("Roll " + i, Or(c[rollKeys[i]], c["rollF"])), 0.05f);
                t.AddCondition(AnimatorConditionMode.If, 0, "Dodge");
                t.AddCondition(AnimatorConditionMode.Equals, i, "DodgeDir");
            }
            var block = State("Block", c["block"], false);
            Any(block, 0.1f).AddCondition(AnimatorConditionMode.If, 0, "Blocking");
            var unblock = block.AddTransition(loco); unblock.hasExitTime = false; unblock.duration = 0.12f;
            unblock.AddCondition(AnimatorConditionMode.IfNot, 0, "Blocking");
            var death = State("Death", c["death"], false);
            var td = sm.AddAnyStateTransition(death);
            td.hasExitTime = false; td.duration = 0.1f; td.canTransitionToSelf = false;
            td.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            var revive = death.AddTransition(loco); revive.hasExitTime = false; revive.duration = 0.2f;
            revive.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");

            // the prefab: same root and scripts as the knight, new body
            var rootGo = PrefabUtility.LoadPrefabContents(knightPath);
            try
            {
                var old = rootGo.transform.Find("Model");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                var m = (GameObject)PrefabUtility.InstantiatePrefab(model, rootGo.transform);
                PrefabUtility.UnpackPrefabInstance(m, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                m.name = "Model";
                m.transform.localPosition = Vector3.zero;
                m.transform.localRotation = Quaternion.identity;
                foreach (var col in m.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
                var anim = m.GetComponentInChildren<Animator>();
                if (anim == null) anim = m.AddComponent<Animator>();
                anim.runtimeAnimatorController = ac;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var rends = m.GetComponentsInChildren<Renderer>();
                if (rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    if (b.size.y > 0.01f) m.transform.localScale *= 1.8f / b.size.y;
                }
                ModularOutfit.Apply(m, "05_C", 3);
                Transform Slot(string key)
                {
                    foreach (var t in m.GetComponentsInChildren<Transform>(true)) if (t.name.ToLowerInvariant().Contains(key)) return t;
                    return null;
                }
                var hand = Slot("right_hand_weapon_slot");
                if (hand != null && sword != null) { var w = (GameObject)PrefabUtility.InstantiatePrefab(sword, hand); w.transform.localPosition = Vector3.zero; w.transform.localRotation = Quaternion.identity; }
                var off = Slot("left_hand_shield_slot");
                if (off != null && shield != null) { var s = (GameObject)PrefabUtility.InstantiatePrefab(shield, off); s.transform.localPosition = Vector3.zero; s.transform.localRotation = Quaternion.identity; }
                var bridge = rootGo.GetComponent<AnimatorBridge>();
                if (bridge != null) bridge.animator = anim;
                var combat = rootGo.GetComponent<PlayerCombat>();
                if (combat != null) combat.shieldVisual = null; // the block animation raises the shield
                rootGo.name = "Player (Hero)";
                PrefabUtility.SaveAsPrefabAsset(rootGo, heroPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(rootGo); }
            return "Hero: created " + heroPath + " (Polytope plate knight with " + sm.states.Length + " animated states: rolls, block, cast, attacks). " +
                   "The level builders now pick it automatically.";
        }

        RuntimeAnimatorController Make(string name, Roles r, List<string> log)
        {
            var baseIdle = r.idle != null ? r.idle : r.walk;
            if (baseIdle == null) { log.Add(name + ": clips not found, skipped."); return null; }
            string cpath = genDir + "/Animators/" + name + ".controller";
            var ac = FreshController(cpath);
            foreach (var p in new[] { "Attack", "Hurt", "Dodge", "Cast" }) ac.AddParameter(p, AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            var sm = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
            tree.AddChild(baseIdle, 0f);
            if (r.walk != null) tree.AddChild(r.walk, 0.45f);
            if (r.run != null) tree.AddChild(r.run, 1f);
            sm.defaultState = loco;
            AnimatorState State(string n, Motion m) { var st = sm.AddState(n); st.motion = m != null ? m : baseIdle; return st; }
            void FromAny(AnimatorState to, string trig)
            {
                var t = sm.AddAnyStateTransition(to);
                t.hasExitTime = false; t.duration = 0.08f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0, trig);
                t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                var back = to.AddTransition(loco); back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.15f;
            }
            FromAny(State("Attack", r.attack), "Attack");
            FromAny(State("Hurt", r.hurt), "Hurt");
            if (r.dodge != null) FromAny(State("Dodge", r.dodge), "Dodge");
            if (r.cast != null) FromAny(State("Cast", r.cast), "Cast");
            var sd = State("Death", r.death);
            var td = sm.AddAnyStateTransition(sd);
            td.hasExitTime = false; td.duration = 0.1f; td.canTransitionToSelf = false;
            td.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            return ac;
        }

        static void Add(List<FVLibrary.Character> list, Found f, int max)
        {
            if (list.Count >= max || list.Any(c => c.prefab == f.go)) return;
            list.Add(new FVLibrary.Character { name = Pretty(f.name), prefab = f.go });
        }

        // ---------------- animator controllers ----------------

        /// <summary>Builds an Animator Controller for a character from the clips that ship in the same pack.</summary>
        RuntimeAnimatorController BuildController(GameObject prefab, List<string> log)
        {
            var existing = prefab.GetComponentInChildren<Animator>(true);
            string path = AssetDatabase.GetAssetPath(prefab);
            string[] parts = path.Split('/');
            string folder = parts.Length > 2 ? parts[0] + "/" + parts[1] : "Assets";
            var clips = new List<AnimationClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (a is AnimationClip c && !c.name.StartsWith("__preview__") && !c.legacy && !clips.Contains(c)) clips.Add(c);
            // prefer clips that belong to this character (monster packs name clips after each monster)
            string stem = prefab.name.ToLowerInvariant().Split('_', ' ', '-')[0];
            var own = clips.Where(c => c.name.ToLowerInvariant().Contains(stem)).ToList();
            if (own.Count >= 2) clips = own;
            if (clips.Count == 0) return existing != null ? existing.runtimeAnimatorController : null;

            AnimationClip Pick(params string[] keys)
            {
                foreach (var k in keys) foreach (var c in clips) if (c.name.ToLowerInvariant().Contains(k)) return c;
                return null;
            }
            var idle = Pick("idle", "stand", "breath");
            var walk = Pick("walk");
            var run = Pick("run", "sprint", "charge", "jog");
            var attack = Pick("attack", "slash", "swing", "hit01", "strike", "punch", "melee", "cast");
            var cast = Pick("cast", "spell", "magic", "summon");
            var hurt = Pick("gethit", "get_hit", "hurt", "damage", "hit", "impact");
            var death = Pick("death", "die", "dead");
            var dodge = Pick("roll", "dodge", "evade", "dash");
            if (idle == null && walk == null) idle = clips[0];

            string cpath = genDir + "/Animators/" + Id(prefab.name) + ".controller";
            var ac = FreshController(cpath);
            foreach (var p in new[] { "Attack", "Hurt", "Dodge", "Cast" }) ac.AddParameter(p, AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Dead", AnimatorControllerParameterType.Bool);
            var sm = ac.layers[0].stateMachine;
            var baseIdle = idle != null ? idle : walk;
            var loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D; tree.blendParameter = "Speed"; tree.useAutomaticThresholds = false;
            tree.AddChild(baseIdle, 0f);
            if (walk != null) tree.AddChild(walk, 0.45f);
            if (run != null) tree.AddChild(run, 1f);
            sm.defaultState = loco;
            AnimatorState State(string n, Motion m) { var s = sm.AddState(n); s.motion = m != null ? m : baseIdle; return s; }
            void FromAny(AnimatorState to, string trig)
            {
                var t = sm.AddAnyStateTransition(to);
                t.hasExitTime = false; t.duration = 0.08f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0, trig);
                t.AddCondition(AnimatorConditionMode.IfNot, 0, "Dead");
                var back = to.AddTransition(loco); back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.15f;
            }
            FromAny(State("Attack", attack), "Attack");
            FromAny(State("Hurt", hurt), "Hurt");
            if (dodge != null) FromAny(State("Dodge", dodge), "Dodge");
            if (cast != null) FromAny(State("Cast", cast), "Cast");
            var sd = State("Death", death);
            var td = sm.AddAnyStateTransition(sd);
            td.hasExitTime = false; td.duration = 0.1f; td.canTransitionToSelf = false;
            td.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            return ac;
        }

        /// <summary>
        /// If the knight is a Humanoid rig, gives his controller extra moves (roll, block, cast, hit) from any
        /// Humanoid clips in the imported packs. Generic rigs can't share clips, so it reports and skips.
        /// </summary>
        string UpgradeKnight()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(root + "/Animation/FV_Knight.controller");
            if (ctrl == null) return "Knight animations: FV_Knight.controller not found, skipped.";
            var knight = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Prefabs/Player (Knight).prefab");
            var anim = knight != null ? knight.GetComponentInChildren<Animator>(true) : null;
            if (anim == null || anim.avatar == null || !anim.avatar.isHuman)
                return "Knight animations: the knight's rig is Generic, so other packs' clips can't be shared with it. Its roll, block and cast use the built-in procedural motion.";
            var clips = new List<AnimationClip>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (p.StartsWith(root)) continue;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(p))
                    if (a is AnimationClip c && c.humanMotion && !c.name.StartsWith("__preview__")) clips.Add(c);
            }
            AnimationClip Pick(params string[] keys)
            {
                foreach (var k in keys) foreach (var c in clips) if (c.name.ToLowerInvariant().Contains(k)) return c;
                return null;
            }
            var sm = ctrl.layers[0].stateMachine;
            var loco = sm.defaultState;
            var added = new List<string>();
            void AddMove(string param, AnimationClip clip)
            {
                if (clip == null || sm.states.Any(s => s.state.name == "FV " + param)) return;
                if (!ctrl.parameters.Any(p => p.name == param)) ctrl.AddParameter(param, AnimatorControllerParameterType.Trigger);
                var st = sm.AddState("FV " + param); st.motion = clip;
                var t = sm.AddAnyStateTransition(st); t.hasExitTime = false; t.duration = 0.08f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0, param);
                if (loco != null) { var back = st.AddTransition(loco); back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.12f; }
                added.Add(param + " = " + clip.name);
            }
            AddMove("Dodge", Pick("roll", "dodge", "evade"));
            AddMove("Cast", Pick("cast", "spell", "magic"));
            AddMove("Heavy", Pick("heavy", "power", "attack02", "attack2"));
            AddMove("Hurt", Pick("gethit", "hurt", "hit", "damage"));
            EditorUtility.SetDirty(ctrl);
            return added.Count > 0 ? "Knight animations added: " + string.Join(", ", added) : "Knight animations: no matching Humanoid clips found.";
        }

        // ---------------- helpers ----------------

        /// <summary>
        /// Returns an empty Animator Controller at 'path'. An existing one is emptied and reused rather than deleted,
        /// so prefabs and scenes that point at it (the player's hero, companions, enemies) never lose their animator.
        /// </summary>
        static AnimatorController FreshController(string path)
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ac == null) return AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var p in ac.parameters) ac.RemoveParameter(p);
            while (ac.layers.Length > 1) ac.RemoveLayer(ac.layers.Length - 1);
            var sm = ac.layers[0].stateMachine;
            foreach (var t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);
            foreach (var t in sm.entryTransitions) sm.RemoveEntryTransition(t);
            foreach (var cs in sm.states) sm.RemoveState(cs.state);
            foreach (var m in sm.stateMachines) sm.RemoveStateMachine(m.stateMachine);
            // blend trees and other leftovers stored inside the controller file
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
                if (sub is BlendTree) Object.DestroyImmediate(sub, true);
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static Vector3 Size(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return Vector3.zero;
            // prefabs aren't in a scene, so use mesh bounds scaled by the renderer's transform
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in rends)
            {
                Bounds rb;
                if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) rb = smr.bounds;
                else { var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue; rb = TransformBounds(r.transform, mf.sharedMesh.bounds); }
                if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
            }
            return any ? b.size : Vector3.zero;
        }

        static Bounds TransformBounds(Transform t, Bounds local)
        {
            var c = t.TransformPoint(local.center);
            var e = local.extents;
            var ax = t.TransformVector(new Vector3(e.x, 0, 0)); var ay = t.TransformVector(new Vector3(0, e.y, 0)); var az = t.TransformVector(new Vector3(0, 0, e.z));
            var ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
            return new Bounds(c, ext * 2f);
        }

        static string Id(string s) => new string(s.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
        static string Name(Object o) => o != null ? o.name : "(none found)";

        static string Pretty(string s)
        {
            s = s.Replace("_", " ").Replace("-", " ");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\b(SM|PF|Prefab|LP|LOD\d*)\b", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
            return s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : "Item";
        }

        static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
