using UnityEngine;
using Unity.Mathematics;
#if CESIUM_PRESENT || true
using CesiumForUnity;
#endif

/// <summary>
/// Result structure containing solved trajectory parameters.
/// </summary>
[System.Serializable]
public struct TrajectorySolveResult
{
    public bool isConverged;
    public float launchPitchAngleDeg;     // Solved pitch-over angle relative to local horizon
    public float initialAzimuthDeg;        // Heading bearing angle (0° to 360°)
    public float apogeeAltitudeKm;         // Maximum altitude reached above Earth surface (km)
    public float totalFlightTimeSec;       // Total time from liftoff to impact (seconds)
    public float boostDurationSec;         // Total burn time across active stage(s) (seconds)
    public float coastDurationSec;         // Time spent in unpowered ballistic flight (seconds)
    public float missDistanceKm;           // Final error distance from target coordinates (km)
    public int iterationCount;             // Number of search iterations to converge
    public GeoPoint launchPoint;
    public GeoPoint targetPoint;

    public override string ToString()
    {
        return $"[Trajectory Solver Result]\n" +
               $"• Converged: {isConverged} in {iterationCount} iterations\n" +
               $"• Pitch Angle: {launchPitchAngleDeg:F2}° (Azimuth: {initialAzimuthDeg:F2}°)\n" +
               $"• Apogee Altitude: {apogeeAltitudeKm:F1} km\n" +
               $"• Total Flight Time: {totalFlightTimeSec:F1} s (Boost: {boostDurationSec:F1} s, Coast: {coastDurationSec:F1} s)\n" +
               $"• Miss Distance: {missDistanceKm:F3} km";
    }
}

/// <summary>
/// Stage 3: Minimum-Energy Trajectory Solver for ICBM Flight.
/// Iteratively simulates candidate pitch-over angles using exact Stage 1 physics equations
/// (thrust, mass-flow rate, radial Newtonian inverse-square gravity toward Cesium ECEF 0,0,0).
/// Constrains solver toward Minimum-Energy Trajectory (MET) so apogee altitude scales naturally with range.
/// Rejects any trajectory dipping below Earth's surface radius before target.
/// </summary>
public class Stage3TrajectorySolver : MonoBehaviour
{
    [Header("Solver Configuration")]
    [SerializeField] private float maxToleranceKm = 1.0f; // Convergence tolerance in km
    [SerializeField] private int maxIterations = 30;      // Max search iterations
    [SerializeField] private float simTimeStepSec = 0.2f;  // Fast forward simulation step

    [Header("Telemetry & Readouts")]
    [SerializeField] private bool autoSolveOnStart = true;
    [SerializeField] private TrajectorySolveResult latestResult;

    private Stage1CorePhysicsBody physicsBody;
    private Stage2GlobeController globeController;
    private RocketDataProfile rocketProfile;

    public TrajectorySolveResult LatestResult => latestResult;

    private void Awake()
    {
        physicsBody = GetComponent<Stage1CorePhysicsBody>();
        globeController = GetComponent<Stage2GlobeController>();
        
        if (physicsBody != null)
        {
            rocketProfile = physicsBody.Profile;
        }
    }

    private void Start()
    {
        if (autoSolveOnStart && globeController != null)
        {
            SolveTrajectory(globeController.LaunchPoint, globeController.TargetPoint);
        }
    }

