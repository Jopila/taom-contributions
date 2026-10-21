using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SandBox.View;
using SandBox.View.Map;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HallOfFire;

/// <summary>
/// Pins the two engine facts Hall of Fire rests on in the installed game: the tableau reads its provider from
/// <c>SandBoxViewSubModule.MapConversationDataProvider</c> when it is constructed, and <c>FirstTimeInit</c> asks that
/// provider for the atmosphere name and applies it to the scene. If either call goes, the wrapper is installed but
/// never consulted, and a TAOM settlement silently keeps the wrong backdrop.
/// </summary>
[TestClass]
[TestCategory("RequiresGameIL")]
public class HallOfFireBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static List<string> Calls(MethodBase method) =>
        PatchProcessor.GetOriginalInstructions(method)
            .Where(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt || ci.opcode == OpCodes.Newobj) && ci.operand is MethodBase)
            .Select(ci => ((MethodBase)ci.operand).DeclaringType!.Name + "." + ((MethodBase)ci.operand).Name)
            .ToList();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MapConversationTableauConstructor_ReadsTheProviderFromTheSubModule()
    {
        RequireGame();

        var constructor = typeof(MapConversationTableau).GetConstructor(Type.EmptyTypes);

        Assert.IsNotNull(constructor, "MapConversationTableau() did not resolve.");
        CollectionAssert.Contains(Calls(constructor), "SandBoxViewSubModule." + "get_" + nameof(SandBoxViewSubModule.MapConversationDataProvider));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void FirstTimeInit_AsksTheProviderForTheAtmosphereNameAndAppliesItToTheScene()
    {
        RequireGame();

        var firstTimeInit = AccessTools.Method(typeof(MapConversationTableau), "FirstTimeInit", Type.EmptyTypes);

        Assert.IsNotNull(firstTimeInit, "MapConversationTableau.FirstTimeInit() did not resolve.");
        var calls = Calls(firstTimeInit);
        CollectionAssert.Contains(calls, nameof(IMapConversationDataProvider) + "." + nameof(IMapConversationDataProvider.GetAtmosphereNameFromData));
        CollectionAssert.Contains(calls, "Scene.SetAtmosphereWithName");
    }
}
