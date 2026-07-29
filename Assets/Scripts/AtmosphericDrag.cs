using UnityEngine;

/// <summary>
/// Simulates KSP-style barometric atmospheric drag decay and dynamic pressure (Max Q).
/// Applies drag forces directly opposite to the velocity vector.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AtmosphericDrag : MonoBehaviour
{
    [Header("Atmosphere Properties")]
    [Tooltip("Sea-level air density in kg/m^3 (Earth standard is 1.225).")]
    [SerializeField] private float seaLevelDensity = 1.225f;

    [Tooltip("Scale height in meters (Earth is ~8500m, KSP Kerbin is ~5600m).")]
    [SerializeField] private float scaleHeight = 8500f;

    [Tooltip("Altitude in meters where the atmosphere ends completely (70km = Space).")]
    [SerializeField] private float atmosphereCeiling = 70000f;

    [Header("Rocket Aerodynamics")]
    [Tooltip("Drag coefficient (Cd). Sleek ICBMs are typically between 0.2 and 0.35.")]
    [SerializeField] private float dragCoefficient = 0.28f;

    [Tooltip("Frontal cross-sectional area in square meters (A = PI * r^2). Example: 1.2m radius rocket = ~4.5m².")]
    [SerializeField] private float crossSectionalArea = 4.5f;

    private Rigidbody rb;
    private float launchPadAltitude;

    // Telemetry Outputs (Read-only for debugging & UI)
    [Header("Flight Telemetry")]
    [SerializeField] private float currentAltitude;
    [SerializeField] private float currentAirDensity;
    [SerializeField] private float dynamicPressureQ; // In Pascals (Pa)
    [SerializeField] private float totalDragForce;  // In Newtons (N)

    public float Altitude => currentAltitude;
    public float AirDensity => currentAirDensity;
    public float DynamicPressure => dynamicPressureQ;
    public float DragForce => totalDragForce;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        launchPadAltitude = transform.position.y;
    }

    private void FixedUpdate()
    {
        // 1. Calculate relative altitude above launchpad
        currentAltitude = Mathf.Max(0f, transform.position.y - launchPadAltitude);

        // 2. Check if rocket is in the vacuum of space
        if (currentAltitude >= atmosphereCeiling)
        {
            currentAirDensity = 0f;
            dynamicPressureQ = 0f;
            totalDragForce = 0f;
            return;
        }

        // 3. Exponential air density drop: rho(h) = rho0 * e^(-h / H)
        currentAirDensity = seaLevelDensity * Mathf.Exp(-currentAltitude / scaleHeight);

        float speed = rb.velocity.magnitude;
        if (speed <= 0.01f)
        {
            dynamicPressureQ = 0f;
            totalDragForce = 0f;
            return;
        }

        // 4. Dynamic Pressure: Q = 0.5 * rho * v^2
        dynamicPressureQ = 0.5f * currentAirDensity * (speed * speed);

        // 5. Total Drag Force: Fd = Q * Cd * A
        totalDragForce = dynamicPressureQ * dragCoefficient * crossSectionalArea;

        // 6. Apply drag force directly opposite to current flight velocity vector
        Vector3 dragDirection = -rb.velocity.normalized;
        rb.AddForce(dragDirection * totalDragForce, ForceMode.Force);
    }
}