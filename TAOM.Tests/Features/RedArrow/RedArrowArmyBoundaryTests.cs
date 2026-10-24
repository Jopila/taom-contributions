using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.RedArrow;

namespace TAOM.Tests.Features.RedArrow;

/// <summary>
/// The engine boundary of the Red Arrow with fake adapters (#873). A null party stands for the engine object the adapters
/// would read (the substitutes ignore it). <c>TryRecheck</c> reads the candidate itself and never asks the active model, so
/// every path runs here. The class builds <see cref="TextObject"/>s, so it needs the game assemblies.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class RedArrowArmyBoundaryTests
{
    private IRedArrowSettingsProvider _settings = null!;
    private IPlayerSiegeStateAdapter _player = null!;
    private IArmyCallCandidateAdapter _candidates = null!;
    private IModLogger _logger = null!;
    private RedArrowArmyBoundary _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IRedArrowSettingsProvider>();
        _settings.Enabled.Returns(true);
        _player = Substitute.For<IPlayerSiegeStateAdapter>();
        _player.ReadSiege().Returns(new PlayerSiegeState(true, true));
        _candidates = Substitute.For<IArmyCallCandidateAdapter>();
        _logger = Substitute.For<IModLogger>();
        var service = new RedArrowService(_settings, Substitute.For<ICoopSessionProvider>(), Substitute.For<IDedicatedServerProvider>());
        _sut = new RedArrowArmyBoundary(service, _player, _candidates, _logger);
    }

    private static ArmyCallCandidate Eligible() => new ArmyCallCandidate(true, false, CandidateArmy.None, false, 0.9f, 0.4f, false,
        false, true, false, 10f, 100f);

    private static ArmyCallCandidate InAnotherArmy() => new ArmyCallCandidate(true, false, CandidateArmy.OtherArmy, false, 0.9f, 0.4f, false,
        false, true, false, 10f, 100f);

    [TestMethod]
    public void TryRecheck_WhenTheFeatureDoesNotApply_LeavesVanillasResult_AndReadsNothingElse()
    {
        _settings.Enabled.Returns(false);

        var handled = _sut.TryRecheck(null!, out var eligible, out var reason);

        Assert.IsFalse(handled);
        Assert.IsFalse(eligible);
        Assert.IsNotNull(reason);
        _candidates.DidNotReceiveWithAnyArgs().Read(null);
        _player.DidNotReceive().ReadMapAction();
    }

    [TestMethod]
    public void TryRecheck_ARefusedParty_CarriesTheCandidatesOwnVanillaText_WithoutTheMapCheck()
    {
        _candidates.Read(null).ReturnsForAnyArgs(InAnotherArmy());

        var handled = _sut.TryRecheck(null!, out var eligible, out var reason);

        Assert.IsTrue(handled);
        Assert.IsFalse(eligible);
        StringAssert.StartsWith(reason.Value, "{=aROohsat}");
        _player.DidNotReceive().ReadMapAction();
    }

    [TestMethod]
    public void TryRecheck_AnEligibleParty_TakesTheMapVerdict_Clear()
    {
        _candidates.Read(null).ReturnsForAnyArgs(Eligible());
        _player.ReadMapAction().Returns(default(MapActionState));

        var handled = _sut.TryRecheck(null!, out var eligible, out var reason);

        Assert.IsTrue(handled);
        Assert.IsTrue(eligible);
        Assert.IsTrue(string.IsNullOrEmpty(reason.Value), "vanilla's own value for an allowed action is the empty text");
    }

    [TestMethod]
    public void TryRecheck_AnEligibleParty_TakesTheMapVerdict_AMapEventBlocks()
    {
        _candidates.Read(null).ReturnsForAnyArgs(Eligible());
        _player.ReadMapAction().Returns(new MapActionState(false, false, false, false, false, false, false, false, false, hasMapEvent: true));

        var handled = _sut.TryRecheck(null!, out var eligible, out var reason);

        Assert.IsTrue(handled);
        Assert.IsFalse(eligible);
        StringAssert.StartsWith(reason.Value, "{=MIylzRc5}");
    }

    [TestMethod]
    public void TryRecheck_AFailingCandidateRead_LeavesVanillasResult_AndIsLoggedOnce()
    {
        _candidates.Read(null).ThrowsForAnyArgs(new InvalidOperationException("boom"));

        var first = _sut.TryRecheck(null!, out var eligible, out _);
        _sut.TryRecheck(null!, out _, out _);

        Assert.IsFalse(first);
        Assert.IsFalse(eligible);
        Assert.AreEqual(2, _sut.Faults);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("[RedArrow]") && m.Contains("boom")));
    }

    [TestMethod]
    public void TryRecheck_AFailingSiegeRead_LeavesVanillasResult_AndIsLogged()
    {
        _player.ReadSiege().Throws(new InvalidOperationException("boom"));

        var handled = _sut.TryRecheck(null!, out var eligible, out _);

        Assert.IsFalse(handled);
        Assert.IsFalse(eligible);
        _logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("TryRecheck")));
    }

    [TestMethod]
    public void TransferDisabled_FollowsTheService_AndFallsBackToNullOnAFault()
    {
        Assert.AreEqual(false, _sut.TransferDisabled(isMainHero: false));
        Assert.AreEqual(true, _sut.TransferDisabled(isMainHero: true));

        _settings.Enabled.Returns(false);
        Assert.IsNull(_sut.TransferDisabled(isMainHero: false));

        _settings.Enabled.Returns(true);
        _player.ReadSiege().Throws(new InvalidOperationException("boom"));
        Assert.IsNull(_sut.TransferDisabled(isMainHero: false));
        Assert.AreEqual(1, _sut.Faults);
    }

    [TestMethod]
    public void EveryPartyReason_HasVanillasKeyedText_AndNoneHasNone()
    {
        Assert.IsNull(RedArrowTexts.For(ArmyCallReason.None));
        var reasons = Enum.GetValues(typeof(ArmyCallReason)).Cast<ArmyCallReason>().Where(r => r != ArmyCallReason.None).ToList();

        var keys = reasons.Select(r => RedArrowTexts.For(r)!.Value.Substring(0, RedArrowTexts.For(r)!.Value.IndexOf('}') + 1)).ToList();

        Assert.AreEqual(11, keys.Count);
        Assert.AreEqual(keys.Count, keys.Distinct().Count(), "one key per reason");
        Assert.IsTrue(keys.All(k => k.StartsWith("{=", StringComparison.Ordinal) && !k.StartsWith("{=taom_", StringComparison.Ordinal)),
            "vanilla's keys, never a TAOM row");
    }

    [TestMethod]
    public void TheMapReasonsThatNeedNoGameText_AreVanillasKeyedTexts()
    {
        StringAssert.StartsWith(RedArrowTexts.For(MapActionReason.InMission).Value, "{=FdzsOvDq}");
        StringAssert.StartsWith(RedArrowTexts.For(MapActionReason.MapEvent).Value, "{=MIylzRc5}");
    }

    [TestMethod]
    public void AnUnmappedReason_IsRefused()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => RedArrowTexts.For((ArmyCallReason)999));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => RedArrowTexts.For((MapActionReason)999));
    }
}
