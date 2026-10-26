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

    public IReadOnlyList<RevealCandidate> FortificationsAround(string settlementId)
    {
        var found = new List<RevealCandidate>();
        var origin = string.IsNullOrEmpty(settlementId) ? null : Settlement.Find(settlementId);
        var tracker = Campaign.Current?.VisualTrackerManager;
        if (origin == null || tracker == null)
            return found;

        var here = origin.GetPosition2D;
        foreach (var fief in Town.AllFiefs)
        {
            var settlement = fief.Settlement;
            if (settlement == origin)
                continue;
            found.Add(new RevealCandidate(settlement.StringId, here.Distance(settlement.GetPosition2D), tracker.CheckTracked(settlement)));
        }
        return found;
    }

    public int Track(IReadOnlyList<string> settlementIds)
    {
        var settlements = new List<Settlement>(settlementIds.Count);
        foreach (var id in settlementIds)
        {
            var settlement = string.IsNullOrEmpty(id) ? null : Settlement.Find(id);
            if (settlement != null)
                settlements.Add(settlement);
        }
        return Track(settlements);
    }

    private static int Track(IEnumerable<Settlement> settlements)
    {
        var marked = 0;
        var tracker = Campaign.Current?.VisualTrackerManager;
        if (tracker == null)
            return marked;
        foreach (var settlement in settlements)
        {
            // A tracked nameplate is drawn every map frame, outside the vanilla distance cull and the Nameplate Cull
            // patch, so only towns and castles are marked; villages stay unmarked.
            if (settlement.IsFortification && !tracker.CheckTracked(settlement))
            {
                tracker.RegisterObject(settlement);
                marked++;
            }
        }
        return marked;
    }

    private static void MarkKnown(IEnumerable<Hero> heroes)
    {
        foreach (var hero in heroes)
            if (hero != null && !hero.IsDead)
                hero.IsKnownToPlayer = true;
    }
}
