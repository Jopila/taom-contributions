using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp;
using TAOM.Features.FieldCamp.Activities;
using TAOM.Features.FieldCamp.Domain;

namespace TAOM.Tests.Features.FieldCamp;

/// <summary>
/// Starting a new campaign already in camp (#860). The engine raises <c>OnCharacterCreationIsOverEvent</c> from
/// <c>CharacterCreationState.FinalizeCharacterCreationState</c>, after the map state is pushed, ten times in a row (once per index);
/// the first map tick is the retry that pitches the camp. The logic is pinned here without the game: the arming, the tick,
/// the one report of a block, the give-up and the per-campaign freshness. The hook hands a block to an injected callback; no
/// test here builds a game text.
/// </summary>
[TestClass]
public class CampStartInCampTests
{
    private ICampService _camps = null!;
    private ICampSettingsProvider _settings = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IModLogger _logger = null!;
    private List<CampBlockReason> _gaveUp = null!;

    [TestInitialize]
    public void SetUp()
    {
        _camps = Substitute.For<ICampService>();
        _camps.CanEstablish(CampType.Field, true).Returns(CampBlockReason.None);
        _camps.Establish(CampType.Field, true).Returns(true);
        _settings = Substitute.For<ICampSettingsProvider>();
        _settings.Enabled.Returns(true);
        _settings.CampStartInCamp.Returns(true);
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _logger = Substitute.For<IModLogger>();
        _gaveUp = new List<CampBlockReason>();
    }

    private CampStartInCamp New() => new(_camps, _settings, _coop, _dedicated, _logger, _gaveUp.Add);

    private static void EngineRaisesTheEvent(CampStartInCamp sut)
    {
        // CampaignEvents.OnCharacterCreationIsOver invokes the event for i in 0..9.
        for (int i = 0; i < 10; i++) sut.OnCharacterCreationIsOver(i);
    }

    [TestMethod]
    public void TheSettingOff_NeverArms_AndNeverTouchesTheCamp()
    {
        _settings.CampStartInCamp.Returns(false);
        var sut = New();

        EngineRaisesTheEvent(sut);
        sut.OnMapTick();

        _camps.DidNotReceiveWithAnyArgs().CanEstablish(default, default);
        _camps.DidNotReceiveWithAnyArgs().Establish(default, default);
        _logger.DidNotReceiveWithAnyArgs().LogInfo(default!);
    }

    [TestMethod]
    public void ATickBeforeTheEvent_DoesNothing()
    {
        New().OnMapTick();

        _camps.DidNotReceiveWithAnyArgs().CanEstablish(default, default);
    }

    [TestMethod]
    public void TheEventAlone_DoesNotEstablish_TheFirstMapTickDoes_Once()
    {
        var sut = New();

        EngineRaisesTheEvent(sut);
        _camps.DidNotReceiveWithAnyArgs().Establish(default, default);

        sut.OnMapTick();
        sut.OnMapTick();
        sut.OnMapTick();

        _camps.Received(1).Establish(CampType.Field, true);
        _camps.Received(1).CanEstablish(CampType.Field, true);
    }

    [TestMethod]
    public void TheStartAttempt_SkipsOnlyTheTownDistanceRule_ThroughTheSeam()
    {
        var sut = New();
        EngineRaisesTheEvent(sut);

        sut.OnMapTick();

        _camps.Received(1).CanEstablish(CampType.Field, ignoreTownDistance: true);
        _camps.Received(1).Establish(CampType.Field, ignoreTownDistance: true);
        _camps.DidNotReceive().CanEstablish(CampType.Field, ignoreTownDistance: false);
    }

    [TestMethod]
    public void ALateEventAfterTheCampStood_DoesNotRearm()
    {
        var sut = New();
        EngineRaisesTheEvent(sut);
        sut.OnMapTick();

        EngineRaisesTheEvent(sut);
        sut.OnMapTick();

        _camps.Received(1).Establish(CampType.Field, true);
    }

    [TestMethod]
    public void TheLog_NamesTheHook_OncePerStart_ForTheSmokeTest()
    {
        var sut = New();
        EngineRaisesTheEvent(sut);
        sut.OnMapTick();

        _logger.Received(1).LogInfo(NSubstitute.Arg.Is<string>(m => m.Contains("OnCharacterCreationIsOverEvent") && m.Contains("established")));
        Assert.AreEqual(0, _gaveUp.Count);
    }

