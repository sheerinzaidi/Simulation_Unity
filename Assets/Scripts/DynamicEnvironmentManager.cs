using UnityEngine;

/// <summary>
/// Strictly controls Atmosphere, Fog, and Skybox transitions.
/// Dynamically evaluates the world altitude of whichever camera is currently active.
/// Works bi-directionally (going up into space AND coming back down to Earth).
/// DOES NOT TOUCH CAMERA POSITIONS OR ROTATIONS.
/// </summary>
public class DynamicEnvironmentManager : MonoBehaviour
{
    [Header("Altitude Boundaries (Meters)")]
    [SerializeField] private float seaLevelY = 0f;
    [SerializeField] private float spaceTransitionAltitude = 80000f; // 80 km space height

    [Header("Skybox Materials")]
    [SerializeField] private Material atmosphereSkybox;
    [SerializeField] private Material spaceSkybox;

    [Header("Lighting Settings")]
    [SerializeField] private Color atmosphereAmbientColor = new Color(0.6f, 0.7f, 0.8f);
    [SerializeField] private Color spaceAmbientColor = new Color(0.02f, 0.02f, 0.05f);

    [Header("Clean Fog Calibration")]
    [SerializeField] private Color atmosphereFogColor = new Color(0.65f, 0.75f, 0.85f);
    [SerializeField] private float groundFogStart = 2000f;  // Crisp ground view
    [SerializeField] private float groundFogEnd = 50000f;

    [Header("Optional Sun Light")]
    [SerializeField] private Light mainSunLight;

    private void Start()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = atmosphereFogColor;
        RenderSettings.fogStartDistance = groundFogStart;
        RenderSettings.fogEndDistance = groundFogEnd;
    }

    private void Update()
    {
        // Get currently active camera in scene
        Camera activeCam = Camera.main;
        if (activeCam == null) return;

        // Calculate active camera altitude relative to ground
        float cameraAltitude = Mathf.Max(0f, activeCam.transform.position.y - seaLevelY);
        float transitionProgress = Mathf.Clamp01(cameraAltitude / spaceTransitionAltitude);

        // 1. Dynamic Skybox Swap (Atmosphere <-> Space)
        if (transitionProgress < 0.5f)
        {
            if (RenderSettings.skybox != atmosphereSkybox && atmosphereSkybox != null)
            {
                RenderSettings.skybox = atmosphereSkybox;
            }
        }
        else
        {
            if (RenderSettings.skybox != spaceSkybox && spaceSkybox != null)
            {
                RenderSettings.skybox = spaceSkybox;
            }
        }

        // 2. Dynamic Ambient Light Adjustment
        RenderSettings.ambientLight = Color.Lerp(atmosphereAmbientColor, spaceAmbientColor, transitionProgress);

        // 3. Dynamic Fog Adjustment (Turns back ON when camera descends to Earth!)
        if (transitionProgress >= 0.85f)
        {
            RenderSettings.fog = false; // Zero fog in deep space
        }
        else
        {
            RenderSettings.fog = true;
            RenderSettings.fogStartDistance = Mathf.Lerp(groundFogStart, 40000f, transitionProgress);
            RenderSettings.fogEndDistance = Mathf.Lerp(groundFogEnd, 200000f, transitionProgress);
            RenderSettings.fogColor = Color.Lerp(atmosphereFogColor, Color.black, transitionProgress);
        }

        // 4. Sun Intensity Adjustment
        if (mainSunLight != null)
        {
            mainSunLight.intensity = Mathf.Lerp(1.0f, 1.4f, transitionProgress);
        }
    }
}