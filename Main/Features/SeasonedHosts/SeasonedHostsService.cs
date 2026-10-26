using System;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.SeasonedHosts;

/// <summary>
/// Every decision of Seasoned Hosts (#890, docs/features/seasoned-hosts.md), pure: the models and the patch hand it facts read by
/// <see cref="ISeasonedHostsAdapter"/> and apply what it answers. A human-driven side never gets a bonus, and a co-op session
/// whose player registry is unreadable (<see cref="PlayerControl.Unknown"/>) counts as human-driven, so a doubtful answer is no bonus.
/// </summary>
public sealed class SeasonedHostsService
{
    /// <summary>The smallest learning rate a skill keeps past its learning limit while "Learning Never Stops" is on.</summary>
    public const float LearningRateFloorValue = 0.05f;

    private readonly ISeasonedHostsSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;

    public SeasonedHostsService(ISeasonedHostsSettingsProvider settings, ICoopSessionProvider coop)
    {
        _settings = settings;
        _coop = coop;
    }

    /// <summary>The factor to multiply a hero's skill XP by: the setting for an AI lord who is not the main hero and a peer that does not defer to the co-op host, else 1.</summary>
    public float SkillXpMultiplier(LordXpFacts lord) =>
        lord.IsLord && !lord.IsMainHero && lord.ClanControl == PlayerControl.NotPlayer && !_coop.ShouldDeferToHost
            ? _settings.SkillXpMultiplier
            : 1f;

    /// <summary>The factor to add to a troop's training XP: the setting for a lord party that no human drives or owns or leads on a peer that does not defer to the co-op host, else 0.</summary>
    public float TrainingFactor(TrainingPartyFacts party) =>
        party.IsLordParty && !party.IsMainParty
        && party.OwnerClanControl == PlayerControl.NotPlayer && party.ArmyLeaderControl == PlayerControl.NotPlayer
        && !_coop.ShouldDeferToHost
            ? _settings.TrainingBonus
            : 0f;

    /// <summary>The learning rate's minimum: the larger of the current one and the floor when the setting is on, else the current one.</summary>
    public float LearningRateFloor(float currentMinimum) =>
        _settings.LearningNeverStops ? Math.Max(currentMinimum, LearningRateFloorValue) : currentMinimum;

    /// <summary>
    /// True when a finished settlement project should grant no Steward XP. A grant moves replicated hero state, so a peer that
    /// defers to the co-op host never skips it.
    /// </summary>
    public bool SkipsProjectStewardXp() => _settings.NoStewardXp && !_coop.ShouldDeferToHost;
}
