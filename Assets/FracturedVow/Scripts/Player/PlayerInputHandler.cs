using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FracturedVow
{
    /// <summary>
    /// Reads every player input in one place. Works with the new Input System (Unity 6 default)
    /// and falls back to the old Input Manager automatically. Keyboard/mouse and gamepad.
    ///
    /// Keyboard: WASD move, Shift sprint, Space jump, LMB attack, RMB block, E cast, Q / 1-3 swap spell,
    /// F interact, H help, R restart, Esc release mouse. Click to capture the mouse again.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Mouse")]
        [Tooltip("Degrees of camera turn per pixel of mouse movement, times 0.1.")]
        public float mouseSensitivity = 1.5f;
        public bool invertY = false;
        public bool lockCursorOnStart = true;

        [Header("Gamepad")]
        public float gamepadLookSpeed = 180f;

        [HideInInspector] public bool inputEnabled = true;

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }
        public float Zoom { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool BlockHeld { get; private set; }
        public bool InteractPressed { get; private set; }
        public bool CastPressed { get; private set; }
        public bool NextSpellPressed { get; private set; }
        public int SpellSlotPressed { get; private set; } = -1;
        public bool HelpPressed { get; private set; }
        public bool RestartPressed { get; private set; }
        public bool AttackHeld { get; private set; }
        public bool AttackReleased { get; private set; }
        public bool DodgePressed { get; private set; }
        public bool LockOnPressed { get; private set; }
        public bool FlaskPressed { get; private set; }
        public bool ManaFlaskPressed { get; private set; }

        public static bool CursorLocked => Cursor.lockState == CursorLockMode.Locked;

        public static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Start()
        {
            mouseSensitivity = GameSettings.MouseSensitivity;
            invertY = GameSettings.InvertY;
            if (lockCursorOnStart && !UIFocus.Any) SetCursorLocked(true);
        }

        void Update()
        {
            Clear();
            if (!inputEnabled || UIFocus.Any) return;

            bool escape, click;
#if ENABLE_INPUT_SYSTEM
            ReadInputSystem(out escape, out click);
#else
            ReadLegacy(out escape, out click);
#endif
            // Esc is handled by the pause menu (GameMenu). Clicking back into the game recaptures the mouse.
            if (click && !CursorLocked) SetCursorLocked(true);

            if (invertY) Look = new Vector2(Look.x, -Look.y);
            Move = Vector2.ClampMagnitude(Move, 1f);
        }

        void Clear()
        {
            Move = Vector2.zero; Look = Vector2.zero; Zoom = 0f;
            JumpPressed = SprintHeld = AttackPressed = BlockHeld = false;
            InteractPressed = CastPressed = NextSpellPressed = HelpPressed = RestartPressed = false;
            AttackHeld = AttackReleased = DodgePressed = LockOnPressed = FlaskPressed = ManaFlaskPressed = false;
            SpellSlotPressed = -1;
        }

#if ENABLE_INPUT_SYSTEM
        void ReadInputSystem(out bool escape, out bool click)
        {
            escape = false; click = false;
            Vector2 move = Vector2.zero, look = Vector2.zero;
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            if (kb != null)
            {
                // keyboard actions come from the player's key bindings (Esc > Controls)
                move.x = (InputBindings.Held(GameAction.MoveRight) || kb.rightArrowKey.isPressed ? 1f : 0f) - (InputBindings.Held(GameAction.MoveLeft) || kb.leftArrowKey.isPressed ? 1f : 0f);
                move.y = (InputBindings.Held(GameAction.MoveForward) || kb.upArrowKey.isPressed ? 1f : 0f) - (InputBindings.Held(GameAction.MoveBack) || kb.downArrowKey.isPressed ? 1f : 0f);
                JumpPressed = InputBindings.Pressed(GameAction.Jump);
                SprintHeld = InputBindings.Held(GameAction.Sprint);
                InteractPressed = InputBindings.Pressed(GameAction.Interact);
                CastPressed = InputBindings.Pressed(GameAction.Cast);
                NextSpellPressed = InputBindings.Pressed(GameAction.NextSpell);
                HelpPressed = kb.hKey.wasPressedThisFrame;
                FlaskPressed = InputBindings.Pressed(GameAction.HealthFlask);
                ManaFlaskPressed = InputBindings.Pressed(GameAction.ManaFlask);
                DodgePressed = InputBindings.Pressed(GameAction.Dodge);
                LockOnPressed = InputBindings.Pressed(GameAction.LockOn);
                if (InputBindings.Pressed(GameAction.Spell1)) SpellSlotPressed = 0;
                if (InputBindings.Pressed(GameAction.Spell2)) SpellSlotPressed = 1;
                if (InputBindings.Pressed(GameAction.Spell3)) SpellSlotPressed = 2;
                escape = kb.escapeKey.wasPressedThisFrame;
            }

            if (mouse != null)
            {
                if (CursorLocked)
                {
                    look += mouse.delta.ReadValue() * 0.1f * mouseSensitivity;
                    AttackPressed = mouse.leftButton.wasPressedThisFrame;
                    AttackHeld = mouse.leftButton.isPressed;
                    AttackReleased = mouse.leftButton.wasReleasedThisFrame;
                    LockOnPressed |= mouse.middleButton.wasPressedThisFrame;
                    BlockHeld = mouse.rightButton.isPressed;
                    float s = mouse.scroll.ReadValue().y;
                    Zoom = Mathf.Abs(s) > 0.01f ? Mathf.Sign(s) : 0f;
                }
                else click = mouse.leftButton.wasPressedThisFrame;
            }

            if (pad != null)
            {
                move += pad.leftStick.ReadValue();
                look += pad.rightStick.ReadValue() * gamepadLookSpeed * Time.deltaTime;
                JumpPressed |= pad.buttonSouth.wasPressedThisFrame;
                AttackPressed |= pad.buttonWest.wasPressedThisFrame;
                AttackHeld |= pad.buttonWest.isPressed;
                AttackReleased |= pad.buttonWest.wasReleasedThisFrame;
                DodgePressed |= pad.buttonEast.wasPressedThisFrame;
                LockOnPressed |= pad.rightStickButton.wasPressedThisFrame;
                FlaskPressed |= pad.dpad.up.wasPressedThisFrame;
                ManaFlaskPressed |= pad.dpad.down.wasPressedThisFrame;
                BlockHeld |= pad.leftTrigger.isPressed;
                SprintHeld |= pad.leftStickButton.isPressed;
                InteractPressed |= pad.buttonNorth.wasPressedThisFrame;
                CastPressed |= pad.rightTrigger.wasPressedThisFrame;
                NextSpellPressed |= pad.rightShoulder.wasPressedThisFrame;
                HelpPressed |= pad.selectButton.wasPressedThisFrame;
            }

            Move = move;
            Look = look;
        }
#else
        void ReadLegacy(out bool escape, out bool click)
        {
            Move = new Vector2((InputBindings.Held(GameAction.MoveRight) ? 1f : 0f) - (InputBindings.Held(GameAction.MoveLeft) ? 1f : 0f),
                               (InputBindings.Held(GameAction.MoveForward) ? 1f : 0f) - (InputBindings.Held(GameAction.MoveBack) ? 1f : 0f));
            JumpPressed = InputBindings.Pressed(GameAction.Jump);
            SprintHeld = InputBindings.Held(GameAction.Sprint);
            InteractPressed = InputBindings.Pressed(GameAction.Interact);
            CastPressed = InputBindings.Pressed(GameAction.Cast);
            NextSpellPressed = InputBindings.Pressed(GameAction.NextSpell);
            HelpPressed = Input.GetKeyDown(KeyCode.H);
            FlaskPressed = InputBindings.Pressed(GameAction.HealthFlask);
            ManaFlaskPressed = InputBindings.Pressed(GameAction.ManaFlask);
            DodgePressed = InputBindings.Pressed(GameAction.Dodge);
            LockOnPressed = InputBindings.Pressed(GameAction.LockOn) || Input.GetMouseButtonDown(2);
            if (InputBindings.Pressed(GameAction.Spell1)) SpellSlotPressed = 0;
            if (InputBindings.Pressed(GameAction.Spell2)) SpellSlotPressed = 1;
            if (InputBindings.Pressed(GameAction.Spell3)) SpellSlotPressed = 2;
            escape = Input.GetKeyDown(KeyCode.Escape);
            click = false;

            if (CursorLocked)
            {
                Look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * mouseSensitivity;
                AttackPressed = Input.GetMouseButtonDown(0);
                AttackHeld = Input.GetMouseButton(0);
                AttackReleased = Input.GetMouseButtonUp(0);
                BlockHeld = Input.GetMouseButton(1);
                float s = Input.mouseScrollDelta.y;
                Zoom = Mathf.Abs(s) > 0.01f ? Mathf.Sign(s) : 0f;
            }
            else click = Input.GetMouseButtonDown(0);
        }
#endif
    }
}
