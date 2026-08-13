using UnityEngine;
using Unity.Mathematics;
#if CESIUM_PRESENT || true
using CesiumForUnity;
#endif

/// <summary>
/// Geodetic Coordinate Point (WGS84)
/// </summary>
[System.Serializable]
public struct GeoPoint
{
    [Range(-90f, 90f)] public double latitude;   // Degrees (-90 to +90)
    [Range(-180f, 180f)] public double longitude; // Degrees (-180 to +180)
    public double heightMeters;                    // WGS84 Altitude in meters

    public GeoPoint(double lat, double lon, double alt = 0.0)
    {
        latitude = lat;
        longitude = lon;
        heightMeters = alt;
    }

    public override string ToString() => $"Lat: {latitude:F4}°, Lon: {longitude:F4}°, Alt: {heightMeters:F1} m";
}

/// <summary>
/// Stage 2: Globe Coordinates and Dynamic Launch/Target Selection.
/// Converts WGS84 Lat/Long/Altitude <-> Unity World Space via CesiumGeoreference.
/// Computes Great-Circle Distance and Initial Bearing (Azimuth) between any two points on Earth.
/// Aligns rocket orientation toward the target heading.
/// </summary>
public class Stage2GlobeController : MonoBehaviour
{
    [Header("Cesium Georeference Source of Truth")]
    [SerializeField] private CesiumGeoreference cesiumGeoreference;

    [Header("Launch Site Selection")]
    [SerializeField] private GeoPoint launchPoint = new GeoPoint(33.6167, 73.0667, 500.0); // Rawalpindi/Islamabad approx

    [Header("Target Site Selection")]
    [SerializeField] private GeoPoint targetPoint = new GeoPoint(24.8607, 67.0011, 10.0);   // Karachi approx (Regional short range)

    [Header("Computed Flight Geodesic Telemetry")]
    [SerializeField] private float greatCircleDistanceKm;
    [SerializeField] private float initialBearingDegrees;
    [SerializeField] private Vector3 launchWorldPosition;
    [SerializeField] private Vector3 targetWorldPosition;
    [SerializeField] private Vector3 initialTargetHeadingVector;

    public CesiumGeoreference CesiumGeo => cesiumGeoreference;
    public GeoPoint LaunchPoint { get => launchPoint; set => launchPoint = value; }
    public GeoPoint TargetPoint { get => targetPoint; set => targetPoint = value; }
    public float GreatCircleDistanceKm => greatCircleDistanceKm;
    public float InitialBearingDegrees => initialBearingDegrees;

    private void Awake()
    {
        if (cesiumGeoreference == null)
        {
            cesiumGeoreference = FindObjectOfType<CesiumGeoreference>();
        }
    }

