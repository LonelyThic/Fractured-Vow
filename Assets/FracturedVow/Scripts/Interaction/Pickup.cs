using UnityEngine;

namespace FracturedVow
{
    public enum PickupType { Health, Mana, Stamina, Spell, Gear }

    /// <summary>
    /// Collectible item. Power-ups (health/mana/stamina) are grabbed by walking into them;
    /// spell tomes and gear need F so the player chooses to take them. Bobs and spins so it reads as loot.
    /// </summary>
    public class Pickup : MonoBehaviour, IInteractable
    {
        public PickupType type = PickupType.Health;
        public string displayName = "Health Potion";
        public float amount = 30f;
        public SpellType spell = SpellType.FrostShard;
        public GearStat gearStat = GearStat.Damage;
        public bool autoPickup = true;

        [Header("Motion")]
        public float bobHeight = 0.15f;
        public float spinSpeed = 90f;

        Vector3 startPos;
        float phase;

        public string InteractionPrompt => "Take " + displayName;
        public bool CanInteract(PlayerInteractor interactor) => true;
        public void Interact(PlayerInteractor interactor) => TryCollect(interactor, true);

        void Start()
        {
            startPos = transform.position;
            phase = Random.value * 10f;
        }

        void Update()
        {
            transform.position = startPos + Vector3.up * Mathf.Sin(Time.time * 2f + phase) * bobHeight;
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!autoPickup) return;
            var interactor = other.GetComponentInParent<PlayerInteractor>();
            if (interactor != null) TryCollect(interactor, false);
        }

        void TryCollect(PlayerInteractor who, bool manual)
        {
            bool used = false;
            switch (type)
            {
                case PickupType.Health:
                    // potions are flasks now: they go into the bag (R to drink)
                    Inventory.Add("hp_flask"); used = true;
                    break;
                case PickupType.Mana:
                    Inventory.Add("mp_flask"); used = true;
                    break;
                case PickupType.Stamina:
                    used = who.Stats != null && who.Stats.AddStamina(amount);
                    if (!used && manual) PrototypeHUD.Message("Stamina is already full");
                    break;
                case PickupType.Spell:
                    if (who.Magic != null) { who.Magic.LearnSpell(spell, Mathf.Max(1, Mathf.RoundToInt(amount))); used = true; }
                    break;
                case PickupType.Gear:
                    if (who.Stats != null)
                    {
                        who.Stats.ApplyGear(gearStat, amount, displayName, who.Health);
                        PrototypeHUD.Message("Equipped " + displayName + " (" + Describe() + ")", 3f);
                        used = true;
                    }
                    break;
            }

            if (!used) return;
            if (type == PickupType.Health || type == PickupType.Mana || type == PickupType.Stamina)
                PrototypeHUD.Message("+" + Mathf.RoundToInt(amount) + " " + type, 1.5f);

            if (type == PickupType.Gear) GameAudio.Play("pickup_gear", transform.position);
            else if (type != PickupType.Spell) GameAudio.Play("pickup", transform.position, 0.8f);
            var glow = GetComponentInChildren<Light>();
            GameFX.Burst(transform.position, glow != null ? glow.color : Color.white, 24);
            WorldLog.Remove(gameObject); // comes back if the knight dies before the next checkpoint
        }

        string Describe()
        {
            switch (gearStat)
            {
                case GearStat.Damage: return "+" + Mathf.RoundToInt(amount * 100f) + "% sword damage";
                case GearStat.Armor: return "+" + Mathf.RoundToInt(amount * 100f) + "% armor";
                case GearStat.MaxHealth: return "+" + Mathf.RoundToInt(amount) + " max health";
                default: return "+" + Mathf.RoundToInt(amount) + " max mana";
            }
        }
    }
}
