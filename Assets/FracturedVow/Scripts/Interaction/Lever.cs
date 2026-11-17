using UnityEngine;

namespace FracturedVow
{
    /// <summary>Lever that unlocks and opens linked doors.</summary>
    public class Lever : MonoBehaviour, IInteractable
    {
        public Door[] targets;
        public Transform handle;
        public Vector3 pulledEuler = new Vector3(-40f, 0f, 0f);

        bool pulled;
        Quaternion pulledRot;

        public string InteractionPrompt => "Pull lever";
        public bool CanInteract(PlayerInteractor interactor) => !pulled;

        void Awake() { pulledRot = Quaternion.Euler(pulledEuler); }

        public void Interact(PlayerInteractor interactor)
        {
            pulled = true;
            GameAudio.Play("lever", transform.position);
            if (targets != null)
                foreach (var d in targets) if (d != null) d.Unlock(true);
            PrototypeHUD.Message("Somewhere, stone grinds against stone. The door opens.", 3f);
        }

        void Update()
        {
            if (pulled && handle != null)
                handle.localRotation = Quaternion.Slerp(handle.localRotation, pulledRot, 10f * Time.deltaTime);
        }
    }
}
