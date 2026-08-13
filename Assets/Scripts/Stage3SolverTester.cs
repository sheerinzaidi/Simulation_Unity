using UnityEngine;

/// <summary>
/// Test runner component for validating Stage 3 Trajectory Solver against the 3 required coordinate pairs.
/// Automatically executes solver and logs solved pitch angle, apogee altitude, flight time, and miss distance.
/// </summary>
[RequireComponent(typeof(Stage3TrajectorySolver))]
public class Stage3SolverTester : MonoBehaviour
{
    [Header("Run Tests Keybind")]
    [SerializeField] private KeyCode runTestKey = KeyCode.Alpha3;

    private Stage3TrajectorySolver solver;

    private void Awake()
    {
        solver = GetComponent<Stage3TrajectorySolver>();
    }

    private void Start()
    {
        RunAllThreeStage3Tests();
    }

    private void Update()
    {
        if (Input.GetKeyDown(runTestKey))
        {
            RunAllThreeStage3Tests();
        }
    }

    [ContextMenu("Run Stage 3 Tests")]
    public void RunAllThreeStage3Tests()
    {
        Debug.Log("<color=cyan>======================================================");
        Debug.Log("          STAGE 3 TRAJECTORY SOLVER TEST RUN          ");
        Debug.Log("======================================================</color>");

        // Test Pair 1: Regional / Short Range (Islamabad -> Karachi, ~1,140 km)
        GeoPoint launch1 = new GeoPoint(33.6167, 73.0667, 500.0);
        GeoPoint target1 = new GeoPoint(24.8607, 67.0011, 10.0);
        TrajectorySolveResult result1 = solver.SolveTrajectory(launch1, target1);

        // Test Pair 2: Intercontinental Long Range (Islamabad -> New York, ~11,000 km)
        GeoPoint launch2 = new GeoPoint(33.6167, 73.0667, 500.0);
        GeoPoint target2 = new GeoPoint(40.7128, -74.0060, 10.0);
        TrajectorySolveResult result2 = solver.SolveTrajectory(launch2, target2);

        // Test Pair 3: Polar Pair (Tromsø -> Ushuaia, ~14,000 km)
        GeoPoint launch3 = new GeoPoint(69.6492, 18.9553, 100.0);
        GeoPoint target3 = new GeoPoint(-54.8019, -68.3030, 10.0);
        TrajectorySolveResult result3 = solver.SolveTrajectory(launch3, target3);

        Debug.Log("<color=yellow>======================================================\n" +
                  "           SUMMARY OF SOLVED TRAJECTORIES             \n" +
                  "======================================================</color>\n" +
                  $"<b>1. Regional Short Range (Islamabad -> Karachi, ~1,140 km):</b>\n" +
                  $"   • Pitch Angle: {result1.launchPitchAngleDeg:F2}° | Azimuth: {result1.initialAzimuthDeg:F2}°\n" +
                  $"   • <b>APOGEE ALTITUDE: {result1.apogeeAltitudeKm:F1} km</b>\n" +
                  $"   • Total Flight Time: {result1.totalFlightTimeSec:F1} s\n" +
                  $"   • Miss Distance: {result1.missDistanceKm:F3} km (Converged: {result1.isConverged})\n\n" +
                  $"<b>2. Intercontinental Long Range (Islamabad -> New York, ~11,000 km):</b>\n" +
                  $"   • Pitch Angle: {result2.launchPitchAngleDeg:F2}° | Azimuth: {result2.initialAzimuthDeg:F2}°\n" +
                  $"   • <b>APOGEE ALTITUDE: {result2.apogeeAltitudeKm:F1} km</b>\n" +
                  $"   • Total Flight Time: {result2.totalFlightTimeSec:F1} s\n" +
                  $"   • Miss Distance: {result2.missDistanceKm:F3} km (Converged: {result2.isConverged})\n\n" +
                  $"<b>3. Polar Range Pair (Tromsø -> Ushuaia, ~14,000 km):</b>\n" +
                  $"   • Pitch Angle: {result3.launchPitchAngleDeg:F2}° | Azimuth: {result3.initialAzimuthDeg:F2}°\n" +
                  $"   • <b>APOGEE ALTITUDE: {result3.apogeeAltitudeKm:F1} km</b>\n" +
                  $"   • Total Flight Time: {result3.totalFlightTimeSec:F1} s\n" +
                  $"   • Miss Distance: {result3.missDistanceKm:F3} km (Converged: {result3.isConverged})\n" +
                  "======================================================");
    }
}
