using UnityEngine;

namespace FracturedVow
{
    /// <summary>
    /// Third-person knight movement on a CharacterController: camera-relative walk/run, sprint with
    /// stamina, jump with coyote time and input buffering, gravity, knockback, crate pushing and
    /// wading through deep water. Drives the Animator through AnimatorBridge.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInputHandler))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        public Transform cameraTransform;

        [Header("Movement")]
        public float walkSpeed = 3.2f;
        public float runSpeed = 6.5f;
        public float acceleration = 14f;
        public float deceleration = 18f;
        public float rotationSpeed = 12f;
        [Range(0f, 1f)] public float airControl = 0.6f;

        [Header("Jump & Gravity")]
        public float jumpHeight = 1.4f;
        public float gravity = -22f;
        public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.15f;
        public float jumpStaminaCost = 10f;

        [Header("Sprint")]
        public float sprintStaminaPerSecond = 18f;
        [Tooltip("After running out, stamina must refill to this before sprinting again.")]
        public float exhaustedRecoverAt = 25f;

        [Header("Action slowdowns")]
        public float attackMoveMultiplier = 0.3f;
        public float blockMoveMultiplier = 0.45f;
        public float castMoveMultiplier = 0.4f;

        [Header("Pushing")]
        public float pushPower = 2.5f;

        [Header("Dodge roll")]
        public float dodgeDistance = 4.2f;
        public float dodgeTime = 0.5f;
        public float dodgeStaminaCost = 18f;
        [Tooltip("Invulnerable for this long at the start of a roll.")]
        public float dodgeIFrames = 0.3f;

        public bool IsGrounded { get; private set; }
        public bool IsDodging { get; private set; }
        /// <summary>The enemy the knight is locked on to (T / middle mouse), or null.</summary>
        public Transform LockTarget { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool ControlEnabled { get; private set; } = true;
        public float CurrentSpeed => currentSpeed;

        CharacterController cc;
        PlayerInputHandler input;
        PlayerStats stats;
        PlayerCombat combat;
        PlayerMagic magic;
        AnimatorBridge anim;
        Health health;

        Vector3 moveDir;
        float currentSpeed;
        float verticalVelocity;
        Vector3 impulse;
        float lastGroundedTime = -99f;
        float lastJumpPressTime = -99f;
        bool exhausted;
        bool wasInWater;
        float groundY;
        float waterWarnTime = -99f;
        bool wasGrounded = true;
        float fallSpeed;
        float stepDistance;
        float dodgeStart;
        Vector3 dodgeDir;
        bool dodgeBack;
        Health lockHealth;
        ProceduralMotion motionFx;
        PlayerFlask flask;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            input = GetComponent<PlayerInputHandler>();
            stats = GetComponent<PlayerStats>();
            combat = GetComponent<PlayerCombat>();
            magic = GetComponent<PlayerMagic>();
            anim = GetComponent<AnimatorBridge>();
            health = GetComponent<Health>();
            moveDir = transform.forward;
        }

        void OnEnable() { if (health != null) health.Damaged += OnDamaged; }
        void OnDisable() { if (health != null) health.Damaged -= OnDamaged; }

        void Start()
        {
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            groundY = transform.position.y;
        }

        void Update()
        {
            if (!ControlEnabled || !cc.enabled) return;
            float dt = Time.deltaTime;

            if (health != null && health.IsDead)
            {
                currentSpeed = 0f;
                ApplyVertical(dt);
                cc.Move(Vector3.up * verticalVelocity * dt);
                UpdateAnimator();
                return;
            }

            IsGrounded = cc.isGrounded;
            if (IsGrounded) groundY = transform.position.y;
            if (IsGrounded) lastGroundedTime = Time.time;
            if (input.JumpPressed) lastJumpPressTime = Time.time;
            if (motionFx == null) motionFx = GetComponent<ProceduralMotion>();
            if (flask == null) flask = GetComponent<PlayerFlask>();

            UpdateLockOn();
            if (IsDodging) { DodgeMove(dt); return; }
            if (input.DodgePressed && IsGrounded && CanDodge()) { StartDodge(); DodgeMove(dt); return; }

            // --- Direction relative to the camera ---
            Vector3 wish = CameraRelative(input.Move);
            float inputMag = Mathf.Clamp01(wish.magnitude);
            if (inputMag > 0.01f) wish /= wish.magnitude;

            // --- Sprint & stamina ---
            if (exhausted && stats != null && stats.Stamina >= exhaustedRecoverAt) exhausted = false;
            bool blocking = combat != null && combat.IsBlocking;
            bool wantsSprint = input.SprintHeld && inputMag > 0.1f && !blocking;
            IsSprinting = wantsSprint && !exhausted;
            if (IsSprinting && stats != null && !stats.TryUseStamina(sprintStaminaPerSecond * dt))
            {
                exhausted = true;
                IsSprinting = false;
                PrototypeHUD.Message("Out of stamina", 1.2f);
            }

            // --- Water ---
            float waterMul = WaterZone.SpeedMultiplierAt(transform.position);
            bool inWater = waterMul < 0.999f;
            wasInWater = inWater;

            // --- Speed ---
            float target = (IsSprinting ? runSpeed : walkSpeed) * inputMag * ActionMultiplier() * waterMul;
            float rate = target > currentSpeed ? acceleration : deceleration;
            if (!IsGrounded) rate *= airControl;
            currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * dt);

