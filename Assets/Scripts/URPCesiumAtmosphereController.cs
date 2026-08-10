using UnityEngine;
using Unity.Mathematics;
using CesiumForUnity;

/// <summary>
/// Camera-Aware Atmospheric & Fog Controller for URP & Cesium for Unity.
/// Dynamically calculates real WGS84 globe altitude and applies realistic exponential atmospheric decay.
/// </summary>
public class URPCesiumAtmosphereController : MonoBehaviour
{
    [Header("Cesium Reference")]
    [SerializeField] private CesiumGeoreference cesiumGeoreference;

    [Header("Sky Material")]
    [SerializeField] private Material proceduralSkyMaterial;

    [Header("Sun Reference")]
    [SerializeField] private Light mainSunLight;

    [Header("Altitude Calibration (Meters)")]
    [SerializeField] private float seaLevelY = 0f;
    [SerializeField] private float spaceStartAltitude = 8000f;   // 8 km (Sky begins darkening)
    [SerializeField] private float spaceEndAltitude = 60000f;   // 60 km (99.9% atmospheric vacuum)

    [Header("Atmosphere Haze Color")]
    [SerializeField] private Color groundFogColor = new Color(0.55f, 0.65f, 0.75f, 1.0f);

    private readonly Color groundAmbient = new Color(0.41f, 0.51f, 0.60f);
    private readonly Color spaceAmbient = new Color(0.015f, 0.018f, 0.025f);
    private readonly Color groundSunColor = new Color(1.0f, 0.96f, 0.92f);
    private readonly Color spaceSunColor = new Color(1.0f, 1.0f, 1.0f);

    private Transform activeCameraTransform;

    private void Start()
    {
        if (cesiumGeoreference == null)
        {
            cesiumGeoreference = FindObjectOfType<CesiumGeoreference>();
        }
    }

    private void LateUpdate()
    {
        // 1. Find active camera in the scene
        Camera activeCam = Camera.main;
        if (activeCam == null)
        {
            activeCam = FindObjectOfType<Camera>();
        }

        if (activeCam == null || proceduralSkyMaterial == null) return;
        activeCameraTransform = activeCam.transform;

        // 2. Accurate WGS84 Altitude Calculation
        float cameraAltitude = GetTrueCameraAltitude();

        // 3. Exponential Atmospheric Decay (Realistic air density curve)
        float linearProgress = Mathf.Clamp01((cameraAltitude - spaceStartAltitude) / (spaceEndAltitude - spaceStartAltitude));
        float exponentialSpaceProgress = Mathf.Pow(linearProgress, 2.2f); // Realistic decay curve

        // 4. Update Skybox Material
        proceduralSkyMaterial.SetFloat("_AtmosphereThickness", Mathf.Lerp(1.0f, 0.0f, exponentialSpaceProgress));
        proceduralSkyMaterial.SetFloat("_Exposure", Mathf.Lerp(1.25f, 0.02f, exponentialSpaceProgress));

        // 5. Ambient & Sunlight Updates
        RenderSettings.ambientLight = Color.Lerp(groundAmbient, spaceAmbient, exponentialSpaceProgress);

        if (mainSunLight != null)
        {
            mainSunLight.intensity = Mathf.Lerp(1.0f, 1.35f, exponentialSpaceProgress);
            mainSunLight.color = Color.Lerp(groundSunColor, spaceSunColor, exponentialSpaceProgress);
        }

        // 6. Smooth, Pop-Free Fog Management
        UpdateFogTransition(cameraAltitude, exponentialSpaceProgress);
    }

    private float GetTrueCameraAltitude()
    {
        if (cesiumGeoreference != null)
        {
            // Step A: Convert Unity float position into double3
            double3 unityPosition = new double3(
                activeCameraTransform.position.x,
                activeCameraTransform.position.y,
                activeCameraTransform.position.z
            );

            // Step B: Transform Unity World Position -> ECEF Coordinates
            double3 ecefPosition = cesiumGeoreference.TransformUnityPositionToEarthCenteredEarthFixed(unityPosition);

            // Step C: Transform ECEF -> Longitude (X), Latitude (Y), Height above WGS84 Ellipsoid in Meters (Z)
            double3 llh = CesiumWgs84Ellipsoid.EarthCenteredEarthFixedToLongitudeLatitudeHeight(ecefPosition);

            return Mathf.Max(0f, (float)llh.z);
        }

        // Fallback for standard flat scenes if CesiumGeoreference isn't present
        return Mathf.Max(0f, activeCameraTransform.position.y - seaLevelY);
    }

    private void UpdateFogTransition(float altitude, float spaceProgress)
    {
        if (spaceProgress >= 0.95f)
        {
            RenderSettings.fog = false;
        }
        else
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;

            Color skyTint = Color.Lerp(groundFogColor, groundAmbient * 0.5f, spaceProgress);
            RenderSettings.fogColor = skyTint;

            RenderSettings.fogStartDistance = Mathf.Lerp(1000f, 50000f, spaceProgress);
            RenderSettings.fogEndDistance = Mathf.Lerp(30000f, 500000f, spaceProgress);
        }
    }

    private void OnApplicationQuit()
    {
        if (proceduralSkyMaterial != null)
        {
            proceduralSkyMaterial.SetFloat("_AtmosphereThickness", 1.0f);
            proceduralSkyMaterial.SetFloat("_Exposure", 1.25f);
        }
    }
}