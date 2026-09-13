using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMoveState : IState
{
    private readonly PlayerMoveStateMachine machine;
    private MoveType moveType;
    private Vector2 moveInput;

    public PlayerMoveState(PlayerMoveStateMachine machine)
    {
        this.machine = machine; 
    }

    public void Enter()
    {
        Debug.Log("进入MoveState");
        moveType = MoveType.Walk;
        machine.player.Animation.animator.CrossFadeInFixedTime("Walk_", 0.1f);
    }

    public void Exit()
    {

    }
    public void Update()
    {
        moveInput = machine.player.inputActions.Player.Move.ReadValue<Vector2>();

        
        if(CalculateStateChange())
        {
            return;
        }

        CalculateType();
    }

    public void PhysicsUpdate()
    {
       if(moveInput == Vector2.zero)
        {
            return;
        }

        Vector3 moveDirection = GetCameraRelativeDirection(moveInput);
        float moveSpeed = GetMoveSpeed();
        machine.player.SetMove(moveDirection, moveSpeed);
    }

    private bool CalculateStateChange()
    {
        if(Keyboard.current.spaceKey.wasPressedThisFrame && machine.player.Animation.isGrounded == true)
        {
            machine.ChangeState<PlayerJumpState>();
            return true;
        }
        if(machine.player.Animation.isGrounded == false)
        {
            machine.ChangeState<PlayerJumpState>();
            return true;
        }

        if(moveInput == Vector2.zero)
        {
            machine.ChangeState<PlayerIdleState>();
            return true;
        }
        return false;
    }

    private void CalculateType()
    {
        if(Mouse.current.rightButton.isPressed)
        {
            if(moveType == MoveType.Sprint)
                return;
            moveType = MoveType.Sprint;
            machine.player.Animation.animator.CrossFadeInFixedTime("Sprint_", 0.1f);
        }
        else if(Keyboard.current.shiftKey.isPressed)
        {
            if(moveType == MoveType.Walk)
                return;
            moveType = MoveType.Walk;
            machine.player.Animation.animator.CrossFadeInFixedTime("Walk_", 0.1f);
        }
        else
        {
            if(moveType == MoveType.Run)
                return;
            moveType = MoveType.Run;
            machine.player.Animation.animator.CrossFadeInFixedTime("Run_", 0.1f);
        }
    }

    public Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        Transform cameraTransform = Camera.main.transform;

        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();

        Vector3 direction = cameraForward * input.y + cameraRight * input.x;
        return direction.normalized;
    }
    public float GetMoveSpeed()
    {
        return moveType switch
        {
            MoveType.Walk =>
                machine.player.playerMovementConfigSO.Walk_Velocity,

            MoveType.Run =>
                machine.player.playerMovementConfigSO.Run_Velocity,

            MoveType.Sprint =>
                machine.player.playerMovementConfigSO.Sprint_Velocity,

            _ => 0f
        };
    }
}

public enum MoveType
{
    Walk,
    Run,
    Sprint
}