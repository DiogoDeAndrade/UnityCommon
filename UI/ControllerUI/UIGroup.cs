using UnityEngine;
using UnityEngine.InputSystem;

namespace UC
{

    public class UIGroup : MonoBehaviour
    {
        [SerializeField]
        protected int playerId;
        [SerializeField]
        protected bool enableOnStart = true;
        [SerializeField]
        protected float moveCooldown = 0.1f;
        [SerializeField]
        protected PlayerInput playerInput;
        [SerializeField, InputPlayer(nameof(playerInput))]
        protected InputControl horizontalControl;
        [SerializeField, InputPlayer(nameof(playerInput))]
        protected InputControl verticalControl;
        [SerializeField, InputPlayer(nameof(playerInput)), InputButton]
        protected InputControl interactControl;
        [SerializeField] AudioClip moveSnd;
        [SerializeField] AudioClip selectSnd;

        [SerializeField] protected BaseUIControl initialControl;

        protected float cooldownTimer;
        protected BaseUIControl _selectedControl;
        protected bool _verticalReset = true;
        protected bool _horizontalReset = true;
        protected bool _uiEnable = true;

        public BaseUIControl selectedControl
        {
            get { return _selectedControl; }
            set
            {
                var prevControl = _selectedControl;
                if (_selectedControl != null) _selectedControl.NotifyDeselect();
                _selectedControl = value;
                if (_selectedControl != null) _selectedControl.NotifySelect(prevControl);
            }
        }

        public bool uiEnable => _uiEnable;

        protected virtual void Start()
        {
            if (playerId != -1)
            {
                MasterInputManager.SetupInput(playerId, playerInput);
            }

            horizontalControl.playerInput = playerInput;
            verticalControl.playerInput = playerInput;
            interactControl.playerInput = playerInput;

            if (initialControl)
            {
                selectedControl = initialControl;
            }

            _uiEnable = enableOnStart;
        }

        void Update()
        {
            if (!_uiEnable) return;

            if (_selectedControl)
            {
                if (cooldownTimer > 0) cooldownTimer -= Time.deltaTime;

                if (cooldownTimer <= 0.0f)
                {
                    float dy = verticalControl.GetAxis();
                    float dx = horizontalControl.GetAxis();
                    if ((Mathf.Abs(dx) > 0.1f) &&
                        (Mathf.Abs(dy) > 0.1f))
                    {
                        // Diagonal, not allowed
                    }
                    else
                    {
                        if (_verticalReset)
                        {
                            if (dy < -0.5f)
                            {
                                var next = _selectedControl.navDown;
                                _selectedControl = (next) ? (next) : (_selectedControl);
                                cooldownTimer = moveCooldown;
                                _verticalReset = false;
                                if (moveSnd) SoundManager.PlaySound(SoundType.SecondaryFX, moveSnd);
                            }
                            else if (dy > 0.5f)
                            {
                                var next = _selectedControl.navUp;
                                _selectedControl = (next) ? (next) : (_selectedControl);
                                cooldownTimer = moveCooldown;
                                _verticalReset = false;
                                if (moveSnd) SoundManager.PlaySound(SoundType.SecondaryFX, moveSnd);
                            }
                        }
                        else
                        {
                            if (Mathf.Abs(dy) < 0.1f) _verticalReset = true;
                        }

                        if (Mathf.Abs(dx) < 0.1f) _horizontalReset = true;
                        else
                        {
                            _selectedControl?.MoveHorizontal(dx, _horizontalReset);
                            _horizontalReset = false;
                        }
                    }

                    if (interactControl.IsDown())
                    {
                        if (selectSnd) SoundManager.PlaySound(SoundType.SecondaryFX, selectSnd);
                        _selectedControl?.Interact();
                        OnSelect();
                    }
                }
            }
        }

        internal void SetControl(BaseUIControl control)
        {
            _selectedControl = control;
            cooldownTimer = moveCooldown;
            _verticalReset = false;
            if (moveSnd) SoundManager.PlaySound(SoundType.SecondaryFX, moveSnd);
        }

        public void SetUI(bool value)
        {
            _uiEnable = value;
            var uiControls = GetComponentsInChildren<BaseUIControl>();
            foreach (var uiControl in uiControls)
            {
                if (value) uiControl.NotifyEnable();
                else uiControl.NotifyDisable();
            }

            if (value)
            {
                selectedControl = initialControl;
                cooldownTimer = moveCooldown;
            }
        }

        protected virtual void OnSelect()
        {

        }

    }
}