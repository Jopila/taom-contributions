using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Core.Logging;
using TAOM.Features.HarvestRoad;
using TAOM.Features.HarvestRoad.Hooks;

namespace TAOM.Tests.Features.HarvestRoad;

/// <summary>
/// The patch bodies cannot run without a campaign, so these pin the one thing a trade must be able to count on: a patch
/// that cannot do its work does nothing and says so once. A trade never breaks for a notice.
/// </summary>
[TestClass]
public class HarvestRoadPatchTests
{
    private static IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        var settings = Substitute.For<IHarvestRoadSettingsProvider>();
        settings.Enabled.Returns(true);
        HarvestRoadPatches.Initialize(new HarvestRoadService(settings), _logger);
    }

    [TestMethod]
    public void SalePrefix_WithNoPartyOrSettlement_LeavesNoSnapshot_AndDoesNotThrow()
    {
        HarvestRoadPatches.VillagerSalePatch.Prefix(null!, null!, out var state);

        Assert.IsNull(state);
    }

    [TestMethod]
    public void FoodPrefix_WithNoPartyOrSettlement_LeavesNoSnapshot_AndDoesNotThrow()
    {
        HarvestRoadPatches.FoodPurchasePatch.Prefix(null!, null!, 10, out var state);

        Assert.IsNull(state);
    }

    [TestMethod]
    public void Postfixes_WithNoSnapshot_DoNothing_AndDoNotThrow()
    {
        HarvestRoadPatches.VillagerSalePatch.Postfix(null!, null!, null);
        HarvestRoadPatches.FoodPurchasePatch.Postfix(null!, null!, null);

        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void Postfixes_WithASnapshotButNoParty_SwallowTheFault_AndWarnOnce()
    {
        var snapshot = new List<(EquipmentElement, int)>();

        HarvestRoadPatches.VillagerSalePatch.Postfix(null!, null!, snapshot);
        HarvestRoadPatches.VillagerSalePatch.Postfix(null!, null!, snapshot);

        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void ReportOnce_NamesAKeyOnce()
    {
        HarvestRoadPatches.ReportOnce("test-key-once", "first");
        HarvestRoadPatches.ReportOnce("test-key-once", "second");

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("first")));
        _logger.DidNotReceive().LogWarning(Arg.Is<string>(m => m.Contains("second")));
    }
}
