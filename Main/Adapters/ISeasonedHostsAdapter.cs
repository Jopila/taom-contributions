using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Adapters;

/// <summary>The boundary for Seasoned Hosts (#890): reads an engine hero or party into plain facts (ADR-007).</summary>
public interface ISeasonedHostsAdapter
{
    /// <summary>
    /// The hero as skill XP facts. Null and a non-lord give <c>default</c> without asking who controls the clan, and the main hero
    /// does not ask either, so only an AI-looking lord costs a control query.
    /// </summary>
    LordXpFacts Read(Hero? hero);

    /// <summary>
    /// The party as training facts. Null and a party that is not a lord party give <c>default</c> without a control query; the
    /// main party asks none either.
    /// </summary>
    TrainingPartyFacts Read(MobileParty? party);
}
