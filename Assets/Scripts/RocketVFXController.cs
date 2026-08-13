using UnityEngine;

/// <summary>
/// Simplified Rocket VFX Controller.
/// Each physical stage gets exactly ONE visual effect (ParticleSystem/Plume object).
/// Activated when that stage's isThrusting becomes true, deactivated when isThrusting becomes false or stage separates.
/// Payload release (reentry trail) and impact VFX remain separate event-driven triggers.
/// </summary>
public class RocketVFXController : MonoBehaviour
{
    [Header("Single VFX Effect Per Stage")]
    [Tooltip("Single plume/exhaust VFX attached to Stage 1 nozzle")]
    [SerializeField] private ParticleSystem stage1VFX;

    [Tooltip("Single plume/exhaust VFX attached to Stage 2 nozzle")]
    [SerializeField] private ParticleSystem stage2VFX;

    [Tooltip("Single plume/exhaust VFX attached to Stage 3 nozzle")]
    [SerializeField] private ParticleSystem stage3VFX;

    [Header("Payload Reentry Trail VFX")]
    [SerializeField] private TrailRenderer reentryTrailRenderer;

    [Header("Impact VFX")]
    [SerializeField] private ParticleSystem impactExplosionPrefab;

    private ParticleSystem activeStageVFX;

    private void Awake()
    {
        InitializeVFX(stage1VFX);
        InitializeVFX(stage2VFX);
        InitializeVFX(stage3VFX);

        if (reentryTrailRenderer != null)
        {
            reentryTrailRenderer.enabled = false;
        }
    }

    private void InitializeVFX(ParticleSystem ps)
    {
        if (ps != null)
        {
            var main = ps.main;
            main.playOnAwake = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    /// <summary>
    /// Activates exactly ONE single VFX for the active stage when thrust starts.
    /// </summary>
    public void SetStageThrustVFX(int stageIndex, bool isThrusting)
    {
        // Turn off any currently active stage VFX
        StopActiveStageVFX();

        if (!isThrusting) return;

        ParticleSystem targetVFX = GetStageVFX(stageIndex);
        if (targetVFX != null)
        {
            activeStageVFX = targetVFX;
            activeStageVFX.Play(true);
            Debug.Log($"<color=cyan>[VFX State]</color> Stage {stageIndex + 1} Single VFX Activated.");
        }
    }

    /// <summary>
    /// Stops active stage VFX instantly on stage separation or thrust cutoff.
    /// </summary>
    public void HandleStageSeparation(int spentStageIndex, Vector3 separationWorldPos)
    {
        Debug.Log($"<color=orange>[VFX State]</color> Stage {spentStageIndex + 1} Separated — Stopping Stage {spentStageIndex + 1} VFX.");
        
        ParticleSystem targetVFX = GetStageVFX(spentStageIndex);
        if (targetVFX != null)
        {
            targetVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (activeStageVFX == targetVFX)
        {
            activeStageVFX = null;
        }
    }

    /// <summary>
    /// Activates payload reentry trail upon warhead release.
    /// </summary>
    public void HandlePayloadRelease(Vector3 releaseWorldPos)
    {
        StopActiveStageVFX();

        if (reentryTrailRenderer != null)
        {
            reentryTrailRenderer.enabled = true;
            reentryTrailRenderer.Clear();
            Debug.Log("<color=magenta>[VFX State]</color> Payload Reentry Trail Activated.");
        }
    }

    /// <summary>
    /// Triggers target impact explosion VFX.
    /// </summary>
    public void HandleImpact(Vector3 impactWorldPos)
    {
        if (impactExplosionPrefab != null)
        {
            ParticleSystem explosion = Instantiate(impactExplosionPrefab, impactWorldPos, Quaternion.identity);
            explosion.Play(true);
            Destroy(explosion.gameObject, 5.0f);
            Debug.Log($"<color=red>[VFX State]</color> Impact Explosion Triggered at {impactWorldPos}.");
        }

        if (reentryTrailRenderer != null)
        {
            reentryTrailRenderer.enabled = false;
        }
    }

    private void StopActiveStageVFX()
    {
        if (activeStageVFX != null)
        {
            activeStageVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            activeStageVFX = null;
        }
    }

    private ParticleSystem GetStageVFX(int stageIndex)
    {
        switch (stageIndex)
        {
            case 0: return stage1VFX;
            case 1: return stage2VFX;
            case 2: return stage3VFX;
            default: return null;
        }
    }
}
