using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client;
using TAOM.Features.AdvancedCombat;

namespace TAOM.Tests.Infrastructure;

/// <summary>
/// ADR-013, B4c. The mission views, the visual order provider, the shader game manager and the WinForms
/// notice types live in TAOM.Client under their original full names. Reflection only: a CustomGameManager or
/// a WinForms context is never constructed here (side effects unverified).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ClientMissionTypesTests
{
    private static readonly Assembly Client = typeof(ClientSubModule).Assembly;
    private static readonly Assembly Core = typeof(SubModule).Assembly;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) =>
        TAOM.Tests.Migration.GameAssemblies.EnsureLoaded();

    private static void AssertClientOnly(string fullName)
    {
        Assert.IsNotNull(Client.GetType(fullName), fullName + " must be declared in TAOM.Client");
        Assert.IsNull(Core.GetType(fullName), fullName + " must not be declared in TAOM.dll");
    }

    [TestMethod]
    public void AutonomousMovementPlayerController_LivesInTaomClient_AsADefaultViewImplementingTheCoreInterface()
    {
        const string name = "TAOM.Features.AdvancedCombat.AutonomousMovementPlayerController";
        AssertClientOnly(name);
        var type = Client.GetType(name)!;

        Assert.IsTrue(typeof(IAutonomousMovementController).IsAssignableFrom(type), "must implement the core interface");
        // ViewCreatorManager.CollectDefaults keeps a behavior carrying exactly one attribute named DefaultView.
        var defaults = type.GetCustomAttributesData().Count(a => a.AttributeType.Name == "DefaultView");
        Assert.AreEqual(1, defaults, "[DefaultView] makes the engine build the instance the Warg code drives");
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes), "the engine builds a default view with Activator.CreateInstance");
    }

    [TestMethod]
    public void TaomClient_ReferencesTheViewAssemblyByItsFullName()
    {
        // GetReferencingAssembliesSafe keeps an assembly only when one reference's AssemblyName.ToString()
        // equals the View assembly's own name, so TAOM.Client is scanned for [DefaultView] types.
        var view = Assembly.Load("TaleWorlds.MountAndBlade.View").GetName().ToString();

        Assert.IsTrue(Client.GetReferencedAssemblies().Any(r => r.ToString() == view),
            "TAOM.Client must reference " + view);
    }

    [TestMethod]
    public void BattleActionBarMissionView_LivesInTaomClient() =>
        AssertClientOnly("TAOM.Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView");

    [TestMethod]
    public void HostReviewVisualOrderProvider_LivesInTaomClient()
    {
        AssertClientOnly("TAOM.Features.HostReview.Hooks.HostReviewVisualOrderProvider");

        // Never call the engine static in a test: its provider list is process wide and a second add asserts.
        var src = RepoPaths.ReadSource("Client/Features/HostReview/HostReviewOrderProviderRegistrar.cs", stripComments: true);
        StringAssert.Contains(src, "VisualOrderFactory.RegisterProvider(new HostReviewVisualOrderProvider())");
    }

    [TestMethod]
    public void TaomShaderGameManager_LivesInTaomClient()
    {
        const string name = "TAOM.Features.ShaderPrecompilation.TaomShaderGameManager";
        AssertClientOnly(name);

        Assert.AreEqual("CustomGameManager", Client.GetType(name)!.BaseType!.Name);
    }

    [TestMethod]
    public void NoticeWindowTypes_LiveInTaomClient()
    {
        AssertClientOnly("TAOM.Features.ShaderCompileNotice.ShaderCompileNoticeWindow");
        AssertClientOnly("TAOM.Features.ShaderCompileNotice.WindowHandleOwner");
        AssertClientOnly("TAOM.Features.ShaderCompileNotice.ShaderCompileNoticeContext");

        var context = Client.GetType("TAOM.Features.ShaderCompileNotice.ShaderCompileNoticeContext")!;
        Assert.AreEqual("ApplicationContext", context.BaseType!.Name);
    }
}
