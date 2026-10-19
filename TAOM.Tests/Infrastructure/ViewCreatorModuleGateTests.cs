using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Validation;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// ViewCreatorManager (TaleWorlds.MountAndBlade.View) walks every static method of a class carrying
/// [ViewCreatorModule] and reads <c>GetCustomAttributesSafe(typeof(ViewMethod))[0]</c> with no length check
/// (ViewCreatorManager.cs:63-66, v1.5.5). A helper without [ViewMethod] throws there, and that breaks the
/// view list of every mission in the game. This gate keeps such a class to [ViewMethod] statics only.
/// Attribute types are matched by name so the check does not depend on which assembly declares them.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ViewCreatorModuleGateTests
{
    // The scan resolves attribute types from the View assemblies, which are not in the test bin.
    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    private static bool Carries(MemberInfo member, string attributeName) =>
        member.GetCustomAttributesData().Any(a => a.AttributeType.Name == attributeName);

    [TestMethod]
    public void ViewCreatorModuleClasses_EveryStaticMethod_CarriesViewMethod()
    {
        Type[] types;
        try
        {
            types = typeof(FiniteFloatValidator).Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray();
        }

        var modules = types.Where(t => Carries(t, "ViewCreatorModule")).ToList();
        Assert.IsTrue(modules.Count > 0, "Expected at least one [ViewCreatorModule] class (HostReviewMissionViews).");

        var offenders = modules
            .SelectMany(t => t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            // Compiler-generated statics count too: the engine walks them, so a static local function trips it.
            .Where(m => !Carries(m, "ViewMethod"))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .ToList();

        Assert.AreEqual(0, offenders.Count,
            "A static method on a [ViewCreatorModule] class without [ViewMethod] throws in ViewCreatorManager and "
            + "breaks every mission view: " + string.Join(", ", offenders));
    }
}
