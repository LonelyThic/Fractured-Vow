using System.Collections;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Drinking flasks from the bag: Health Flask (R) and Mana Flask (V). Flasks are items: you find them,
    /// loot them from enemies and chests, or buy them from the merchant.
    /// </summary>
    public class PlayerFlask : MonoBehaviour
    {
        public float healthAmount = 45f;
        public float manaAmount = 50f;
        public float drinkTime = 0.9f;

        public bool Drinking { get; private set; }
        public int HealthFlasks => Inventory.Count("hp_flask");

        static PlayerFlask current;
        void OnEnable() => current = this;

        /// <summary>How much one Health Flask heals right now (skills like Potent Flask included).</summary>
        public static int HealthRestore
        {
            get
            {
                if (current == null) return 45;
                var st = current.GetComponent<PlayerStats>();
                return Mathf.RoundToInt(current.healthAmount * (st != null ? st.flaskHealMult : 1f));
            }
        }
        /// <summary>How much one Mana Flask restores.</summary>
        public static int ManaRestore => current != null ? Mathf.RoundToInt(current.manaAmount) : 50;
        public int ManaFlasks => Inventory.Count("mp_flask");

        PlayerStats stats;
        Health health;
        PlayerInputHandler input;
        ProceduralMotion motion;

        void Awake()
        {
            stats = GetComponent<PlayerStats>();
            health = GetComponent<Health>();
            input = GetComponent<PlayerInputHandler>();
        }

        /// <summary>Kept for older callers; flasks no longer refill on their own.</summary>
        public void Refill() { }

        void Update()
        {
            if (input == null || Drinking || health == null || health.IsDead) return;
            if (input.FlaskPressed) Drink(false);
            else if (input.ManaFlaskPressed) Drink(true);
        }

        public void Drink(bool mana)
        {
            if (Drinking || health == null || health.IsDead) return;
            var pc = GetComponent<PlayerController>();
            if (pc != null && (!pc.ControlEnabled || pc.IsDodging)) return;
            string id = mana ? "mp_flask" : "hp_flask";
            if (Inventory.Count(id) <= 0) { PrototypeHUD.Message(mana ? "No mana flasks" : "No health flasks", 1.2f); return; }
            Inventory.Remove(id);
            StartCoroutine(DrinkRoutine(mana));
        }

        IEnumerator DrinkRoutine(bool mana)
        {
            Drinking = true;
            if (motion == null) motion = GetComponent<ProceduralMotion>();
            if (motion != null) motion.Drink = 1f;
            GameAudio.Play("pickup", transform.position + Vector3.up, 0.6f);
            yield return new WaitForSeconds(drinkTime * 0.5f);
            if (!health.IsDead)
            {
                float amount = mana ? manaAmount : healthAmount * (stats != null ? stats.flaskHealMult : 1f);
                if (mana) { if (stats != null) stats.AddMana(amount); }
                else health.Heal(amount);
                PrototypeHUD.FloatText(transform.position + Vector3.up * 2.2f, "+" + Mathf.RoundToInt(amount) + (mana ? " mana" : " health"),
                    mana ? new Color(0.45f, 0.65f, 1f) : new Color(0.5f, 1f, 0.55f));
                GameFX.Burst(transform.position + Vector3.up, mana ? new Color(0.3f, 0.5f, 1f) : new Color(1f, 0.45f, 0.3f), 24);
                GameAudio.PlayUI("spell_learn", 0.4f);
            }
            yield return new WaitForSeconds(drinkTime * 0.5f);
            if (motion != null) motion.Drink = 0f;
            Drinking = false;
        }
    }
}
