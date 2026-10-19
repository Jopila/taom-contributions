using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace TAOM.Adapters;

/// <summary>Boundary implementation of <see cref="IPlayerTraitAdapter"/> over the public leveling helper.</summary>
public sealed class PlayerTraitAdapter : IPlayerTraitAdapter
{
    public bool AddTraitXp(string traitId, int xp)
    {
        var trait = traitId switch
        {
            "Honor" => DefaultTraits.Honor,
            "Mercy" => DefaultTraits.Mercy,
            _ => null,
        };
        if (trait == null || xp == 0)
            return false;
        TraitLevelingHelper.OnIncidentResolved(trait, xp);
        return true;
    }
}
