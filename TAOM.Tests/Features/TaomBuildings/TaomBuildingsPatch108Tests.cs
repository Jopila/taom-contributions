using System;
using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.Localization;
using TAOM.Core.Logging;
using TAOM.Features.TaomBuildings;
using TAOM.Features.TaomBuildings.Hooks;
using TAOM.Tests.Migration;
using Arg = NSubstitute.Arg;

namespace TAOM.Tests.Features.TaomBuildings;

/// <summary>
/// The town-screen postfixes: a TAOM id gets its line, a vanilla id is left alone, and a fault leaves the engine's result
/// in place with one warning per class (never one per call). The signatures mention engine types, so the class needs the
/// game assemblies.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomBuildingsPatch108Tests
{
    private const string TaomId = "building_taom_test_wall";

    private static readonly FieldInfo? ContainerField =
        typeof(TAOM.IoC).GetField("_container", BindingFlags.NonPublic | BindingFlags.Static);

    private static bool _gameLoaded;
    private ITaomBuildingUiHook _hook = null!;
    private object? _savedContainer;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        Assert.IsNotNull(ContainerField, "IoC._container moved: update this test");
        _savedContainer = ContainerField!.GetValue(null);
        _hook = Substitute.For<ITaomBuildingUiHook>();
        TaomBuildingUiHooks.Initialize(() => _hook);
        SetWarned(typeof(Patch108_LevelExplanation), false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        ContainerField?.SetValue(null, _savedContainer);
        TaomBuildingUiHooks.Initialize(() => null);
        SetWarned(typeof(Patch108_LevelExplanation), false);
    }

    private static void SetWarned(Type patch, bool value) =>
        patch.GetField("_warned", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, value);

    [TestMethod]
    public void LevelExplanation_TaomType_FillsTheTaomLine()
    {
        _hook.LevelText(TaomId, 2).Returns(new BuildingEffectText("{=t_effect}{PERCENT}% less", "PERCENT", "67"));
        var result = new TextObject("");

        Patch108_LevelExplanation.Postfix(new BuildingType(TaomId), 2, ref result);

        StringAssert.Contains(result.ToString(), "67% less");
    }

    [TestMethod]
    public void LevelExplanation_VanillaId_LeavesTheResultAlone()
    {
        var original = new TextObject("vanilla line");
        var result = original;

        Patch108_LevelExplanation.Postfix(new BuildingType("building_settlement_barracks"), 2, ref result);

        Assert.AreSame(original, result);
    }

    [TestMethod]
    public void LevelExplanation_HookThrows_LeavesTheResultUntouched_AndWarnsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        using var container = new Container();
        container.RegisterInstance(logger);
        ContainerField!.SetValue(null, container);
        _hook.LevelText(Arg.Any<string>(), Arg.Any<int>()).Throws(new InvalidOperationException("boom"));
        var original = new TextObject("vanilla line");
        var result = original;

        Patch108_LevelExplanation.Postfix(new BuildingType(TaomId), 1, ref result);
        Patch108_LevelExplanation.Postfix(new BuildingType(TaomId), 1, ref result);
        Patch108_LevelExplanation.Postfix(new BuildingType(TaomId), 1, ref result);

        Assert.AreSame(original, result, "the engine's line stands");
        logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("effect-line postfix failed")));
    }
}
