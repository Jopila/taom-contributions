using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Relations;
using TAOM.Features.Relations.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Relations;

/// <summary>
/// Drift guard for Patch107. The target is a private handler named by string, so a rename in a Bannerlord update would leave
/// the patch applying to nothing (or Harmony throwing at apply time). Harmony binds the prefix's parameters by name, so the
/// first four names and types are pinned here against the engine's seven. <c>HarmonyPatchBindingTests</c> resolves the same
/// target for every patch; this pins the signature it depends on.
/// </summary>
[TestClass]
public class PersonalRelationNoticeBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static MethodInfo Target()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName("SandBox.CampaignBehaviors.DefaultNotificationsCampaignBehavior");
        Assert.IsNotNull(type, "DefaultNotificationsCampaignBehavior did not resolve: Patch107 would apply to nothing.");
        var method = AccessTools.Method(type, "OnRelationChanged");
        Assert.IsNotNull(method, "OnRelationChanged is gone or ambiguous: Patch107 would apply to nothing or throw at apply time.");
        return method;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnRelationChanged_ResolvesAsAPrivateInstanceVoid_WithTheSevenParametersTheNoticeNeeds()
    {
        var method = Target();

        Assert.IsFalse(method.IsStatic);
        Assert.IsFalse(method.IsPublic, "vanilla keeps it private; a public one would be a different method");
        Assert.AreEqual(typeof(void), method.ReturnType);
        CollectionAssert.AreEqual(
            new[] { "effectiveHero", "effectiveHeroGainedRelationWith", "relationChange", "showNotification", "detail", "originalHero", "originalGainedRelationWith" },
            method.GetParameters().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePrefix_BindsItsFourParametersByTheEnginesNamesAndTypes()
    {
        var engine = Target().GetParameters();
        var prefix = typeof(DefaultNotificationsCampaignBehavior_OnRelationChanged_Patch).GetMethod("Prefix")!;

        Assert.AreEqual(typeof(bool), prefix.ReturnType);
        foreach (var parameter in prefix.GetParameters())
        {
            var match = engine.SingleOrDefault(e => e.Name == parameter.Name);
            Assert.IsNotNull(match, parameter.Name + " is not a parameter of the engine's handler: Harmony would throw at apply time.");
            Assert.AreEqual(match.ParameterType, parameter.ParameterType, parameter.Name);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ThePatchClass_IsInTheModulesCategory()
    {
        var attribute = typeof(DefaultNotificationsCampaignBehavior_OnRelationChanged_Patch)
            .GetCustomAttributes<HarmonyPatchCategory>().Single();

        Assert.AreEqual("Patch107_PersonalRelationNotice", attribute.info.category);
    }
}
