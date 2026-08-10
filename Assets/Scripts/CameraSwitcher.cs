using UnityEngine;
using CesiumForUnity;

/// <summary>
/// Cesium-Aware Camera Switcher with Dual-Location Background Preloading.
/// Keeps Target Overview Camera streaming tiles continuously to prevent load glitches.
/// Hotkeys: R / 0 (Main Rocket), 1-4 (Warheads 1-4), 5 / T (Target Overview).
/// </summary>
public class CameraSwitcher : MonoBehaviour
{
    [Header("Camera References")]
    [Tooltip("The static camera child attached inside ICBM_Root.")]
    [SerializeField] private Camera mainRocketCamera;

    [Tooltip("The static cameras attached inside each Warhead (1 to 4).")]
    [SerializeField] private Camera[] warheadCameras;

    [Tooltip("High-rise camera overlooking the target environment.")]
    [SerializeField] private Camera targetOverviewCamera;

    private Camera currentActiveCamera;
    public Camera CurrentActiveCamera => currentActiveCamera;

    private RenderTexture backgroundPreloadTexture;

    private void Start()
    {
        // 1. Create a tiny off-screen texture so Target Cam can stream tiles silently
        backgroundPreloadTexture = new RenderTexture(16, 16, 16);
        
        // 2. Enable background tile pre-streaming for target area
        if (targetOverviewCamera != null)
        {
            targetOverviewCamera.targetTexture = backgroundPreloadTexture;
            targetOverviewCamera.enabled = true;
            targetOverviewCamera.gameObject.SetActive(true);

            // Ensure CesiumOriginShift is present
            if (targetOverviewCamera.GetComponent<CesiumOriginShift>() == null)
            {
                targetOverviewCamera.gameObject.AddComponent<CesiumOriginShift>();
            }
        }

        // 3. Start game focused on main rocket
        ActivateMainCamera();
    }

    private void Update()
    {
        // Press R or 0 to switch back to Main Rocket View
        if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Alpha0))
        {
            ActivateMainCamera();
        }

        // Press 1, 2, 3, 4 to switch to Warhead Cameras
        if (warheadCameras != null && warheadCameras.Length > 0)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) && warheadCameras.Length >= 1) ActivateWarheadCamera(0);
            if (Input.GetKeyDown(KeyCode.Alpha2) && warheadCameras.Length >= 2) ActivateWarheadCamera(1);
            if (Input.GetKeyDown(KeyCode.Alpha3) && warheadCameras.Length >= 3) ActivateWarheadCamera(2);
            if (Input.GetKeyDown(KeyCode.Alpha4) && warheadCameras.Length >= 4) ActivateWarheadCamera(3);
        }

        // Press 5 or T to switch to Target Overview View
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.T))
        {
            ActivateTargetCamera();
        }
    }

    public void ActivateMainCamera()
    {
        if (mainRocketCamera == null) return;
        SetCameraActive(mainRocketCamera);
        Debug.Log("🎥 Active View: MAIN ROCKET CAMERA [R / 0]");
    }

    public void ActivateWarheadCamera(int index)
    {
        if (warheadCameras == null || index < 0 || index >= warheadCameras.Length) return;
        if (warheadCameras[index] == null) return;

        SetCameraActive(warheadCameras[index]);
        Debug.Log($"🎥 Active View: NUKE #{index + 1} CAMERA [{index + 1}]");
    }

    public void ActivateTargetCamera()
    {
        if (targetOverviewCamera == null) return;

        // Release off-screen texture so Target Cam renders directly to screen
        targetOverviewCamera.targetTexture = null;
        SetCameraActive(targetOverviewCamera);
        Debug.Log("🎥 Active View: TARGET OVERVIEW CAMERA [5 / T]");
    }

    private void SetCameraActive(Camera targetCamera)
    {
        DisableAllCamerasExceptPreload();

        targetCamera.gameObject.tag = "MainCamera";
        targetCamera.nearClipPlane = 0.5f;
        targetCamera.farClipPlane = 10000000f; // 10,000,000 meters

        if (targetCamera.GetComponent<CesiumOriginShift>() == null)
        {
            targetCamera.gameObject.AddComponent<CesiumOriginShift>();
        }

        targetCamera.enabled = true;
        targetCamera.gameObject.SetActive(true);
        currentActiveCamera = targetCamera;
    }

    private void DisableAllCamerasExceptPreload()
    {
        if (mainRocketCamera != null && mainRocketCamera != currentActiveCamera)
        {
            mainRocketCamera.tag = "Untagged";
            mainRocketCamera.enabled = false;
        }

        if (warheadCameras != null)
        {
            foreach (Camera cam in warheadCameras)
            {
                if (cam != null && cam != currentActiveCamera)
                {
                    cam.tag = "Untagged";
                    cam.enabled = false;
                }
            }
        }

        // Keep Target Overview Cam active in background if it's not the main view
        if (targetOverviewCamera != null && targetOverviewCamera != currentActiveCamera)
        {
            targetOverviewCamera.tag = "Untagged";
            targetOverviewCamera.targetTexture = backgroundPreloadTexture;
            targetOverviewCamera.enabled = true;
        }
    }

    private void OnDestroy()
    {
        if (backgroundPreloadTexture != null)
        {
            backgroundPreloadTexture.Release();
            Destroy(backgroundPreloadTexture);
        }
    }
}