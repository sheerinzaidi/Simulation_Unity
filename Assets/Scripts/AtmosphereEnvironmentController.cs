using UnityEngine;

/// <summary>
/// Controls natural atmospheric transparency, skybox transitions, 
/// and realistic zero-density space boundaries.
/// </summary>
public class AtmosphereEnvironmentController : MonoBehaviour
{
    [Header("Tracking Target")]
    [SerializeField] private Transform trackingTarget;

    [Header("Altitude Calibration (Meters)")]
    [SerializeField] private float seaLevelY = 0f;
    [SerializeField] private float spaceBoundaryAltitude = 90000f; // 90 km

    [Header("Skybox Materials")]
    [SerializeField] private Material atmosphereSkybox;
    [SerializeField] private Material spaceSkybox;

    [Header("Realistic Ambient Lighting")]
    [SerializeField] private Color atmosphereAmbient = new Color(0.55f, 0.65f, 0.75f);
    [SerializeField] private Color spaceAmbient = new Color(0.02f, 0.02f, 0.04f);

    [Header("Linear Fog Calibration (Crisp & Realistic)")]
    [SerializeField] private Color fogColor = new Color(0.65f, 0.75f, 0.85f);
    [SerializeField] private float launchFogStartDistance = 1000f;  // Fog starts 1 km away
    [SerializeField] private float launchFogEndDistance = 40000f;   // Fog ends 40 km away

    [Header("Sun Directional Light")]
    [SerializeField] private Light mainSunLight;

    private void Start()
    {
        // Use Linear fog mode for crisp ground visuals
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = launchFogStartDistance;
        RenderSettings.fogEndDistance = launchFogEndDistance;
        RenderSettings.fogColor = fogColor;
    }

    private void Update()
    {
        if (trackingTarget == null) return;

        float currentAltitude = Mathf.Max(0f, trackingTarget.position.y - seaLevelY);
        float progress = Mathf.Clamp01(currentAltitude / spaceBoundaryAltitude);

        // 1. Swap Skyboxes near upper atmosphere boundary (~45 km)
        if (progress < 0.5f)
        {
            if (RenderSettings.skybox != atmosphereSkybox && atmosphereSkybox != null)
                RenderSettings.skybox = atmosphereSkybox;
        }
        else
        {
            if (RenderSettings.skybox != spaceSkybox && spaceSkybox != null)
                RenderSettings.skybox = spaceSkybox;
        }

        // 2. Smoothly fade ambient sky light to deep space shadows
        RenderSettings.ambientLight = Color.Lerp(atmosphereAmbient, spaceAmbient, progress);

        // 3. Push fog distance outward as missile climbs, then disable in vacuum space
        if (progress >= 0.85f)
        {
            RenderSettings.fog = false; // Zero fog in space
        }
        else
        {
            RenderSettings.fog = true;
            RenderSettings.fogStartDistance = Mathf.Lerp(launchFogStartDistance, 50000f, progress);
            RenderSettings.fogEndDistance = Mathf.Lerp(launchFogEndDistance, 200000f, progress);
            RenderSettings.fogColor = Color.Lerp(fogColor, Color.black, progress);
        }

        // 4. Intensify sunlight in space
        if (mainSunLight != null)
        {
            mainSunLight.intensity = Mathf.Lerp(1.0f, 1.35f, progress);
        }
    }
}