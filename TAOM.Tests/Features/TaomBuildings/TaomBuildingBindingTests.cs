using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Buildings;
using TaleWorlds.GauntletUI;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade.GauntletUI.Widgets.Menu.TownManagement;
using TaleWorlds.TwoDimension;

namespace TAOM.Tests.Features.TaomBuildings;

/// <summary>
/// The engine members Patch108 and the Postern Gate reference, against the installed engine. Harmony binds the postfix
/// parameters by name, and a private method that moves or changes shape is a silent no-op in game.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomBuildingBindingTests
{
    private const BindingFlags AnyMember =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void DetermineSpriteImageFromSpriteCode_IsPrivateInstance_StringOfStringAndBool()
    {
        var method = typeof(DevelopmentItemVisualButtonWidget).GetMethod("DetermineSpriteImageFromSpriteCode", AnyMember);

        Assert.IsNotNull(method);
        Assert.IsTrue(method!.IsPrivate && !method.IsStatic);
        Assert.AreEqual(typeof(string), method.ReturnType);
        CollectionAssert.AreEqual(new[] { "spriteCode", "useSmallVariant" }, method.GetParameters().Select(p => p.Name).ToArray());
        CollectionAssert.AreEqual(new[] { typeof(string), typeof(bool) }, method.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheWidget_ReachesSpriteData_ThroughContext()
    {
        var context = typeof(DevelopmentItemVisualButtonWidget).GetProperty("Context", AnyMember);
        var spriteData = typeof(UIContext).GetProperty("SpriteData", AnyMember);
        var getSprite = typeof(SpriteData).GetMethod("GetSprite", new[] { typeof(string) });

        Assert.IsNotNull(context);
        Assert.IsNotNull(spriteData);
        Assert.IsNotNull(getSprite);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void GetExplanationAtLevel_ReturnsTextObject_TakesIntNamedLevel()
    {
        var method = typeof(BuildingType).GetMethod("GetExplanationAtLevel", AnyMember);

        Assert.IsNotNull(method);
        Assert.AreEqual(typeof(TextObject), method!.ReturnType);
        CollectionAssert.AreEqual(new[] { "level" }, method.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual(typeof(int), method.GetParameters()[0].ParameterType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void BuildingType_CanBeRegisteredAndInitialisedTheWayTheRegistrarDoes()
    {
        Assert.IsNotNull(typeof(BuildingType).GetConstructor(new[] { typeof(string) }));
        var initialize = typeof(BuildingType).GetMethod("Initialize", AnyMember, null,
            new[]
            {
                typeof(TextObject), typeof(TextObject), typeof(int[]),
                typeof(System.Tuple<BuildingEffectEnum, BuildingEffectIncrementType, float, float, float>[]),
                typeof(bool), typeof(float), typeof(int),
            }, null);
        Assert.IsNotNull(initialize, "BuildingType.Initialize(name, explanation, costs, effects, isMilitary, variance, startLevel)");
        Assert.IsNotNull(typeof(Building).GetConstructor(new[] { typeof(BuildingType), typeof(Town), typeof(float), typeof(int) }));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ExplainedNumber_ExposesSumOfFactors_AndTheModelsKeepTheirSignatures()
    {
        Assert.IsNotNull(typeof(ExplainedNumber).GetProperty("SumOfFactors", BindingFlags.Public | BindingFlags.Instance));

        var breakIn = typeof(TroopSacrificeModel).GetMethod("GetLostTroopCountForBreakingInBesiegedSettlement");
        var breakOut = typeof(TroopSacrificeModel).GetMethod("GetLostTroopCountForBreakingOutOfBesiegedSettlement");
        Assert.AreEqual(typeof(ExplainedNumber), breakIn!.ReturnType);
        Assert.AreEqual(2, breakIn.GetParameters().Length);
        Assert.AreEqual(typeof(ExplainedNumber), breakOut!.ReturnType);
        Assert.AreEqual(3, breakOut.GetParameters().Length);
        Assert.IsNotNull(typeof(BuildingModel).GetMethod("CanAddBuildingTypeToTown", new[] { typeof(BuildingType), typeof(Town) }));
    }
}
