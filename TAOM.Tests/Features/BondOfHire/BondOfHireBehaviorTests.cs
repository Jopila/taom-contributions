using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BondOfHire;
using TAOM.Features.BondOfHire.Hooks;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.BondOfHire;

/// <summary>
/// The listener is a thin boundary: it hands the service the clan that just signed on, and a fault never escapes into the
/// engine's event invoke, which has no try of its own. Building a CampaignBehaviorBase runs engine code, hence the category.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class BondOfHireBehaviorTests
{
    private IBondOfHireSettingsProvider _settings = null!;
    private IModLogger _logger = null!;
    private IMercenaryClanAdapter _clan = null!;
    private BondOfHireBehavior _behavior = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IBondOfHireSettingsProvider>();
        _settings.MinimumDays.Returns(30);
        _logger = Substitute.For<IModLogger>();
        _clan = Substitute.For<IMercenaryClanAdapter>();
        var service = new BondOfHireService(_settings, Substitute.For<ICoopSessionProvider>());
        _behavior = new BondOfHireBehavior(service, _logger);
    }

    [TestMethod]
    public void Handle_ANewMercenary_ReachesTheService()
    {
        _behavior.Handle(_clan);

        _clan.Received(1).SetStayUntilDaysFromNow(30);
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Handle_NoClan_DoesNothingAndNeverFaults()
    {
        _behavior.Handle(null);

        _ = _settings.DidNotReceive().MinimumDays;
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void OnMercenaryServiceStarted_ANullClan_DoesNothingAndNeverFaults()
    {
        _behavior.OnMercenaryServiceStarted(null!, StartMercenaryServiceAction.StartMercenaryServiceActionDetails.ApplyByDefault);

        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void Handle_AFaultReadingTheClan_IsLoggedAndNeverEscapes()
    {
        _clan.RemainingStayDays.Returns(_ => throw new InvalidOperationException("boom"));

        _behavior.Handle(_clan);

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[BondOfHire]") && s.Contains("boom")));
    }

    [TestMethod]
    public void Handle_AFaultWritingTheDate_IsLoggedAndNeverEscapes()
    {
        _clan.When(c => c.SetStayUntilDaysFromNow(Arg.Any<int>())).Do(_ => throw new InvalidOperationException("no write"));

        _behavior.Handle(_clan);

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[BondOfHire]") && s.Contains("no write")));
    }
}
