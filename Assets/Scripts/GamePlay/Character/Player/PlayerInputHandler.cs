using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using DialogueSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public InputSystem_Actions inputActions;
    public bool CanMove;
    bool _bound;
    DialogueUIController _dialogueUI;
    CinemachineInputAxisController _cameraInput;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();
        CanMove = true;
    }

    void Start()
    {
        Bind();
        _dialogueUI = FindFirstObjectByType<DialogueUIController>();
        _cameraInput = FindFirstObjectByType<CinemachineInputAxisController>();
    }

    void Update()
    {
        var runner = DialogueRunner.Instance;
        if (runner == null || !runner.isDialoguing)
            return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        if (_dialogueUI == null)
            _dialogueUI = FindFirstObjectByType<DialogueUIController>();
        if (_dialogueUI == null || _dialogueUI.DialogueContext == null)
            return;

        var clickArea = _dialogueUI.DialogueContext.transform as RectTransform;
        if (clickArea == null)
            return;

        Canvas canvas = clickArea.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 pointerPosition = Mouse.current.position.ReadValue();
        if (RectTransformUtility.RectangleContainsScreenPoint(clickArea, pointerPosition, eventCamera))
            runner.Advance();
    }

    void OnEnable()
    {
        inputActions.Player.Zoom.performed += OnZoom;
        inputActions.Player.Interact.performed += OnFPress;
        Bind();
    }

    void OnDisable()
    {
        inputActions.Disable();
        inputActions.Player.Zoom.performed -= OnZoom;
        inputActions.Player.Interact.performed -= OnFPress;
        Unbind();
    }

    void Bind()
    {
        if (DialogueRunner.Instance == null || _bound)
            return;
        DialogueRunner.Instance.DialogueLockChanged += HandleDialogueLock;
        _bound = true;
    }

    void Unbind()
    {
        if (DialogueRunner.Instance != null && _bound)
            DialogueRunner.Instance.DialogueLockChanged -= HandleDialogueLock;
        _bound = false;
    }

    void OnZoom(InputAction.CallbackContext context)
    {
        if (DialogueRunner.Instance == null || DialogueRunner.Instance.isDialoguing)
            return;

        float value = context.ReadValue<float>();
        DialogueRunner.Instance.ChangeSelectID(value > 0 ? -1 : 1);
    }

    void OnFPress(InputAction.CallbackContext context)
    {
        if (DialogueRunner.Instance == null || DialogueRunner.Instance.isDialoguing)
            return;

        DialogueRunner.Instance.ShowDialogue();
    }

    void HandleDialogueLock(bool locked)
    {
        CanMove = !locked;

        if (_cameraInput == null)
            _cameraInput = FindFirstObjectByType<CinemachineInputAxisController>();
        if (_cameraInput != null)
            _cameraInput.enabled = !locked;
    }

    public void SetMoveEnable(bool b)
    {
        CanMove = b;
    }
}
