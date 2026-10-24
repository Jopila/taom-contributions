using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Equipment;
using TAOM.Features.Enlistment.Presentation;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>#837: the saved list of past service, the re-enlistment policy and the service around them.</summary>
[TestClass]
public class PastServiceTests
{
    private const string Hero = "hero_1";

    private static ServiceHistoryEntry E(
        string cmd = "lord_a", string clan = "clan_a", string kingdom = "k_a", ServiceRank rank = ServiceRank.Sergeant,
        DischargeReason reason = DischargeReason.PlayerRequest, double end = 100, EnlistmentKitTier? tier = null,
        string hero = Hero)
        => new ServiceHistoryEntry(cmd, clan, kingdom, rank, reason, end, tier, hero);

    // ---- the saved list ----

    [TestMethod]
    public void History_RoundTrips()
    {
        var history = new ServiceHistory();
        history.Entries.Add(E());
        history.Entries.Add(E("lord_b", "", "", ServiceRank.Private, DischargeReason.Desertion, 250.5, EnlistmentKitTier.Veteran, "hero_2"));

        var parsed = ServiceHistory.Parse(history.Serialize());

        Assert.AreEqual(2, parsed.Entries.Count);
        Assert.AreEqual("lord_a", parsed.Entries[0].CommanderHeroId);
        Assert.AreEqual(ServiceRank.Sergeant, parsed.Entries[0].Rank);
        Assert.IsNull(parsed.Entries[0].KitTier);
        Assert.AreEqual(Hero, parsed.Entries[0].HeroId);
        Assert.AreEqual("", parsed.Entries[1].KingdomId);
        Assert.AreEqual(DischargeReason.Desertion, parsed.Entries[1].Reason);
        Assert.AreEqual(250.5, parsed.Entries[1].EndDay);
        Assert.AreEqual(EnlistmentKitTier.Veteran, parsed.Entries[1].KitTier);
        Assert.AreEqual("hero_2", parsed.Entries[1].HeroId);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(null)]
    [DataRow("garbage")]
    [DataRow("|||||||")]
    [DataRow("lord|c|k|99|0|100||h")]
    [DataRow("lord|c|k|2|99|100||h")]
    [DataRow("lord|c|k|2|0|NaN||h")]
    [DataRow("lord|c|k|2|0|Infinity||h")]
    [DataRow("|c|k|2|0|100||h")]
    [DataRow("lord|c|k|x|0|100||h")]
    [DataRow("lord|c|k|2|0|100|9|h", DisplayName = "an undefined kit tier")]
    [DataRow("lord|c|k|2|0|100|x|h", DisplayName = "a kit tier that is not a number")]
    [DataRow("lord|c|k|2|0|100||", DisplayName = "no hero id")]
    [DataRow("lord|c|k|2|0|100|")]
    public void History_GarbageRows_AreDropped(string row)
    {
        Assert.AreEqual(0, ServiceHistory.Parse(row).Entries.Count);
    }

    [TestMethod]
    public void History_OneBadRow_DoesNotCostTheGoodOnes()
    {
        var good = E().ToRow();

        var parsed = ServiceHistory.Parse(good + "~garbage~" + E("lord_b").ToRow());

        Assert.AreEqual(2, parsed.Entries.Count);
    }

    [TestMethod]
    public void History_IdsWithSeparators_AreNotWritten()
    {
        var history = new ServiceHistory();
        history.Entries.Add(E("lord|x"));
        history.Entries.Add(E(hero: "hero;x"));
        history.Entries.Add(E("lord_ok"));

        Assert.AreEqual(1, ServiceHistory.Parse(history.Serialize()).Entries.Count);
    }

    [TestMethod]
    public void Prune_DropsExpiredThenTheOldestOverTheCap()
    {
        var history = new ServiceHistory();
        history.Entries.Add(E("old", end: 10));
        history.Entries.Add(E("a", end: 1000));
        history.Entries.Add(E("b", end: 1100));
        history.Entries.Add(E("c", end: 1200));

        history.Prune(nowDays: 1300, expiryDays: 730, maxEntries: 2, cooldownDays: 60);

        CollectionAssert.AreEqual(new[] { "b", "c" }, history.Entries.Select(e => e.CommanderHeroId).ToArray());
    }

