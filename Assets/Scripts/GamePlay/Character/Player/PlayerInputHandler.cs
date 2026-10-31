using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using DialogueSystem;
using Game.UI;
using UnityEngine.EventSystems;
using TMPro;

public class PlayerInputHandler : MonoBehaviour
{
    //新输入系统
    public InputSystem_Actions inputActions;
    public bool CanMove => isActiveAndEnabled && movementBlocks.Count == 0;
    //三个集合，记录禁用移动/相机/鼠标的来源，当集合为空才可以解除
    //HashSet是一个不保存重复项的Set集合
    private readonly HashSet<object> movementBlocks = new();
    private readonly HashSet<object> cameraBlocks = new();
    private readonly HashSet<object> cursorRequests = new();
    //三种来源标识
    private readonly object uiSource = new();
    private readonly object dialogueSource = new();
    private readonly object legacySource = new();
    //
    private DialogueRunner boundDialogue;
    private UIManager boundUI;
    //相机状态
    private bool cameraLocked;
    private bool previousCameraEnabled;
    //鼠标
    private bool cursorOverridden;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    CinemachineInputAxisController _cameraInput;

    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void Start()
    {
        Bind();
        _cameraInput = FindFirstObjectByType<CinemachineInputAxisController>();
    }

    void Update()
    {
        Bind();
        RefreshControls();
        InventoryUIInput();
    }

    /// <summary>
    /// 处理输入
    /// </summary>
    private void InventoryUIInput()
    {

        Keyboard keyboard = Keyboard.current;
        UIManager manager = UIManager.Instance;

        if (keyboard == null || manager == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
        {
            // Global UIManager handles Back, including scenes without a player.
            return;
        }

        if (!keyboard.bKey.wasPressedThisFrame)
            return;

        if (IsEditingText())
            return;

        if (manager.IsOpen(UIId.Backpack))
        {
            manager.Close(UIId.Backpack);
            return;
        }

        if (DialogueRunner.Instance != null &&
            DialogueRunner.Instance.isDialoguing)
            return;

        manager.Open(UIId.Backpack);
    }

    private bool IsEditingText()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        GameObject selectedObject = eventSystem.currentSelectedGameObject;
        if (selectedObject == null)
            return false;

        TMP_InputField inputField =
            selectedObject.GetComponentInParent<TMP_InputField>();

        if (inputField != null && inputField.isFocused)
            return true;

        UnityEngine.UI.InputField legacyInput = selectedObject.GetComponentInParent<UnityEngine.UI.InputField>();
        return legacyInput != null && legacyInput.isFocused;
    }

    void OnEnable()
    {
        inputActions.Player.Zoom.performed += OnZoom;
        inputActions.Player.Interact.performed += OnFPress;
        inputActions.Enable();
        Bind();
    }

    void OnDisable()
    {
        inputActions.Disable();
        inputActions.Player.Zoom.performed -= OnZoom;
        inputActions.Player.Interact.performed -= OnFPress;
        Unbind();
    }

    /// <summary>
    /// 尝试连接 UI 和对话事件，并同步它们当前的状态。
    /// </summary>
    void Bind()
    {
        if (!ReferenceEquals(boundDialogue, DialogueRunner.Instance))
        {
            if (boundDialogue != null)
                boundDialogue.DialogueLockChanged -= HandleDialogueLock;
            boundDialogue = DialogueRunner.Instance;
            if (boundDialogue != null)
                boundDialogue.DialogueLockChanged += HandleDialogueLock;
            HandleDialogueLock(boundDialogue != null && boundDialogue.isDialoguing);
        }

        if (!ReferenceEquals(boundUI, UIManager.Instance))
        {
            if (boundUI != null)
                boundUI.Input.StateChanged -= HandleUIInput;
            boundUI = UIManager.Instance;
            if (boundUI != null)
                boundUI.Input.StateChanged += HandleUIInput;
            HandleUIInput(boundUI != null ? boundUI.Input.Current : default);
        }
    }

