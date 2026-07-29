using UnityEngine;
using UnityEngine.SceneManagement;

public class FloatingOrigin : MonoBehaviour
{
    [Header("Threshold Settings")]
    [Tooltip("Distance from (0,0,0) that triggers world recentering (in meters).")]
    [SerializeField] private float threshold = 5000f;

    [Header("Target Tracking")]
    [SerializeField] private Transform targetToTrack;

    private void LateUpdate()
    {
        if (targetToTrack == null) return;

        // Check if tracking object is beyond threshold from world center
        if (targetToTrack.position.magnitude > threshold)
        {
            ShiftWorldOrigin();
        }
    }

    private void ShiftWorldOrigin()
    {
        Vector3 offset = targetToTrack.position;

        // Shift root objects in all active loaded scenes
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            GameObject[] rootObjects = scene.GetRootGameObjects();
            foreach (GameObject go in rootObjects)
            {
                // Subtract the offset to bring object positions back towards origin
                go.transform.position -= offset;
            }
        }

        // Shift TrailRenderers / Particle Systems if needed
        TrailRenderer[] trails = FindObjectsOfType<TrailRenderer>();
        foreach (TrailRenderer trail in trails)
        {
            Vector3[] positions = new Vector3[trail.positionCount];
            int count = trail.GetPositions(positions);
            for (int i = 0; i < count; i++)
            {
                positions[i] -= offset;
            }
            trail.SetPositions(positions);
        }

        Debug.Log($"<color=orange>[Floating Origin]</color> World recentered! Shift Offset: {offset}");
    }
}