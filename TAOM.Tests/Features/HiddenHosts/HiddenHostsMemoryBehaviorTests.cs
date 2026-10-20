using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.HiddenHosts;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846 phase 3: the memory behavior against a faked campaign. The engine's events are not raised here (that they are
/// registered is pinned in <see cref="HiddenHostsWiringTests"/>); each handler is called as the event would, and the
/// effect is read back through the service. The Scouting XP grant has its co-op gate checked for every peer role.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HiddenHostsMemoryBehaviorTests
{
    private IHiddenHostsSettings _settings = null!;
    private HiddenHostsService _service = null!;
    private IHiddenHostsCampaignAdapter _adapter = null!;
    private ICoopSessionProvider _coop = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void SetUp()
    {
        _settings = Substitute.For<IHiddenHostsSettings>();
        _settings.Enabled.Returns(true);
        _service = new HiddenHostsService(_settings);
        _adapter = Substitute.For<IHiddenHostsCampaignAdapter>();
        _adapter.NowHour.Returns(100);
        _adapter.MainScoutingSkill.Returns(0);
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _coop.IsCoopClient.Returns(false);
        _logger = Substitute.For<IModLogger>();
    }

    private HiddenHostsMemoryBehavior NewBehavior() => new(_service, _adapter, _coop, _logger);

    private static PartyIntelSnapshot Snap(int count, float ratio, string id = "lord_1", int hour = 100, bool atWar = false) => new()
    {
        HasData = true, StringId = id, HealthyCount = count, OwnHealthyCount = count, DistanceToMain = ratio * 100f,
        MainSeeingRange = 100f, SpottingRatio = 1f, NowHour = hour, AtWar = atWar, IsVisible = true,
    };

    private string? FarRead(string id = "lord_1", int count = 123, int hour = 101) => _service.Evaluate(Snap(count, 0.7f, id, hour)).CountText;

    private void StampParty(string id, int healthy, bool visible, int hour, int scout = 0) =>
        _service.Stamp(new PartyIntelSnapshot
        {
            HasData = true, StringId = id, HealthyCount = healthy, OwnHealthyCount = healthy, IsVisible = visible, NowHour = hour,
            ScoutingSkill = scout, InPlainSight = true,
        });

    private void StampTown(string id, int defenders, int hour) =>
        _service.StampSettlement(new SettlementIntelSnapshot
        {
            HasData = true, StringId = id, DefenderCount = defenders, IsPlayerSettlement = true, NowHour = hour,
        });

    private static PartyBase BarePartyBase() => (PartyBase)FormatterServices.GetUninitializedObject(typeof(PartyBase));

    // ---- Session reset

    [TestMethod]
    public void Constructor_ResetsTheMemoryAndThePendingXp_ForANewCampaign()
    {
        _service.Evaluate(Snap(123, 0.1f, atWar: true));
        Assert.AreEqual("~123", FarRead(), "precondition: the old campaign remembers the host");

        NewBehavior();

        Assert.AreEqual("???", FarRead());
        Assert.AreEqual(0, _service.TakePendingScoutXp());
    }

    // ---- Tick: the Scouting XP grant and its co-op gate

    private void EarnHundredXp() => _service.Evaluate(Snap(123, 0.1f, atWar: true));

    [TestMethod]
    public void Tick_TheAuthority_GrantsThePendingXp_OnceAndZeroesIt()
    {
                var behavior = NewBehavior();
        EarnHundredXp();

        behavior.OnTick(0.1f);
        behavior.OnTick(0.1f);

        _adapter.Received(1).GrantScoutingXp(100);
        Assert.AreEqual(0, _service.TakePendingScoutXp());
    }

    [DataTestMethod]
    [DataRow(true, false, true, "singleplayer or the co-op host")]
    [DataRow(false, true, false, "a co-op client")]
    [DataRow(false, false, false, "a peer that is not the authority")]
    [DataRow(true, true, false, "a peer that may not write save-backed state")]
    public void Tick_GrantsOnlyWhenThePeerMayWrite(bool authority, bool coopClient, bool granted, string peer)
    {
        _coop.IsAuthority.Returns(authority);
        _coop.IsCoopClient.Returns(coopClient);
        var behavior = NewBehavior();
        EarnHundredXp();

        behavior.OnTick(0.1f);

        _adapter.Received(granted ? 1 : 0).GrantScoutingXp(Arg.Any<int>());
        Assert.AreEqual(0, _service.TakePendingScoutXp(), peer + ": the pending XP is zeroed either way, so a client cannot bank it");
    }

    [TestMethod]
    public void BeforeSave_GrantsThePendingXp_SoASaveMadeWhileTimeIsStoppedHoldsIt()
    {
        var behavior = NewBehavior();
        EarnHundredXp();

        behavior.OnBeforeSave();

        _adapter.Received(1).GrantScoutingXp(100);
        Assert.AreEqual(0, _service.TakePendingScoutXp());
    }

    [DataTestMethod]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void BeforeSave_AClientOrAPeerThatMayNotWrite_DropsTheXp(bool authority, bool coopClient)
    {
        _coop.IsAuthority.Returns(authority);
        _coop.IsCoopClient.Returns(coopClient);
        var behavior = NewBehavior();
        EarnHundredXp();

        behavior.OnBeforeSave();

        _adapter.DidNotReceive().GrantScoutingXp(Arg.Any<int>());
        Assert.AreEqual(0, _service.TakePendingScoutXp());
    }

    [TestMethod]
    public void BeforeSave_TheSettingOff_GrantsNothing_AndAGrantFaultIsLoggedOnce()
    {
        var behavior = NewBehavior();
        _service.Evaluate(Snap(123, 0.1f, id: "a", atWar: true));
        _settings.Enabled.Returns(false);
        behavior.OnBeforeSave();
        _adapter.DidNotReceive().GrantScoutingXp(Arg.Any<int>());

        _settings.Enabled.Returns(true);
        _adapter.When(a => a.GrantScoutingXp(Arg.Any<int>())).Do(_ => throw new InvalidOperationException("no hero"));
        _service.Evaluate(Snap(123, 0.1f, id: "b", atWar: true));
        behavior.OnBeforeSave();
        _service.Evaluate(Snap(123, 0.1f, id: "c", atWar: true));
        behavior.OnBeforeSave();

        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Tick_NoPendingXp_GrantsNothing()
    {
        NewBehavior().OnTick(0.1f);

        _adapter.DidNotReceive().GrantScoutingXp(Arg.Any<int>());
    }

    [TestMethod]
    public void Tick_TheSettingOff_StillZeroesAnyPendingXp()
    {
        var behavior = NewBehavior();
        EarnHundredXp();
        _settings.Enabled.Returns(false);

        behavior.OnTick(0.1f);

        _adapter.DidNotReceive().GrantScoutingXp(Arg.Any<int>());
        Assert.AreEqual(0, _service.TakePendingScoutXp());
    }

    [TestMethod]
    public void Tick_TheGrantFaults_IsLogged_AndTheXpIsNotKept()
    {
        _adapter.When(a => a.GrantScoutingXp(Arg.Any<int>())).Do(_ => throw new InvalidOperationException("no hero"));
        var behavior = NewBehavior();
        EarnHundredXp();

        behavior.OnTick(0.1f);

        _logger.Received(1).LogError(Arg.Any<string>());
        Assert.AreEqual(0, _service.TakePendingScoutXp());
    }

    [TestMethod]
    public void Tick_TheSettingsReadFaults_DoesNotThrow_AndLogsOnce()
    {
        var behavior = NewBehavior();
        EarnHundredXp();
        _settings.Enabled.Returns(_ => throw new InvalidOperationException("settings gone"));

        behavior.OnTick(0.1f);
        behavior.OnBeforeSave();

        _logger.Received(1).LogError(Arg.Any<string>());
    }

    // ---- Hourly fade

    [TestMethod]
    public void Hourly_FadesTheMemoryByTheClock_WithTheMainPartysScout()
    {
        var behavior = NewBehavior();
        _service.Evaluate(Snap(123, 0.1f, hour: 100));
        _adapter.TryReadVisibilityChange(Arg.Any<PartyBase>(), out Arg.Any<string>(), out Arg.Any<bool>())
            .Returns(x => { x[1] = "lord_1"; x[2] = false; return true; });
        behavior.OnVisibilityChanged(BarePartyBase());

        _adapter.NowHour.Returns(106);
        behavior.OnHourlyTick();

        Assert.AreEqual("~12?", FarRead(hour: 106));
    }

    [TestMethod]
    public void Hourly_AsksTheAdapterAboutEverySettlementRow()
    {
        var behavior = NewBehavior();
        StampTown("town_1", 245, 100);
        _adapter.IsSettlementInSight("town_1").Returns((bool?)null);
        _adapter.NowHour.Returns(101);

        behavior.OnHourlyTick();

        _adapter.Received(1).IsSettlementInSight("town_1");
        Assert.IsNull(_service.EvaluateTooltipSettlement(new SettlementIntelSnapshot
        {
            HasData = true, StringId = "town_1", DefenderCount = 245, DistanceToMain = 70f, MainSeeingRange = 100f, NowHour = 102,
        }).MemoryText, "a settlement that is gone is forgotten");
    }

    [TestMethod]
    public void Hourly_TheSettingOff_StillFades()
    {
        var behavior = NewBehavior();
        StampParty("lord_1", 123, false, 100);
        _settings.Enabled.Returns(false);
        _adapter.NowHour.Returns(106);

        behavior.OnHourlyTick();
        _settings.Enabled.Returns(true);

        Assert.AreEqual("~12?", FarRead(hour: 106), "the memory kept aging while the feature was off");
    }

    [TestMethod]
    public void Hourly_LogsAnInsertTheMemoryRefused_Once()
    {
        var behavior = NewBehavior();
        for (int i = 0; i < HiddenHostsMemory.MaxParties; i++) _service.Evaluate(Snap(123, 0.1f, id: "p" + i));
        _service.Evaluate(Snap(123, 0.1f, id: "one_too_many"));

        behavior.OnHourlyTick();
        behavior.OnHourlyTick();

        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Hourly_AnAdapterFault_IsLoggedOnce()
    {
        _adapter.NowHour.Throws(new InvalidOperationException("no clock"));
        var behavior = NewBehavior();

        behavior.OnHourlyTick();
        behavior.OnHourlyTick();

        _logger.Received(1).LogError(Arg.Any<string>());
    }

    // ---- Visibility and destroy

    [TestMethod]
    public void Visibility_LeavingSight_StartsTheClock_ComingBackStopsIt()
    {
        var behavior = NewBehavior();
        _service.Evaluate(Snap(123, 0.1f, hour: 100));
        _adapter.TryReadVisibilityChange(Arg.Any<PartyBase>(), out Arg.Any<string>(), out Arg.Any<bool>())
            .Returns(x => { x[1] = "lord_1"; x[2] = false; return true; });
        behavior.OnVisibilityChanged(BarePartyBase());
        _adapter.NowHour.Returns(106);
        behavior.OnHourlyTick();
        Assert.AreEqual("~12?", FarRead(hour: 106));

        _adapter.TryReadVisibilityChange(Arg.Any<PartyBase>(), out Arg.Any<string>(), out Arg.Any<bool>())
            .Returns(x => { x[1] = "lord_1"; x[2] = true; return true; });
        behavior.OnVisibilityChanged(BarePartyBase());
        _adapter.NowHour.Returns(300);
        behavior.OnHourlyTick();

        Assert.AreEqual("~12?", FarRead(hour: 300), "back in sight: no more fading");
    }

    [TestMethod]
    public void Visibility_APartyWithNoMobileParty_IsIgnored()
    {
        var behavior = NewBehavior();
        _service.Evaluate(Snap(123, 0.1f));
        _adapter.TryReadVisibilityChange(Arg.Any<PartyBase>(), out Arg.Any<string>(), out Arg.Any<bool>()).Returns(false);

        behavior.OnVisibilityChanged(BarePartyBase());
        _adapter.NowHour.Returns(500);
        behavior.OnHourlyTick();

        Assert.AreEqual("~123", FarRead(hour: 500));
    }

    [TestMethod]
    public void Visibility_TheSettingOff_StillRuns()
    {
        _settings.Enabled.Returns(false);
        var behavior = NewBehavior();

        behavior.OnVisibilityChanged(BarePartyBase());

        _adapter.Received(1).TryReadVisibilityChange(Arg.Any<PartyBase>(), out Arg.Any<string>(), out Arg.Any<bool>());
    }

    [TestMethod]
    public void Visibility_AnAdapterFault_IsLoggedOnce()
    {
        _adapter.TryReadVisibilityChange(Arg.Any<PartyBase>(), out Arg.Any<string>(), out Arg.Any<bool>())
            .Throws(new InvalidOperationException("boom"));
        var behavior = NewBehavior();

        behavior.OnVisibilityChanged(BarePartyBase());
        behavior.OnVisibilityChanged(BarePartyBase());

        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Destroyed_ForgetsThePartysRow()
    {
        var behavior = NewBehavior();
        _service.Evaluate(Snap(123, 0.1f, id: "lord_1"));
        _service.Evaluate(Snap(123, 0.1f, id: "lord_2"));
        var party = (MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty));
        party.StringId = "lord_1";

        behavior.OnPartyDestroyed(party, BarePartyBase());

        Assert.AreEqual("???", FarRead("lord_1"));
        Assert.AreEqual("~123", FarRead("lord_2"));
    }

    [TestMethod]
    public void Destroyed_ANullOrUnnamedParty_IsIgnored()
    {
        var behavior = NewBehavior();

        behavior.OnPartyDestroyed(null!, null!);
        behavior.OnPartyDestroyed((MobileParty)FormatterServices.GetUninitializedObject(typeof(MobileParty)), null!);

        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    // ---- SyncData

    [TestMethod]
    public void SyncData_SaveThenLoad_IntoAFreshBehavior_RestoresTheMemory()
    {
        var a = NewBehavior();
        _service.Evaluate(Snap(123, 0.3f));
        StampTown("town_1", 245, 100);
        var store = new EngineDataStore { Loading = false };
        a.SyncData(store);
        Assert.IsTrue(store.Has("_taomHiddenHostsMemoryCount"));
        Assert.IsTrue(store.Has("_taomHiddenHostsMemory_0"));

        store.Loading = true;
        var fresh = new HiddenHostsService(_settings);
        var b = new HiddenHostsMemoryBehavior(fresh, _adapter, _coop, _logger);
        b.SyncData(store);

        Assert.AreEqual("~12?", fresh.Evaluate(Snap(123, 0.7f, hour: 101)).CountText);
        Assert.AreEqual("~245", fresh.EvaluateTooltipSettlement(new SettlementIntelSnapshot
        {
            HasData = true, StringId = "town_1", DefenderCount = 245, DistanceToMain = 70f, MainSeeingRange = 100f, NowHour = 101,
        }).MemoryText);
    }

    [TestMethod]
    public void SyncData_LoadOverAnOldSession_ReplacesItWithTheSavedMemory_NotMergesIt()
    {
        var store = new EngineDataStore { Loading = false };
        NewBehavior().SyncData(store);

        var behavior = NewBehavior();
        _service.Evaluate(Snap(123, 0.1f, id: "from_the_old_session"));
        store.Loading = true;
        behavior.SyncData(store);

        Assert.AreEqual("???", FarRead("from_the_old_session"));
    }

    [TestMethod]
    public void SyncData_ASaveBeforeThisFeature_LoadsEmpty()
    {
        var store = new EngineDataStore { Loading = true };

        NewBehavior().SyncData(store);

        Assert.AreEqual("???", FarRead());
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SyncData_AForeignPayload_LoadsNothing_AndWarnsOnce()
    {
        var store = new EngineDataStore { Loading = true };
        store.Seed("_taomHiddenHostsMemoryCount", 1);
        store.Seed("_taomHiddenHostsMemory_0", "{\"from\":\"another mod\"}");

        NewBehavior().SyncData(store);

        Assert.AreEqual("???", FarRead());
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("unknown memory format")));
    }

    [TestMethod]
    public void SyncData_InvalidRows_AreSkipped_AndCountedInOneWarning()
    {
        var store = new EngineDataStore { Loading = true };
        store.Seed("_taomHiddenHostsMemoryCount", 1);
        store.Seed("_taomHiddenHostsMemory_0", "HH1\np|good|123|3|100|-1|0|-1\np|bad|x|3|100|-1|0|-1\np|worse|1|9|100|-1|0|-1");

        NewBehavior().SyncData(store);

        Assert.AreEqual("~123", FarRead("good"));
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("2 invalid")));
    }

    [TestMethod]
    public void SyncData_SaveFaults_WriteZeroChunks_AndLog()
    {
        var behavior = NewBehavior();
        _adapter.NowHour.Throws(new InvalidOperationException("no clock"));
        var store = new EngineDataStore { Loading = false };

        behavior.SyncData(store);

        Assert.AreEqual(0, store.Get<int>("_taomHiddenHostsMemoryCount"));
        _logger.Received(1).LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void SyncData_ALargeMemory_IsSplitAcrossChunksUnderTheEngineLimit()
    {
        var behavior = NewBehavior();
        for (int i = 0; i < 600; i++) StampParty("a_party_with_a_fairly_long_string_id_" + i, 150 + i, true, 100);
        var store = new EngineDataStore { Loading = false };

        behavior.SyncData(store);

        int chunks = store.Get<int>("_taomHiddenHostsMemoryCount");
        Assert.IsTrue(chunks > 1, "chunks: " + chunks);
        for (int i = 0; i < chunks; i++)
            Assert.IsTrue(TAOM.Core.Persistence.SyncDataChunker.Utf8ByteLength(store.Get<string>("_taomHiddenHostsMemory_" + i))
                          <= TAOM.Core.Persistence.SyncDataChunker.EngineEntryByteLimit);
    }

    [TestMethod]
    public void SyncData_TheSettingOff_StillSavesTheMemory()
    {
        var behavior = NewBehavior();
        _service.Evaluate(Snap(123, 0.3f));
        _settings.Enabled.Returns(false);
        var store = new EngineDataStore { Loading = false };

        behavior.SyncData(store);

        StringAssert.Contains(store.Get<string>("_taomHiddenHostsMemory_0"), "p|lord_1|123|2|100");
    }
}
