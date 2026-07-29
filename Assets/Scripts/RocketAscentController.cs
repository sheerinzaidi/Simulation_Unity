using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Cinematic ICBM Ascent & Staging Controller.
/// Features Spacebar Launch control, continuous early atmospheric gravity turn curve,
/// and space inertia coasting during stage separation.
/// </summary>
public class RocketAscentController : MonoBehaviour
{
    public enum FlightPhase
    {
        PreLaunch,
        VerticalAscent,
        GravityTurnArc,
        StagingSequence,
        Stage2Flight
    }

    [Header("Flight State Monitoring")]
    [SerializeField] private FlightPhase currentPhase = FlightPhase.PreLaunch;
    public FlightPhase CurrentPhase => currentPhase;

    [Header("Atmospheric Gravity Turn Curve")]
    [Tooltip("Altitude (meters) where subtle pitching begins in Earth's lower atmosphere (e.g., 2000m = 2km).")]
    [SerializeField] private float pitchStartAltitude = 2000f;

    [Tooltip("Altitude (meters) where full pitch angle (45°) is achieved in upper atmosphere/space.")]
    [SerializeField] private float pitchFullAltitude = 45000f; // 45 km

    [Tooltip("Target max forward pitch angle in degrees relative to vertical.")]
    [SerializeField] private float targetPitchAngle = 45f;

    [Header("Cinematic Timing & Speeds")]
    [Tooltip("Liftoff acceleration rate (m/s²).")]
    [SerializeField] private float stage1Acceleration = 120f;

    [Tooltip("Target cruise speed during Stage 1 ascent (m/s).")]
    [SerializeField] private float stage1AscentSpeed = 1200f;

    [Tooltip("Forward inertia drift speed maintained while staging in space (m/s).")]
    [SerializeField] private float stagingCoastSpeed = 500f;

    [Tooltip("Speed achieved after Stage 2 ignition (m/s).")]
    [SerializeField] private float stage2FlightSpeed = 2200f;

    [Header("Mesh References (Hierarchy Setup)")]
    [Tooltip("The Stage 1 mesh container to detach and drop.")]
    [SerializeField] private Transform stage1MeshTransform;

    [Tooltip("The Stage 2 mesh container to keep attached to ICBM_Root.")]
    [SerializeField] private Transform stage2MeshTransform;

    [Header("Particle Systems")]
    [SerializeField] private ParticleSystem stage1EngineParticles;
    [SerializeField] private ParticleSystem stage2EngineParticles;

    [Header("Physical Staging Parameters")]
    [Tooltip("Delay while stage 1 drops away in space (6.0 to 7.0 seconds).")]
    [Range(6.0f, 7.0f)]
    [SerializeField] private float separationDelay = 6.5f;

    [Tooltip("Backward impulse applied to separated Stage 1.")]
    [SerializeField] private float separationImpulse = 20f;

    [Tooltip("Tumble torque applied to Stage 1.")]
    [SerializeField] private float tumbleTorque = 10f;

    [Header("Events")]
    public UnityEvent onLaunch;
    public UnityEvent onPitchKickStarted;
    public UnityEvent onStagingSequenceStarted;
    public UnityEvent onStage2Ignited;

    private Quaternion initialRotation;
    private Quaternion targetRotation;
    private float currentSpeed;
    private float startYPosition;
    private bool hasTriggeredPitchEvent = false;

