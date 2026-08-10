using UnityEngine;

/// <summary>
/// Ultra-Smooth Interactive Tactical Camera.
/// Features SmoothDamp tracking, zero-jitter LateUpdate execution,
/// orbital control (RMB), height adjustment (Q/E), zoom (Z/C), and target switching (0-4).
/// </summary>
public class InteractiveFlightCam : MonoBehaviour
{
    [Header("Target Assignments")]
    [Tooltip("Drag ICBM_Root here.")]
    [SerializeField] private Transform rocketTarget;

    [Tooltip("Drag your 4 Warhead Transforms here in order (1 to 4).")]
    [SerializeField] private Transform[] warheadTargets;

    [Header("Camera Control Speeds")]
    [SerializeField] private float orbitSensitivity = 3f;
    [SerializeField] private float heightSpeed = 15f;
    [SerializeField] private float zoomSpeed = 25f;

    [Header("Smoothness Settings")]
    [Tooltip("How smoothly the camera glides when switching between targets (e.g., Rocket -> Nuke 1).")]
    [SerializeField] private float targetSwitchSmoothTime = 0.3f;

    [Tooltip("How smoothly the camera follows target motion.")]
    [SerializeField] private float movementSmoothTime = 0.05f;

    [Header("Distance & Height Limits")]
    [SerializeField] private float distance = 30f;
    [SerializeField] private float minDistance = 3f;
    [SerializeField] private float maxDistance = 200f;

    [SerializeField] private float heightOffset = 2f;
    [SerializeField] private float minHeight = -30f;
    [SerializeField] private float maxHeight = 60f;

    private Transform currentTarget;
    private Vector3 currentPivotPosition;
    private Vector3 pivotVelocity;

    private float rotX = 0f;
    private float rotY = 15f;
    private bool canSelectWarheads = false;

    private void Start()
    {
        currentTarget = rocketTarget;

        if (currentTarget != null)
        {
            currentPivotPosition = currentTarget.position + (Vector3.up * heightOffset);
            Vector3 angles = transform.eulerAngles;
            rotX = angles.y;
            rotY = angles.x;
        }
    }

    public void EnableWarheadSelection()
    {
        canSelectWarheads = true;
        Debug.Log("🎯 WARHEAD SELECTION ENABLED! Press 1, 2, 3, 4 to focus on Nukes.");
    }

    private void Update()
    {
        HandleTargetSelectionInput();
    }

    // CRITICAL: LateUpdate ensures camera updates AFTER physics and rocket movement!
    private void LateUpdate()
    {
        HandleCameraControlsAndTracking();
    }

    private void HandleTargetSelectionInput()
    {
        // Press 0 or R to return to Main Rocket
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.R))
        {
            if (rocketTarget != null)
            {
                currentTarget = rocketTarget;
                Debug.Log("🎥 Camera smoothly gliding back to MAIN ROCKET.");
            }
        }

        // Press 1, 2, 3, 4 to switch focus to Nukes
        if (warheadTargets != null && warheadTargets.Length > 0)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) && warheadTargets.Length >= 1) SwitchTarget(warheadTargets[0], 1);
            if (Input.GetKeyDown(KeyCode.Alpha2) && warheadTargets.Length >= 2) SwitchTarget(warheadTargets[1], 2);
            if (Input.GetKeyDown(KeyCode.Alpha3) && warheadTargets.Length >= 3) SwitchTarget(warheadTargets[2], 3);
            if (Input.GetKeyDown(KeyCode.Alpha4) && warheadTargets.Length >= 4) SwitchTarget(warheadTargets[3], 4);
        }
    }

    private void SwitchTarget(Transform newTarget, int nukeNumber)
    {
        if (newTarget == null)
        {
            Debug.LogWarning($"⚠️ Nuke #{nukeNumber} reference is missing!");
            return;
        }

        currentTarget = newTarget;
        canSelectWarheads = true;
        Debug.Log($"🎥 Camera gliding to focus on NUKE #{nukeNumber}");
    }

    private void HandleCameraControlsAndTracking()
    {
        if (currentTarget == null) return;

        // 1. Orbit Controls (Right Mouse Button Drag)
        if (Input.GetMouseButton(1))
        {
            rotX += Input.GetAxis("Mouse X") * orbitSensitivity;
            rotY -= Input.GetAxis("Mouse Y") * orbitSensitivity;
            rotY = Mathf.Clamp(rotY, -85f, 85f);
        }

        // 2. Height Controls (Q = Down, E = Up)
        if (Input.GetKey(KeyCode.Q)) heightOffset -= heightSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E)) heightOffset += heightSpeed * Time.deltaTime;
        heightOffset = Mathf.Clamp(heightOffset, minHeight, maxHeight);

        // 3. Zoom Controls (Z = Zoom In, C = Zoom Out, plus Scroll Wheel)
        if (Input.GetKey(KeyCode.Z)) distance -= zoomSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.C)) distance += zoomSpeed * Time.deltaTime;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance -= scroll * zoomSpeed * 2f;
        }
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // 4. Smooth Pivot Tracking (Glides smoothly when switching targets)
        Vector3 targetPivotGoal = currentTarget.position + (Vector3.up * heightOffset);
        currentPivotPosition = Vector3.SmoothDamp(currentPivotPosition, targetPivotGoal, ref pivotVelocity, targetSwitchSmoothTime);

        // 5. Compute Final Position & Orientation
        Quaternion rotation = Quaternion.Euler(rotY, rotX, 0f);
        Vector3 finalPosition = currentPivotPosition - (rotation * Vector3.forward * distance);

        transform.rotation = rotation;
        transform.position = finalPosition;
    }
}