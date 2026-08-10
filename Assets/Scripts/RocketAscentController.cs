using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Master ICBM Flight & Staging Controller using local flat-space parabolic trajectory math.
/// Features continuous staging momentum, target pitch steering, MIRV deployment, and time warp controls.
/// </summary>
public class RocketAscentController : MonoBehaviour
{
    public enum FlightPhase
    {
        PreLaunch,
        VerticalAscent,
        GravityTurnArc,
        Staging_1_to_2,
        Stage2Flight,
        Staging_2_to_3,
        Stage3Flight,
        Stage3Separation,
        PBV_ActiveFlight,
        PBV_MIRV_Deployment,
        PBV_Spent_Tumble
    }

    [Header("Flight State Monitoring")]
    [SerializeField] private FlightPhase currentPhase = FlightPhase.PreLaunch;
    public FlightPhase CurrentPhase => currentPhase;

    [Header("Targeting & Trajectory")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Transform[] mirvTargetTransforms;
    [SerializeField] private float mirvSpreadRadiusMeters = 15000f;

    [SerializeField] private float apogeeRatio = 0.25f;
    [SerializeField] private float minApogeeAltitude = 150000f;

    [Header("Altitude Milestones")]
    [SerializeField] private float stage1BurnoutAltitude = 45000f;
    [SerializeField] private float shroudJettisonAltitude = 100000f;
    [SerializeField] private float stage2BurnoutAltitude = 180000f;

    [Header("Flight Speeds")]
    [SerializeField] private float stage1AscentSpeed = 1500f;
    [SerializeField] private float stage2MaxSpeed = 4200f;
    [SerializeField] private float stage3MaxSpeed = 6800f;
    [SerializeField] private float pbvCoastSpeed = 7200f;
    [SerializeField] private float steeringSmoothness = 2.0f;

    [Header("Hierarchy References")]
    [SerializeField] private Transform stage1MeshTransform;
    [SerializeField] private Transform stage2MeshTransform;
    [SerializeField] private Transform stage3MeshTransform;
    [SerializeField] private Transform pbvBusMeshTransform;

    [Header("Shroud & Warhead Meshes")]
    [SerializeField] private Transform shroudLeftMesh;
    [SerializeField] private Transform shroudRightMesh;
    [SerializeField] private Transform[] warheadTransforms;

    [Header("PBV & Warhead Alignment Offsets")]
    [Tooltip("Keep at (0,0,0) unless 3D model nose isn't aligned with local Y-axis.")]
    [SerializeField] private Vector3 pbvMeshRotationOffset = Vector3.zero;

    [Tooltip("Keep at (0,0,0) unless warhead nose isn't aligned with local Y-axis.")]
    [SerializeField] private Vector3 warheadMeshRotationOffset = Vector3.zero;

    [Header("Particles")]
    [SerializeField] private ParticleSystem stage1EngineParticles;
    [SerializeField] private ParticleSystem stage2EngineParticles;
    [SerializeField] private ParticleSystem stage3EngineParticles;
    [SerializeField] private ParticleSystem pbvRcsParticles;

    [Header("Impulse Parameters")]
    [SerializeField] private float separationDelay = 2.5f;
    [SerializeField] private float separationImpulse = 12f;
    [SerializeField] private float shroudEjectionForce = 10f;
    [SerializeField] private float tumbleTorque = 6f;

    [Header("Time Warp Speeds")]
    [SerializeField] private float[] timeScales = new float[] { 1f, 2f, 4f, 8f };
    private int currentTimeScaleIndex = 0;

    [Header("Events")]
    public UnityEvent onLaunch;
    public UnityEvent onStage1Separated;
    public UnityEvent onShroudJettisoned;
    public UnityEvent onStage2Separated;
    public UnityEvent onStage3Ignited;
    public UnityEvent onStage3Separated;
    public UnityEvent onPBVActivated;

    private Vector3 launchPosition;
    private Vector3 apogeePosition;
    private Vector3 primaryTargetPosition;
    private Vector3[] resolvedWarheadTargets;

    private Quaternion initialRotation;
    private Quaternion pitchTargetRotation;
    private float currentSpeed;
    private float startYPosition;
    private bool hasJettisonedShroud = false;
    private float trajectoryProgress;

    private void Start()
    {
        startYPosition = transform.position.y;
        currentSpeed = 0f;

        StopParticleSystem(stage2EngineParticles);
        StopParticleSystem(stage3EngineParticles);
        StopParticleSystem(pbvRcsParticles);
    }

    private void Update()
    {
        HandleTimeWarpInput();

        switch (currentPhase)
        {
            case FlightPhase.PreLaunch:
                if (Input.GetKeyDown(KeyCode.Space)) LaunchRocket();
                break;

            case FlightPhase.VerticalAscent:
            case FlightPhase.GravityTurnArc:
                ExecuteStage1Flight();
                break;

            case FlightPhase.Staging_1_to_2:
            case FlightPhase.Staging_2_to_3:
            case FlightPhase.Stage3Separation:
                transform.position += transform.up * (currentSpeed * Time.deltaTime);
                break;

            case FlightPhase.Stage2Flight:
                ExecuteStage2Flight();
                break;

            case FlightPhase.Stage3Flight:
                ExecuteStage3Flight();
                break;

            case FlightPhase.PBV_ActiveFlight:
                ExecutePBVFlight();
                break;

            case FlightPhase.PBV_MIRV_Deployment:
                transform.position += transform.up * (pbvCoastSpeed * Time.deltaTime);
                break;

            case FlightPhase.PBV_Spent_Tumble:
                transform.position += transform.up * (currentSpeed * 0.5f * Time.deltaTime);
                transform.Rotate(Vector3.up * 10f * Time.deltaTime);
                break;
        }
    }

    private void HandleTimeWarpInput()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (currentTimeScaleIndex < timeScales.Length - 1)
            {
                currentTimeScaleIndex++;
                ApplyTimeScale();
            }
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if (currentTimeScaleIndex > 0)
            {
                currentTimeScaleIndex--;
                ApplyTimeScale();
            }
        }
    }

    private void ApplyTimeScale()
    {
        Time.timeScale = timeScales[currentTimeScaleIndex];
        Time.fixedDeltaTime = 0.02f * Time.timeScale;
        Debug.Log($"⏩ TIME WARP: {Time.timeScale}x Speed");
    }

    private void LaunchRocket()
    {
        FlattenHierarchy();
        CalculateTrajectory();
        AlignHeading();

        initialRotation = transform.rotation;
        pitchTargetRotation = initialRotation * Quaternion.Euler(45f, 0f, 0f);

        currentPhase = FlightPhase.VerticalAscent;
        currentSpeed = 0f;

        PlayParticleSystem(stage1EngineParticles);
        onLaunch?.Invoke();
    }

    private void FlattenHierarchy()
    {
        if (stage1MeshTransform != null) stage1MeshTransform.SetParent(transform, true);
        if (stage2MeshTransform != null) stage2MeshTransform.SetParent(transform, true);
        if (stage3MeshTransform != null) stage3MeshTransform.SetParent(transform, true);
        if (pbvBusMeshTransform != null) pbvBusMeshTransform.SetParent(transform, true);
        if (shroudLeftMesh != null) shroudLeftMesh.SetParent(transform, true);
        if (shroudRightMesh != null) shroudRightMesh.SetParent(transform, true);

        if (warheadTransforms != null && pbvBusMeshTransform != null)
        {
            foreach (Transform warhead in warheadTransforms)
            {
                if (warhead != null) warhead.SetParent(pbvBusMeshTransform, true);
            }
        }
    }

    private void CalculateTrajectory()
    {
        launchPosition = transform.position;
        primaryTargetPosition = (targetTransform != null) ? targetTransform.position : launchPosition + (transform.forward * 1000000f);

        float distanceMeters = Vector3.Distance(launchPosition, primaryTargetPosition);
        float apogeeMeters = Mathf.Max(distanceMeters * apogeeRatio, minApogeeAltitude);

        Vector3 midPoint = Vector3.Lerp(launchPosition, primaryTargetPosition, 0.5f);
        apogeePosition = midPoint + (Vector3.up * apogeeMeters);

        int count = (warheadTransforms != null) ? warheadTransforms.Length : 0;
        resolvedWarheadTargets = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            if (mirvTargetTransforms != null && i < mirvTargetTransforms.Length && mirvTargetTransforms[i] != null)
            {
                resolvedWarheadTargets[i] = mirvTargetTransforms[i].position;
            }
            else
            {
                float angle = i * (360f / Mathf.Max(1, count)) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * mirvSpreadRadiusMeters;
                resolvedWarheadTargets[i] = primaryTargetPosition + offset;
            }
        }
    }

    private void AlignHeading()
    {
        Vector3 directionToTarget = primaryTargetPosition - transform.position;
        directionToTarget.y = 0;

        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetHeading = Quaternion.LookRotation(directionToTarget, transform.up);
            transform.rotation = targetHeading;
        }
    }

    private void ExecuteStage1Flight()
    {
        float currentAltitude = transform.position.y - startYPosition;
        currentSpeed = Mathf.MoveTowards(currentSpeed, stage1AscentSpeed, 30f * Time.deltaTime);

        if (currentAltitude >= 3000f)
        {
            currentPhase = FlightPhase.GravityTurnArc;
            float rawT = Mathf.Clamp01((currentAltitude - 3000f) / (stage1BurnoutAltitude - 3000f));
            float curvedT = Mathf.Pow(rawT, 1.2f);
            transform.rotation = Quaternion.Slerp(initialRotation, pitchTargetRotation, curvedT);
        }

        transform.position += transform.up * (currentSpeed * Time.deltaTime);

        if (currentAltitude >= stage1BurnoutAltitude && currentPhase != FlightPhase.Staging_1_to_2)
        {
            currentPhase = FlightPhase.Staging_1_to_2;
            StartCoroutine(SequenceStage1To2());
        }
    }

    private void ExecuteStage2Flight()
    {
        float currentAltitude = transform.position.y - startYPosition;

        SteerSmoothlyAlongTrajectory();

        if (!hasJettisonedShroud && (currentAltitude >= shroudJettisonAltitude || trajectoryProgress >= 0.15f))
        {
            JettisonShroud();
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, stage2MaxSpeed, 80f * Time.deltaTime);
        transform.position += transform.up * (currentSpeed * Time.deltaTime);

        if (currentAltitude >= stage2BurnoutAltitude || trajectoryProgress >= 0.28f)
        {
            currentPhase = FlightPhase.Staging_2_to_3;
            StartCoroutine(SequenceStage2To3());
        }
    }

    private void ExecuteStage3Flight()
    {
        SteerSmoothlyAlongTrajectory();

        currentSpeed = Mathf.MoveTowards(currentSpeed, stage3MaxSpeed, 120f * Time.deltaTime);
        transform.position += transform.up * (currentSpeed * Time.deltaTime);

        if (trajectoryProgress >= 0.48f && currentPhase != FlightPhase.Stage3Separation)
        {
            currentPhase = FlightPhase.Stage3Separation;
            StartCoroutine(SequenceStage3ToPBV());
        }
    }

    private void ExecutePBVFlight()
    {
        SteerSmoothlyAlongTrajectory();

        currentSpeed = Mathf.MoveTowards(currentSpeed, pbvCoastSpeed, 20f * Time.deltaTime);
        transform.position += transform.up * (currentSpeed * Time.deltaTime);

        if (trajectoryProgress >= 0.50f && currentPhase == FlightPhase.PBV_ActiveFlight)
        {
            StartCoroutine(SequenceMIRVDeployment());
        }
    }

    private void SteerSmoothlyAlongTrajectory()
    {
        float totalDistance = Vector3.Distance(launchPosition, primaryTargetPosition);
        float distanceCovered = Vector3.Distance(launchPosition, transform.position);

        trajectoryProgress = Mathf.Clamp(distanceCovered / totalDistance, 0.01f, 1.0f);

        float t = trajectoryProgress;
        Vector3 curveTangent = 2f * (1f - t) * (apogeePosition - launchPosition) + 2f * t * (primaryTargetPosition - apogeePosition);

        if (curveTangent != Vector3.zero)
        {
            Quaternion targetFlightRotation = Quaternion.LookRotation(curveTangent.normalized, transform.up);
            targetFlightRotation *= Quaternion.Euler(90f, 0f, 0f);

            transform.rotation = Quaternion.Slerp(transform.rotation, targetFlightRotation, steeringSmoothness * Time.deltaTime);
        }
    }

    private IEnumerator SequenceMIRVDeployment()
    {
        currentPhase = FlightPhase.PBV_MIRV_Deployment;

        for (int i = 0; i < warheadTransforms.Length; i++)
        {
            Transform warhead = warheadTransforms[i];
            if (warhead == null) continue;

            Vector3 warheadTarget = resolvedWarheadTargets[i];
            Vector3 directionToTarget = (warheadTarget - transform.position).normalized;

            if (directionToTarget != Vector3.zero)
            {
                Vector3 safeUp = (Mathf.Abs(Vector3.Dot(directionToTarget, Vector3.up)) > 0.99f) ? Vector3.forward : Vector3.up;
                Quaternion pbvTargetRotation = Quaternion.LookRotation(directionToTarget, safeUp) * Quaternion.Euler(90f, 0f, 0f);

                if (pbvMeshRotationOffset != Vector3.zero)
                {
                    pbvTargetRotation *= Quaternion.Euler(pbvMeshRotationOffset);
                }

                float alignTimer = 0f;
                Quaternion startRot = transform.rotation;

                while (alignTimer < 1.5f)
                {
                    alignTimer += Time.deltaTime;
                    transform.rotation = Quaternion.Slerp(startRot, pbvTargetRotation, alignTimer / 1.5f);
                    transform.position += transform.up * (pbvCoastSpeed * Time.deltaTime);
                    yield return null;
                }
                transform.rotation = pbvTargetRotation;
            }

            PlayParticleSystem(pbvRcsParticles);
            yield return new WaitForSeconds(0.3f);
            StopParticleSystem(pbvRcsParticles);

            warhead.SetParent(null);

            if (!warhead.TryGetComponent<WarheadController>(out var whController))
            {
                whController = warhead.gameObject.AddComponent<WarheadController>();
            }

            whController.InitializeDescent(warheadTarget, pbvCoastSpeed + 150f, warheadMeshRotationOffset);

            yield return new WaitForSeconds(2.0f);
        }

        currentPhase = FlightPhase.PBV_Spent_Tumble;
    }

    private void JettisonShroud()
    {
        hasJettisonedShroud = true;
        EjectShroudPart(shroudLeftMesh, transform.right);
        EjectShroudPart(shroudRightMesh, -transform.right);
        onShroudJettisoned?.Invoke();
    }

    private void EjectShroudPart(Transform shroudPart, Vector3 sideDirection)
    {
        if (shroudPart == null) return;
        shroudPart.SetParent(null);

        if (!shroudPart.TryGetComponent<Rigidbody>(out var rb)) rb = shroudPart.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.AddForce((sideDirection * shroudEjectionForce) + (-transform.up * 4f), ForceMode.Impulse);
    }

    private IEnumerator SequenceStage1To2()
    {
        StopParticleSystem(stage1EngineParticles);
        SeparateStageMesh(stage1MeshTransform, true);
        onStage1Separated?.Invoke();

        yield return new WaitForSeconds(separationDelay);

        PlayParticleSystem(stage2EngineParticles);
        currentPhase = FlightPhase.Stage2Flight;
    }

    private IEnumerator SequenceStage2To3()
    {
        StopParticleSystem(stage2EngineParticles);
        SeparateStageMesh(stage2MeshTransform, false);
        onStage2Separated?.Invoke();

        yield return new WaitForSeconds(separationDelay);

        PlayParticleSystem(stage3EngineParticles);
        onStage3Ignited?.Invoke();
        currentPhase = FlightPhase.Stage3Flight;
    }

    private IEnumerator SequenceStage3ToPBV()
    {
        StopParticleSystem(stage3EngineParticles);
        SeparateStageMesh(stage3MeshTransform, false);
        onStage3Separated?.Invoke();

        yield return new WaitForSeconds(1.8f);

        onPBVActivated?.Invoke();
        currentPhase = FlightPhase.PBV_ActiveFlight;
    }

    private void SeparateStageMesh(Transform stageMesh, bool enableGravity)
    {
        if (stageMesh == null) return;
        stageMesh.SetParent(null);

        if (!stageMesh.TryGetComponent<Rigidbody>(out var rb)) rb = stageMesh.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.useGravity = enableGravity;
        rb.AddForce(-transform.up * separationImpulse, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * tumbleTorque, ForceMode.Impulse);
    }

    private void PlayParticleSystem(ParticleSystem ps)
    {
        if (ps != null && !ps.isPlaying) ps.Play(true);
    }

    private void StopParticleSystem(ParticleSystem ps)
    {
        if (ps != null && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}