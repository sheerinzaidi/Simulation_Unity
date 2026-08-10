using UnityEngine;

/// <summary>
/// Silky-smooth camera follow script using SmoothDamp physics.
/// Anchors to ICBM_Root so unparenting stage meshes will NEVER cause camera glitches.
/// </summary>
public class SmoothCameraFollow : MonoBehaviour
{
    [Header("Target Lock")]
    [Tooltip("ALWAYS assign ICBM_Root here, never child stage meshes!")]
    [SerializeField] private Transform target;

    [Header("Position Offset & Smoothing")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 8f, -25f);
    [SerializeField] private float smoothTime = 0.25f;
    [SerializeField] private float rotationSpeed = 3f;

    private Vector3 currentVelocity;

    private void LateUpdate()
    {
        if (target == null) return;

        // 1. Calculate offset in world space based on target orientation
        Vector3 targetPosition = target.position + (target.rotation * offset);

        // 2. SmoothDamp creates buttery smooth acceleration/deceleration
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, smoothTime);

        // 3. Smoothly rotate camera to face the rocket
        Quaternion targetRotation = Quaternion.LookRotation(target.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}