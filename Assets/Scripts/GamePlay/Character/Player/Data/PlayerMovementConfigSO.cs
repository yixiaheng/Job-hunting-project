using UnityEngine;

[CreateAssetMenu(fileName = "PlayerMovementConfigSO", menuName = "StateData /PlayerMovementConfigSO")]
public class PlayerMovementConfigSO : ScriptableObject
{
    [Header("速度档位")]
    public float Walk_Velocity;
    public float Run_Velocity;
    public float Sprint_Velocity;

    [Header("跳跃施加的力")]
    public float JumpForce;
}
