using System.Collections.Generic;
using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Safe wrapper around an Animator. It only sets parameters that actually exist on the
    /// controller, so gameplay code never throws warnings when a model or clip is missing.
    /// Expected parameters: Speed (float 0-1), Grounded, Blocking, Dead (bool), AttackIndex (int),
    /// Jump, Attack, Cast, Hurt (triggers).
    /// </summary>
    public class AnimatorBridge : MonoBehaviour
    {
        public Animator animator;

        readonly Dictionary<int, AnimatorControllerParameterType> known = new Dictionary<int, AnimatorControllerParameterType>();
        RuntimeAnimatorController cachedFor;

        public bool HasController => animator != null && animator.runtimeAnimatorController != null;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            Refresh();
        }

        public void Refresh()
        {
            known.Clear();
            cachedFor = animator != null ? animator.runtimeAnimatorController : null;
            if (!HasController) return;
            foreach (var p in animator.parameters) known[p.nameHash] = p.type;
        }

        /// <summary>True if the controller has a parameter with this name (e.g. a "Dodge" roll clip).</summary>
        public bool Has(string name) => Ready(Animator.StringToHash(name));

        bool Ready(int hash)
        {
            if (!HasController || !animator.isActiveAndEnabled) return false;
            if (cachedFor != animator.runtimeAnimatorController || known.Count == 0) Refresh();
            return known.ContainsKey(hash);
        }

        public void SetFloat(string name, float value, float dampTime = 0f)
        {
            int h = Animator.StringToHash(name);
            if (!Ready(h)) return;
            if (dampTime > 0f) animator.SetFloat(h, value, dampTime, Time.deltaTime);
            else animator.SetFloat(h, value);
        }

        public void SetBool(string name, bool value)
        {
            int h = Animator.StringToHash(name);
            if (!Ready(h)) return;
            // Controllers made by hand sometimes use a trigger where ours use a bool (e.g. "Dead")
            if (known[h] == AnimatorControllerParameterType.Trigger)
            {
                if (value) animator.SetTrigger(h); else animator.ResetTrigger(h);
            }
            else if (known[h] == AnimatorControllerParameterType.Bool) animator.SetBool(h, value);
        }

        public void SetInt(string name, int value)
        {
            int h = Animator.StringToHash(name);
            if (Ready(h)) animator.SetInteger(h, value);
        }

        public void Trigger(string name)
        {
            int h = Animator.StringToHash(name);
            if (!Ready(h)) return;
            if (known[h] == AnimatorControllerParameterType.Trigger) animator.SetTrigger(h);
            else if (known[h] == AnimatorControllerParameterType.Bool) animator.SetBool(h, true);
        }

        /// <summary>Puts the Animator back in its default state (used when a character respawns).</summary>
        public void ResetAnimator()
        {
            if (!HasController || !animator.isActiveAndEnabled) return;
            animator.speed = 1f;
            animator.Rebind();
            animator.Update(0f);
        }
    }
}
