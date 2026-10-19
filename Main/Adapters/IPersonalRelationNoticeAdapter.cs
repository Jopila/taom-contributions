using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

/// <summary>Vanilla's relation notice, naming the hero (ADR-007: the engine heroes are converted here, in the patch's boundary).</summary>
public interface IPersonalRelationNoticeAdapter
{
    /// <summary>
    /// Shows vanilla's own "relation with this hero changed" quick information, with the hero's name, when the change involves
    /// the player (the same test vanilla's handler applies). True when it showed the notice, so the patch skips vanilla's
    /// handler; false when vanilla would have shown nothing.
    /// </summary>
    bool ShowNamedNotice(Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange);
}
