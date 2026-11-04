using Unity.VisualScripting;
using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    [SerializeField] private Transform GroundCheckPoint;
    [SerializeField] private float GroundCheckRadius = 0.2f;
    [SerializeField] private float GroundCheckDistance = 0.1f;
    [SerializeField] private LayerMask groundLayer;

    public bool isGrounded()
    {
        return TryGetGroundHit(out _);
    }

    public bool TryGetGroundHit(out RaycastHit hit)
    {
        if (GroundCheckPoint == null) { hit = default; return false; }
        return Physics.SphereCast(
            GroundCheckPoint.position,
            GroundCheckRadius,
            Vector3.down,
            out hit,
            GroundCheckDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore
        ) && hit.normal.y >= 0.55f;
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