    void Unbind()
    {
        if (boundDialogue != null)
            boundDialogue.DialogueLockChanged -= HandleDialogueLock;
        if (boundUI != null)
            boundUI.Input.StateChanged -= HandleUIInput;
        boundDialogue = null;
        boundUI = null;
        movementBlocks.Remove(uiSource);
        movementBlocks.Remove(dialogueSource);
        cameraBlocks.Remove(uiSource);
        cameraBlocks.Remove(dialogueSource);
        cursorRequests.Remove(uiSource);
        cursorRequests.Remove(dialogueSource);
        RestoreCamera();
        RestoreCursor();
    }

    void OnDestroy()
    {
        inputActions?.Dispose();
    }

    /// <summary>
    /// 处理来自UI锁定
    /// </summary>
    /// <param name="state"></param>
    void HandleUIInput(UIInputState state)
    {
        // UIManager 统一管理界面光标，玩家输入只合并移动和相机限制。
        SetInputRestriction(uiSource, state.BlockMovement, state.BlockCamera, false);
    }

    void OnZoom(InputAction.CallbackContext context)
    {
        if (!CanMove || IsEditingText() || DialogueRunner.Instance == null || DialogueRunner.Instance.isDialoguing)
            return;

        float value = context.ReadValue<float>();
        DialogueRunner.Instance.ChangeSelectID(value > 0 ? -1 : 1);
    }

    void OnFPress(InputAction.CallbackContext context)
    {
        if (!CanMove || IsEditingText() || DialogueRunner.Instance == null || DialogueRunner.Instance.isDialoguing)
            return;

        DialogueRunner.Instance.ShowDialogue();
    }

    /// <summary>
    /// 处理来自对话的变化
    /// </summary>
    /// <param name="locked"></param>
    void HandleDialogueLock(bool locked)
    {   
        SetInputRestriction(dialogueSource, locked, locked, false);
    }

    //旧接口
    public void SetMoveEnable(bool enabled)
    {
        SetInputRestriction(legacySource, !enabled, false, false);
    }

    /// <summary>
    /// 增加/撤销来源限制
    /// </summary>
    /// <param name="source">来源</param>
    /// <param name="blockMovement">是否锁移动</param>
    /// <param name="blockCamera">是否锁相机</param>
    /// <param name="showCursor">是否锁鼠标</param>
    public void SetInputRestriction(object source, bool blockMovement, bool blockCamera, bool showCursor)
    {
        if (source == null)
            throw new System.ArgumentNullException(nameof(source));

        //更新三个集合
        SetRequest(movementBlocks, source, blockMovement);
        SetRequest(cameraBlocks, source, blockCamera);
        SetRequest(cursorRequests, source, showCursor);

        //更新状态
        if (isActiveAndEnabled)
            RefreshControls();
    }

    /// <summary>
    /// 清除某个来源的限制
    /// </summary>
    /// <param name="source"></param>
    public void ClearInputRestriction(object source)
    {
        SetInputRestriction(source, false, false, false);
    }

    /// <summary>
    /// 处理请求
    /// </summary>
    /// <param name="requests"></param>
    /// <param name="source"></param>
    /// <param name="requested"></param>
    private static void SetRequest(HashSet<object> requests, object source, bool requested)
    {
        //有禁用要求就添加
        if (requested)
            requests.Add(source);
        //没有要求就移除，若不存在，Remove也不会报错
        else
            requests.Remove(source);
    }

    /// <summary>
    /// 更新摄像机，鼠标状态
    /// </summary>
    private void RefreshControls()
    {
        if (_cameraInput == null)
        {
            cameraLocked = false;
            _cameraInput = FindFirstObjectByType<CinemachineInputAxisController>();
        }

        if (_cameraInput != null && cameraBlocks.Count > 0)
        {
            if (!cameraLocked)
            {
                previousCameraEnabled = _cameraInput.enabled;
                cameraLocked = true;
            }
            _cameraInput.enabled = false;
        }
        else
        {
            RestoreCamera();
        }

        if (cursorRequests.Count > 0)
        {
            if (!cursorOverridden)
            {
                previousCursorVisible = Cursor.visible;
                previousCursorLock = Cursor.lockState;
                cursorOverridden = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            RestoreCursor();
        }
    }

    //恢复
    private void RestoreCamera()
    {
        if (cameraLocked && _cameraInput != null)
            _cameraInput.enabled = previousCameraEnabled;
        cameraLocked = false;
    }
    //恢复
    private void RestoreCursor()
    {
        if (!cursorOverridden)
            return;
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
        cursorOverridden = false;
    }
}
