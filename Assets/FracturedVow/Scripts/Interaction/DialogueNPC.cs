using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// A character the knight can talk to with F. Shows a dialogue box line by line (F to continue),
    /// turns to face the knight while he is near, ends the talk if he walks away, and after the first
    /// conversation switches to a short repeat line. Optionally heals the knight once.
    /// </summary>
    public class DialogueNPC : MonoBehaviour, IInteractable
    {
        public string npcName = "The Chained Prisoner";
        [TextArea(2, 4)] public string[] lines =
        {
            "Another one... no. You. I know that armor.",
            "They dragged you down here bleeding. Said you'd drawn steel on your own order.",
            "Why? I can't remember. Neither can you, I'd wager. This place eats memories.",
            "The Warden guards the way up. When he lifts that axe over his head, get clear of the red.",
            "Strike the husks while they wind up and they'll stagger. Raise your shield to the rest.",
            "Here. The last of my strength. Go find out who you were.",
        };
        [TextArea(2, 4)] public string[] repeatLines =
        {
            "Still here? North, through the guard room. The lever is on the west wall.",
        };
        [Tooltip("Health given once, at the end of the first conversation.")]
        public float giftHealth = 30f;
        public float talkRange = 4f;
        public float turnSpeed = 3f;

        string[] current;
        int index;
        bool talking, talkedOnce, gifted;
        Transform player;
        GUIStyle nameStyle, lineStyle, hintStyle;

        PrimitiveRig rig;
        float chainRattle;

        void Start()
        {
            GiveRealModel();
            // the code-built stand-in kneels; he lifts his head and gestures while he speaks
            rig = GetComponentInChildren<PrimitiveRig>();
            if (rig != null) rig.pose = PrimitiveRig.Pose.Kneel;
        }

        /// <summary>
        /// The prisoner was built as a pale ghost (or a code-built figure). He becomes a real man: the villager from
        /// the character pack in his own colours, idling in his chains. The ghost glow and sparkles go away.
        /// </summary>
        void GiveRealModel()
        {
            var lib = FVLibrary.Get;
            FVLibrary.Character who = null;
            if (lib != null)
                foreach (var c in lib.heroes)
                    if (c.prefab != null && !ModularOutfit.IsModular(c.prefab)) { who = c; break; } // the plain villager
            if (who == null && lib != null && lib.heroes.Count > 0) who = lib.heroes[0];
            if (who == null) return;
            foreach (Transform child in transform)
            {
                bool ghost = child.name == "Model" || child.name == "Ghost Light" || child.name == "Sparkle" ||
                             child.GetComponentInChildren<PrimitiveRig>(true) != null;
                if (ghost) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            }
            var m = CharacterVisuals.SpawnModel(who, transform, 1.75f);
            if (m != null)
            {
                ModularOutfit.Apply(m, "cloth_00", 11);
                var lamp = new GameObject("Cell Light").AddComponent<Light>(); // a little warm light so he can be seen
                lamp.transform.SetParent(transform, false);
                lamp.transform.localPosition = new Vector3(0f, 2.2f, 0.8f);
                lamp.type = LightType.Point; lamp.color = new Color(1f, 0.72f, 0.45f); lamp.range = 3.5f; lamp.intensity = 0.9f;
                lamp.shadows = LightShadows.None;
            }
        }

        public string InteractionPrompt => talking ? "Continue" : "Talk to " + npcName;
        public bool CanInteract(PlayerInteractor interactor) => true;

        public void Interact(PlayerInteractor who)
        {
            player = who.transform;
            if (!talking)
            {
                current = talkedOnce ? repeatLines : lines;
                index = 0;
                talking = current.Length > 0;
            }
            else
            {
                index++;
                if (index >= current.Length) { End(who); return; }
            }
            GameAudio.PlayUI("ui_click", 0.7f);
        }

        void End(PlayerInteractor who)
        {
            talking = false;
            if (!talkedOnce && !gifted && giftHealth > 0f && who != null && who.Health != null)
            {
                gifted = true;
                who.Health.Heal(giftHealth);
                PrototypeHUD.Message("+" + Mathf.RoundToInt(giftHealth) + " health", 2f);
                GameAudio.PlayUI("spell_learn", 0.6f);
                GameFX.Burst(transform.position + Vector3.up, new Color(0.6f, 0.8f, 1f), 30);
            }
            talkedOnce = true;
        }

        void Update()
        {
            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
                return;
            }
            Vector3 to = player.position - transform.position; to.y = 0f;
            if (talking && to.magnitude > talkRange) talking = false;
            bool near = to.magnitude < 7f && to.sqrMagnitude > 0.01f;
            if (near) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), turnSpeed * Time.deltaTime);
            if (rig != null)
            {
                rig.pose = talking ? PrimitiveRig.Pose.Talk : PrimitiveRig.Pose.Kneel;
                float yaw = near ? Mathf.Clamp(Vector3.SignedAngle(transform.forward, to, Vector3.up), -40f, 40f) : 0f;
                rig.headYaw = Mathf.Lerp(rig.headYaw, yaw, 4f * Time.deltaTime);
            }
            if (talking && Time.time > chainRattle)
            {
                chainRattle = Time.time + Random.Range(2.5f, 4.5f);
                GameAudio.Play("lever", transform.position, 0.25f, 0.2f, 1.4f);
            }
        }

        void OnGUI()
        {
            if (!talking || UIFocus.Any || current == null || index >= current.Length) return;
            float scale = Screen.height / 900f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = Screen.height / scale;
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
                nameStyle.normal.textColor = new Color(0.95f, 0.78f, 0.42f);
                lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
                lineStyle.normal.textColor = new Color(0.93f, 0.9f, 0.85f);
                hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.LowerRight };
                hintStyle.normal.textColor = new Color(0.62f, 0.6f, 0.58f);
            }
            var box = new Rect(w / 2f - 380f, h - 230f, 760f, 140f);
            var old = GUI.color;
            GUI.color = new Color(0.05f, 0.045f, 0.05f, 0.9f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(0.95f, 0.78f, 0.42f, 0.6f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2f), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(box.x + 20f, box.y + 12f, box.width - 40f, 28f), npcName, nameStyle);
            GUI.Label(new Rect(box.x + 20f, box.y + 44f, box.width - 40f, 70f), current[index], lineStyle);
            GUI.Label(new Rect(box.x + 20f, box.y + 104f, box.width - 40f, 28f),
                "[F] " + (index < current.Length - 1 ? "continue" : "end") + "   " + (index + 1) + "/" + current.Length, hintStyle);
        }
    }
}
