using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerIdleState : IState
{
    private readonly PlayerMoveStateMachine machine;


    public PlayerIdleState(PlayerMoveStateMachine machine)
    {
        this.machine = machine;
        
    }

    public void Enter()
    {
        Debug.Log("进入IdleState");
        machine.player.Animation.animator.CrossFadeInFixedTime("Idle", 0.1f);
    }

    public void Exit()
    {
        
    }

    public void PhysicsUpdate()
    {
        
    }

    public void Update()
    {
        if(Keyboard.current.spaceKey.wasPressedThisFrame && machine.player.Animation.isGrounded == true)
        {
            machine.ChangeState<PlayerJumpState>();
            return;
        }
        if(machine.player.Animation.isGrounded == false)
        {
            Debug.Log("因为Ground为false进入的Jump");
            machine.ChangeState<PlayerJumpState>();
            return;
        }

        if (machine.player.InputHandler.inputActions.Player.Move.ReadValue<Vector2>() != Vector2.zero)
        {
            machine.ChangeState<PlayerMoveState>();
            return;
        }
    }
}
