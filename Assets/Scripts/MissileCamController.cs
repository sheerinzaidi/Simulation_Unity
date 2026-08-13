using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cinematic Tracking Camera for 4-Stage ICBM Rocket & Separate Nuke Cameras.
/// Supports 4 Rocket Stages (Keys 1-4), Separate Nuke Cameras (Keys 5-8),
/// and full Mouse Drag Orbit & Zoom controls for ALL cameras (Main & Nuke Cams).
/// </summary>
public class MissileCamController : MonoBehaviour
{
    [Header("Target Tracking")]
    [Tooltip("Drag your main ICBM / Rocket Root here initially to follow the entire rocket.")]
    [SerializeField] private Transform rocketRoot;
    [Tooltip("Currently tracked target transform.")]
    [SerializeField] private Transform currentTarget;

    [Header("Camera Smoothing & Stability")]
    [Tooltip("Check this to eliminate position lag & high-velocity jitter completely (1:1 lock).")]
    [SerializeField] private bool tightTracking = false;
    [SerializeField] private Vector3 offset = new Vector3(0f, 15f, -40f);
    [Tooltip("Smoothing factor for camera movement. Higher = tighter follow.")]
    [SerializeField] private float smoothSpeed = 20f;
    [Tooltip("Use critically damped SmoothDamp to prevent velocity overshoot.")]
    [SerializeField] private bool useSmoothDamp = true;

    [Header("Mouse Orbit Controls")]
    [Tooltip("Enable free mouse left-click drag orbit rotation for all cameras.")]
    [SerializeField] private bool allowMouseOrbit = true;
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float pitchMin = -85f;
    [SerializeField] private float pitchMax = 85f;
    [SerializeField] private float zoomSpeed = 10f;
    [SerializeField] private float minDistance = 5f;
    [SerializeField] private float maxDistance = 500f;

    [Header("Rocket Stages (Keys 1-4)")]
    [Tooltip("Drag Stage 1, Stage 2, Stage 3, and Stage 4 / PBV here in order (1 to 4).")]
    [SerializeField] private List<Transform> rocketStages = new List<Transform>();

    [Header("Separate Nuke Cameras (Keys 5-8)")]
    [Tooltip("Drag your separate Nuke Camera components or GameObjects here (Keys 5, 6, 7, 8).")]
    [SerializeField] private Camera[] nukeCameras = new Camera[4];

    [Tooltip("Alternatively drag Nuke Camera Transforms or Warhead Transforms here (Keys 5, 6, 7, 8).")]
    [SerializeField] private List<Transform> activeWarheads = new List<Transform>();

    private Camera mainTrackingCamera;
    private Camera currentActiveCamera;
    private float currentYaw = 0f;
    private float currentPitch = 15f;
    private float currentDistance = 40f;
    private Vector3 cameraVelocity = Vector3.zero;

    private void Awake()
    {
        mainTrackingCamera = GetComponent<Camera>();
        if (mainTrackingCamera == null)
        {
            mainTrackingCamera = Camera.main;
        }
        currentActiveCamera = mainTrackingCamera;
    }

    private void Start()
    {
        // Default camera target to the main rocket root (entire rocket), or currentTarget, or first stage
        if (currentTarget == null)
        {
            if (rocketRoot != null)
            {
                currentTarget = rocketRoot;
            }
            else
            {
                Transform defaultStage = GetStageTransform(0);
                if (defaultStage != null)
                {
                    currentTarget = defaultStage;
                }
            }
        }

        // Initialize distance and orbit angles
        currentDistance = offset.magnitude > 0.1f ? offset.magnitude : 40f;
        Vector3 initialAngles = transform.eulerAngles;
        currentYaw = initialAngles.y;
        currentPitch = initialAngles.x;

        // Auto-discover warheads in scene if activeWarheads is empty
        AutoFindWarheads();
    }

    private void LateUpdate()
    {
        // 1. Handle Input Switching (Keys 0-8)
        HandleInputSwitching();

        // 2. Handle Mouse Orbit Dragging & Zoom for whatever camera is active
        HandleMouseOrbit();

        // 3. Update Camera Position & Rotation
        UpdateActiveCameraTransform();
    }