    private void Start()
    {
        RecalculateTrajectory();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && cesiumGeoreference != null)
        {
            RecalculateTrajectory();
        }
    }

    /// <summary>
    /// Recalculates Great-Circle distance, initial azimuth bearing, and world positions using Cesium.
    /// </summary>
    public void RecalculateTrajectory()
    {
        if (cesiumGeoreference == null)
        {
            cesiumGeoreference = FindObjectOfType<CesiumGeoreference>();
        }

        if (cesiumGeoreference == null) return;

        // 1. Convert Launch & Target WGS84 -> Unity World Positions
        launchWorldPosition = GeoPointToUnityWorld(launchPoint);
        targetWorldPosition = GeoPointToUnityWorld(targetPoint);

        // 2. Compute Great-Circle Distance
        double distMeters = ComputeGreatCircleDistance(launchPoint, targetPoint);
        greatCircleDistanceKm = (float)(distMeters / 1000.0);

        // 3. Compute Initial Bearing (Azimuth)
        initialBearingDegrees = (float)ComputeInitialBearing(launchPoint, targetPoint);

        // 4. Compute Horizontal Target Direction Vector at Launch Site
        Vector3 earthCenterWorld = TransformEcefToUnityWorld(double3.zero);
        Vector3 localUp = (launchWorldPosition - earthCenterWorld).normalized;
        Vector3 directTargetVector = (targetWorldPosition - launchWorldPosition).normalized;
        initialTargetHeadingVector = Vector3.ProjectOnPlane(directTargetVector, localUp).normalized;

        Debug.Log($"<color=cyan>[Stage 2 Globe]</color> Launch: [{launchPoint}] -> Target: [{targetPoint}]\n" +
                  $"• Great-Circle Distance: {greatCircleDistanceKm:F1} km\n" +
                  $"• Initial Azimuth Bearing: {initialBearingDegrees:F2}°\n" +
                  $"• Heading Vector: {initialTargetHeadingVector}");
    }

    /// <summary>
    /// Converts a WGS84 GeoPoint to Unity World Coordinates via Cesium ECEF.
    /// </summary>
    public Vector3 GeoPointToUnityWorld(GeoPoint point)
    {
        if (cesiumGeoreference == null) return Vector3.zero;
        double3 ecefPos = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(
            new double3(point.longitude, point.latitude, point.heightMeters)
        );
        double3 unityPosD = cesiumGeoreference.TransformEarthCenteredEarthFixedPositionToUnity(ecefPos);
        return new Vector3((float)unityPosD.x, (float)unityPosD.y, (float)unityPosD.z);
    }

    /// <summary>
    /// Converts Unity World Coordinates to WGS84 GeoPoint via Cesium ECEF.
    /// </summary>
    public GeoPoint UnityWorldToGeoPoint(Vector3 worldPos)
    {
        if (cesiumGeoreference == null) return new GeoPoint();
        double3 ecefPos = cesiumGeoreference.TransformUnityPositionToEarthCenteredEarthFixed(
            new double3(worldPos.x, worldPos.y, worldPos.z)
        );
        double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecefPos);
        return new GeoPoint(llh.y, llh.x, llh.z);
    }

    private Vector3 TransformEcefToUnityWorld(double3 ecef)
    {
        if (cesiumGeoreference == null) return Vector3.zero;
        double3 unityPosD = cesiumGeoreference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
        return new Vector3((float)unityPosD.x, (float)unityPosD.y, (float)unityPosD.z);
    }

    /// <summary>
    /// Great-Circle distance using Haversine formula on WGS84 sphere (R = 6,371,000 m).
    /// </summary>
    public static double ComputeGreatCircleDistance(GeoPoint p1, GeoPoint p2)
    {
        double r = 6371000.0; // Mean Earth radius in meters
        double lat1Rad = p1.latitude * Mathf.Deg2Rad;
        double lat2Rad = p2.latitude * Mathf.Deg2Rad;
        double deltaLat = (p2.latitude - p1.latitude) * Mathf.Deg2Rad;
        double deltaLon = (p2.longitude - p1.longitude) * Mathf.Deg2Rad;

        double a = Mathd.Sin(deltaLat / 2.0) * Mathd.Sin(deltaLat / 2.0) +
                   Mathd.Cos(lat1Rad) * Mathd.Cos(lat2Rad) *
                   Mathd.Sin(deltaLon / 2.0) * Mathd.Sin(deltaLon / 2.0);

        double c = 2.0 * Mathd.Atan2(Mathd.Sqrt(a), Mathd.Sqrt(1.0 - a));
        return r * c;
    }

    /// <summary>
    /// Initial Bearing / Azimuth calculation between two WGS84 points (0° North, 90° East).
    /// </summary>
    public static double ComputeInitialBearing(GeoPoint p1, GeoPoint p2)
    {
        double lat1Rad = p1.latitude * Mathf.Deg2Rad;
        double lat2Rad = p2.latitude * Mathf.Deg2Rad;
        double deltaLonRad = (p2.longitude - p1.longitude) * Mathf.Deg2Rad;

        double y = Mathd.Sin(deltaLonRad) * Mathd.Cos(lat2Rad);
        double x = Mathd.Cos(lat1Rad) * Mathd.Sin(lat2Rad) -
                   Mathd.Sin(lat1Rad) * Mathd.Cos(lat2Rad) * Mathd.Cos(deltaLonRad);

        double bearingRad = Mathd.Atan2(y, x);
        double bearingDeg = bearingRad * Mathf.Rad2Deg;
        return (bearingDeg + 360.0) % 360.0;
    }

    private void OnDrawGizmos()
    {
        if (cesiumGeoreference != null)
        {
            Vector3 launchPos = GeoPointToUnityWorld(launchPoint);
            Vector3 targetPos = GeoPointToUnityWorld(targetPoint);

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(launchPos, 50f);

            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(targetPos, 50f);

            // Draw direct line connecting launch and target
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(launchPos, targetPos);
        }
    }
}

/// <summary>
/// Math helper for double precision trigonometric functions.
/// </summary>
public static class Mathd
{
    public static double Sin(double rad) => System.Math.Sin(rad);
    public static double Cos(double rad) => System.Math.Cos(rad);
    public static double Sqrt(double val) => System.Math.Sqrt(val);
    public static double Atan2(double y, double x) => System.Math.Atan2(y, x);
}
