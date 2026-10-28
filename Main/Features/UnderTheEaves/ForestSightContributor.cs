using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Domain;

namespace TAOM.Features.UnderTheEaves;

/// <summary>
/// Under the Eaves (#849) through <see cref="IPartySpottingContributor"/>: the main party sees less far while it travels through
/// forest. It is keyed on the SPOTTER, which is what the seam can express; vanilla already makes a target in forest harder for the
/// player to see (docs/features/under-the-eaves.md). Asked per spotting query, which the engine's visibility pass makes every frame,
/// so the gates run cheapest first: the toggle, a null party, the main party, a settlement, then the forest read (a struct copy while
/// the party's stored position has a warm face cache; while it is cold, after a load until the party moves, each read repeats
/// vanilla's native face lookup), and only in forest the Keen Sight perk. Nothing is allocated, no IoC, null-tolerant on the party.
/// The cap bonus is always 0: a penalty never widens the engine's per-frame search.
/// </summary>
internal sealed class ForestSightContributor : IPartySpottingContributor
{
    private readonly IUnderTheEavesSettings _settings;
    private readonly IMapTerrainAdapter _terrain;

    public ForestSightContributor(IUnderTheEavesSettings settings, IMapTerrainAdapter terrain)
    {
        _settings = settings;
        _terrain = terrain;
    }

    public float GetSpottingRangeBonusFactor(MobileParty party)
    {
        if (!_settings.Enabled)
            return 0f;
        if (party == null || !party.IsMainParty)
            return 0f;
        bool inSettlement = party.CurrentSettlement != null;
        bool inForest = !inSettlement && _terrain.IsInForest(party);
        bool keenSight = inForest && party.HasPerk(DefaultPerks.Scouting.KeenSight);
        return UnderTheEavesRule.Factor(inForest, inSettlement, keenSight);
    }

    public float GetSpottingRangeCapBonus(MobileParty party) => 0f;
}
