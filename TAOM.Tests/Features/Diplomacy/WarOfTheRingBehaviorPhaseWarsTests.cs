using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// Wars the phase forbids are ended on the daily tick, after the phase check, on the authority peer
/// only (never at session launch). The order matters: a mid-campaign install loads in Peace and jumps to FullWar inside CheckPhaseTransition,
/// so reconciling first would end every war. IsAuthority alone fails open under BannerlordTogether,
/// so the gate also reads ShouldDeferToHost.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class WarOfTheRingBehaviorPhaseWarsTests
{
    private IWarOfTheRingService _service = null!;
    private ICoopSessionProvider _coop = null!;
    private WarOfTheRingBehavior _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IWarOfTheRingService>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _coop.ShouldDeferToHost.Returns(false);
        _sut = new WarOfTheRingBehavior(_service, Substitute.For<IModLogger>(), _coop, () => 50f);
    }

    // A load runs OnSessionLaunched before MapEventManager/SiegeEventManager.OnAfterLoad, and a
    // MakePeace there can disturb live sieges and battles, so the reconcile waits for the daily tick.
    [TestMethod]
    public void OnSessionLaunched_Authority_DoesNotEndWars()
    {
        _sut.OnSessionLaunched(null!);

        _service.Received(1).CheckPhaseTransition(50f);
        _service.DidNotReceive().EndWarsForbiddenByPhase();
    }

    [TestMethod]
    public void OnDailyTick_NotAuthority_DoesNotEndWars()
    {
        _coop.IsAuthority.Returns(false);

        _sut.OnDailyTick();

        _service.DidNotReceive().EndWarsForbiddenByPhase();
    }

    [TestMethod]
    public void OnDailyTick_DeferToHost_DoesNotEndWars()
    {
        _coop.ShouldDeferToHost.Returns(true);

        _sut.OnDailyTick();

        _service.DidNotReceive().EndWarsForbiddenByPhase();
    }

    [TestMethod]
    public void OnDailyTick_Authority_EndsForbiddenWarsAfterPhaseCheck()
    {
        _sut.OnDailyTick();

        Received.InOrder(() =>
        {
            _service.CheckPhaseTransition(50f);
            _service.EndWarsForbiddenByPhase();
        });
    }
}