    [DataTestMethod]
    [DataRow(CampBlockReason.InSettlement)]
    [DataRow(CampBlockReason.Moving)]
    [DataRow(CampBlockReason.External)]
    [DataRow(CampBlockReason.TerrainUnsuitable)]
    public void ABlockedStart_ReportsTheReasonOnce_AndGivesUp(CampBlockReason reason)
    {
        _camps.CanEstablish(CampType.Field, true).Returns(reason);
        var sut = New();
        EngineRaisesTheEvent(sut);

        sut.OnMapTick();
        sut.OnMapTick();

        CollectionAssert.AreEqual(new[] { reason }, _gaveUp);
        _camps.DidNotReceiveWithAnyArgs().Establish(default, default);
        _camps.Received(1).CanEstablish(CampType.Field, true);
    }

    [TestMethod]
    public void TheFeatureOff_GivesUpSilently()
    {
        _camps.CanEstablish(CampType.Field, true).Returns(CampBlockReason.FeatureDisabled);
        var sut = New();
        EngineRaisesTheEvent(sut);

        sut.OnMapTick();

        Assert.AreEqual(0, _gaveUp.Count);
        _camps.DidNotReceiveWithAnyArgs().Establish(default, default);
    }

    [TestMethod]
    public void AnEstablishThatFailsAfterTheCheck_ReportsOnce_AndGivesUp()
    {
        _camps.Establish(CampType.Field, true).Returns(false);
        var sut = New();
        EngineRaisesTheEvent(sut);

        sut.OnMapTick();
        sut.OnMapTick();

        Assert.AreEqual(1, _gaveUp.Count);
        _camps.Received(1).Establish(CampType.Field, true);
    }

    [TestMethod]
    public void ASharedCampaign_GivesUpSilently()
    {
        _coop.IsSessionActive.Returns(true);
        var sut = New();
        EngineRaisesTheEvent(sut);

        sut.OnMapTick();

        _camps.DidNotReceiveWithAnyArgs().CanEstablish(default, default);
        Assert.AreEqual(0, _gaveUp.Count);
    }

    [TestMethod]
    public void ACheckThatThrows_LogsOneError_AndMakesNoSecondAttempt()
    {
        _camps.CanEstablish(CampType.Field, true).Returns(_ => throw new InvalidOperationException("no campaign"));
        var sut = New();
        EngineRaisesTheEvent(sut);

        sut.OnMapTick();
        sut.OnMapTick();
        sut.OnMapTick();

        _logger.ReceivedWithAnyArgs(1).LogError(default!);
        _camps.Received(1).CanEstablish(CampType.Field, true);
        _camps.DidNotReceiveWithAnyArgs().Establish(default, default);
    }

    [TestMethod]
    public void ALoadedSave_NeverArms()
    {
        // No new-game event is raised on a load, so a hook that only ever sees ticks stays idle.
        var sut = New();
        for (int i = 0; i < 100; i++) sut.OnMapTick();

        _camps.DidNotReceiveWithAnyArgs().CanEstablish(default, default);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void EachCampaignStart_GetsAFreshHook_FromTheModulesBehaviorFactory()
    {
        // The runner calls the declaration's factory once per campaign start; a second campaign in one game session must arm again.
        using var container = new Container();
        container.RegisterInstance(_camps);
        container.RegisterInstance(_settings);
        container.RegisterInstance(_coop);
        container.RegisterInstance(_dedicated);
        container.RegisterInstance(_logger);
        container.RegisterInstance(Substitute.For<ICampWorkAdapter>());
        container.RegisterInstance(Substitute.For<IGameMenuAdapter>());
        container.Register<CampActivityService>(Reuse.Singleton);
        var decl = new CampActivitiesModule().CampaignBehaviors.Single();

        var first = decl.Create(container);
        var second = decl.Create(container);

        Assert.AreNotSame(first, second);
        var field = first.GetType().GetField("_start", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var firstHook = (CampStartInCamp)field.GetValue(first)!;
        var secondHook = (CampStartInCamp)field.GetValue(second)!;
        Assert.AreNotSame(firstHook, secondHook);

        EngineRaisesTheEvent(firstHook);
        firstHook.OnMapTick();
        _camps.Received(1).Establish(CampType.Field, true);

        EngineRaisesTheEvent(secondHook);
        secondHook.OnMapTick();
        _camps.Received(2).Establish(CampType.Field, true);
    }
}
