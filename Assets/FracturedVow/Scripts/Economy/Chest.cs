using System.Collections;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>A treasure chest: F to open. The lid swings up and gold and items spill out. Tier 3 is the boss's big chest.</summary>
    public class Chest : MonoBehaviour, IInteractable
    {
        [Range(1, 3)] public int tier = 1;
        public Transform lid;
        bool opened;
        Quaternion lidClosed;
        bool lidKnown;
        public bool Opened => opened;

        public string InteractionPrompt => tier >= 3 ? "Open the great chest" : "Open chest";
        public bool CanInteract(PlayerInteractor who) => !opened;

        void Awake()
        {
            if (lid != null) return;
            foreach (var t in GetComponentsInChildren<Transform>())
            {
                string n = t.name.ToLowerInvariant();
                if (t != transform && (n.Contains("lid") || n.Contains("top") || n.Contains("cover") || n.Contains("cap"))) { lid = t; break; }
            }
        }

        public void Interact(PlayerInteractor who)
        {
            if (opened) return;
            opened = true;
            WorldLog.Changed(this);
            if (lid != null && !lidKnown) { lidClosed = lid.localRotation; lidKnown = true; }
            StartCoroutine(Open());
        }

        /// <summary>The knight died before the next checkpoint: the chest is closed and full again.</summary>
        public void ResetChest()
        {
            StopAllCoroutines();
            opened = false;
            if (lid != null && lidKnown) lid.localRotation = lidClosed;
        }

        IEnumerator Open()
        {
            GameAudio.Play("lever", transform.position, 0.8f, 0.05f, 0.8f);
            if (lid != null)
            {
                Quaternion from = lid.localRotation, to = from * Quaternion.Euler(-105f, 0f, 0f);
                for (float t = 0f; t < 0.6f; t += Time.deltaTime) { lid.localRotation = Quaternion.Slerp(from, to, t / 0.6f); yield return null; }
            }
            else yield return new WaitForSeconds(0.2f);
            GameAudio.PlayUI(tier >= 3 ? "victory_sting" : "pickup_gear", tier >= 3 ? 0.7f : 0.8f);
            GameFX.Burst(transform.position + Vector3.up, new Color(1f, 0.85f, 0.4f), tier >= 3 ? 80 : 40);
            Loot.ChestDrop(transform.position + transform.forward * 1.2f, tier);
            if (tier >= 3) PrototypeHUD.Message("The Warden's hoard!", 2.5f);
        }
    }
}
