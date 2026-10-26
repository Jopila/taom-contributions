using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Features.KingsMuster;

/// <summary>
/// Boundary reader (ADR-007) for the King's Muster (#874): two facts about the party that leads an army, both null-safe. The
/// decision ("an AI ruler") is <see cref="KingsMusterService.ApplyRulerDiscount"/>'s, not this class's.
/// </summary>
internal static class RulerFactsReader
{
    /// <summary>True when the party's leader hero rules the kingdom it belongs to (<c>Hero.IsKingdomLeader</c>). False for a null party or a party with no leader.</summary>
    public static bool IsKingdomRuler(MobileParty? party) => party?.LeaderHero?.IsKingdomLeader == true;

    /// <summary>True when the party's leader hero is <see cref="Hero.MainHero"/>, the player. False for a null party or a party with no leader.</summary>
    public static bool IsLedByMainHero(MobileParty? party)
    {
        var hero = party?.LeaderHero;
        return hero != null && hero == Hero.MainHero;
    }
}
