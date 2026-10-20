using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Adapters;

/// <summary>
/// Boundary over the campaign facts Hidden Hosts' memory needs (#846 phase 3, ADR-007): the clock, the scout, whether a
/// settlement is in sight, a party's visibility change, and the Scouting XP grant. Who is in plain sight is
/// <see cref="IPartyIntelAdapter.CollectPlainSight"/>, next to the one definition of "in plain sight". Plain ids and numbers go out; the memory
/// behavior and the service never touch an engine object except the events' own arguments.
/// </summary>
public interface IHiddenHostsCampaignAdapter
{
    /// <summary>The campaign clock in whole hours, or -1 when there is no campaign or the clock is unusable.</summary>
    int NowHour { get; }

    /// <summary>The Scouting skill of the main party's effective scout, 0 when it has none.</summary>
    int MainScoutingSkill { get; }

    /// <summary>True when the settlement is inspected now, false when it is not, null when no settlement has that id.</summary>
    bool? IsSettlementInSight(string id);

    /// <summary>Reads the id and the new visibility of a mobile party whose visibility changed; false for a party with no mobile party.</summary>
    bool TryReadVisibilityChange(PartyBase? party, out string id, out bool visible);

    /// <summary>Adds <paramref name="xp"/> raw Scouting XP to the main party's effective scout; nothing when there is no scout or no XP.</summary>
    void GrantScoutingXp(int xp);
}