    private void UpdateActiveCameraTransform()
    {
        if (currentActiveCamera == null) return;
        Transform camTransform = currentActiveCamera.transform;

        // Compute orbit rotation
        Quaternion orbitRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

        if (currentTarget != null && currentTarget != camTransform)
        {
            Vector3 offsetVector = orbitRotation * new Vector3(0f, 0f, -currentDistance);
            Vector3 desiredPosition = currentTarget.position + offsetVector;

            float dt = Time.unscaledDeltaTime;
            if (tightTracking || smoothSpeed <= 0f)
            {
                camTransform.position = desiredPosition;
            }
            else if (useSmoothDamp)
            {
                float smoothTime = smoothSpeed > 0.01f ? (1f / smoothSpeed) : 0.05f;
                camTransform.position = Vector3.SmoothDamp(camTransform.position, desiredPosition, ref cameraVelocity, smoothTime, Mathf.Infinity, dt);
            }
            else
            {
                float t = 1f - Mathf.Exp(-smoothSpeed * dt);
                camTransform.position = Vector3.Lerp(camTransform.position, desiredPosition, t);
            }

            // Zero-jitter rotation lock
            camTransform.rotation = orbitRotation;
        }
        else
        {
            // Rotate camera in place if no separate target object is attached
            camTransform.rotation = orbitRotation;
        }
    }

    private void HandleMouseOrbit()
    {
        if (!allowMouseOrbit) return;

        // Left mouse button drag to orbit around active target/camera
        if (Input.GetMouseButton(0))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            currentYaw += mouseX * mouseSensitivity;
            currentPitch -= mouseY * mouseSensitivity;
            currentPitch = Mathf.Clamp(currentPitch, pitchMin, pitchMax);
        }