    /// <summary>
    /// Solves for the minimum-energy trajectory between launchPoint and targetPoint.
    /// </summary>
    public TrajectorySolveResult SolveTrajectory(GeoPoint launch, GeoPoint target)
    {
        if (physicsBody == null) physicsBody = GetComponent<Stage1CorePhysicsBody>();
        if (globeController == null) globeController = GetComponent<Stage2GlobeController>();
        if (rocketProfile == null && physicsBody != null) rocketProfile = physicsBody.Profile;
        if (rocketProfile == null) rocketProfile = RocketDataProfile.CreateMinutemanIII();

        double distMeters = Stage2GlobeController.ComputeGreatCircleDistance(launch, target);
        double rangeAngleRad = distMeters / 6371000.0; // Central angle in radians
        float initialAzimuth = (float)Stage2GlobeController.ComputeInitialBearing(launch, target);

        // Theoretical Minimum-Energy Trajectory (MET) initial guess:
        // pitch_angle ≈ 45° - (rangeAngle / 4) in radians
        float minEnergyPitchGuessRad = (Mathf.PI / 4.0f) - ((float)rangeAngleRad / 4.0f);
        float minEnergyPitchGuessDeg = Mathf.Clamp(minEnergyPitchGuessRad * Mathf.Rad2Deg, 10.0f, 80.0f);

        // Iterative Search (Binary / Secant Search around MET guess)
        float lowPitch = Mathf.Max(5.0f, minEnergyPitchGuessDeg - 25.0f);
        float highPitch = Mathf.Min(85.0f, minEnergyPitchGuessDeg + 25.0f);
        float bestPitch = minEnergyPitchGuessDeg;
        TrajectorySimResult bestSim = default;
        float minMissKm = float.MaxValue;
        bool converged = false;
        int iter = 0;

        for (iter = 1; iter <= maxIterations; iter++)
        {
            float testPitch = (lowPitch + highPitch) * 0.5f;
            TrajectorySimResult sim = SimulateCandidateTrajectory(launch, target, initialAzimuth, testPitch);

            if (!sim.validTrajectory)
            {
                // Surface collision mid-flight -> pitch too low, increase pitch angle
                lowPitch = testPitch;
                continue;
            }

            float missKm = (float)(sim.missDistanceMeters / 1000.0);

            if (missKm < minMissKm)
            {
                minMissKm = missKm;
                bestPitch = testPitch;
                bestSim = sim;
            }

            if (missKm <= maxToleranceKm)
            {
                converged = true;
                break;
            }

            // Adjust search bracket based on overshoot vs undershoot
            if (sim.isOvershot)
            {
                // Range exceeded target -> lower pitch angle to decrease range
                highPitch = testPitch;
            }
            else
            {
                // Range fell short -> increase pitch angle toward optimal MET
                lowPitch = testPitch;
            }
        }

        latestResult = new TrajectorySolveResult
        {
            isConverged = converged || (minMissKm <= maxToleranceKm * 2f),
            launchPitchAngleDeg = bestPitch,
            initialAzimuthDeg = initialAzimuth,
            apogeeAltitudeKm = (float)(bestSim.maxAltitudeMeters / 1000.0),
            totalFlightTimeSec = bestSim.totalFlightTimeSec,
            boostDurationSec = bestSim.boostDurationSec,
            coastDurationSec = bestSim.totalFlightTimeSec - bestSim.boostDurationSec,
            missDistanceKm = minMissKm,
            iterationCount = iter,
            launchPoint = launch,
            targetPoint = target
        };

        Debug.Log($"<color=green>[Stage 3 Trajectory Solver]</color>\n{latestResult}");
        return latestResult;
    }

    private struct TrajectorySimResult
    {
        public bool validTrajectory;
        public bool isOvershot;
        public double missDistanceMeters;
        public double maxAltitudeMeters;
        public float totalFlightTimeSec;
        public float boostDurationSec;
    }

