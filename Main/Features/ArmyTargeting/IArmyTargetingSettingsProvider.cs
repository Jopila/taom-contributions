namespace TAOM.Features.ArmyTargeting;

public interface IArmyTargetingSettingsProvider
{
    bool EnableArmyStrategicIntelligence { get; }
    bool EnableWarTheaters { get; }
    float CommitmentMultiplier { get; }
    float MaxPriorityBoost { get; }
    float EvilAggressionScale { get; }
    float BorderProximityFloor { get; }

    /// <summary>
    /// How far, in town gaps, Patch22 may reach when overturning vanilla's "unreachable" verdict
    /// for a priority-list target. Not a general distance penalty: vanilla already applies one.
    /// </summary>
    float BorderRescueRadiusInTownGaps { get; }

    /// <summary>Multiplier on Defender-mission target scores, the home-defence lever.</summary>
    float DefenderPriorityMultiplier { get; }

    /// <summary>
    /// "Good armies hold captured forts" (Hold the Walls, #871). Read on every hourly pass that has a capture or a hold to
    /// look at; the service holds only while <see cref="EnableArmyStrategicIntelligence"/> is on too. Turning it off ends
    /// the running holds at the next pass, but holds are not saved: after a save is loaded the engine's own pause (the
    /// longer one, saved with the party) runs to its end and an early release no longer reaches it.
    /// </summary>
    bool HoldCapturedForts { get; }
}