    [TestMethod]
    public void Prune_DropsAnEntryDatedAfterNow()
    {
        var history = new ServiceHistory();
        history.Entries.Add(E("future", end: 500));
        history.Entries.Add(E("now", end: 100));

        history.Prune(nowDays: 100, expiryDays: 730, maxEntries: 10, cooldownDays: 60);

        CollectionAssert.AreEqual(new[] { "now" }, history.Entries.Select(e => e.CommanderHeroId).ToArray());
    }

    [TestMethod]
    public void Prune_ExactlyAtExpiry_IsKept()
    {
        var history = new ServiceHistory();
        history.Entries.Add(E(end: 100));

        history.Prune(830, 730, 10, 60);

        Assert.AreEqual(1, history.Entries.Count);
    }

    [TestMethod]
    public void Prune_KeepsACoolingDesertion_PastTheCap()
    {
        var history = new ServiceHistory();
        history.Entries.Add(E("deserted", reason: DischargeReason.Desertion, end: 100));
        history.Entries.Add(E("a", end: 50));
        history.Entries.Add(E("b", end: 60));
        history.Entries.Add(E("c", end: 70));

        history.Prune(nowDays: 120, expiryDays: 730, maxEntries: 2, cooldownDays: 60);

        CollectionAssert.AreEqual(new[] { "deserted", "c" }, history.Entries.Select(e => e.CommanderHeroId).ToArray(),
            "the cap takes the oldest rows that are not a desertion still cooling");
    }

    [TestMethod]
    public void Prune_KeepsACoolingDesertion_PastExpiry()
    {
        var history = new ServiceHistory();
        history.Entries.Add(E("deserted", reason: DischargeReason.Desertion, end: 1980));
        history.Entries.Add(E("old", end: 1980));

        history.Prune(nowDays: 2000, expiryDays: 10, maxEntries: 10, cooldownDays: 60);

        CollectionAssert.AreEqual(new[] { "deserted" }, history.Entries.Select(e => e.CommanderHeroId).ToArray());
    }

    [TestMethod]
    public void Prune_MoreCoolingDesertionsThanTheCap_StopsAndKeepsThem()
    {
        var history = new ServiceHistory();
        history.Entries.Add(E("d1", reason: DischargeReason.Desertion, end: 100));
        history.Entries.Add(E("d2", reason: DischargeReason.Desertion, end: 110));
        history.Entries.Add(E("a", end: 50));

        history.Prune(nowDays: 120, expiryDays: 730, maxEntries: 1, cooldownDays: 60);

        CollectionAssert.AreEqual(new[] { "d1", "d2" }, history.Entries.Select(e => e.CommanderHeroId).ToArray());
    }

    [TestMethod]
    public void Prune_ADesertionPastItsCooldown_IsAnOrdinaryRow()
    {
        var history = new ServiceHistory();
        history.Entries.Add(E("deserted", reason: DischargeReason.Desertion, end: 100));
        history.Entries.Add(E("a", end: 200));

        history.Prune(nowDays: 300, expiryDays: 730, maxEntries: 1, cooldownDays: 60);

        CollectionAssert.AreEqual(new[] { "a" }, history.Entries.Select(e => e.CommanderHeroId).ToArray());
    }

    // ---- the store saves it in the same section; a new campaign drops it ----

    [TestMethod]
    public void Store_RoundTripsRankXpAndHistory_InOneSection()
    {
        var store = new EnlistmentContentStore(Substitute.For<IModLogger>());
        store.History.Entries.Add(E());
        store.Record.Rank = ServiceRank.Specialist;
        store.Record.ServiceXp = 123;

        var loaded = new EnlistmentContentStore(Substitute.For<IModLogger>());
        loaded.Deserialize(store.Serialize());

        Assert.AreEqual(1, loaded.History.Entries.Count);
        Assert.AreEqual(ServiceRank.Specialist, loaded.Record.Rank, "the record survives next to history=");
        Assert.AreEqual(123, loaded.Record.ServiceXp);
    }

