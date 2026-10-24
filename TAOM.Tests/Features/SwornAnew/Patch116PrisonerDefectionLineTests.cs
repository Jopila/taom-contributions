using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.SwornAnew;
using TAOM.Features.SwornAnew.Hooks;
using TAOM.Features.SwornAnew.Models;
using TAOM.Features.UncapturableHeroes;
using TAOM.Tests.Infrastructure;
using static TAOM.Tests.Features.SwornAnew.DefectionFactsFixture;

namespace TAOM.Tests.Features.SwornAnew;

/// <summary>
/// The postfix on the ruler's prisoner line ("join us, and be set free") takes the line away when the service refuses
/// the prisoner: vanilla's own answer is the ceiling, the service can only take the line away, and a free hero or the switch off
/// leaves vanilla's answer alone. A fault keeps vanilla's answer and is logged.
/// </summary>
[TestClass]
public class Patch116PrisonerDefectionLineTests
{
    private ISwornAnewSettingsProvider _settings = null!;
    private ISwornAnewAdapter _adapter = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<ISwornAnewSettingsProvider>();
        _settings.Enabled.Returns(true);
        _adapter = Substitute.For<ISwornAnewAdapter>();
        _logger = Substitute.For<IModLogger>();
        var alignment = Substitute.For<IAlignmentService>();
        alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(call => call.ArgAt<string>(0) switch
        {
            "k_free" => FactionSide.Free,
            "k_evil" => FactionSide.Evil,
            _ => FactionSide.Neutral,
        });
        var service = new SwornAnewService(_settings, alignment, Substitute.For<IUncapturableRegistry>(),
            Substitute.For<ICoopSessionProvider>(), Substitute.For<IDedicatedServerProvider>());
        Patch116_PrisonerDefectionLine_Postfix.Initialize(service, _adapter, _logger);
    }

    private static bool Run(bool vanilla)
    {
        var result = vanilla;
        Patch116_PrisonerDefectionLine_Postfix.Postfix(ref result);
        return result;
    }

    [TestMethod]
    public void Postfix_VanillaHidesTheLine_StaysHidden_AndAsksNothing()
    {
        Assert.IsFalse(Run(false));
        _adapter.DidNotReceive().ReadConversationPartner();
    }

    [TestMethod]
    public void Postfix_VanillaOffersTheLine_AndTheServiceAllows_KeepsIt()
    {
        _adapter.ReadConversationPartner().Returns(EligiblePrisoner());

        Assert.IsTrue(Run(true));
    }

    [TestMethod]
    public void Postfix_VanillaOffersTheLine_ButTheAlignmentGateRefuses_TakesItAway()
    {
        _adapter.ReadConversationPartner().Returns(EligiblePrisoner(lord: Free, player: Evil));

        Assert.IsFalse(Run(true));
    }

    [TestMethod]
    public void Postfix_VanillaOffersTheLine_ButTheLordFailsAFactionCheck_TakesItAway()
    {
        _adapter.ReadConversationPartner().Returns(EligiblePrisoner() with { IsMercenary = true });

        Assert.IsFalse(Run(true));
    }

    [TestMethod]
    public void Postfix_TheServiceHasNoVerdict_LeavesVanillasAnswer()
    {
        _adapter.ReadConversationPartner().Returns(EligiblePrisoner(lord: Free, player: Evil) with { IsPrisoner = false });
        Assert.IsTrue(Run(true), "a free hero is vanilla's own check");

        _settings.Enabled.Returns(false);
        _adapter.ReadConversationPartner().Returns(EligiblePrisoner(lord: Free, player: Evil));
        Assert.IsTrue(Run(true), "the switch off restores vanilla's line untouched");
    }

    [TestMethod]
    public void Postfix_NoFactsCouldBeRead_LeavesVanillasAnswer()
    {
        _adapter.ReadConversationPartner().Returns((DefectionFacts?)null);

        Assert.IsTrue(Run(true));
    }

    [TestMethod]
    public void Postfix_AFault_KeepsVanillasAnswer_AndIsLogged()
    {
        _adapter.ReadConversationPartner().Returns(_ => throw new InvalidOperationException("boom"));

        Assert.IsTrue(Run(true));
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("[SwornAnew]") && s.Contains("Patch116") && s.Contains("boom")));
    }

    [TestMethod]
    public void ThePatchClass_IsThinAndNamesItsCategoryByTheModulesConstant()
    {
        var src = RepoPaths.ReadSource("Main/Features/SwornAnew/Hooks/Patch116_PrisonerDefectionLine_Postfix.cs", stripComments: true);

        Assert.IsTrue(src.Split('\n').Length < 150, "ADR-002: entry points stay under 150 lines");
        StringAssert.Contains(src, "[HarmonyPatchCategory(SwornAnewModule.PatchCategory)]");
        StringAssert.Contains(src, "service.HidesPrisonerLine(adapter.ReadConversationPartner())");
    }
}
