using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Makes the knight's gear visible when he is the Polytope hero: equipped armor switches his armor set
    /// (Traveler's Garb, Ranger's Mail, Knight's Plate) and an equipped weapon replaces the sword in his hand.
    /// The Toon RTS knight keeps his own look (his sword is part of his mesh).
    /// </summary>
    public static class PlayerLook
    {
        static bool HasVisibleChild(Transform hand)
        {
            foreach (Transform c in hand)
            {
                if (c.name == "Held Weapon" || !c.gameObject.activeSelf) continue; // only the prefab's own sword counts
                foreach (var r in c.GetComponentsInChildren<Renderer>(false))
                    if (r.enabled && r.bounds.size.magnitude > 0.2f) return true;
            }
            return false;
        }

        public static void Apply(GameObject player)
        {
            if (player == null) return;
            var anim = player.GetComponentInChildren<Animator>();
            if (anim == null || !ModularOutfit.IsModular(anim.gameObject)) return;
            var model = anim.gameObject;

            var armor = string.IsNullOrEmpty(RunState.EquippedArmor) ? null : FVLibrary.Item(RunState.EquippedArmor);
            ModularOutfit.Apply(model, armor != null && !string.IsNullOrEmpty(armor.outfit) ? armor.outfit : "05_C", 3);

            var hand = CharacterVisuals.FindHand(player.transform);
            if (hand == null) return;
            var weapon = string.IsNullOrEmpty(RunState.EquippedWeapon) ? null : FVLibrary.Item(RunState.EquippedWeapon);
            bool custom = weapon != null && weapon.model != null;
            foreach (Transform c in hand) if (c.name != "Held Weapon") c.gameObject.SetActive(!custom); // the model's own sword
            if (!custom && !HasVisibleChild(hand))
            {
                // no starting sword in the hand (or it can't be seen): give him the library's longsword
                var lib = FVLibrary.Get;
                ItemDef fallback = null;
                if (lib != null)
                    foreach (var it in lib.items)
                        if (it.kind == ItemKind.Weapon && it.model != null && (fallback == null || it.model.name.Contains("Longsword"))) fallback = it;
                CharacterVisuals.HoldWeapon(player.transform, fallback != null ? fallback.model : null, 1.1f);
                return;
            }
            CharacterVisuals.HoldWeapon(player.transform, custom ? weapon.model : null, 1.1f);
        }
    }
}
