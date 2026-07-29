using UnityEngine;

public class AutopilotGuidance : MonoBehaviour
{
    [Header("Launch Guidance Parameters")]
    public float pitchKickAltitude = 1000f;
    public float pitchKickAngle = 4f;
    public float launchAzimuth = 90f;

    [Header("Component Connections")]
    [SerializeField] private RocketEngine rocketEngine;
    [SerializeField] private EngineGimbal engineGimbal;

    private Rigidbody rb;
    private bool isLaunched = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rocketEngine == null) rocketEngine = GetComponent<RocketEngine>();
        if (engineGimbal == null) engineGimbal = GetComponent<EngineGimbal>();
    }

    private void Update()
    {
        // One-time spacebar press triggers fully automated launch sequence
        if (!isLaunched && Input.GetKeyDown(KeyCode.Space))
        {
            InitiateAutomatedLaunch();
        }
    }

    private void InitiateAutomatedLaunch()
    {
        isLaunched = true;

        // 1. Enable Rigidbody Physics
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // 2. Full Throttle & Engine Ignition
        if (rocketEngine != null)
        {
            rocketEngine.SetThrottle(1.0f);
            rocketEngine.SetEngineState(true);
        }

        Debug.Log("<color=green>[Autopilot]</color> IGNITION & LIFTOFF CONFIRMED!");
    }

    private void FixedUpdate()
    {
        if (!isLaunched || rb == null) return;

        // Automated Pitch Kick Control
        float currentAltitude = transform.position.y;
        if (currentAltitude >= pitchKickAltitude && engineGimbal != null)
        {
            // Tilt nozzle slightly to initiate gravity turn East
            engineGimbal.SetGimbalInputs(pitchKickAngle / 10f, 0f);
        }
    }
}