            if (inputMag > 0.1f)
                moveDir = IsGrounded ? wish : Vector3.Slerp(moveDir, wish, airControl * 10f * dt).normalized;

            // --- Facing: face the lock-on target, strafe while blocking/casting, otherwise face movement ---
            Vector3 face = Vector3.zero;
            if (LockTarget != null && !IsSprinting) { face = LockTarget.position - transform.position; face.y = 0f; }
            else if (blocking || (magic != null && magic.IsCasting)) face = CameraForwardFlat();
            else if (inputMag > 0.1f) face = wish;
            if (motionFx != null) motionFx.Lean = IsSprinting ? 7f : 0f;
            if (face.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(face), rotationSpeed * dt);

            // --- Jump (coyote time + buffer) ---
            bool canCoyote = Time.time - lastGroundedTime <= coyoteTime;
            bool buffered = Time.time - lastJumpPressTime <= jumpBufferTime;
            if (buffered && canCoyote && CanJump())
            {
                if (stats == null || stats.TryUseStamina(jumpStaminaCost))
                {
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    lastJumpPressTime = -99f;
                    lastGroundedTime = -99f;
                    if (anim != null) anim.Trigger("Jump");
                    GameAudio.Play("jump", transform.position + Vector3.up, 0.6f);
                }
                else
                {
                    lastJumpPressTime = -99f;
                    PrototypeHUD.Message("Too tired to jump", 1f);
                }
            }

            ApplyVertical(dt);

