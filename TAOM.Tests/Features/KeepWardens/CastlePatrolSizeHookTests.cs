using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Adapters;
using TAOM.Features.KeepWardens;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>
/// The size hook (#888). The decision is <see cref="CastlePatrolService.SizeLimitOverride"/>, tested with the service; these
/// tests pin the boundary around it and the hook's place in <c>TaomPartySizeModel</c>.
/// </summary>
[TestClass]
public class CastlePatrolSizeHookTests
{
    private static CastlePatrolSizeHook Hook(int level)
    {
        var config = Substitute.For<ICastlePatrolConfigProvider>();
        config.SpawnDays.Returns(new[] { 10f, 8f, 6f });
        config.SizeLimits.Returns(new[] { 15, 20, 25 });
        var settings = Substitute.For<ICastlePatrolSettingsProvider>();
        // Off on purpose: the hook must ignore the setting, so a live patrol never drops to a limit of 0.
        settings.Enabled.Returns(false);
        var adapter = Substitute.For<ICastlePatrolAdapter>();
        adapter.CastlePatrolGuardHouseLevel(Arg.Any<MobileParty?>()).Returns(level);
        return new CastlePatrolSizeHook(new CastlePatrolService(config, settings), adapter);
    }

    [DataTestMethod]
    [TestCategory("RequiresGame")] // constructs an ExplainedNumber
    [DataRow(1, 15f)]
    [DataRow(2, 20f)]
    [DataRow(3, 25f)]
    public void ApplyCastlePatrolSize_ABaseOfZero_GetsTheLevelsLimit_EvenWithTheSettingOff(int level, float expected)
    {
        var result = new ExplainedNumber(0f);

        Hook(level).ApplyCastlePatrolSize(null, ref result, includeDescriptions: false);

        Assert.AreEqual(expected, result.ResultNumber);
    }

    [TestMethod]
    [TestCategory("RequiresGame")] // constructs an ExplainedNumber
    public void ApplyCastlePatrolSize_ANonZeroBase_IsLeftAlone()
    {
        var result = new ExplainedNumber(20f);

        Hook(3).ApplyCastlePatrolSize(null, ref result, includeDescriptions: false);

        Assert.AreEqual(20f, result.ResultNumber);
    }

    [TestMethod]
    [TestCategory("RequiresGame")] // constructs an ExplainedNumber
    public void ApplyCastlePatrolSize_AnyOtherParty_IsLeftAlone()
    {
        var result = new ExplainedNumber(0f);

        Hook(CastlePatrolSnapshot.NotACastlePatrol).ApplyCastlePatrolSize(null, ref result, includeDescriptions: false);

        Assert.AreEqual(0f, result.ResultNumber);
    }

    [TestMethod]
    public void TaomPartySizeModel_AppliesTheHookAfterTheBaseCall_AndBeforeTheCultureFeats()
    {
        // The hook replaces the number the base model returned, so it must follow the base call; the feats and every later
        // step add factors to the base number, so a replacement after them would wipe their work out. Both orders give the
        // same type-checking code, so only a source-order assertion catches a move.
        var src = ReadSource("Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs", stripComments: true);

        var baseCall = src.IndexOf("base.GetPartyMemberSizeLimit(", System.StringComparison.Ordinal);
        var hook = src.IndexOf("ApplyCastlePatrolSize(", System.StringComparison.Ordinal);
        var feats = src.IndexOf("_feats.ApplyPartySizeFeats(", System.StringComparison.Ordinal);

        Assert.AreNotEqual(-1, baseCall, "TaomPartySizeModel must call base.GetPartyMemberSizeLimit(");
        Assert.AreNotEqual(-1, hook, "TaomPartySizeModel must call the castle patrol size hook");
        Assert.AreNotEqual(-1, feats, "TaomPartySizeModel must call _feats.ApplyPartySizeFeats(");
        Assert.IsTrue(baseCall < hook, "the hook must run AFTER the base call, which returns the 0 it replaces");
        Assert.IsTrue(hook < feats, "the hook must run BEFORE the culture feats, whose factors then apply to the replaced number");
    }

    [TestMethod]
    public void TaomPartySizeModel_ResolvesTheHookOnceInItsConstructor_AndSubModuleNeverNamesIt()
    {
        // SubModule.cs builds the model with `new`; a constructor parameter would force an edit to that single-owner file.
        var src = ReadSource("Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs", stripComments: true);

        StringAssert.Contains(src, "private readonly ICastlePatrolSizeHook");
        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(src, @"IoC\.Resolve<ICastlePatrolSizeHook>\(\)").Count);
        Assert.IsFalse(File.ReadAllText(RepoPath("Main", "SubModule.cs")).Contains("ICastlePatrolSizeHook"));

        // The resolve sits in the constructor (after its signature, before the size-limit method), and the method body uses it.
        var ctor = src.IndexOf("public TaomPartySizeModel(", System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, ctor, "TaomPartySizeModel must declare its constructor");
        var method = src.IndexOf("GetPartyMemberSizeLimit(", ctor, System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, method, "TaomPartySizeModel must declare GetPartyMemberSizeLimit( after its constructor");
        var resolve = src.IndexOf("IoC.Resolve<ICastlePatrolSizeHook>", System.StringComparison.Ordinal);
        Assert.IsTrue(ctor < resolve && resolve < method, "the hook must be resolved inside the constructor, before GetPartyMemberSizeLimit(");
        var use = src.IndexOf("_castlePatrolSize.ApplyCastlePatrolSize(", method, System.StringComparison.Ordinal);
        Assert.AreNotEqual(-1, use, "the GetPartyMemberSizeLimit body must call _castlePatrolSize.ApplyCastlePatrolSize(");
    }
}
