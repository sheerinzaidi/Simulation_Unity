using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cinematic Tracking Camera for ICBM & MIRV Warheads.
/// Smoothly follows and rotates around rockets, PBVs, and descending warheads.
/// </summary>
public class MissileCamController : MonoBehaviour
{
    [Header("Target Tracking")]
    [Tooltip("Drag your ICBM_Root here initially.")]
    [SerializeField] private Transform currentTarget;

    [Header("Camera Distance & Angle")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 15f, -40f);
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private float rotationSmoothSpeed = 5f;

    [Header("Active Warheads (Auto-Populated)")]
    [SerializeField] private List<Transform> activeWarheads = new List<Transform>();

    private void LateUpdate()
    {
        if (currentTarget == null) return;

        // 1. Calculate camera position relative to target orientation
        Vector3 desiredPosition = currentTarget.position + (currentTarget.rotation * offset);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // 2. Smoothly look at target
        Quaternion targetRotation = Quaternion.LookRotation(currentTarget.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.deltaTime);

        // 3. Manual view switching controls
        HandleInputSwitching();
    }

    /// <summary>
    /// Sets a new object for the camera to follow (e.g., PBV, Warhead 1, Warhead 2).
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        if (newTarget != null)
        {
            currentTarget = newTarget;
            Debug.Log($"📹 CAMERA TRACKING SWITCHED TO: {newTarget.name}");
        }
    }

    /// <summary>
    /// Registers newly spawned warheads so the camera can cycle between them.
    /// </summary>
    public void RegisterWarhead(Transform warheadTransform)
    {
        if (warheadTransform != null && !activeWarheads.Contains(warheadTransform))
        {
            activeWarheads.Add(warheadTransform);
            
            // Automatically snap camera to follow the newest released warhead!
            SetTarget(warheadTransform);
        }
    }

    private void HandleInputSwitching()
    {
        // Press 1-4 to focus on specific warheads
        if (Input.GetKeyDown(KeyCode.Alpha1) && activeWarheads.Count >= 1) SetTarget(activeWarheads[0]);
        if (Input.GetKeyDown(KeyCode.Alpha2) && activeWarheads.Count >= 2) SetTarget(activeWarheads[1]);
        if (Input.GetKeyDown(KeyCode.Alpha3) && activeWarheads.Count >= 3) SetTarget(activeWarheads[2]);
        if (Input.GetKeyDown(KeyCode.Alpha4) && activeWarheads.Count >= 4) SetTarget(activeWarheads[3]);
    }
}