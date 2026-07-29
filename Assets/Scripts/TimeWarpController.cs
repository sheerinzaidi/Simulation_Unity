using UnityEngine;

/// <summary>
/// Controls KSP-style physical time warp (1x, 2x, 5x, 10x) for physics testing.
/// Dynamically updates fixedDeltaTime to maintain physics integration stability.
/// </summary>
public class TimeWarpController : MonoBehaviour
{
    [Header("Warp Configuration")]
    [SerializeField] private float[] warpFactors = new float[] { 1f, 2f, 5f, 10f };
    private int currentWarpIndex = 0;

    private float defaultFixedDeltaTime;

    private void Awake()
    {
        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void Update()
    {
        // KSP-style Keybindings: '.' to increase warp, ',' to decrease warp
        if (Input.GetKeyDown(KeyCode.Period) || Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            SetWarpIndex(currentWarpIndex + 1);
        }
        else if (Input.GetKeyDown(KeyCode.Comma) || Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            SetWarpIndex(currentWarpIndex - 1);
        }
    }

    private void SetWarpIndex(int targetIndex)
    {
        currentWarpIndex = Mathf.Clamp(targetIndex, 0, warpFactors.Length - 1);
        float targetScale = warpFactors[currentWarpIndex];

        Time.timeScale = targetScale;
        // Scale fixedDeltaTime proportionally to maintain physics accuracy
        Time.fixedDeltaTime = defaultFixedDeltaTime * targetScale;

        Debug.Log($"<color=cyan>[TimeWarp]</color> Warp factor set to: {targetScale}x | FixedDeltaTime: {Time.fixedDeltaTime:F4}s");
    }

    private void OnDisable()
    {
        // Safety reset on disable/destroy
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;
    }
}