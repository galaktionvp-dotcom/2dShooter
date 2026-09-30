using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float smoothTime = 0.12f;
    [SerializeField] private float lookAheadDistance = 2f;
    [SerializeField] private float lookAheadSmoothTime = 0.15f;

    private Vector3 followVelocity;
    private Vector3 lookAheadVelocity;
    private Vector3 currentLookAhead;

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 targetPosition = target.position;
        targetPosition.z = transform.position.z;

        Vector3 moveDirection = Vector3.zero;

        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb != null)
            moveDirection = targetRb.linearVelocity.normalized;

        Vector3 desiredLookAhead = moveDirection * lookAheadDistance;

        currentLookAhead = Vector3.SmoothDamp(
            currentLookAhead,
            desiredLookAhead,
            ref lookAheadVelocity,
            lookAheadSmoothTime
        );

        targetPosition += currentLookAhead;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref followVelocity,
            smoothTime
        );
    }
}
