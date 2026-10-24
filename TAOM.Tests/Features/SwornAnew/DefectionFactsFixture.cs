using TAOM.Features.Execution;
using TAOM.Features.SwornAnew.Models;

namespace TAOM.Tests.Features.SwornAnew;

/// <summary>
/// The one set of facts every Sworn Anew test starts from, so a new <c>DefectionFacts</c> field is added in one place. Sides are read
/// from the faction id by the tests' alignment fakes: "k_free" is Free, "k_evil" is Evil, anything else is Neutral.
/// </summary>
internal static class DefectionFactsFixture
{
    internal static readonly SideIds Free = new("k_free", "c_free");
    internal static readonly SideIds Evil = new("k_evil", "c_evil");
    internal static readonly SideIds Unaligned = new("k_other", "c_other");

    /// <summary>A captured Free lord of another kingdom, held by the player's party, for a Free player: every gate open.</summary>
    internal static DefectionFacts EligiblePrisoner(SideIds? lord = null, SideIds? player = null) => new(
        HeroId: "lord_1",
        RaceId: 0,
        IsPrisoner: true,
        InKingdom: true,
        InPlayerFaction: false,
        IsFactionLeader: false,
        HasClan: true,
        IsMinorFaction: false,
        IsMercenary: false,
        ClanHasKingdom: true,
        TargetRuledByPlayerClan: false,
        HasBloodFeudWithPlayer: false,
        HeldByMainParty: true,
        InPlayerClanSettlement: false,
        OnRansomScreen: false,
        LordSide: lord ?? Free,
        PlayerSide: player ?? Free);
}
