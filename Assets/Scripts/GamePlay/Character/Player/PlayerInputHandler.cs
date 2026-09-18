using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    public InputSystem_Actions inputActions;
    public bool CanMove;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        inputActions.Enable();
        CanMove = true;
    }

    void OnEnable()
    {
        inputActions.Player.Zoom.performed += OnZoom;
        inputActions.Player.Interact.performed += OnFPress;
    }

    void OnDisable()
    {
        inputActions.Disable();
        inputActions.Player.Zoom.performed -= OnZoom;
        inputActions.Player.Interact.performed -= OnFPress;
    }
    private void OnZoom(InputAction.CallbackContext context)
    {
        float value = context.ReadValue<float>();
        if(value > 0)
        {
            DialogueRunner.Instance.ChangeSelectID(-1);
        }
        else
        {
            DialogueRunner.Instance.ChangeSelectID(1);
        }
    }

    private void OnFPress(InputAction.CallbackContext context)
    {
        Debug.Log("OnFPress");
        if(!DialogueRunner.Instance.isDialoguing)
        {
            bool get = DialogueRunner.Instance.ShowDialogue();
            if(get == true)
                CanMove = false;
        }
    }

    public void SetMoveEnable(bool b)
    {
        if(b)
        {
            CanMove = true;
        }
        else
        {
            CanMove = false;
        }
    }
}
