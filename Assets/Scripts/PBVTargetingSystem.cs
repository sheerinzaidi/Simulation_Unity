using System.Collections;
using UnityEngine;

/// <summary>
/// Manages realistic MIRV footprint targeting.
/// Keeps PBV moving forward in space, rotates toward targets,
/// and launches warheads toward their target coordinates.
/// </summary>
public class PBVTargetingSystem : MonoBehaviour
{
    [Header("Main Target Area")]
    [Tooltip("Drag a Target Marker or GameObject representing the primary strike area.")]
    [SerializeField] private Transform primaryTarget;

    [Header("Footprint Radius (Meters)")]
    [Tooltip("Maximum distance (in meters) warheads can spread from the main target.")]
    [SerializeField] private float maxFootprintRadius = 300000f; // 300 km footprint

    [Header("3D Mesh Orientation Offset")]
    [Tooltip("Tweak if PBV points UP or backward when targeting (e.g. 90, 0, 0 or -90, 0, 0).")]
    [SerializeField] private Vector3 meshRotationOffset = Vector3.zero;

    [Header("Warhead References")]
    [SerializeField] private GameObject[] warheads = new GameObject[4];

    [Header("PBV Speed & Timing Settings")]
    [SerializeField] private float pbvSpeed = 3000f; // Hypersonic speed (meters per second)
    [SerializeField] private float rotationDuration = 2.5f; // Time PBV takes to align to target
    [SerializeField] private float delayBetweenDeployments = 1.5f;

    private Vector3[] calculatedTargets = new Vector3[4];
    private bool isFlying = false;

    private void Start()
    {
        GenerateFootprintTargets();

        // 1. Ensure Rigidbody physics won't drop PBV on launch
        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        // 2. Ensure child warheads also have gravity disabled at start
        foreach (GameObject wh in warheads)
        {
            if (wh != null && wh.TryGetComponent<Rigidbody>(out var whRb))
            {
                whRb.useGravity = false;
                whRb.isKinematic = true;
            }
        }

        // NOTE: StartDeploymentSequence() is NOT called here anymore!
        // It will be triggered when Stage 3 separates.
    }

    private void Update()
    {
        // Keep PBV moving forward through space continuously once activated
        if (isFlying)
        {
            transform.position += transform.forward * (pbvSpeed * Time.deltaTime);
        }
    }

    public void GenerateFootprintTargets()
    {
        Vector3 center = (primaryTarget != null) ? primaryTarget.position : Vector3.zero;

        for (int i = 0; i < 4; i++)
        {
            Vector2 randomPoint = Random.insideUnitCircle * maxFootprintRadius;
            calculatedTargets[i] = new Vector3(center.x + randomPoint.x, center.y, center.z + randomPoint.y);
        }
    }

    /// <summary>
    /// Call this function when Stage 3 separates!
    /// </summary>
    public void StartDeploymentSequence()
    {
        isFlying = true;
        StartCoroutine(ExecuteDeploymentSequence());
    }

    private IEnumerator ExecuteDeploymentSequence()
    {
        for (int i = 0; i < warheads.Length; i++)
        {
            if (warheads[i] == null) continue;

            Vector3 targetPosition = calculatedTargets[i];

            // 1. Smoothly align PBV toward target while moving forward
            yield return StartCoroutine(OrientPBVToTarget(targetPosition));

            // 2. Unparent and launch warhead
            DeployWarhead(warheads[i], targetPosition);

            // 3. Short delay before aligning for next warhead
            yield return new WaitForSeconds(delayBetweenDeployments);
        }
    }

    private IEnumerator OrientPBVToTarget(Vector3 targetPos)
    {
        Vector3 directionToTarget = (targetPos - transform.position).normalized;

        if (directionToTarget == Vector3.zero) yield break;

        // Calculate rotation facing target coordinate and apply model offset
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        targetRotation *= Quaternion.Euler(meshRotationOffset);

        Quaternion startRotation = transform.rotation;

        float elapsed = 0f;
        while (elapsed < rotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / rotationDuration;

            // Smoothly rotate PBV nose toward target coordinate
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        transform.rotation = targetRotation;
    }

    private void DeployWarhead(GameObject warhead, Vector3 targetPos)
    {
        // Unparent warhead from PBV
        warhead.transform.SetParent(null);

        // Turn off gravity on warhead mesh
        if (warhead.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }

        // Attach simple flight movement to warhead so it flies toward its target
        var warheadFlight = warhead.GetComponent<SimpleWarheadFlight>();
        if (warheadFlight == null)
        {
            warheadFlight = warhead.AddComponent<SimpleWarheadFlight>();
        }

        warheadFlight.Launch(targetPos, pbvSpeed + 200f, meshRotationOffset);

        Debug.Log($"🚀 Deployed Warhead '{warhead.name}' targeting coordinates: {targetPos}");
    }
}

/// <summary>
/// Simple flight script added automatically to warheads upon release.
/// Drives warhead straight to its assigned ground target.
/// </summary>
public class SimpleWarheadFlight : MonoBehaviour
{
    private Vector3 targetCoordinates;
    private float flightSpeed;
    private Vector3 meshRotationOffset;
    private bool isLaunched = false;

    public void Launch(Vector3 target, float speed, Vector3 offset)
    {
        targetCoordinates = target;
        flightSpeed = speed;
        meshRotationOffset = offset;
        isLaunched = true;

        // Point warhead directly at its target ground coordinate
        Vector3 dir = (targetCoordinates - transform.position).normalized;
        if (dir != Vector3.zero)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            rot *= Quaternion.Euler(meshRotationOffset);
            transform.rotation = rot;
        }
    }

    private void Update()
    {
        if (!isLaunched) return;

        // Fly toward ground target at hypersonic speed
        transform.position += transform.forward * (flightSpeed * Time.deltaTime);

        // Destroy warhead when close to target
        if (Vector3.Distance(transform.position, targetCoordinates) < 50f)
        {
            Debug.Log("💥 Warhead Impact!");
            Destroy(gameObject);
        }
    }
}