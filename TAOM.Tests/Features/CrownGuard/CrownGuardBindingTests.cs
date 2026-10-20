using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.CrownGuard;
using TAOM.Features.CrownGuard.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;

namespace TAOM.Tests.Features.CrownGuard;

/// <summary>
/// Proves the postfix still fits the installed engine (#825): the category applies to
/// <c>CampaignSceneNotificationHelper.GetBodyguardOfCulture</c> with exactly this postfix and the replacement JIT-compiles.
/// </summary>
[TestClass]
public class CrownGuardBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void PatchCategory_AppliedToTheInstalledEngine_AttachesThisPostfixAndTheJitAccepts()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies unavailable.");
        const string id = "taom.tests.crownguard.jit";
        var target = typeof(CampaignSceneNotificationHelper).GetMethod(nameof(CampaignSceneNotificationHelper.GetBodyguardOfCulture));
        Assert.IsNotNull(target, "GetBodyguardOfCulture did not resolve.");
        var harmony = new Harmony(id);
        try
        {
            PatchCategoryIndex.Build(typeof(CrownGuardModule).Assembly).Apply(harmony, CrownGuardModule.PatchCategory);

            var info = Harmony.GetPatchInfo(target);
            Assert.IsNotNull(info, "applying the category patched nothing: the class is not in it");
            var postfixes = info!.Postfixes.Where(p => p.owner == id).ToList();
            Assert.AreEqual(1, postfixes.Count, "exactly one postfix is attached");
            Assert.AreEqual(typeof(CampaignSceneNotificationHelper_GetBodyguardOfCulture_CrownGuard_Patch), postfixes[0].PatchMethod.DeclaringType);
            Assert.AreEqual(0, info.Prefixes.Count(p => p.owner == id), "no prefix is attached");
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareMethod(target!.MethodHandle);
        }
        finally
        {
            harmony.UnpatchAll(id);
        }
    }
}
