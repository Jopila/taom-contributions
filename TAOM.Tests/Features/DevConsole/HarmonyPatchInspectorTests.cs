using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.DevConsole;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.DevConsole;

/// <summary>
/// The dev console's patch report declares a category from the assemblies it is given. The Telchar's Mark
/// postfixes live in TAOM.Client (ADR-013, B5): a report that read TAOM.dll alone would count their applied
/// patches but never declare the category.
/// </summary>
[TestClass]
public class HarmonyPatchInspectorTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("No game install resolved: the patch categories cannot be read.");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Collect_TwoAssemblies_DeclaresTheClientCategories()
    {
        RequireGame();
        var result = HarmonyPatchInspector.Collect("com.taom.mod", TaomAssemblies.All);

        CollectionAssert.Contains(result.Declared.ToList(), "Patch37_CrashReport");
        CollectionAssert.Contains(result.Declared.ToList(), "Patch109_TelcharsMark");
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Collect_CoreAlone_DoesNotDeclareTheClientCategories()
    {
        RequireGame();
        var result = HarmonyPatchInspector.Collect("com.taom.mod", new[] { TaomAssemblies.All[0] });

        CollectionAssert.Contains(result.Declared.ToList(), "Patch37_CrashReport");
        CollectionAssert.DoesNotContain(result.Declared.ToList(), "Patch109_TelcharsMark");
    }
}
