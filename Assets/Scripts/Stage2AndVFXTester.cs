using UnityEngine;

/// <summary>
/// Combined Test Rig for Part A (Swappable RocketDataProfile), Part B (Stage 2 Globe Coordinates & Azimuth),
/// and Part C (Simplified 1-VFX-per-stage driven by live physics events).
/// </summary>
[RequireComponent(typeof(Stage1CorePhysicsBody))]
[RequireComponent(typeof(Stage2GlobeController))]
public class Stage2AndVFXTester : MonoBehaviour
{
    [Header("Testing Keybinds")]
    [SerializeField] private KeyCode igniteKey = KeyCode.Space;
    [SerializeField] private KeyCode recalculateTrajectoryKey = KeyCode.R;
    [SerializeField] private KeyCode simulateBurnoutKey = KeyCode.B;

    [Header("VFX Integration")]
    [SerializeField] private RocketVFXController vfxController;

    private Stage1CorePhysicsBody physicsBody;
    private Stage2GlobeController globeController;

    private void Awake()
    {
        physicsBody = GetComponent<Stage1CorePhysicsBody>();
        globeController = GetComponent<Stage2GlobeController>();

        if (vfxController == null)
        {
            vfxController = GetComponent<RocketVFXController>();
        }
    }

    private void Start()
    {
        // 1. Log Part A Data Profile & Liftoff TWR
        if (physicsBody.Profile != null)
        {
            float totalMass = physicsBody.Profile.CalculateTotalInitialWetMass();
            float stage1Thrust = physicsBody.Profile.stages[0].thrustNewtons;
            float liftoffTWR = stage1Thrust / (totalMass * 9.81f);
            Debug.Log($"<color=cyan>[Part A Test]</color> Profile Loaded: '{physicsBody.Profile.rocketName}'. Initial Wet Mass: {totalMass:F0} kg, Stage 1 Thrust: {stage1Thrust:F0} N. Liftoff TWR: {liftoffTWR:F2} (Minuteman III Realistic Range 1.2–1.5).");
        }

        // 2. Log Part B Globe Geodesics
        globeController.RecalculateTrajectory();

        // 3. Connect Part C Simplified Stage 1 VFX to Physics Events
        if (physicsBody.IsThrusting && vfxController != null)
        {
            vfxController.SetStageThrustVFX(0, true);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(igniteKey))
        {
            physicsBody.IsThrusting = !physicsBody.IsThrusting;
            if (physicsBody.IsThrusting)
            {
                physicsBody.StartThrust();
                if (vfxController != null) vfxController.SetStageThrustVFX(0, true);
            }
            else
            {
                physicsBody.StopThrust();
                if (vfxController != null) vfxController.SetStageThrustVFX(0, false);
            }
        }

        if (Input.GetKeyDown(recalculateTrajectoryKey))
        {
            globeController.RecalculateTrajectory();
        }

        if (Input.GetKeyDown(simulateBurnoutKey))
        {
            physicsBody.StopThrust();
            if (vfxController != null) vfxController.SetStageThrustVFX(0, false);
        }
    }
}
