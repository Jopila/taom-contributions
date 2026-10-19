using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.ShaderSourceInstall;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.ShaderSourceInstall;

/// <summary>
/// Wiring guard for the bow shader source install (#916): the module is listed once, installs at ProcessLoad
/// (never on a dedicated server), warns through an inquiry at the main menu, and SubModule.cs no longer
/// carries a hand-wired call.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class ShaderSourceInstallWiringTests
{
    private Container _container = null!;
    private readonly ShaderSourceInstallModule _sut = new ShaderSourceInstallModule();

    [TestInitialize]
    public void Setup()
    {
        _container = new Container();
        _container.RegisterInstance(Substitute.For<IModLogger>());
        _container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        _container.RegisterInstance(Substitute.For<IPathService>());
        _sut.RegisterServices(_container);
    }

    [TestCleanup]
    public void Cleanup() => _container.Dispose();

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<ShaderSourceInstallModule>().Count());
    }

    [TestMethod]
    public void Module_IsEnabledAndDeclaresNoPatchOrBehavior()
    {
        Assert.AreEqual("ShaderSourceInstall", _sut.Id);
        Assert.IsNull(_sut.ParkedReason);
        Assert.IsFalse(_sut.OwnsSaveData);
        Assert.AreEqual(0, _sut.PatchCategories.Count);
        Assert.AreEqual(0, _sut.CampaignBehaviors.Count);
        Assert.AreEqual(0, _sut.MissionBehaviors.Count);
    }

    [TestMethod]
    public void RegisterServices_TheServiceAndTheFileSystemResolveAsSingletons()
    {
        var service = _container.Resolve<ShaderSourceInstallService>();

        Assert.AreSame(service, _container.Resolve<ShaderSourceInstallService>());
        Assert.IsInstanceOfType(_container.Resolve<IShaderSourceFileSystem>(), typeof(ShaderSourceFileSystem));
    }

    [TestMethod]
    public void OnPhase_ProcessLoadOnADedicatedServer_WritesAndReadsNothing()
    {
        var fileSystem = Substitute.For<IShaderSourceFileSystem>();
        _container.RegisterInstance(fileSystem, IfAlreadyRegistered.Replace);
        _container.Resolve<IDedicatedServerProvider>().IsDedicatedServer.Returns(true);

        _sut.OnPhase(ApplyPhase.ProcessLoad, _container);

        Assert.AreEqual(0, fileSystem.ReceivedCalls().Count());
    }

    [TestMethod]
    public void OnPhase_OtherPhasesWithNothingPending_ShowNothingAndDoNotThrow()
    {
        _sut.OnPhase(ApplyPhase.MainMenu, _container);
        _sut.OnPhase(ApplyPhase.GameInit, _container);
        _sut.OnPhase(ApplyPhase.FirstMission, _container);
    }

    // The feature module owns the wiring; SubModule.cs (single-owner) must not grow a hand-wired call again.
    [TestMethod]
    public void SubModule_NoLongerWiresTheInstallByHand()
    {
        var source = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(source.Contains("ShaderSourceInstall"), "the FeatureModules list owns this wiring now");
    }

    [TestMethod]
    public void Warning_IsAnInquiryNotAChatLine()
    {
        var source = RepoPaths.ReadSource("Main/Features/ShaderSourceInstall/ShaderSourceInstallEntry.cs", stripComments: true)
            + RepoPaths.ReadSource("Main/Features/ShaderSourceInstall/ShaderSourceInstallModule.cs", stripComments: true);

        StringAssert.Contains(source, "InformationManager.ShowInquiry(");
        Assert.IsFalse(source.Contains("DisplayMessage"), "the initial screen clears the chat log (localization-ui.md)");
    }
}