    [TestMethod]
    public void Store_Clear_DropsTheProgressAndTheHistory()
    {
        var store = new EnlistmentContentStore(Substitute.For<IModLogger>());
        store.History.Entries.Add(E());
        store.Record.Rank = ServiceRank.Specialist;

        store.Clear();

        Assert.AreEqual(0, store.History.Entries.Count, "Clear is the new-campaign reset: the past belongs to the old campaign");
        Assert.AreEqual(ServiceRank.Private, store.Record.Rank);
    }

    [TestMethod]
    public void NewCampaign_BothContentBehaviorEdges_ClearTheStore()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Hooks/EnlistmentContentBehavior.cs", stripComments: true);
        var newGame = src.Substring(src.IndexOf("void OnNewGameCreated", StringComparison.Ordinal));
        newGame = newGame.Substring(0, newGame.IndexOf("void OnSessionLaunched", StringComparison.Ordinal));
        var launched = src.Substring(src.IndexOf("void OnSessionLaunched", StringComparison.Ordinal));

        StringAssert.Contains(newGame, "_contentStore.Clear()");
        StringAssert.Contains(launched, "_contentStore.Clear()");
    }

    [TestMethod]
    public void Store_AnOldSectionWithoutHistory_LoadsEmpty()
    {
        var store = new EnlistmentContentStore(Substitute.For<IModLogger>());
        store.History.Entries.Add(E());
        var old = new ServiceContentRecord { Rank = ServiceRank.Sergeant }.Serialize();

        store.Deserialize(old);

        Assert.AreEqual(0, store.History.Entries.Count);
        Assert.AreEqual(ServiceRank.Sergeant, store.Record.Rank);
    }

    [TestMethod]
    public void Store_EmptySection_ClearsHistory()
    {
        var store = new EnlistmentContentStore(Substitute.For<IModLogger>());
        store.History.Entries.Add(E());

        store.Deserialize(null);

        Assert.AreEqual(0, store.History.Entries.Count);
    }

    [TestMethod]
    public void Store_AMalformedRecordWithGoodHistory_KeepsTheHistory()
    {
        var store = new EnlistmentContentStore(Substitute.For<IModLogger>());

        store.Deserialize("rank=banana;history=" + E().ToRow());

        Assert.AreEqual(1, store.History.Entries.Count);
    }

    // ---- the policy ----

    private static IReadOnlyList<ServiceHistoryEntry> H(params ServiceHistoryEntry[] e) => e;

    private static RestoredService R(
        IReadOnlyList<ServiceHistoryEntry> h, string cmd = "lord_x", string clan = "clan_x", string kingdom = "k_x",
        double now = 200, string hero = Hero)
        => ReenlistmentPolicy.Restore(h, now, 730, cmd, clan, kingdom, hero);

    [TestMethod]
    public void Restore_SameCommander_OldRankMinusOne()
        => Assert.AreEqual(ServiceRank.Sergeant, R(H(E(rank: ServiceRank.StaffSergeant)), cmd: "lord_a").Rank);

    [TestMethod]
    public void Restore_SameClan_OldRankMinusOne()
        => Assert.AreEqual(ServiceRank.Specialist, R(H(E(rank: ServiceRank.Sergeant)), clan: "clan_a").Rank);

    [TestMethod]
    public void Restore_SameKingdom_TwoBelow()
        => Assert.AreEqual(ServiceRank.Specialist, R(H(E(rank: ServiceRank.StaffSergeant)), kingdom: "k_a").Rank);

    [TestMethod]
    public void Restore_NoMatch_IsPrivateAndNotRestored()
    {
        var result = R(H(E(rank: ServiceRank.SergeantMajor)));

        Assert.AreEqual(ServiceRank.Private, result.Rank);
        Assert.IsFalse(result.Restored);
    }

    [TestMethod]
    public void Restore_BestOfSeveralMatches_Wins()
    {
        var result = R(H(
            E("lord_a", "clan_z", "k_z", ServiceRank.Specialist),
            E("lord_z", "clan_a", "k_z", ServiceRank.MasterSergeant),
            E("lord_z", "clan_z", "k_a", ServiceRank.SergeantMajor)),
            cmd: "lord_a", clan: "clan_a", kingdom: "k_a");

        Assert.AreEqual(
            ServiceRank.SergeantFirstClass, result.Rank,
            "the clan entry (MasterSergeant 5 - 1) and the kingdom entry (SergeantMajor 6 - 2) both give 4, and the same-commander Specialist entry gives 0");
    }

    [TestMethod]
    public void Restore_FloorIsPrivate()
        => Assert.AreEqual(ServiceRank.Private, R(H(E(rank: ServiceRank.Private)), cmd: "lord_a").Rank);

    [TestMethod]
    public void Restore_ExpiredEntry_GivesNothing()
        => Assert.IsFalse(R(H(E(rank: ServiceRank.Sergeant, end: 100)), cmd: "lord_a", now: 831).Restored);

    [TestMethod]
    public void Restore_ExactlyAtExpiry_StillCounts()
        => Assert.IsTrue(R(H(E(rank: ServiceRank.Sergeant, end: 100)), cmd: "lord_a", now: 830).Restored);

    [TestMethod]
    public void Restore_AnEntryDatedAfterNow_IsIgnored()
        => Assert.IsFalse(R(H(E(rank: ServiceRank.Sergeant, end: 250)), cmd: "lord_a", now: 200).Restored);

    [TestMethod]
    public void Restore_Desertion_GivesNoCredit()
        => Assert.IsFalse(R(H(E(rank: ServiceRank.Sergeant, reason: DischargeReason.Desertion)), cmd: "lord_a").Restored);

    [TestMethod]
    public void Restore_EmptyKingdomIdsNeverMatch()
        => Assert.IsFalse(R(H(E(kingdom: "", clan: "", rank: ServiceRank.Sergeant)), clan: "", kingdom: "").Restored);

    [TestMethod]
    public void Restore_AnotherHeroesEntries_GiveNoCredit()
        => Assert.IsFalse(R(H(E(rank: ServiceRank.StaffSergeant, hero: "hero_2")), cmd: "lord_a", hero: Hero).Restored);

    [TestMethod]
    public void Restore_NoCurrentHero_GivesNothing()
        => Assert.IsFalse(R(H(E(rank: ServiceRank.StaffSergeant)), cmd: "lord_a", hero: "").Restored);

    [TestMethod]
    public void Restore_CarriesTheKitTierOfTheMatchedEntry()
    {
        var result = R(H(
            E("lord_a", rank: ServiceRank.StaffSergeant, tier: EnlistmentKitTier.Veteran),
            E("lord_b", "clan_b", "k_b", ServiceRank.Private, tier: EnlistmentKitTier.Recruit)),
            cmd: "lord_a");

        Assert.AreEqual(EnlistmentKitTier.Veteran, result.KitTier);
    }

    [TestMethod]
    public void Restore_NoKitKept_CarriesNoTier()
        => Assert.IsNull(R(H(E("lord_a", rank: ServiceRank.StaffSergeant, tier: null)), cmd: "lord_a").KitTier);

    [TestMethod]
    public void Restore_TwoMatchesWithTheSameCredit_TheMostRecentOnesKitTierWins()
    {
        var older = E("lord_a", rank: ServiceRank.Sergeant, end: 100, tier: EnlistmentKitTier.Soldier);
        var newer = E("lord_a", rank: ServiceRank.Sergeant, end: 150, tier: EnlistmentKitTier.Veteran);

        Assert.AreEqual(EnlistmentKitTier.Veteran, R(H(older, newer), cmd: "lord_a").KitTier);
        Assert.AreEqual(EnlistmentKitTier.Veteran, R(H(newer, older), cmd: "lord_a").KitTier, "list order must not matter");
    }

    [TestMethod]
    public void Restore_AnOlderHonourableEntry_StillGivesCredit_AfterALaterDesertionFromTheSameCommander()
    {
        var honourable = E("lord_a", rank: ServiceRank.Sergeant, reason: DischargeReason.PlayerRequest, end: 100);
        var deserted = E("lord_a", rank: ServiceRank.Sergeant, reason: DischargeReason.Desertion, end: 150);

        var afterCooldown = R(H(honourable, deserted), cmd: "lord_a", now: 150 + 60);

        Assert.AreEqual(ServiceRank.Specialist, afterCooldown.Rank, "the honourable term still counts; only the desertion gives nothing");
        Assert.IsFalse(R(H(deserted), cmd: "lord_a", now: 150 + 60).Restored, "the desertion entry alone gives no credit");
    }

    [TestMethod]
    public void Restore_NullHistory_IsNothing() => Assert.IsFalse(ReenlistmentPolicy.Restore(null, 1, 730, "a", "b", "c", Hero).Restored);

    // ---- the desertion cooldown ----

    [DataTestMethod]
    [DataRow(100.0, 159.9, 60, true, DisplayName = "inside the window")]
    [DataRow(100.0, 160.0, 60, false, DisplayName = "exactly the cooldown: free")]
    [DataRow(100.0, 100.0, 60, true, DisplayName = "the same day")]
    [DataRow(100.0, 99.0, 60, false, DisplayName = "an entry from the future is ignored")]
    [DataRow(100.0, 110.0, 0, false, DisplayName = "cooldown of zero days is off")]
    public void InDesertionCooldown_Edges(double end, double now, int days, bool expected)
    {
        var history = H(E("lord_a", reason: DischargeReason.Desertion, end: end));

        Assert.AreEqual(expected, ReenlistmentPolicy.InDesertionCooldown(history, "lord_a", now, days, Hero));
        Assert.AreEqual(expected, history[0].IsCoolingDownDesertion(now, days), "the policy and Prune share one predicate");
    }

    [TestMethod]
    public void IsCoolingDownDesertion_AnHonourableExit_NeverCools()
        => Assert.IsFalse(E(reason: DischargeReason.PlayerRequest, end: 100).IsCoolingDownDesertion(110, 60));

    [TestMethod]
    public void InDesertionCooldown_OnlyTheSameCommander_AndOnlyDesertion()
    {
        var history = H(E("lord_a", reason: DischargeReason.Desertion, end: 100), E("lord_b", reason: DischargeReason.PlayerRequest, end: 100));

        Assert.IsFalse(ReenlistmentPolicy.InDesertionCooldown(history, "lord_c", 110, 60, Hero));
        Assert.IsFalse(ReenlistmentPolicy.InDesertionCooldown(history, "lord_b", 110, 60, Hero));
        Assert.IsTrue(ReenlistmentPolicy.InDesertionCooldown(history, "lord_a", 110, 60, Hero));
    }

    [TestMethod]
    public void InDesertionCooldown_AnotherHeroesDesertion_GivesNoCooldown()
    {
        var history = H(E("lord_a", reason: DischargeReason.Desertion, end: 100, hero: "hero_2"));

        Assert.IsFalse(ReenlistmentPolicy.InDesertionCooldown(history, "lord_a", 110, 60, Hero));
        Assert.IsTrue(ReenlistmentPolicy.InDesertionCooldown(history, "lord_a", 110, 60, "hero_2"));
    }

    // ---- the service ----

    private EnlistmentContentStore _content = null!;
    private IEnlistmentContentConfigProvider _config = null!;
    private IEquipmentIssueLedger _ledger = null!;
    private IServiceDailyAnnouncer _announcer = null!;
    private ICampaignClockAdapter _clock = null!;
    private IPlayerPartyAdapter _playerParty = null!;
    private ServiceHistoryService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var logger = Substitute.For<IModLogger>();
        _content = new EnlistmentContentStore(logger);
        _config = Substitute.For<IEnlistmentContentConfigProvider>();
        _config.GetConfig().Returns(EnlistmentContentConfigProvider.BuildDefaults());
        _ledger = new PersistedEquipmentIssueLedger(_content);
        _announcer = Substitute.For<IServiceDailyAnnouncer>();
        _clock = Substitute.For<ICampaignClockAdapter>();
        _clock.NowDays.Returns(300.0);
        _playerParty = Substitute.For<IPlayerPartyAdapter>();
        _playerParty.GetMainHeroId().Returns(Hero);
        _sut = new ServiceHistoryService(_content, _config, _ledger, _announcer, _clock, _playerParty, logger);
    }

    private static CommanderSnapshot Lord(string clan = "clan_a", string faction = "k_a")
        => new CommanderSnapshot(exists: true, isAlive: true, partyId: "p", clanId: clan, factionId: faction, name: "Boromir");

    [TestMethod]
    public void RecordEnd_WritesTheNotedCommanderHeroRankReasonDayAndKitTier()
    {
        _sut.NoteCommander("lord_a", Hero, Lord());

        _sut.RecordEnd(DischargeReason.PlayerRequest, ServiceRank.Sergeant, EnlistmentKitTier.Veteran);

        var e = _content.History.Entries.Single();
        Assert.AreEqual("lord_a", e.CommanderHeroId);
        Assert.AreEqual(Hero, e.HeroId);
        Assert.AreEqual("clan_a", e.ClanId);
        Assert.AreEqual("k_a", e.KingdomId);
        Assert.AreEqual(ServiceRank.Sergeant, e.Rank);
        Assert.AreEqual(DischargeReason.PlayerRequest, e.Reason);
        Assert.AreEqual(300.0, e.EndDay);
        Assert.AreEqual(EnlistmentKitTier.Veteran, e.KitTier);
    }

    [TestMethod]
    public void RecordEnd_AnIndependentCommander_HasNoKingdom()
    {
        _sut.NoteCommander("lord_a", Hero, Lord(clan: "clan_a", faction: "clan_a"));

        _sut.RecordEnd(DischargeReason.CommanderDead, ServiceRank.Private, null);

        Assert.AreEqual("", _content.History.Entries.Single().KingdomId);
    }

    [TestMethod]
    public void RecordEnd_AMercenaryClan_RecordsTheKingdomThatHiredIt()
    {
        _sut.NoteCommander("lord_a", Hero, Lord(clan: "clan_merc", faction: "k_hirer"));

        _sut.RecordEnd(DischargeReason.PlayerRequest, ServiceRank.Sergeant, null);

        Assert.AreEqual("k_hirer", _content.History.Entries.Single().KingdomId);
    }

    [TestMethod]
    public void RecordEnd_OnlyTheTwoQuietReasonsWriteNothing()
    {
        foreach (var reason in Enum.GetValues(typeof(DischargeReason)).Cast<DischargeReason>())
        {
            Setup();
            _sut.NoteCommander("lord_a", Hero, Lord());

            _sut.RecordEnd(reason, ServiceRank.Specialist, null);

            var quiet = reason == DischargeReason.HeirSuccessionOrPossessionMismatch || reason == DischargeReason.SaveNormalization;
            Assert.AreEqual(quiet ? 0 : 1, _content.History.Entries.Count, reason.ToString());
        }
    }

    [TestMethod]
    public void RecordEnd_AQuietReason_ForgetsTheNotedCommander()
    {
        _sut.NoteCommander("lord_a", Hero, Lord());
        _sut.RecordEnd(DischargeReason.SaveNormalization, ServiceRank.Specialist, null);

        _sut.RecordEnd(DischargeReason.PlayerRequest, ServiceRank.Specialist, null);

        Assert.AreEqual(0, _content.History.Entries.Count, "the second call has no noted commander");
    }

    [TestMethod]
    public void RecordEnd_WithoutANotedCommander_WritesNothing()
    {
        _sut.RecordEnd(DischargeReason.PlayerRequest, ServiceRank.Specialist, null);

        Assert.AreEqual(0, _content.History.Entries.Count);
    }

    [TestMethod]
    public void RecordEnd_WithoutAnEnlistedHero_WritesNothing()
    {
        _sut.NoteCommander("lord_a", null, Lord());

        _sut.RecordEnd(DischargeReason.PlayerRequest, ServiceRank.Specialist, null);

        Assert.AreEqual(0, _content.History.Entries.Count);
    }

    [TestMethod]
    public void RecordEnd_PrunesToTheConfiguredCap()
    {
        var cfg = EnlistmentContentConfigProvider.BuildDefaults();
        cfg.ServiceHistoryMaxEntries = 2;
        _config.GetConfig().Returns(cfg);
        for (var i = 0; i < 3; i++)
        {
            _clock.NowDays.Returns(300.0 + i);
            _sut.NoteCommander("lord_" + i, Hero, Lord());
            _sut.RecordEnd(DischargeReason.PlayerRequest, ServiceRank.Specialist, null);
        }

        CollectionAssert.AreEqual(new[] { "lord_1", "lord_2" }, _content.History.Entries.Select(e => e.CommanderHeroId).ToArray());
    }

    [TestMethod]
    public void RecordEnd_ADesertionStaysThroughTheCapWhileItCools()
    {
        var cfg = EnlistmentContentConfigProvider.BuildDefaults();
        cfg.ServiceHistoryMaxEntries = 1;
        _config.GetConfig().Returns(cfg);
        _clock.NowDays.Returns(300.0);
        _sut.NoteCommander("deserted_from", Hero, Lord());
        _sut.RecordEnd(DischargeReason.Desertion, ServiceRank.Specialist, null);
        _clock.NowDays.Returns(310.0);
        _sut.NoteCommander("lord_b", Hero, Lord());
        _sut.RecordEnd(DischargeReason.PlayerRequest, ServiceRank.Specialist, null);

        Assert.IsTrue(_sut.IsInDesertionCooldown("deserted_from"));
    }

    [TestMethod]
    public void IsInDesertionCooldown_UsesTheConfiguredDays_ForTheCurrentHero()
    {
        _content.History.Entries.Add(E("lord_a", reason: DischargeReason.Desertion, end: 280));

        Assert.IsTrue(_sut.IsInDesertionCooldown("lord_a"));
        _clock.NowDays.Returns(341.0);
        Assert.IsFalse(_sut.IsInDesertionCooldown("lord_a"));
    }

    [TestMethod]
    public void IsInDesertionCooldown_AnotherMainHero_HasNone()
    {
        _content.History.Entries.Add(E("lord_a", reason: DischargeReason.Desertion, end: 280));
        _playerParty.GetMainHeroId().Returns("hero_2");

        Assert.IsFalse(_sut.IsInDesertionCooldown("lord_a"));
    }

    [TestMethod]
    public void RestoreOnOath_AppliesTheRank_AndAnnouncesIt_NoXpNoDays()
    {
        _content.History.Entries.Add(E("lord_a", rank: ServiceRank.StaffSergeant, end: 250));

        _sut.RestoreOnOath("lord_a", Hero, Lord());

        Assert.AreEqual(ServiceRank.Sergeant, _content.Record.Rank);
        Assert.AreEqual(0, _content.Record.ServiceXp);
        Assert.AreEqual(0, _content.Record.DaysServed);
        _announcer.Received(1).AnnounceRemembered("Boromir", ServiceRank.Sergeant);
    }

    [TestMethod]
    public void RestoreOnOath_SeedsTheLedgerWithTheTierHeReallyKept()
    {
        _content.History.Entries.Add(E("lord_a", rank: ServiceRank.StaffSergeant, end: 250, tier: EnlistmentKitTier.Soldier));

        _sut.RestoreOnOath("lord_a", Hero, Lord());

        Assert.AreEqual((int)EnlistmentKitTier.Soldier, _content.Record.HighestIssuedKitTier);
        Assert.AreEqual(0, _content.Record.IssuedEquipmentItemIds.Count, "a seed carries no items");
    }

    [TestMethod]
    public void RestoreOnOath_AfterAFullReclaim_LeavesTheLedgerEmpty()
    {
        _content.History.Entries.Add(E("lord_a", rank: ServiceRank.StaffSergeant, end: 250, tier: null));

        _sut.RestoreOnOath("lord_a", Hero, Lord());

        Assert.IsNull(_content.Record.HighestIssuedKitTier, "the veteran can draw kit for his rank again");
    }

    [TestMethod]
    public void RestoreOnOath_ALowerKeptTier_StillLetsTheNextPromotionDrawAKit()
    {
        _content.History.Entries.Add(E("lord_a", rank: ServiceRank.StaffSergeant, end: 250, tier: EnlistmentKitTier.Veteran));

        _sut.RestoreOnOath("lord_a", Hero, Lord());

        Assert.AreEqual(ServiceRank.Sergeant, _content.Record.Rank);
        Assert.IsFalse(EnlistmentKitTiers.KitWaits(ServiceRank.Sergeant, _ledger), "the Veteran kit he kept covers a Sergeant");
        Assert.IsTrue(EnlistmentKitTiers.KitWaits(ServiceRank.StaffSergeant, _ledger), "the Sergeant-tier kit is still owed after the promotion");
    }

    [TestMethod]
    public void RestoreOnOath_NothingToRestore_ChangesAndSaysNothing()
    {
        _content.History.Entries.Add(E("lord_z", "clan_z", "k_z", ServiceRank.Sergeant, end: 250));

        _sut.RestoreOnOath("lord_a", Hero, Lord());

        Assert.AreEqual(ServiceRank.Private, _content.Record.Rank);
        _announcer.DidNotReceiveWithAnyArgs().AnnounceRemembered(default!, default);
    }

    [TestMethod]
    public void RestoreOnOath_AnotherHeroesService_RestoresNothing()
    {
        _content.History.Entries.Add(E("lord_a", rank: ServiceRank.StaffSergeant, end: 250, hero: "hero_2"));

        _sut.RestoreOnOath("lord_a", Hero, Lord());

        Assert.AreEqual(ServiceRank.Private, _content.Record.Rank);
    }

    [TestMethod]
    public void RestoreOnOath_NeverLowersARankAlreadyHeld()
    {
        _content.Record.Rank = ServiceRank.SergeantMajor;
        _content.History.Entries.Add(E("lord_a", rank: ServiceRank.Sergeant, end: 250));

        _sut.RestoreOnOath("lord_a", Hero, Lord());

        Assert.AreEqual(ServiceRank.SergeantMajor, _content.Record.Rank);
    }

    // ---- wiring around it ----

    [TestMethod]
    public void TheClanId_IsReadFromTheHero()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/CommanderLordAdapter.cs", stripComments: true);
        StringAssert.Contains(src, "clanId:");
        Assert.AreEqual("clan_a", Lord().ClanId);
    }

    [TestMethod]
    public void TheAnnouncer_UsesTheOathKey()
    {
        StringAssert.Contains(RepoPaths.ReadSource("Main/Features/Enlistment/Presentation/ServiceDailyAnnouncer.cs", stripComments: true), "{=taom_enlist_oath_remembered}");
    }

    [TestMethod]
    public void TheConfigKeys_AreShippedWithTheirDefaults()
    {
        var d = EnlistmentContentConfigProvider.BuildDefaults();
        Assert.AreEqual(730, d.ServiceHistoryExpiryDays);
        Assert.AreEqual(10, d.ServiceHistoryMaxEntries);
        Assert.AreEqual(60, d.DesertionCooldownDays);
    }
}
