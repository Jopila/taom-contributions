namespace TAOM.Features.SeasonedHosts;

/// <summary>The validated "World/Seasoned Hosts" MCM values, one read per property. Read for every skill XP grant and every troop's daily training, so a read allocates nothing.</summary>
public interface ISeasonedHostsSettingsProvider
{
    /// <summary>Extra training XP for an AI lord party as a fraction, 0 to 1. 0 is no bonus.</summary>
    float TrainingBonus { get; }

    /// <summary>Multiplier on an AI lord's skill XP, 1 to 2. 1 is no change.</summary>
    float SkillXpMultiplier { get; }

    /// <summary>The learning rate keeps a small floor past a skill's learning limit, for every hero.</summary>
    bool LearningNeverStops { get; }

    /// <summary>A finished settlement project grants no Steward XP.</summary>
    bool NoStewardXp { get; }
}
