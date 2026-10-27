using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Dependencies.Foundation;
using TAOM.Features.AdvancedCombat.Hooks;
using TAOM.Features.Arena.Hooks;
using TAOM.Features.ArmyTargeting.Hooks;
using TAOM.Features.HeroRace.Hooks;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// The NRE containment finalizers hand every other exception back. A value-returning finalizer makes
/// Harmony end with <c>throw</c>, which erases the throw site unless the exception goes back through
/// <see cref="RethrowStackPreserver"/> (lessons/harmony-il.md, "A value-returning finalizer that hands
/// back its exception erases the throw site"; NRE guard audit 2026-10-27). Each test throws a real
/// exception, so it has frames to preserve.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ContainmentFinalizerRethrowTests
{
    [TestMethod]
    public void Patch49_NonNre_GoesBackWithItsThrowSite() =>
        AssertPreserved(ex => Army_FindBestGatheringSettlementAndMoveTheLeader_Patch.Finalizer(ex, null!, null!));

    [TestMethod]
    public void Patch50_NonNre_GoesBackWithItsThrowSite() =>
        AssertPreserved(Agent_CheckToDropFlaggedItem_Guard_Patch.Finalizer);

    [TestMethod]
    public void Patch56_NonNre_GoesBackWithItsThrowSite() =>
        AssertPreserved(ex => GauntletSceneNotification_OpenScene_Guard_Patch.Finalizer(ex, null!));

    [TestMethod]
    public void Patch56_Nre_IsSwallowed_LogsAWarningWithTheStack_AndRequestsTheClose()
    {
        var logger = Substitute.For<IModLogger>();
        GauntletSceneNotification_OpenScene_Guard_Patch.UseLogger(logger);
        GauntletSceneNotification_OpenScene_Guard_Patch.CloseRequested = false;
        NullReferenceException nre;
        try { throw new NullReferenceException("scene character"); }
        catch (NullReferenceException ex) { nre = ex; }

        try
        {
            var returned = GauntletSceneNotification_OpenScene_Guard_Patch.Finalizer(nre, null!);

            Assert.IsNull(returned);
            Assert.IsTrue(GauntletSceneNotification_OpenScene_Guard_Patch.CloseRequested);
            logger.Received(1).LogWarning(Arg.Is<string>(m =>
                m.Contains("NullReferenceException")
                && m.Contains(nameof(Patch56_Nre_IsSwallowed_LogsAWarningWithTheStack_AndRequestsTheClose))));
        }
        finally
        {
            GauntletSceneNotification_OpenScene_Guard_Patch.CloseRequested = false;
            GauntletSceneNotification_OpenScene_Guard_Patch.UseLogger(null!);
        }
    }

    [TestMethod]
    public void Patch69_NonNre_NoLogger_GoesBackWithItsThrowSite() =>
        AssertPreserved(ex => Patch69_TournamentEndGuard.Settle(ex, null!, () => "bracket", null!));

    [TestMethod]
    public void Patch69_NonNre_WithLogger_GoesBackWithItsThrowSite() =>
        AssertPreserved(ex => Patch69_TournamentEndGuard.Settle(ex, Substitute.For<IModLogger>(), () => "bracket", null!));

    [TestMethod]
    public void Patch69_Nre_IsSwallowed_AndTheErrorLineCarriesTheStack()
    {
        var logger = Substitute.For<IModLogger>();
        NullReferenceException nre;
        try { throw new NullReferenceException("winner panel"); }
        catch (NullReferenceException ex) { nre = ex; }

        var returned = Patch69_TournamentEndGuard.Settle(nre, logger, () => "bracket", null!);

        Assert.IsNull(returned);
        logger.Received().LogError(Arg.Is<string>(m =>
            m.Contains("NullReferenceException") && m.Contains(nameof(Patch69_Nre_IsSwallowed_AndTheErrorLineCarriesTheStack))));
    }

    private static void AssertPreserved(Func<Exception, Exception?> finalizer)
    {
        var thrown = Thrown();

        var returned = finalizer(thrown);

        Assert.AreSame(thrown, returned, "Only the NRE is contained; anything else must still propagate.");
        Assert.IsNotNull(returned!.Data[RethrowStackPreserver.ThrowSiteDataKey],
            "The finalizer handed the exception back bare, so Harmony's throw erases its throw site.");
    }

    private static InvalidOperationException Thrown()
    {
        try { throw new InvalidOperationException("not this guard's defect"); }
        catch (InvalidOperationException ex) { return ex; }
    }
}
