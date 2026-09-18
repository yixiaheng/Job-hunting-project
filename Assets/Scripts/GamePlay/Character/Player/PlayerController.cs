using UnityEngine;
using System;
using UnityEngine.Animations.Rigging;
using System.Collections.Generic;


public class PlayerController : MonoBehaviour
{
    [HideInInspector]public Rigidbody rb;
    //新输入系统
    public PlayerInputHandler InputHandler{get; private set;}
    //角色移动状态管理
    public PlayerMoveStateMachine playerMoveStateMachine{get; private set;}
    //Animation数据管理
    public PlayerAnimation Animation{get; private set;}
    //角色移动数据SO
    public PlayerMovementConfigSO playerMovementConfigSO;
    //地面检测
    private GroundCheck groundCheck;
    private float airborneTime;
    //Trigger检测
    private List<DialogueTrigger> dialogues = new List<DialogueTrigger>();
    [Header("超过此时间，视为脱离地面")][SerializeField] private float coyoteTime = 0.12f; //超过这个时间，视为脱离地面
    void Awake()
    {
        playerMoveStateMachine = new PlayerMoveStateMachine(this);
        Animation = GetComponent<PlayerAnimation>();
        rb = GetComponent<Rigidbody>();
        groundCheck = GetComponent<GroundCheck>();
        InputHandler = GetComponent<PlayerInputHandler>();

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

    void OnTriggerStay(Collider other)
    {
        var dt = other.GetComponent<DialogueTrigger>();
        if (dt != null && !dialogues.Contains(dt))
        {
            dialogues.Add(dt);
            Debug.Log($"发现 DialogueTrigger: {dt.name}");
        }
    }
    void OnTriggerExit(Collider other)
    {
        var dt = other.GetComponent<DialogueTrigger>();
        if (dt != null)
            dialogues.Remove(dt);
    }

    void OnDisable()
    {

        //清空dialogues
        dialogues.Clear();
    }

    public void SetMove(Vector3 direction, float speed)
    {
        if(!InputHandler.CanMove)
        {
            Debug.Log("不在可移动状态");
            return;
        }

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
