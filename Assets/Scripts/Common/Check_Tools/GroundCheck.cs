using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem.iOS;

public class GroundCheck : MonoBehaviour
{
    [SerializeField] private Transform GroundCheckPoint;
    [SerializeField] private float GroundCheckRadius = 0.2f;
    [SerializeField] private float GroundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    public bool isGrounded()
    {
        Debug.Log("Checking if player is grounded");

        return Physics.SphereCast(
            GroundCheckPoint.position,
            GroundCheckRadius,
            Vector3.down,
            out _,
            GroundCheckDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(GroundCheckPoint.position, GroundCheckRadius);
        Vector3 endPosition = 
            new Vector3(GroundCheckPoint.position.x, 
                        GroundCheckPoint.position.y - GroundCheckDistance,
                        GroundCheckPoint.position.z
                        );
        Gizmos.DrawSphere(endPosition, GroundCheckRadius);
    }
}
