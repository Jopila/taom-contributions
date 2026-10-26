using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Features.CoopInterop;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SeasonedHosts;

/// <summary>
/// The adapter reads engine heroes and parties that a no-game test cannot build, so the null answers run for real and the rest
/// is a source pin. Control always comes from <see cref="IPlayerControlQuery"/>, never from a comparison with the player clan.
/// </summary>
[TestClass]
public class SeasonedHostsAdapterTests
{
    private static string Source() => RepoPaths.ReadSource("Main/Adapters/SeasonedHostsAdapter.cs", stripComments: true);

    [TestMethod]
    public void Read_ANullHero_ReturnsDefaultAndAsksNoOne()
    {
        var control = Substitute.For<IPlayerControlQuery>();

        Assert.AreEqual(default(LordXpFacts), new SeasonedHostsAdapter(control).Read((Hero?)null));
        Assert.AreEqual(0, control.ReceivedCalls().Count());
    }

    [TestMethod]
    public void Read_ANullParty_ReturnsDefaultAndAsksNoOne()
    {
        var control = Substitute.For<IPlayerControlQuery>();

        Assert.AreEqual(default(TrainingPartyFacts), new SeasonedHostsAdapter(control).Read((MobileParty?)null));
        Assert.AreEqual(0, control.ReceivedCalls().Count());
    }

    [TestMethod]
    public void DefaultFacts_AreNeitherALordNorALordParty()
    {
        Assert.IsFalse(default(LordXpFacts).IsLord);
        Assert.IsFalse(default(TrainingPartyFacts).IsLordParty);
    }

    [TestMethod]
    public void Source_AsksTheControlQueryAndNeverThePlayerClanStatics()
    {
        var src = Source();

        StringAssert.Contains(src, "_control.OfClan(hero.Clan)");
        StringAssert.Contains(src, "_control.OfClan(party.Owner?.Clan)");
        StringAssert.Contains(src, "_control.OfParty(party.Army?.LeaderParty)");
        Assert.IsFalse(src.Contains("Clan.PlayerClan"), "a remote co-op player's clan is not Clan.PlayerClan");
    }

    private static void AssertPrecedes(string src, string guard, string query)
    {
        var guardAt = src.IndexOf(guard, System.StringComparison.Ordinal);
        var queryAt = src.IndexOf(query, System.StringComparison.Ordinal);

        Assert.IsTrue(guardAt >= 0, "guard missing from the adapter: " + guard);
        Assert.IsTrue(queryAt >= 0, "query missing from the adapter: " + query);
        Assert.IsTrue(guardAt < queryAt, guard + " must come before " + query);
    }

    [TestMethod]
    public void TheControlQueries_ComeAfterTheNonLordAndPlayerOwnSideGuards()
    {
        var src = Source();

        AssertPrecedes(src, "!hero.IsLord", "_control.OfClan(hero.Clan)");
        AssertPrecedes(src, "hero == Hero.MainHero", "_control.OfClan(hero.Clan)");
        AssertPrecedes(src, "!party.IsLordParty", "_control.OfClan(party.Owner");
        AssertPrecedes(src, "party == MobileParty.MainParty", "_control.OfClan(party.Owner");
    }
}
