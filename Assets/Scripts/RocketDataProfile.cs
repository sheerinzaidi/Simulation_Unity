using UnityEngine;

/// <summary>
/// Per-stage physics configuration for multi-stage rocket trajectory simulation.
/// Allows swapping rocket profiles (e.g. Minuteman III, Trident II, custom NESCOM profiles) with zero code changes.
/// </summary>
[System.Serializable]
public struct StageData
{
    public string stageName;
    [Tooltip("Structural / empty dry mass of this stage in kg")]
    public float dryMassKg;

    [Tooltip("Usable propellant / fuel mass of this stage in kg")]
    public float propellantMassKg;

    [Tooltip("Vacuum/Atmospheric thrust force in Newtons")]
    public float thrustNewtons;

    [Tooltip("Burn duration in seconds")]
    public float burnDurationSec;

    /// <summary>
    /// Total wet mass of this stage alone (dry mass + propellant mass).
    /// </summary>
    public readonly float StageWetMassKg => dryMassKg + propellantMassKg;

    /// <summary>
    /// Derived fuel consumption rate in kg/s: propellantMass / burnDuration.
    /// </summary>
    public readonly float MassFlowRate => (burnDurationSec > 0f) ? (propellantMassKg / burnDurationSec) : 0f;
}

[CreateAssetMenu(fileName = "MinutemanIII_Profile", menuName = "Rocket Physics/Rocket Data Profile")]
public class RocketDataProfile : ScriptableObject
{
    public string rocketName = "LGM-30G Minuteman III";
    
    [Header("Payload Configuration")]
    [Tooltip("Warhead / Post-Boost Vehicle (PBV) payload mass in kg")]
    public float payloadMassKg = 1150f; // W78/W87 reentry vehicle payload

    [Header("Inter-Stage Separation Gap")]
    [Tooltip("Unpowered coast delay between stage burnout and next stage ignition in seconds")]
    public float separationDelaySec = 2.0f;

    [Header("Multi-Stage Rocket Configuration")]
    public StageData[] stages;

    /// <summary>
    /// Computes the total initial wet mass of the entire rocket assembly (all stages + payload).
    /// </summary>
    public float CalculateTotalInitialWetMass()
    {
        float total = payloadMassKg;
        if (stages != null)
        {
            foreach (var stage in stages)
            {
                total += stage.StageWetMassKg;
            }
        }
        return total;
    }

    /// <summary>
    /// Returns default real-world figures for Minuteman III ICBM as placeholder data.
    /// Stage 1: Thiokol M55 (23,100 kg wet, ~500-890 kN thrust, ~60s burn)
    /// Stage 2: Aerojet SR19 (7,000 kg wet, ~260 kN thrust, ~66s burn)
    /// Stage 3: Hercules M57 (3,600 kg wet, ~150 kN thrust, ~60s burn)
    /// </summary>
    public static RocketDataProfile CreateMinutemanIII()
    {
        var profile = CreateInstance<RocketDataProfile>();
        profile.rocketName = "LGM-30G Minuteman III";
        profile.payloadMassKg = 1150f;
        profile.separationDelaySec = 2.0f;
        profile.stages = new StageData[]
        {
            new StageData {
                stageName = "Stage 1 (M55)",
                dryMassKg = 3100f,
                propellantMassKg = 20000f,
                thrustNewtons = 480000f, // 480 kN gives liftoff TWR = 1.40, strictly in 1.2–1.5 range
                burnDurationSec = 60f
            },
            new StageData {
                stageName = "Stage 2 (SR19)",
                dryMassKg = 1000f,
                propellantMassKg = 6000f,
                thrustNewtons = 260000f,
                burnDurationSec = 66f
            },
            new StageData {
                stageName = "Stage 3 (M57)",
                dryMassKg = 600f,
                propellantMassKg = 3000f,
                thrustNewtons = 150000f,
                burnDurationSec = 60f
            }
        };
        return profile;
    }
}
