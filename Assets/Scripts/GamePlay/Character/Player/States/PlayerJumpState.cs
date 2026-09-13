using System;
using TMPro;
using UnityEngine;

public class PlayerJumpState : IState
{
    private readonly PlayerMoveStateMachine machine;
    private JumpStateType jumpStateType;
    private bool hasAddForce;
    public PlayerJumpState(PlayerMoveStateMachine machine)
    {
        this.machine = machine;
    }

    public void Enter()
    {
        Debug.Log("进入PlayerJumpState");
        

        if(machine.player.Animation.isGrounded)
        {
            hasAddForce = false;
            jumpStateType = JumpStateType.JumpStart;
            machine.player.Animation.animator.CrossFadeInFixedTime("JumpStart", 0.05f);
        }
        else
        {
            jumpStateType = JumpStateType.JumpOnAir;
            machine.player.Animation.animator.CrossFadeInFixedTime("JumpOnAir", 0.1f);
        }

    }

    public void Exit()
    {
       
    }

    public void PhysicsUpdate()
    {
        
    }

    public void Update()
    {
        //如果状态切换了，不再进行Type检测
        if(CalculateStateChange())
        {
            return;
        }
        CalculateType();
        if(jumpStateType == JumpStateType.JumpStart && hasAddForce == false)
        {
            Debug.Log("施加力了");
            Vector3 Direction = new Vector3(0, machine.player.playerMovementConfigSO.JumpForce, 0);
            //施加力
            machine.player.rb.AddForce(Direction, ForceMode.Impulse);
            hasAddForce = true;
        }
    }

    private bool CalculateStateChange()
    {
        
        return false;
    }

    public void CalculateType()
    {
        switch(jumpStateType)
        {
            case JumpStateType.JumpStart:
                UpdateStart();
                break;
            case JumpStateType.JumpOnAir:
                UpdateOnAir();
                break;
            case JumpStateType.JumpEnd:
                UpdateEnd();
                break;
        }
    }

    private void UpdateStart()
    {
        if(hasAddForce == false) 
        {
            Debug.Log("起跳加冲量");
            Vector3 force = new Vector3(0f, machine.player.playerMovementConfigSO.JumpForce, 0f);
            machine.player.rb.AddForce(force, ForceMode.Impulse);

            hasAddForce = true;
        }
        if(machine.player.Animation.IsAnimationFinished("JumpStart"))
        {
            jumpStateType = JumpStateType.JumpOnAir;
            machine.player.Animation.animator.CrossFadeInFixedTime("JumpOnAir", 0.05f);
        }
    }
    private void UpdateOnAir()
    {
        if(machine.player.Animation.isGrounded)
        {
            jumpStateType = JumpStateType.JumpEnd;
            machine.player.Animation.animator.CrossFadeInFixedTime("JumpEnd", 0.05f);
        }
    }
    private void UpdateEnd()
    {
        if(machine.player.Animation.IsAnimationFinished("JumpEnd", 0.6f))
        {
            bool hasMoveInput = machine.player.inputActions.Player.Move.ReadValue<Vector2>() != Vector2.zero;
            if(hasMoveInput)
                machine.ChangeState<PlayerMoveState>();
            else
                machine.ChangeState<PlayerIdleState>();
        }
    }

}



public enum JumpStateType
{
    JumpStart,
    JumpOnAir,
    JumpEnd
}