    private void Start()
    {
        startYPosition = transform.position.y;
        initialRotation = transform.rotation;
        targetRotation = initialRotation * Quaternion.Euler(targetPitchAngle, 0f, 0f);

        currentSpeed = 0f;

        if (stage2EngineParticles != null && stage2EngineParticles.isPlaying)
        {
            stage2EngineParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void Update()
    {
        switch (currentPhase)
        {
            case FlightPhase.PreLaunch:
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    LaunchRocket();
                }
                break;

            case FlightPhase.VerticalAscent:
            case FlightPhase.GravityTurnArc:
                HandleAscentAndGravityTurn();
                break;

            case FlightPhase.StagingSequence:
                // Maintained inside ExecuteStagingSequence Coroutine
                break;

            case FlightPhase.Stage2Flight:
                HandleStage2Flight();
                break;
        }
    }

    private void LaunchRocket()
    {
        currentPhase = FlightPhase.VerticalAscent;
        currentSpeed = 0f;

        if (stage1EngineParticles != null && !stage1EngineParticles.isPlaying)
        {
            stage1EngineParticles.Play(true);
        }

        onLaunch?.Invoke();
        Debug.Log("🚀 ROCKET LAUNCHED!");
    }

    private void HandleAscentAndGravityTurn()
    {
        float currentAltitude = transform.position.y - startYPosition;

        // 1. Smoothly ramp up speed
        currentSpeed = Mathf.MoveTowards(currentSpeed, stage1AscentSpeed, stage1Acceleration * Time.deltaTime);

        // 2. Continuous Dynamic Gravity Turn Curve
        if (currentAltitude >= pitchStartAltitude)
        {
            if (!hasTriggeredPitchEvent)
            {
                currentPhase = FlightPhase.GravityTurnArc;
                onPitchKickStarted?.Invoke();
                hasTriggeredPitchEvent = true;
            }

            // Calculate progress factor (0.0 to 1.0) between pitchStartAltitude (2km) and pitchFullAltitude (45km)
            float rawT = Mathf.Clamp01((currentAltitude - pitchStartAltitude) / (pitchFullAltitude - pitchStartAltitude));

            // Power curve (t^1.5): Ultra-gentle tilt low in atmosphere, gradually opening into full 45° in space
            float curvedT = Mathf.Pow(rawT, 1.5f);

            transform.rotation = Quaternion.Slerp(initialRotation, targetRotation, curvedT);
        }

        // Apply forward translation along current engine facing vector
        transform.position += transform.up * (currentSpeed * Time.deltaTime);

        // Transition to staging sequence once target altitude is reached
        if (currentAltitude >= pitchFullAltitude && currentPhase != FlightPhase.StagingSequence)
        {
            currentPhase = FlightPhase.StagingSequence;
            StartCoroutine(ExecuteStagingSequence());
        }
    }

    private void HandleStage2Flight()
    {
        // Smoothly accelerate from staging inertia drift speed to full Stage 2 speed
        currentSpeed = Mathf.MoveTowards(currentSpeed, stage2FlightSpeed, (stage2FlightSpeed - stagingCoastSpeed) * Time.deltaTime);
        transform.position += transform.up * (currentSpeed * Time.deltaTime);
    }

    private IEnumerator ExecuteStagingSequence()
    {
        onStagingSequenceStarted?.Invoke();

        // 1. Cut Stage 1 Exhaust
        if (stage1EngineParticles != null)
        {
            stage1EngineParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // 2. Preserve Stage 2 hierarchy attachment on ICBM_Root
        if (stage2MeshTransform != null)
        {
            stage2MeshTransform.SetParent(transform);
        }

        // 3. Uncouple Stage 1 Mesh & Apply Separation Forces
        if (stage1MeshTransform != null)
        {
            stage1MeshTransform.SetParent(null);

            if (!stage1MeshTransform.TryGetComponent<Rigidbody>(out var stage1Rb))
            {
                stage1Rb = stage1MeshTransform.gameObject.AddComponent<Rigidbody>();
            }

            stage1Rb.isKinematic = false;
            stage1Rb.useGravity = true;

            stage1Rb.AddForce(-transform.up * separationImpulse, ForceMode.Impulse);
            stage1Rb.AddTorque(Random.insideUnitSphere * tumbleTorque, ForceMode.Impulse);
        }

        // 4. INERTIA DRIFT: Continue gliding smoothly through space during separation delay
        float timer = 0f;
        while (timer < separationDelay)
        {
            timer += Time.deltaTime;
            
            // Retain forward drift momentum (coasting speed)
            currentSpeed = Mathf.Lerp(currentSpeed, stagingCoastSpeed, Time.deltaTime * 2f);
            transform.position += transform.up * (currentSpeed * Time.deltaTime);

            yield return null;
        }

        // 5. Stage 2 Ignition
        if (stage2EngineParticles != null)
        {
            stage2EngineParticles.Play(true);
        }

        onStage2Ignited?.Invoke();
        currentPhase = FlightPhase.Stage2Flight;
    }
}