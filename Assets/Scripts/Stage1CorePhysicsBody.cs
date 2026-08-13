using UnityEngine;
using Unity.Mathematics;
#if CESIUM_PRESENT || true
using CesiumForUnity;
#endif

/// <summary>
/// Stage 1: Core Physics Body for Rocket Trajectory Rebuild.
/// Reads mass, thrust, and burn parameters from a swappable RocketDataProfile ScriptableObject asset.
/// Integrates directly with CesiumGeoreference to locate Earth's true ECEF (0,0,0) center in Unity world space.
/// Applies manual radial inverse-square gravity toward Earth center and applies thrust along local up axis.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Stage1CorePhysicsBody : MonoBehaviour
{
    [Header("Rocket Data Asset (Swappable)")]
    [SerializeField] private RocketDataProfile rocketProfile;

    [Header("Cesium Globe Reference")]
    [SerializeField] private CesiumGeoreference cesiumGeoreference;

    [Header("Mass Configuration (kg)")]
    [SerializeField] private float dryMass = 14850f;         // Default structural + upper stage mass
    [SerializeField] private float propellantMass = 20000f; // Stage 1 fuel mass

    [Header("Thrust Configuration")]
    [SerializeField] private float thrustForce = 480000f;   // Stage 1 thrust in Newtons (480 kN for liftoff TWR = 1.40)
    [SerializeField] private float massFlowRate = 333.33f;  // Derived kg/s fuel consumption rate
    [SerializeField] private bool isThrusting = true;       // Active on Play
    [SerializeField] private Vector3 localThrustDirection = Vector3.up;

    [Header("Gravity Configuration")]
    [SerializeField] private bool useNewtonianGravity = true;
    [SerializeField] private float earthMass = 5.972e24f;     // Mass of Earth in kg
    [SerializeField] private float G = 6.67430e-11f;         // Gravitational constant m^3 kg^-1 s^-2
    [SerializeField] private float surfaceGravityG = 9.81f;   // Fallback uniform g magnitude

    [Header("Debug & Telemetry Readouts")]
    [SerializeField] private string currentRocketName = "Minuteman III";
    [SerializeField] private float currentMassReadout;
    [SerializeField] private float currentAccelerationReadout;
    [SerializeField] private float currentSpeedReadout;
    [SerializeField] private float distanceToCenterReadout;
    [SerializeField] private Vector3 gravityDirectionReadout;
    [SerializeField] private float thrustToWeightRatio;

    private Rigidbody rb;
    private Vector3 previousVelocity;
    private float logTimer = 0f;

    public float DryMass => dryMass;
    public float PropellantMass => propellantMass;
    public float TotalMass => dryMass + propellantMass;
    public bool IsThrusting { get => isThrusting; set => isThrusting = value; }
    public CesiumGeoreference CesiumGeo => cesiumGeoreference;
    public RocketDataProfile Profile => rocketProfile;

    /// <summary>
    /// Computes the true Earth center position in Unity World Space using CesiumGeoreference (ECEF origin 0,0,0).
    /// </summary>
    public Vector3 EarthCenterPosition
    {
        get
        {
            if (cesiumGeoreference == null)
            {
                cesiumGeoreference = FindObjectOfType<CesiumGeoreference>();
            }

            if (cesiumGeoreference != null)
            {
                double3 ecefCenter = double3.zero;
                double3 unityCenterD = cesiumGeoreference.TransformEarthCenteredEarthFixedPositionToUnity(ecefCenter);
                return new Vector3((float)unityCenterD.x, (float)unityCenterD.y, (float)unityCenterD.z);
            }

            return transform.position - (transform.up * 6371000f);
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.None;

        if (cesiumGeoreference == null)
        {
            cesiumGeoreference = FindObjectOfType<CesiumGeoreference>();
        }

        // Initialize parameters from RocketDataProfile if assigned
        LoadProfileData();
        UpdateRigidbodyMass();
    }

    /// <summary>
    /// Loads rocket physics parameters from the swappable RocketDataProfile asset.
    /// </summary>
    public void LoadProfileData()
    {
        if (rocketProfile == null)
        {
            // Auto-create Minuteman III fallback profile if none is assigned in Inspector
            rocketProfile = RocketDataProfile.CreateMinutemanIII();
        }

        if (rocketProfile != null && rocketProfile.stages != null && rocketProfile.stages.Length > 0)
        {
            currentRocketName = rocketProfile.rocketName;
            var s1 = rocketProfile.stages[0];
            
            // Calculate total mass above Stage 1 fuel (Stage 1 dry + upper stages + payload)
            float totalWetMass = rocketProfile.CalculateTotalInitialWetMass();
            propellantMass = s1.propellantMassKg;
            dryMass = Mathf.Max(0f, totalWetMass - propellantMass);
            
            thrustForce = s1.thrustNewtons;
            massFlowRate = s1.MassFlowRate;
        }
    }

    private void Start()
    {
        previousVelocity = rb.linearVelocity;
        float liftoffWeightN = TotalMass * surfaceGravityG;
        float liftoffTWR = (liftoffWeightN > 0f) ? (thrustForce / liftoffWeightN) : 0f;
        Debug.Log($"<color=cyan>[Stage1CorePhysicsBody]</color> Profile: '{currentRocketName}' loaded! Liftoff Mass: {TotalMass:F0} kg | Stage 1 Thrust: {thrustForce:F0} N | Liftoff TWR: {liftoffTWR:F2}");
    }

    private void FixedUpdate()
    {
        Vector3 centerPos = EarthCenterPosition;
        Vector3 toCenter = centerPos - rb.position;
        float distance = toCenter.magnitude;

        // 1. Radial Gravity application toward Cesium Earth Center ECEF (0,0,0)
        float gAccelMagnitude = 9.81f;
        if (distance > 0.0001f)
        {
            Vector3 gravityDir = toCenter / distance;
            gravityDirectionReadout = gravityDir;

            if (useNewtonianGravity)
            {
                gAccelMagnitude = (G * earthMass) / (distance * distance);
            }
            else
            {
                gAccelMagnitude = surfaceGravityG;
            }

            rb.AddForce(gravityDir * (rb.mass * gAccelMagnitude), ForceMode.Force);
        }

        float weightN = rb.mass * gAccelMagnitude;
        thrustToWeightRatio = (weightN > 0f) ? (thrustForce / weightN) : 0f;

        // 2. Mass reduction & thrust application while active
        if (isThrusting && propellantMass > 0f)
        {
            float fuelBurned = massFlowRate * Time.fixedDeltaTime;
            propellantMass = Mathf.Max(0f, propellantMass - fuelBurned);
            UpdateRigidbodyMass();

            Vector3 forceDir = transform.TransformDirection(localThrustDirection.normalized);
            rb.AddForce(forceDir * thrustForce, ForceMode.Force);
        }

        // 3. Telemetry updates
        currentMassReadout = rb.mass;
        currentSpeedReadout = rb.linearVelocity.magnitude;
        Vector3 currentAccelVec = (rb.linearVelocity - previousVelocity) / Time.fixedDeltaTime;
        currentAccelerationReadout = currentAccelVec.magnitude;
        distanceToCenterReadout = distance;
        previousVelocity = rb.linearVelocity;

        if (isThrusting)
        {
            logTimer += Time.fixedDeltaTime;
            if (logTimer >= 1.0f)
            {
                logTimer = 0f;
                Debug.Log($"<color=green>[Stage 1 Physics Active]</color> Profile: '{currentRocketName}' | Speed: {currentSpeedReadout:F1} m/s | Accel: {currentAccelerationReadout:F1} m/s² | Mass: {rb.mass:F0} kg | TWR: {thrustToWeightRatio:F2}");
            }
        }
    }

    private void UpdateRigidbodyMass()
    {
        rb.mass = TotalMass;
    }

    [ContextMenu("Ignite Engine")]
    public void StartThrust()
    {
        isThrusting = true;
        Debug.Log($"<color=cyan>[Stage1CorePhysicsBody]</color> IGNITION! Mass: {rb.mass:F0} kg, Thrust: {thrustForce:F0} N");
    }

    [ContextMenu("Cut Engine")]
    public void StopThrust()
    {
        isThrusting = false;
        Debug.Log($"<color=yellow>[Stage1CorePhysicsBody]</color> CUTOFF! Mass: {rb.mass:F0} kg, Propellant left: {propellantMass:F0} kg");
    }
}
