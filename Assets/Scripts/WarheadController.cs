using UnityEngine;

/// <summary>
/// Controls individual warhead descent and activates the TrailRenderer 
/// immediately upon deployment from the PBV.
/// </summary>
public class WarheadController : MonoBehaviour
{
    [Header("Mesh Alignment")]
    [SerializeField] private Vector3 meshRotationOffset = Vector3.zero;

    [Header("Flight Parameters")]
    [SerializeField] private float rotationSmoothness = 8f;

    [Header("Trail FX")]
    [SerializeField] private TrailRenderer trailRenderer;

    private Vector3 targetPosition;
    private float flightSpeed;
    private bool isReleased = false;

    private void Awake()
    {
        // Automatically grab attached TrailRenderer if not assigned in Inspector
        if (trailRenderer == null)
        {
            trailRenderer = GetComponent<TrailRenderer>();
        }

        // Keep trail hidden while mounted on the PBV
        if (trailRenderer != null)
        {
            trailRenderer.emitting = false;
        }
    }

    public void InitializeDescent(Vector3 targetPos, float initialSpeed, Vector3 rotationOffset)
    {
        targetPosition = targetPos;
        flightSpeed = initialSpeed;
        meshRotationOffset = rotationOffset;
        isReleased = true;

        // Separate from rocket bus
        transform.SetParent(null);

        // Turn on the re-entry trail right on deployment!
        if (trailRenderer != null)
        {
            trailRenderer.emitting = true;
        }

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
        }
    }

    private void Update()
    {
        if (!isReleased) return;

        // 1. Point nose cleanly at target
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;

        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            targetRotation *= Quaternion.Euler(meshRotationOffset);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothness * Time.deltaTime);
        }

        // 2. Fly forward toward target
        transform.position += directionToTarget * (flightSpeed * Time.deltaTime);

        // 3. Cleanup near ground impact
        if (Vector3.Distance(transform.position, targetPosition) < 100f)
        {
            OnImpact();
        }
    }

    private void OnImpact()
    {
        // Detach trail so it dissipates naturally in mid-air
        if (trailRenderer != null)
        {
            trailRenderer.transform.SetParent(null);
            trailRenderer.emitting = false;
            Destroy(trailRenderer.gameObject, trailRenderer.time);
        }

        Destroy(gameObject);
    }
}