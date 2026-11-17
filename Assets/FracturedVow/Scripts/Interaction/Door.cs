using UnityEngine;

namespace FracturedVow
{
    /// <summary>Sliding door. A locked door says what is needed; an unlocked door opens/closes with F.</summary>
    public class Door : MonoBehaviour, IInteractable
    {
        public bool locked = true;
        public string lockedMessage = "Sealed shut. There must be a lever nearby.";
        public Vector3 openOffset = new Vector3(0f, 3.2f, 0f);
        public float speed = 3f;

        Vector3 closedPos;
        bool open;

        public string InteractionPrompt => locked ? "Sealed door" : open ? "Close door" : "Open door";
        public bool CanInteract(PlayerInteractor interactor) => true;

        void Awake() { closedPos = transform.position; }

        public void Interact(PlayerInteractor interactor)
        {
            if (locked) { PrototypeHUD.Message(lockedMessage, 2.5f); GameAudio.Play("lever", transform.position, 0.4f); return; }
            open = !open;
            GameAudio.Play("gate_grind", transform.position, 0.8f);
        }

        public void Unlock(bool openNow)
        {
            locked = false;
            if (openNow && !open) { open = true; GameAudio.Play("gate_grind", transform.position, 0.9f); }
        }

        void Update()
        {
            Vector3 target = open ? closedPos + openOffset : closedPos;
            transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        }
    }
}
