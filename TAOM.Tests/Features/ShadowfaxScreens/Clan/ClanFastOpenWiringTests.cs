using System.IO;
using System.Linq;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Clan;
using TAOM.Features.ShadowfaxScreens.Clan.Hooks;

namespace TAOM.Tests.Features.ShadowfaxScreens.Clan;

[TestClass]
public class ClanFastOpenWiringTests
{
    private Container _container = null!;
    private readonly ShadowfaxScreensModule _module = new ShadowfaxScreensModule();

    [TestInitialize]
    public void Setup()
    {
        _container = new Container();
        _container.RegisterInstance(Substitute.For<IStampClock>());
        _container.RegisterInstance(Substitute.For<IModLogger>());
        _container.RegisterInstance(Substitute.For<ICoopPresenceProvider>());
        _module.RegisterServices(_container);
    }

    [TestCleanup]
    public void Cleanup()
    {
        ClanFastOpenCalls.Initialize(null);
        _container.Dispose();
    }

    [TestMethod]
    public void RegisterServices_TheClanServiceResolvesAsASingleton()
    {
        Assert.AreSame(_container.Resolve<ClanScreenFastPathService>(), _container.Resolve<ClanScreenFastPathService>());
    }

    [TestMethod]
    public void InitializeStatics_WiresTheClanFacadeToTheService()
    {
        _module.InitializeStatics(_container);

        Assert.AreSame(_container.Resolve<ClanScreenFastPathService>(), ClanFastOpenCalls.Service);
    }

    [TestMethod]
    public void TheHooksCategory_IsOneTheModuleDeclares()
    {
        CollectionAssert.Contains(_module.PatchCategories.Select(d => d.Category).ToList(), ClanFastOpenCalls.PatchCategory);
    }

    [TestMethod]
    public void EveryClanHook_CarriesTheModulesCategory()
    {
        var hooks = new[]
        {
            typeof(ClanPartiesVM_RefreshValues_ClanFastOpen_Patch),
            typeof(ClanPartiesVM_RefreshPartiesList_ClanFastOpen_Patch),
            typeof(GauntletClanScreen_OnActivate_ClanFastOpen_Patch),
        };
        foreach (var hook in hooks)
        {
            var attr = (HarmonyPatchCategory?)hook.GetCustomAttributes(typeof(HarmonyPatchCategory), false).SingleOrDefault();
            Assert.IsNotNull(attr, hook.Name);
            Assert.AreEqual(ClanFastOpenCalls.PatchCategory, attr!.info.category, hook.Name);
        }
    }

    [TestMethod]
    public void TheHookFiles_StayUnder150Lines()
    {
        var dir = Path.GetDirectoryName(typeof(ClanFastOpenWiringTests).Assembly.Location)!;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "Main", "Features", "ShadowfaxScreens", "Clan", "Hooks")))
            dir = Path.GetDirectoryName(dir);
        if (dir == null) Assert.Inconclusive("Main/ source tree not found from the test assembly.");

        foreach (var file in Directory.GetFiles(Path.Combine(dir!, "Main", "Features", "ShadowfaxScreens", "Clan", "Hooks"), "*.cs"))
            Assert.IsTrue(File.ReadAllLines(file).Length < 150, file);
    }
}