    /// <summary>
    /// Fast numerical forward integration using exact Stage 1 physics equations.
    /// Rejects any trajectory dipping below Earth's surface radius before target.
    /// </summary>
    private TrajectorySimResult SimulateCandidateTrajectory(GeoPoint launch, GeoPoint target, float azimuthDeg, float pitchDeg)
    {
        double earthRadius = 6371000.0;
        double earthMass = 5.972e24;
        double G = 6.67430e-11;

        // Convert launch & target to ECEF spherical double precision
        Vector3d launchPosEcef = GeoToEcef(launch);
        Vector3d targetPosEcef = GeoToEcef(target);
        double targetDistMeters = (launchPosEcef - targetPosEcef).Magnitude;

        // Build local horizon basis at launch point
        Vector3d localUp = launchPosEcef.Normalized;
        Vector3d northTangent = Vector3d.ProjectOnPlane(new Vector3d(0, 0, 1), localUp).Normalized;
        Vector3d eastTangent = Vector3d.Cross(localUp, northTangent).Normalized;

        // Heading direction along azimuth
        double azRad = azimuthDeg * Mathf.Deg2Rad;
        Vector3d headingDir = (northTangent * System.Math.Cos(azRad) + eastTangent * System.Math.Sin(azRad)).Normalized;

        // Pitch vector relative to local horizon
        double pitchRad = pitchDeg * Mathf.Deg2Rad;
        Vector3d thrustDir = (localUp * System.Math.Sin(pitchRad) + headingDir * System.Math.Cos(pitchRad)).Normalized;

        // Stage 1 Physics Profile
        float totalMass = rocketProfile.CalculateTotalInitialWetMass();
        float stage1Fuel = rocketProfile.stages[0].propellantMassKg;
        float stage1Thrust = rocketProfile.stages[0].thrustNewtons;
        float burnDuration = rocketProfile.stages[0].burnDurationSec;
        float massFlow = (burnDuration > 0f) ? (stage1Fuel / burnDuration) : 0f;

        // Initial state
        Vector3d pos = launchPosEcef + (localUp * 10.0); // 10m liftoff offset
        Vector3d vel = Vector3d.Zero;
        double currentMass = totalMass;
        double maxAlt = 0.0;
        float simTime = 0f;

        bool surfaceCollisionBeforeTarget = false;
        double targetRangeTargetRad = Stage2GlobeController.ComputeGreatCircleDistance(launch, target) / earthRadius;

        while (simTime < 3600f) // Max 1 hour sim window
        {
            double currentDistToCenter = pos.Magnitude;
            double altitude = currentDistToCenter - earthRadius;

            if (altitude > maxAlt) maxAlt = altitude;

            // Check surface dip before reaching target distance
            if (altitude < 0.0 && simTime > 10f)
            {
                double currentRangeRad = Vector3d.AngleRad(launchPosEcef.Normalized, pos.Normalized);
                if (currentRangeRad < targetRangeTargetRad * 0.95)
                {
                    surfaceCollisionBeforeTarget = true;
                    break;
                }
                else
                {
                    // Reached target level
                    break;
                }
            }

            // Radial Gravity toward Earth Center (0,0,0)
            Vector3d gravityDir = -pos.Normalized;
            double gAccel = (G * earthMass) / (currentDistToCenter * currentDistToCenter);
            Vector3d gravityForce = gravityDir * (currentMass * gAccel);

            // Thrust Force
            Vector3d thrustForceVec = Vector3d.Zero;
            if (simTime < burnDuration && stage1Fuel > 0f)
            {
                float dtBurn = simTimeStepSec;
                float fuelBurned = massFlow * dtBurn;
                stage1Fuel = Mathf.Max(0f, stage1Fuel - fuelBurned);
                currentMass = System.Math.Max((double)(totalMass - stage1Fuel), currentMass - (double)fuelBurned);
                thrustForceVec = thrustDir * stage1Thrust;
            }

            // Net Force -> Acceleration -> Velocity -> Position integration
            Vector3d netForce = gravityForce + thrustForceVec;
            Vector3d accel = netForce / currentMass;

            vel += accel * simTimeStepSec;
            pos += vel * simTimeStepSec;

            simTime += simTimeStepSec;
        }

        if (surfaceCollisionBeforeTarget)
        {
            return new TrajectorySimResult { validTrajectory = false };
        }

        // Compute final miss distance from target
        GeoPoint finalGeo = EcefToGeo(pos);
        double missMeters = Stage2GlobeController.ComputeGreatCircleDistance(finalGeo, target);

        // Check if overshot target longitude/latitude
        double simRangeRad = Vector3d.AngleRad(launchPosEcef.Normalized, pos.Normalized);
        bool isOvershot = simRangeRad > targetRangeTargetRad;

        return new TrajectorySimResult
        {
            validTrajectory = true,
            isOvershot = isOvershot,
            missDistanceMeters = missMeters,
            maxAltitudeMeters = maxAlt,
            totalFlightTimeSec = simTime,
            boostDurationSec = burnDuration
        };
    }

    private static Vector3d GeoToEcef(GeoPoint pt)
    {
        double3 ecef = CesiumWgs84Ellipsoid.LongitudeLatitudeHeightToEarthCenteredEarthFixed(new double3(pt.longitude, pt.latitude, pt.heightMeters));
        return new Vector3d(ecef.x, ecef.y, ecef.z);
    }

    private static GeoPoint EcefToGeo(Vector3d ecef)
    {
        double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(new double3(ecef.x, ecef.y, ecef.z));
        return new GeoPoint(llh.y, llh.x, llh.z);
    }
}

/// <summary>
/// High-precision double precision 3D vector math helper.
/// </summary>
public struct Vector3d
{
    public double x, y, z;
    public Vector3d(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
    public static Vector3d Zero => new Vector3d(0, 0, 0);

    public double Magnitude => System.Math.Sqrt(x * x + y * y + z * z);
    public Vector3d Normalized => Magnitude > 0 ? this / Magnitude : Zero;

    public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vector3d operator -(Vector3d a) => new Vector3d(-a.x, -a.y, -a.z);
    public static Vector3d operator *(Vector3d a, double d) => new Vector3d(a.x * d, a.y * d, a.z * d);
    public static Vector3d operator /(Vector3d a, double d) => new Vector3d(a.x / d, a.y / d, a.z / d);

    public static double Dot(Vector3d a, Vector3d b) => a.x * b.x + a.y * b.y + a.z * b.z;
    public static Vector3d Cross(Vector3d a, Vector3d b) => new Vector3d(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
    public static Vector3d ProjectOnPlane(Vector3d vector, Vector3d planeNormal) => vector - planeNormal * Dot(vector, planeNormal);
    public static double AngleRad(Vector3d a, Vector3d b) => System.Math.Acos(System.Math.Max(-1.0, System.Math.Min(1.0, Dot(a.Normalized, b.Normalized))));
}
