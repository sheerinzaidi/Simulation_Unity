using UnityEngine;
using Unity.Mathematics;
#if CESIUM_PRESENT || true
using CesiumForUnity;
#endif

/// <summary>
/// Helper component for testing Stage 1 on a real Cesium Globe scene in Unity Editor.
/// Pulls true Earth center directly from CesiumGeoreference and logs real-time telemetry.
/// </summary>
[RequireComponent(typeof(Stage1CorePhysicsBody))]
public class Stage1Tester : MonoBehaviour
{
    [Header("Controls")]
    [SerializeField] private KeyCode toggleThrustKey = KeyCode.Space;
    [SerializeField] private KeyCode logTelemetryKey = KeyCode.T;

    private Stage1CorePhysicsBody physicsBody;
    private Rigidbody rb;

    private void Awake()
    {
        physicsBody = GetComponent<Stage1CorePhysicsBody>();
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleThrustKey))
        {
            physicsBody.IsThrusting = !physicsBody.IsThrusting;
            Debug.Log($"<color=green>[Stage 1 Test]</color> Thrust state toggled to: {physicsBody.IsThrusting}");
        }

        if (Input.GetKeyDown(logTelemetryKey))
        {
            LogTelemetry();
        }
    }

    private void LogTelemetry()
    {
        Vector3 centerPos = physicsBody.EarthCenterPosition;
        Vector3 gravityVector = (centerPos - transform.position).normalized;
        float distanceToCenter = Vector3.Distance(transform.position, centerPos);

        string cesiumInfo = "CesiumGeoreference attached";
        if (physicsBody.CesiumGeo != null)
        {
            double3 ecefPos = physicsBody.CesiumGeo.TransformUnityPositionToEarthCenteredEarthFixed(
                new double3(transform.position.x, transform.position.y, transform.position.z)
            );
            double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecefPos);
            cesiumInfo = $"Lat: {llh.y:F4}°, Lon: {llh.x:F4}°, Alt: {llh.z:F1} m";
        }

        Debug.Log($"<color=yellow>[Stage 1 Telemetry]</color>\n" +
                  $"• Mass: {physicsBody.TotalMass:F1} kg (Propellant: {physicsBody.PropellantMass:F1} kg)\n" +
                  $"• Speed: {rb.linearVelocity.magnitude:F2} m/s\n" +
                  $"• Gravity Dir (Toward Cesium ECEF (0,0,0)): {gravityVector}\n" +
                  $"• Distance to Earth Center: {distanceToCenter:F1} m\n" +
                  $"• Globe Location: {cesiumInfo}");
    }

    private void OnDrawGizmos()
    {
        if (physicsBody != null)
        {
            Vector3 centerPos = physicsBody.EarthCenterPosition;
            
            // Draw ray toward Cesium Earth center
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, (centerPos - transform.position).normalized * 50f);

            // Draw local thrust vector
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.up * 20f);
        }
    }
}