        // Scroll wheel to zoom in / out
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            currentDistance -= scroll * zoomSpeed;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }
    }

    /// <summary>
    /// Sets a new target for the tracking camera to follow.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        if (newTarget != null)
        {
            currentTarget = newTarget;
            cameraVelocity = Vector3.zero;
            EnableMainTrackingCamera();
            SyncOrbitAnglesFromTransform(mainTrackingCamera.transform);
            Debug.Log($"📹 CAMERA TRACKING SWITCHED TO: {newTarget.name}");
        }
    }

    /// <summary>
    /// Registers a rocket stage transform (up to 4 stages total).
    /// </summary>
    public void RegisterStage(Transform stageTransform)
    {
        if (stageTransform != null && !rocketStages.Contains(stageTransform))
        {
            rocketStages.Add(stageTransform);
        }
    }

    /// <summary>
    /// Registers newly spawned nuke warheads/cameras so the camera can cycle between them.
    /// </summary>
    public void RegisterWarhead(Transform warheadTransform)
    {
        if (warheadTransform != null && !activeWarheads.Contains(warheadTransform))
        {
            activeWarheads.Add(warheadTransform);
            SetTarget(warheadTransform);
        }
    }

    /// <summary>
    /// Auto-scans scene for active WarheadControllers if activeWarheads list is missing references.
    /// </summary>
    public void AutoFindWarheads()
    {
        WarheadController[] found = FindObjectsOfType<WarheadController>();
        foreach (var wh in found)
        {
            if (wh != null && !activeWarheads.Contains(wh.transform))
            {
                activeWarheads.Add(wh.transform);
            }
        }
    }

    private void HandleInputSwitching()
    {
        // Key 0: Focus on entire rocket root
        if (Input.GetKeyDown(KeyCode.Alpha0) && rocketRoot != null)
        {
            SetTarget(rocketRoot);
        }

        // Press 1-4 to switch between rocket stage 1, stage 2, stage 3, and stage 4
        if (Input.GetKeyDown(KeyCode.Alpha1)) TrySwitchToStage(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) TrySwitchToStage(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) TrySwitchToStage(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) TrySwitchToStage(3);

        // Press 5-8 to switch to separate Nuke Cameras 1, 2, 3, and 4
        if (Input.GetKeyDown(KeyCode.Alpha5)) TrySwitchToNukeCamera(0);
        if (Input.GetKeyDown(KeyCode.Alpha6)) TrySwitchToNukeCamera(1);
        if (Input.GetKeyDown(KeyCode.Alpha7)) TrySwitchToNukeCamera(2);
        if (Input.GetKeyDown(KeyCode.Alpha8)) TrySwitchToNukeCamera(3);
    }

    private void TrySwitchToStage(int index)
    {
        Transform stageTarget = GetStageTransform(index);
        if (stageTarget != null)
        {
            SetTarget(stageTarget);
        }
    }

    private void TrySwitchToNukeCamera(int index)
    {
        // 1. Check if a dedicated separate Camera object is assigned in nukeCameras array
        if (nukeCameras != null && index >= 0 && index < nukeCameras.Length && nukeCameras[index] != null)
        {
            Transform warheadTarget = GetWarheadTransform(index);
            ActivateNukeCamera(index, warheadTarget);
            return;
        }

        // 2. Otherwise auto-find warhead transforms and follow with main tracking camera
        if (index >= activeWarheads.Count || activeWarheads[index] == null)
        {
            AutoFindWarheads();
        }

        Transform target = GetWarheadTransform(index);
        if (target != null)
        {
            SetTarget(target);
        }
        else
        {
            Debug.LogWarning($"⚠️ Nuke Camera / Warhead {index + 1} (Key {index + 5}) not found or assigned!");
        }
    }

    private void ActivateNukeCamera(int activeIndex, Transform warheadTarget)
    {
        // Disable main tracking camera
        if (mainTrackingCamera != null)
        {
            mainTrackingCamera.enabled = false;
        }

        // Cycle through nuke cameras: enable selected, disable others
        for (int i = 0; i < nukeCameras.Length; i++)
        {
            if (nukeCameras[i] != null)
            {
                bool isSelected = (i == activeIndex);
                nukeCameras[i].gameObject.SetActive(isSelected);
                nukeCameras[i].enabled = isSelected;
            }
        }

        Camera selectedNukeCam = nukeCameras[activeIndex];
        currentActiveCamera = selectedNukeCam;
        currentTarget = (warheadTarget != null) ? warheadTarget : selectedNukeCam.transform;

        cameraVelocity = Vector3.zero;
        SyncOrbitAnglesFromTransform(selectedNukeCam.transform);

        Debug.Log($"📹 SWITCHED TO SEPARATE NUKE CAMERA {activeIndex + 1}: {selectedNukeCam.name} (MOUSE ORBIT ACTIVE)");
    }

    private void EnableMainTrackingCamera()
    {
        // Re-enable main camera
        if (mainTrackingCamera != null)
        {
            mainTrackingCamera.enabled = true;
            currentActiveCamera = mainTrackingCamera;
        }

        // Disable all separate nuke cameras
        if (nukeCameras != null)
        {
            foreach (var cam in nukeCameras)
            {
                if (cam != null)
                {
                    cam.gameObject.SetActive(false);
                    cam.enabled = false;
                }
            }
        }
    }

    private void SyncOrbitAnglesFromTransform(Transform t)
    {
        if (t == null) return;
        Vector3 euler = t.eulerAngles;
        currentPitch = euler.x;
        if (currentPitch > 180f) currentPitch -= 360f;
        currentPitch = Mathf.Clamp(currentPitch, pitchMin, pitchMax);
        currentYaw = euler.y;
    }

    private Transform GetStageTransform(int index)
    {
        if (index >= 0 && index < rocketStages.Count && rocketStages[index] != null)
        {
            return rocketStages[index];
        }
        return null;
    }

    private Transform GetWarheadTransform(int index)
    {
        if (index >= 0 && index < activeWarheads.Count && activeWarheads[index] != null)
        {
            return activeWarheads[index];
        }
        return null;
    }
}