            Vector3 motion = moveDir * currentSpeed + impulse;
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, 14f * dt);
            motion.y = verticalVelocity;

            if (!IsGrounded) fallSpeed = Mathf.Min(fallSpeed, verticalVelocity);

            // only the boat crosses deep water: walking (or being knocked) into it is blocked at the shore
            Vector3 want = new Vector3(motion.x, 0f, motion.z) * dt;
            Vector3 step = WaterZone.ClampMove(transform.position, groundY, want, cc.radius);
            bool blockedX = step.x != want.x, blockedZ = step.z != want.z;
            if (blockedX) { motion.x = 0f; impulse.x = 0f; }
            if (blockedZ) { motion.z = 0f; impulse.z = 0f; }
            if ((blockedX || blockedZ) && Time.time - waterWarnTime > 3f)
            {
                waterWarnTime = Time.time;
                PrototypeHUD.Message("Too deep to wade. Take the boat.", 2f);
            }
            CollisionFlags flags = cc.Move(motion * dt);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            Footsteps(dt, inWater);
            UpdateAnimator();
        }

        void Footsteps(float dt, bool inWater)
        {
            bool grounded = cc.isGrounded;
            if (grounded && !wasGrounded && fallSpeed < -7f)
            {
                GameAudio.Play("land", transform.position, Mathf.Clamp01(-fallSpeed / 14f));
                GameFX.Dust(transform.position + Vector3.up * 0.1f, 0.5f, 10);
            }
            if (grounded) fallSpeed = 0f;
            wasGrounded = grounded;

            if (!grounded || currentSpeed < 0.6f) return;
            stepDistance += currentSpeed * dt;
            float stride = IsSprinting ? 1.7f : 1.15f;
            if (stepDistance >= stride)
            {
                stepDistance = 0f;
                GameAudio.Play("step_stone", transform.position, (IsSprinting ? 0.55f : 0.4f) * (inWater ? 0.7f : 1f), inWater ? 0.25f : 0.1f);
            }
        }

        void ApplyVertical(float dt)
        {
            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;
        }

        bool CanJump()
        {
            if (combat != null && (combat.IsBlocking || combat.IsAttacking)) return false;
            if (magic != null && magic.IsCasting) return false;
            return true;
        }

        float ActionMultiplier()
        {
            if (combat != null && combat.IsCharging) return 0.35f;
            if (flask != null && flask.Drinking) return 0.4f;
            if (combat != null && combat.IsAttacking) return attackMoveMultiplier;
            if (combat != null && combat.IsBlocking) return blockMoveMultiplier;
            if (magic != null && magic.IsCasting) return castMoveMultiplier;
            return 1f;
        }

        void UpdateAnimator()
        {
            if (anim == null) return;
            // 0 = idle, 0.5 = walk, 1 = run
            float s = currentSpeed <= walkSpeed
                ? currentSpeed / Mathf.Max(0.01f, walkSpeed) * 0.5f
                : 0.5f + (currentSpeed - walkSpeed) / Mathf.Max(0.01f, runSpeed - walkSpeed) * 0.5f;
            anim.SetFloat("Speed", s, 0.08f);
            anim.SetBool("Grounded", cc.enabled ? cc.isGrounded : true);
        }

        Vector3 CameraRelative(Vector2 move)
        {
            Vector3 f = CameraForwardFlat();
            Vector3 r = Vector3.Cross(Vector3.up, f);
            return f * move.y + r * move.x;
        }

        public Vector3 CameraForwardFlat()
        {
            Transform cam = cameraTransform != null ? cameraTransform : (Camera.main != null ? Camera.main.transform : null);
            Vector3 f = cam != null ? cam.forward : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.001f ? f.normalized : transform.forward;
        }

        void OnDamaged(DamageInfo info)
        {
            if (motionFx != null) motionFx.Flinch(info.sourcePosition);
            if (info.knockback <= 0f) return;
            Vector3 dir = transform.position - info.sourcePosition;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = -transform.forward;
            AddImpulse(dir.normalized * info.knockback);
        }

        public void AddImpulse(Vector3 velocity) { impulse += velocity; }

        // ---------------- dodge roll ----------------

        bool CanDodge()
        {
            if (combat != null && combat.IsAttacking) return false;
            if (magic != null && magic.IsCasting) return false;
            if (flask != null && flask.Drinking) return false;
            float cost = dodgeStaminaCost * (stats != null ? stats.dodgeCostMult : 1f);
            if (stats != null && !stats.TryUseStamina(cost)) { PrototypeHUD.Message("Too tired to dodge", 1f); return false; }
            return true;
        }

        void StartDodge()
        {
            // roll the way you're steering; with no direction held, roll forward (where the camera looks)
            Vector3 wish = CameraRelative(input.Move);
            dodgeBack = false;
            dodgeDir = wish.sqrMagnitude >= 0.04f ? wish.normalized : CameraForwardFlat();
            dodgeDir.y = 0f;
            if (dodgeDir.sqrMagnitude < 0.001f) dodgeDir = transform.forward;
            dodgeDir.Normalize();
            if (LockTarget == null) transform.rotation = Quaternion.LookRotation(dodgeDir);
            IsDodging = true;
            dodgeStart = Time.time;
            float time = dodgeBack ? dodgeTime * 0.6f : dodgeTime;
            if (health != null) health.SetInvulnerable(dodgeIFrames + (stats != null ? stats.iFrameBonus : 0f));
            // the code-driven roll only plays when the model has no roll animation of its own
            if (motionFx != null && (anim == null || !anim.Has("Dodge"))) motionFx.Roll(time, dodgeBack);
            if (anim != null)
            {
                // 0 forward, 1 back, 2 left, 3 right (relative to where the knight faces), for 4-way roll clips
                Vector3 local = transform.InverseTransformDirection(dodgeDir);
                int dir = dodgeBack ? 1 : Mathf.Abs(local.x) > Mathf.Abs(local.z) ? (local.x < 0f ? 2 : 3) : (local.z < 0f ? 1 : 0);
                anim.SetInt("DodgeDir", dir);
                anim.Trigger("Dodge");
                // the roll clip is longer than the dodge itself: play it faster so the knight is seen rolling while he
                // actually travels, instead of finishing the roll on the spot after the movement has stopped
                float len = RollClipLength();
                if (len > 0f && anim.animator != null) anim.animator.speed = Mathf.Clamp(len / (time * 1.1f), 1f, 2.6f);
            }
            GameAudio.Play("jump", transform.position + Vector3.up * 0.5f, 0.7f, 0.1f, 0.85f);
            GameFX.Dust(transform.position + Vector3.up * 0.1f, 0.5f, 8);
        }

        void DodgeMove(float dt)
        {
            float time = dodgeBack ? dodgeTime * 0.6f : dodgeTime;
            float t = (Time.time - dodgeStart) / time;
            float dist = dodgeBack ? dodgeDistance * 0.55f : dodgeDistance;
            float speed = dist / time * 1.6f * (1f - Mathf.Clamp01(t) * 0.75f);
            ApplyVertical(dt);
            Vector3 motion = dodgeDir * speed;
            Vector3 step = WaterZone.ClampMove(transform.position, groundY, motion * dt, cc.radius);
            if (step.x != motion.x * dt) motion.x = 0f;
            if (step.z != motion.z * dt) motion.z = 0f;
            motion.y = verticalVelocity;
            cc.Move(motion * dt);
            currentSpeed = 0f;
            impulse = Vector3.zero;
            if (t >= 1f)
            {
                IsDodging = false;
                if (anim != null && anim.animator != null) anim.animator.speed = 1f;
            }
            UpdateAnimator();
        }

        float rollLen = -1f;

        float RollClipLength()
        {
            if (rollLen >= 0f) return rollLen;
            rollLen = 0f;
            var ctrl = anim != null && anim.animator != null ? anim.animator.runtimeAnimatorController : null;
            if (ctrl == null) return 0f;
            foreach (var c in ctrl.animationClips)
                if (c != null && (c.name.ToLowerInvariant().Contains("roll") || c.name.ToLowerInvariant().Contains("dodge")))
                    rollLen = Mathf.Max(rollLen, c.length);
            return rollLen;
        }

        // ---------------- lock-on ----------------

        void UpdateLockOn()
        {
            if (input.LockOnPressed) { if (LockTarget != null) ClearLock(); else AcquireLock(); }
            if (LockTarget != null && (lockHealth == null || lockHealth.IsDead || !lockHealth.isActiveAndEnabled ||
                (LockTarget.position - transform.position).sqrMagnitude > 28f * 28f)) ClearLock();
        }

        void AcquireLock()
        {
            Vector3 camF = CameraForwardFlat();
            Health pick = null;
            float best = float.MaxValue;
            foreach (var h in Health.All)
            {
                if (h == null || h == health || h.IsDead) continue;
                if (h.GetComponent<EnemyAI>() == null && h.GetComponent<WardenBoss>() == null &&
                    h.GetComponent<LeviathanBoss>() == null && h.GetComponent<TrainingDummy>() == null) continue;
                Transform t = h.anchor != null ? h.anchor : h.transform;
                Vector3 to = t.position - transform.position; to.y = 0f;
                float d = to.magnitude, ang = Vector3.Angle(camF, to);
                if (d > 20f || ang > 70f || t.position.y < -1f) continue;
                float score = d + ang * 0.1f;
                if (score < best) { best = score; pick = h; }
            }
            if (pick == null) { PrototypeHUD.Message("No target in sight", 0.8f); return; }
            lockHealth = pick;
            LockTarget = pick.anchor != null ? pick.anchor : pick.transform;
            SetCameraLock(LockTarget);
            GameAudio.PlayUI("ui_hover", 0.8f);
        }

        void ClearLock()
        {
            LockTarget = null;
            lockHealth = null;
            SetCameraLock(null);
        }

        void SetCameraLock(Transform t)
        {
            var cam = cameraTransform != null ? cameraTransform : (Camera.main != null ? Camera.main.transform : null);
            var tpc = cam != null ? cam.GetComponent<ThirdPersonCamera>() : null;
            if (tpc != null) tpc.lockTarget = t;
        }

        /// <summary>Throws the knight: the horizontal part becomes knockback, the vertical part replaces his fall speed.</summary>
        public void Launch(Vector3 velocity)
        {
            impulse += new Vector3(velocity.x, 0f, velocity.z);
            verticalVelocity = velocity.y;
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic || hit.moveDirection.y < -0.3f) return;
            Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            rb.linearVelocity = push * pushPower;
        }

        /// <summary>Used by the boat: turns off walking and the CharacterController collider.</summary>
        public void SetControlEnabled(bool enabled)
        {
            ControlEnabled = enabled;
            cc.enabled = enabled;
            IsDodging = false;
            if (anim != null && anim.animator != null) anim.animator.speed = 1f;
            currentSpeed = 0f;
            verticalVelocity = 0f;
            impulse = Vector3.zero;
            if (anim != null) anim.SetFloat("Speed", 0f);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool was = cc.enabled;
            cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            cc.enabled = was;
            verticalVelocity = 0f;
            currentSpeed = 0f;
            impulse = Vector3.zero;
            moveDir = transform.forward;
        }
    }
}
