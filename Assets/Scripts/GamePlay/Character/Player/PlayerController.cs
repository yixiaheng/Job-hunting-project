using UnityEngine;
using System;
using UnityEngine.Animations.Rigging;

public class PlayerController : MonoBehaviour
{
    [HideInInspector]public Rigidbody rb;
    //新输入系统
    public InputSystem_Actions inputActions;
    //角色移动状态管理
    public PlayerMoveStateMachine playerMoveStateMachine{get; private set;}
    //Animation数据管理
    public PlayerAnimation Animation{get; private set;}
    //角色移动数据SO
    public PlayerMovementConfigSO playerMovementConfigSO;
    //地面检测
    private GroundCheck groundCheck;
    private float airborneTime;
    [Header("超过此时间，视为脱离地面")][SerializeField] private float coyoteTime = 0.12f; //超过这个时间，视为脱离地面
    void Awake()
    {
        playerMoveStateMachine = new PlayerMoveStateMachine(this);
        inputActions = new InputSystem_Actions();
        Animation = GetComponent<PlayerAnimation>();
        rb = GetComponent<Rigidbody>();
        groundCheck = GetComponent<GroundCheck>();
        inputActions.Enable();
    }

    void Start()
    {   
        playerMoveStateMachine.ChangeState<PlayerIdleState>();
    }


    void Update()
    {
        playerMoveStateMachine.currentState.Update();
        UpdateStateData();
    }

    void FixedUpdate()
    {
        playerMoveStateMachine.currentState.PhysicsUpdate();
    }
    void ODisable()
    {
        inputActions.Disable();
    }

    public void SetMove(Vector3 direction, float speed)
    {
        //设置角色移动速度
        Vector3 velocity = rb.linearVelocity;

        Vector3 targetVelocity =
            direction * speed;

        rb.linearVelocity = new Vector3(
            targetVelocity.x,
            velocity.y,
            targetVelocity.z
        );
        //设置角色朝向
        if(direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.MoveRotation(targetRotation);
        }
    }

    private void UpdateStateData()
    {
        bool rawGrounded = groundCheck.isGrounded();

        if(rawGrounded)
        {
            airborneTime = 0f;
            Animation.isGrounded = true;
        }
        else
        {
            airborneTime += Time.deltaTime;
            Animation.isGrounded = airborneTime < coyoteTime;
        }
    }

}
