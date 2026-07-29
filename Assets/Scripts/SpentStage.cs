using UnityEngine;

/// <summary>
/// Handles passive atmospheric drag, rotational tumbling, 
/// and memory cleanup for jettisoned booster stages.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SpentStage : MonoBehaviour
{
    [Header("Spent Stage Aerodynamics")]
    [SerializeField] private float dragCoefficient = 0.8f; // Higher drag for unstable tumbling stage
    [SerializeField] private float crossSectionalArea = 5.0f; // Surface area (m²)
    [SerializeField] private float seaLevelAirDensity = 1.225f;
    [SerializeField] private float scaleHeight = 8500f;

    [Header("Cleanup Configuration")]
    [Tooltip("Lifespan in seconds before destroying spent stage to save performance.")]
    [SerializeField] private float autoDestroyTime = 120f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        Destroy(gameObject, autoDestroyTime); // Auto-cleanup after 2 minutes
    }

    private void FixedUpdate()
    {
        ApplyAtmosphericDrag();
    }

    private void ApplyAtmosphericDrag()
    {
        float altitude = Mathf.Max(0f, transform.position.y);
        if (altitude >= 70000f) return; // Ceiling of atmosphere

        // Calculate exponential air density: rho = rho0 * e^(-h / H)
        float airDensity = seaLevelAirDensity * Mathf.Exp(-altitude / scaleHeight);

        // Aerodynamic Drag: F_drag = 0.5 * rho * v^2 * Cd * A
        Vector3 velocity = rb.velocity;
        float speedSqr = velocity.sqrMagnitude;

        if (speedSqr < 0.1f) return;

        float dragMagnitude = 0.5f * airDensity * speedSqr * dragCoefficient * crossSectionalArea;
        Vector3 dragForce = -velocity.normalized * dragMagnitude;

        rb.AddForce(dragForce, ForceMode.Force);
    }
}