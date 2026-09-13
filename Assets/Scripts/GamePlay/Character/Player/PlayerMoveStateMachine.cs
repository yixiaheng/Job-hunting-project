using Unity.VisualScripting;
using UnityEngine;

public class PlayerMoveStateMachine : StateMachine
{
    public PlayerController player;

    public PlayerMoveStateMachine(PlayerController playerController)
    {
        player = playerController;
        //注册
        Register(new PlayerMoveState(this));
        Register(new PlayerIdleState(this));
        Register(new PlayerJumpState(this));
    }

    public void StartMachine()
    {
        ChangeState<PlayerIdleState>();
    }
}
