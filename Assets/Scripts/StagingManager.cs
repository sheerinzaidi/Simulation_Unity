using System.Collections;
using UnityEngine;

/// <summary>
/// Data structure defining physical properties for a rocket staging event.
/// </summary>
[System.Serializable]
public class StageProfile
{
    public string stageName = "Stage 1";

    [Header("Stage Transforms")]
    [Tooltip("Spent stage mesh container to be decoupled.")]
    public Transform stageTransform;

    [Tooltip("Upper stage mesh container to be preserved on the vehicle.")]
    public Transform nextStageTransform;

    [Tooltip("Nozzle transform for the upper stage engine.")]
    public Transform nozzleTransform;

    [Header("Upper Stage Engine Parameters")]
    public float maxThrust = 600000f;       // Newtons
    public float specificImpulse = 315f;    // Vacuum Isp (seconds)
    public float stageWetMass = 14000f;     // Total mass at upper stage ignition (kg)
    public float stageDryMass = 4500f;      // Mass when upper stage runs out of fuel (kg)

    [Header("Separation Dynamics")]
    public float separationImpulse = 15000f; // N*s
    public float emptyBoosterMass = 10000f;  // Mass of jettisoned empty stage (kg)
    public float interstageDelay = 1.5f;     // Coast delay in seconds
}

/// <summary>
/// Fully Autonomous Multi-Stage Flight Computer.
/// Tracks fuel depletion and manages separation, interstage coasting, 
/// parameter handoffs, and engine re-ignition automatically.
/// </summary>
public class StagingManager : MonoBehaviour
{
    [Header("Stage Configuration Sequence")]
    [SerializeField] private StageProfile[] stages;

    [Header("Component References")]
    [SerializeField] private RocketEngine rocketEngine;
    [SerializeField] private EngineGimbal engineGimbal;

    private Rigidbody mainRb;
    private int currentStageIndex = 0;
    private bool isStagingInProgress = false;

    public int CurrentStageIndex => currentStageIndex;
    public bool IsStagingInProgress => isStagingInProgress;

    private void Awake()
    {
        mainRb = GetComponent<Rigidbody>();
        if (rocketEngine == null) rocketEngine = GetComponent<RocketEngine>();
        if (engineGimbal == null) engineGimbal = GetComponent<EngineGimbal>();
    }

    private void FixedUpdate()
    {
        if (isStagingInProgress || currentStageIndex >= stages.Length) return;

        // AUTOMATIC STAGING TRIGGER:
        // Triggers the exact frame fuel runs out
        if (rocketEngine != null && rocketEngine.IsEngineActive && rocketEngine.CurrentMass <= rocketEngine.DryMass + 0.1f)
        {
            StartCoroutine(ExecuteStagingSequence(stages[currentStageIndex]));
        }
    }

    private IEnumerator ExecuteStagingSequence(StageProfile profile)
    {
        isStagingInProgress = true;
        Debug.Log($"<color=red>[Autopilot Staging]</color> MECO Detected! Jettisoning {profile.stageName}...");

        // 1. Cut Main Engine
        if (rocketEngine != null)
        {
            rocketEngine.SetEngineState(false);
            rocketEngine.SetThrottle(0f);
        }

        // 2. Unparent next stage to preserve it on ICBM_Root
        if (profile.nextStageTransform != null)
        {
            profile.nextStageTransform.SetParent(transform, true);
        }

        // 3. Jettison spent stage into world space
        if (profile.stageTransform != null)
        {
            profile.stageTransform.SetParent(null, true);

            // Add physics to jettisoned stage
            Rigidbody spentRb = profile.stageTransform.gameObject.AddComponent<Rigidbody>();
            spentRb.mass = profile.emptyBoosterMass;
            spentRb.drag = 0f;
            spentRb.angularDrag = 0.05f;
            spentRb.useGravity = true;
            spentRb.interpolation = RigidbodyInterpolation.Interpolate;
            spentRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Inherit current vehicle velocity
            spentRb.velocity = mainRb.velocity;
            spentRb.angularVelocity = mainRb.angularVelocity;

            // Retro-impulse pushing spent stage backward
            spentRb.AddForce(-transform.up * profile.separationImpulse, ForceMode.Impulse);
            mainRb.AddForce(transform.up * (profile.separationImpulse * 0.15f), ForceMode.Impulse);

            // Induce tumbling torque
            spentRb.AddTorque(Random.onUnitSphere * 5000f, ForceMode.Impulse);

            // Attach drag & cleanup script
            profile.stageTransform.gameObject.AddComponent<SpentStage>();
        }

        // 4. Autonomous Interstage Coast Delay
        Debug.Log($"<color=yellow>[Autopilot Staging]</color> Interstage Coast Phase: {profile.interstageDelay}s...");
        yield return new WaitForSeconds(profile.interstageDelay);

        // 5. Parameter Handoff to Upper Stage
        if (rocketEngine != null)
        {
            rocketEngine.UpdateEngineParameters(
                profile.maxThrust,
                profile.specificImpulse,
                profile.stageWetMass,
                profile.stageDryMass,
                profile.nozzleTransform
            );
        }

        if (engineGimbal != null && profile.nozzleTransform != null)
        {
            engineGimbal.UpdateGimbalTarget(profile.nozzleTransform);
        }

        // 6. Autonomous Upper Stage Engine Ignition
        if (rocketEngine != null)
        {
            rocketEngine.SetThrottle(1f);
            rocketEngine.SetEngineState(true);
            Debug.Log($"<color=green>[Autopilot Staging]</color> Upper Stage Engine Ignited!");
        }

        currentStageIndex++;
        isStagingInProgress = false;
    }
}