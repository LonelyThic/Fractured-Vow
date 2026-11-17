using UnityEngine;

namespace FracturedVow
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        bool CanInteract(PlayerInteractor interactor);
        void Interact(PlayerInteractor interactor);
    }

   [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerInteractor : MonoBehaviour
    {
        public float radius = 2.2f;
        public LayerMask mask = ~0;

        public PlayerInputHandler Input { get; private set; }
        public PlayerController Controller { get; private set; }
        public PlayerStats Stats { get; private set; }
        public Health Health { get; private set; }
        public PlayerMagic Magic { get; private set; }
        public PlayerCombat Combat { get; private set; }

        public IInteractable Current { get; private set; }
        public string CurrentPrompt => IsAlive(Current) ? "[F] " + Current.InteractionPrompt : null;

        readonly Collider[] buffer = new Collider[32];

        void Awake()
        {
            Input = GetComponent<PlayerInputHandler>();
            Controller = GetComponent<PlayerController>();
            Stats = GetComponent<PlayerStats>();
            Health = GetComponent<Health>();
            Magic = GetComponent<PlayerMagic>();
            Combat = GetComponent<PlayerCombat>();
        }

        void OnDisable() { Current = null; }

        void Update()
        {
            Current = FindBest();
            if (Current == null || !Input.InteractPressed) return;
            if (Health != null && Health.IsDead) return;
            if (Controller != null && !Controller.ControlEnabled) return;
            Current.Interact(this);
        }

        IInteractable FindBest()
        {
            Vector3 origin = transform.position + Vector3.up;
            int n = Physics.OverlapSphereNonAlloc(origin, radius, buffer, mask, QueryTriggerInteraction.Collide);
            IInteractable best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var it = buffer[i].GetComponentInParent<IInteractable>();
                if (!IsAlive(it) || !it.CanInteract(this)) continue;
                Vector3 to = buffer[i].bounds.center - origin;
                to.y = 0f;
                float angle = to.sqrMagnitude > 0.001f ? Vector3.Angle(transform.forward, to) : 0f;
                float score = to.magnitude + angle / 90f; // prefer close and in front
                if (score < bestScore) { bestScore = score; best = it; }
            }
            return best;
        }

        static bool IsAlive(IInteractable it) => it != null && (it as Object) != null;
    }
}
