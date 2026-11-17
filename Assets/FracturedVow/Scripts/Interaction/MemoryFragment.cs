using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// End-of-floor memory fragment: shows a memory, grants a permanent bonus, refills resources and
    /// sets the checkpoint. This is the GDD's "survive, collect, remember" payoff in prototype form.
    /// </summary>
    public class MemoryFragment : MonoBehaviour, IInteractable
    {
        public string title = "Memory Fragment I";
        [TextArea(2, 5)]
        public string memoryText = "A cathedral of white stone. A blade laid across your palms. \"Guard the kingdom against the devouring dark.\" You were an anointed protector, bound by a holy oath.";
        public float maxHealthBonus = 20f;
        public bool grantsSpell = false;
        public SpellType spell = SpellType.StoneWall;

        Vector3 startPos;

        public string InteractionPrompt => "Recover memory";
        public bool CanInteract(PlayerInteractor interactor) => true;

        void Start() { startPos = transform.position; }

        void Update()
        {
            transform.position = startPos + Vector3.up * Mathf.Sin(Time.time * 1.5f) * 0.2f;
            transform.Rotate(new Vector3(15f, 60f, 0f) * Time.deltaTime, Space.World);
        }

        public void Interact(PlayerInteractor who)
        {
            string reward = "+" + Mathf.RoundToInt(maxHealthBonus) + " max health";
            if (who.Stats != null && who.GetComponent<PlayerProgress>() != null)
                who.Stats.ApplyGear(GearStat.MaxHealth, maxHealthBonus, "Memory: " + title, who.Health); // kept for the run
            else if (who.Health != null) who.Health.IncreaseMax(maxHealthBonus);
            if (who.Health != null) who.Health.Heal(9999f);
            if (who.Stats != null) who.Stats.RefillAll();
            if (grantsSpell && who.Magic != null) { who.Magic.LearnSpell(spell); reward += ", new spell"; }
            if (GameManager.Instance != null)
                GameManager.Instance.SetCheckpoint(who.transform.position, who.transform.rotation, false);

            PrototypeHUD.BigMessage(title, memoryText + "\n\n(" + reward + " - checkpoint saved)", 7f);
            GameAudio.PlayUI("memory_chime");
            GameFX.Burst(transform.position, new Color(0.8f, 0.55f, 1f), 50);
            var r = GetComponentInChildren<Renderer>();
            if (r != null) ImpactFlash.Spawn(transform.position, new Color(0.8f, 0.5f, 1f), r.sharedMaterial, 1.5f);
            Destroy(gameObject);
        }
    }
}
