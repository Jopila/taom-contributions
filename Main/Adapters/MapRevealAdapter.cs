using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IMapRevealAdapter"/>. Setting <c>IsKnownToPlayer</c> on a hero the player
/// did not know raises the engine's "learned about" event, whose own listener prints the "You've learned about"
/// line and places the hero's last-seen settlement; the setter does nothing for a hero already known. The reveal
/// deliberately leaves <c>SetHasMet</c> alone: it would stamp a meeting that never happened and skip the lord's
/// later introduction dialog.
/// </summary>
public sealed class MapRevealAdapter : IMapRevealAdapter
{
    public void RevealClan(string clanId)
    {
        var clan = Campaign.Current?.CampaignObjectManager?.Find<Clan>(clanId);
        if (clan == null)
            return;
        MarkVisited(clan.Settlements);
        MarkKnown(clan.Heroes);
    }

    public void RevealKingdom(string kingdomId)
    {
        var kingdom = Campaign.Current?.CampaignObjectManager?.Find<Kingdom>(kingdomId);
        if (kingdom == null)
            return;
        MarkVisited(kingdom.Settlements);
        MarkKnown(kingdom.Heroes);
    }

    private static void MarkVisited(IEnumerable<Settlement> settlements)
    {
        foreach (var settlement in settlements)
            settlement.HasVisited = true;
    }

    private static void MarkKnown(IEnumerable<Hero> heroes)
    {
        foreach (var hero in heroes)
            if (hero != null && !hero.IsDead)
                hero.IsKnownToPlayer = true;
    }
}
