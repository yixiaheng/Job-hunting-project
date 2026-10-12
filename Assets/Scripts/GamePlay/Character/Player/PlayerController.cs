using UnityEngine;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    [HideInInspector] public Rigidbody rb;
    public PlayerInputHandler InputHandler { get; private set; }
    public PlayerMoveStateMachine playerMoveStateMachine { get; private set; }
    public PlayerAnimation Animation { get; private set; }
    public PlayerMovementConfigSO playerMovementConfigSO;
    private GroundCheck groundCheck;
    private float airborneTime;
    [Header("Coyote Time")] [SerializeField] private float coyoteTime = 0.12f;

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

    void OnDisable()
    {
    }

    public void SetMove(Vector3 direction, float speed)
    {
        if (!InputHandler.CanMove)
            return;

        Vector3 velocity = rb.linearVelocity;
        Vector3 targetVelocity = direction * speed;
        rb.linearVelocity = new Vector3(targetVelocity.x, velocity.y, targetVelocity.z);
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.MoveRotation(targetRotation);
        }
    }

    private void UpdateStateData()
    {
        bool rawGrounded = groundCheck.isGrounded();
        if (rawGrounded)
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
