using UnityEngine;

namespace FracturedVow
{
    /// <summary>A sellsword waiting to be hired. Pay their price in gold and they join you (two at most).</summary>
    public class MercenaryPost : MonoBehaviour, IInteractable
    {
        public string mercName = "Bram the Sellsword";
        public int price = 150;
        public int model;

        PrimitiveRig rig;

        public string InteractionPrompt => "Hire " + mercName + " (" + price + " gold)";
        public bool CanInteract(PlayerInteractor who) => true;

        void Start()
        {
            // already in the party (hired earlier this run): they're with the knight, not waiting here
            foreach (var c in RunState.Companions) if (c.name == mercName) { Destroy(gameObject); return; }
            var lib = FVLibrary.Get;
            if (lib != null && lib.CompanionChar(model) != null && CharacterVisuals.NeedsRealModel(transform))
                CharacterVisuals.SpawnModel(lib.CompanionChar(model), transform, 1.8f);
            var m = transform.Find("Model");
            if (m != null) ModularOutfit.Apply(m.gameObject, "cloth_00", mercName.GetHashCode());
            NpcFacing.On(gameObject);
        }

        public void Interact(PlayerInteractor who)
        {
            if (RunState.Companions.Count >= RunState.MaxCompanions) { PrototypeHUD.Message("You can lead at most two companions. Dismiss one first (Tab).", 2.5f); return; }
            if (RunState.Gold < price) { PrototypeHUD.Message("Not enough gold: " + mercName + " wants " + price + ".", 2f); GameAudio.PlayUI("ui_click", 0.5f); return; }
            RunState.AddGold(-price);
            RunState.Companions.Add(new CompanionSave { name = mercName, model = model });
            CompanionAI.Spawn(RunState.Companions.Count - 1, transform.position, transform.rotation);
            PrototypeHUD.Message(mercName + " joins you!  Equip them from Tab > Companions.", 3f);
            GameAudio.PlayUI("spell_learn", 0.7f);
            WorldLog.Remove(gameObject); // a death before the next checkpoint undoes the hiring (and refunds it)
        }
    }
}
