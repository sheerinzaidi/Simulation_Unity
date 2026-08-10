using UnityEngine;

/// <summary>
/// Manages switching between multiple cinematic cameras during flight phases.
/// Prevents camera panicking and target snapping during staging events.
/// </summary>
public class CinematicCameraManager : MonoBehaviour
{
    [Header("Camera References")]
    [SerializeField] private Camera launchCam;
    [SerializeField] private Camera flightCam;
    [SerializeField] private Camera stagingCam;
    [SerializeField] private Camera warheadCam;

    private void Start()
    {
        // Default to Launch Camera at start
        ActivateCamera(launchCam);
    }

    public void ActivateLaunchCam() => ActivateCamera(launchCam);
    public void ActivateFlightCam() => ActivateCamera(flightCam);
    public void ActivateStagingCam() => ActivateCamera(stagingCam);
    public void ActivateWarheadCam() => ActivateCamera(warheadCam);

    private void ActivateCamera(Camera targetCam)
    {
        if (targetCam == null) return;

        // Disable all cameras first
        if (launchCam != null) launchCam.gameObject.SetActive(false);
        if (flightCam != null) flightCam.gameObject.SetActive(false);
        if (stagingCam != null) stagingCam.gameObject.SetActive(false);
        if (warheadCam != null) warheadCam.gameObject.SetActive(false);

        // Turn on the selected camera
        targetCam.gameObject.SetActive(true);
        Debug.Log($"🎥 SWITCHED CAMERA TO: {targetCam.name}");
    }
}