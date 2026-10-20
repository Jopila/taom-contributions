using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IMapRevealAdapter"/>. Each town and castle gets the engine's visual tracker
/// mark (<c>VisualTrackerManager.RegisterObject</c>), which the engine saves with the campaign; the player removes a
/// mark with the Track toggle on the settlement's encyclopedia page. A settlement already tracked is left alone,
/// because <c>RegisterObject</c> on a tracked object raises its tracker count and a single Track toggle would then no
/// longer clear it. Setting <c>IsKnownToPlayer</c> on a hero the player did not know raises the engine's "learned
/// about" event, whose own listener prints the "You've learned about" line and places the hero's last-seen
/// settlement; the setter does nothing for a hero already known. The reveal deliberately leaves <c>SetHasMet</c>
/// alone: it would stamp a meeting that never happened and skip the lord's later introduction dialog.
/// <c>Settlement.HasVisited</c> is not written: nothing in v1.5.5 reads it.
/// </summary>
public sealed class MapRevealAdapter : IMapRevealAdapter
{
    public void RevealClan(string clanId)
    {
        var clan = Campaign.Current?.CampaignObjectManager?.Find<Clan>(clanId);
        if (clan == null)
            return;
        MarkKnown(clan.Heroes);
        Track(clan.Settlements);
    }

    public void RevealKingdom(string kingdomId)
    {
        var kingdom = Campaign.Current?.CampaignObjectManager?.Find<Kingdom>(kingdomId);
        if (kingdom == null)
            return;
        MarkKnown(kingdom.Heroes);
        Track(kingdom.Settlements);
    }

    private static void Track(IEnumerable<Settlement> settlements)
    {
        var tracker = Campaign.Current?.VisualTrackerManager;
        if (tracker == null)
            return;
        foreach (var settlement in settlements)
        {
            // A tracked nameplate is drawn every map frame, outside the vanilla distance cull and the Nameplate Cull
            // patch, so only towns and castles are marked; villages stay unmarked.
            if (settlement.IsFortification && !tracker.CheckTracked(settlement))
                tracker.RegisterObject(settlement);
        }
    }

    private static void MarkKnown(IEnumerable<Hero> heroes)
    {
        foreach (var hero in heroes)
            if (hero != null && !hero.IsDead)
                hero.IsKnownToPlayer = true;
    }
}
