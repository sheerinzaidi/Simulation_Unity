using UnityEngine;

public class RocketEngine : MonoBehaviour
{
    [Header("Engine Physics Configuration")]
    [SerializeField] private float maxThrust = 1500000f; // Newtons
    [SerializeField] private float specificImpulse = 280f; // Seconds
    [SerializeField] private float wetMass = 50000f;       // Total mass (kg)
    [SerializeField] private float dryMass = 15000f;       // Structural mass (kg)

    [Header("Engine State & Controls")]
    [SerializeField] private bool isEngineActive = false;
    [Range(0f, 1f)] [SerializeField] private float currentThrottle = 1f;
    [SerializeField] private Transform thrustTransform;

    private Rigidbody rb;
    private float currentMass;

    public bool IsEngineActive => isEngineActive;
    public float CurrentMass => currentMass;
    public float DryMass => dryMass;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentMass = wetMass;
        if (rb != null) rb.mass = currentMass;
    }

    private void Update()
    {
        // Press Spacebar to ignite the engine
        if (Input.GetKeyDown(KeyCode.Space) && !isEngineActive)
        {
            SetEngineState(true);
            SetThrottle(1f);
            Debug.Log("<color=green>[Rocket Engine]</color> IGNITION COMMAND RECEIVED!");
        }
    }

    private void FixedUpdate()
    {
        if (!isEngineActive || thrustTransform == null || rb == null) return;

        // 1. Consume Fuel
        float massFlowRate = maxThrust / (specificImpulse * 9.81f);
        float fuelBurn = massFlowRate * currentThrottle * Time.fixedDeltaTime;

        if (currentMass > dryMass)
        {
            currentMass = Mathf.Max(dryMass, currentMass - fuelBurn);
            rb.mass = currentMass;
        }

        // 2. Apply Thrust Force UPWARDS along Nozzle (+Y axis)
        float activeThrust = maxThrust * currentThrottle;
        Vector3 thrustForce = thrustTransform.up * activeThrust;

        rb.AddForceAtPosition(thrustForce, thrustTransform.position, ForceMode.Force);
    }

    public void SetEngineState(bool active) => isEngineActive = active;
    public void SetThrottle(float throttle) => currentThrottle = Mathf.Clamp01(throttle);

    public void UpdateEngineParameters(float newMaxThrust, float newIsp, float newWetMass, float newDryMass, Transform newNozzle)
    {
        maxThrust = newMaxThrust;
        specificImpulse = newIsp;
        wetMass = newWetMass;
        dryMass = newDryMass;
        currentMass = newWetMass;
        if (rb != null) rb.mass = currentMass;
        if (newNozzle != null) thrustTransform = newNozzle;
    }
}