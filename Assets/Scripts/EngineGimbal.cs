using UnityEngine;

/// <summary>
/// Controls Thrust Vector Control (TVC) nozzle swiveling.
/// Supports dynamic re-anchoring when stage handoffs occur.
/// </summary>
public class EngineGimbal : MonoBehaviour
{
    [Header("Gimbal Settings")]
    [Tooltip("Maximum nozzle pitch/yaw angle in degrees.")]
    [SerializeField] private float maxGimbalAngle = 5.0f;

    [Tooltip("Speed of hydraulic actuator response.")]
    [SerializeField] private float gimbalSpeed = 15.0f;

    [Tooltip("Active engine nozzle transform being rotated.")]
    [SerializeField] private Transform activeNozzleTransform;

    private Quaternion initialLocalRotation;
    private Vector2 currentGimbalInput;

    private void Awake()
    {
        if (activeNozzleTransform != null)
        {
            initialLocalRotation = activeNozzleTransform.localRotation;
        }
    }

    private void Update()
    {
        if (activeNozzleTransform == null) return;

        // Calculate target swivel orientation from input values
        float targetPitch = currentGimbalInput.x * maxGimbalAngle;
        float targetYaw = currentGimbalInput.y * maxGimbalAngle;

        Quaternion targetRotation = initialLocalRotation * Quaternion.Euler(targetPitch, 0f, targetYaw);

        // Smoothly interpolate nozzle transform
        activeNozzleTransform.localRotation = Quaternion.Slerp(
            activeNozzleTransform.localRotation, 
            targetRotation, 
            Time.deltaTime * gimbalSpeed
        );
    }

    public void SetGimbalInputs(float pitch, float yaw)
    {
        currentGimbalInput.x = Mathf.Clamp(pitch, -1f, 1f);
        currentGimbalInput.y = Mathf.Clamp(yaw, -1f, 1f);
    }

    /// <summary>
    /// Dynamic handoff to direct TVC actuation to the new upper stage nozzle transform.
    /// </summary>
    public void UpdateGimbalTarget(Transform newNozzleTransform)
    {
        if (newNozzleTransform == null) return;

        activeNozzleTransform = newNozzleTransform;
        initialLocalRotation = activeNozzleTransform.localRotation;
        Debug.Log($"<color=green>[EngineGimbal]</color> TVC Actuator target updated to: {activeNozzleTransform.name}");
    }
}