using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Clan;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Features.ShadowfaxScreens.Party;
using TAOM.Features.ShadowfaxScreens.Quests;

namespace TAOM.Tests.Features.ShadowfaxScreens.Party;

[TestClass]
public class PartyFastOpenWiringTests
{
    private Container _container = null!;

    [TestInitialize]
    public void Setup()
    {
        _container = new Container();
        _container.RegisterInstance(Substitute.For<IStampClock>());
        _container.RegisterInstance(Substitute.For<IModLogger>());
        _container.RegisterInstance(Substitute.For<ICoopPresenceProvider>());
        new ShadowfaxScreensModule().RegisterServices(_container);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // InitializeStatics wires every screen's facade, so every one is reset.
        ScreenOpenTiming.Initialize(null);
        ClanFastOpenCalls.Initialize(null);
        ShadowfaxInventoryCalls.Initialize(null, null, null);
        PartyFastOpenCalls.Initialize(null);
        QuestLogDeferCalls.Initialize(null);
        _container.Dispose();
    }

    [TestMethod]
    public void RegisterServices_ServiceResolvesAsASingleton()
    {
        var service = _container.Resolve<PartyScreenFastPathService>();

        Assert.AreSame(service, _container.Resolve<PartyScreenFastPathService>());
    }

    [TestMethod]
    public void InitializeStatics_WiresTheFacadeToTheService()
    {
        new ShadowfaxScreensModule().InitializeStatics(_container);

        Assert.AreSame(_container.Resolve<PartyScreenFastPathService>(), PartyFastOpenCalls.Service);
    }

    [TestMethod]
    public void Provider_PartyFastOpenDefaultsOn()
    {
        Assert.IsTrue(new ShadowfaxScreensSettingsProvider().PartyFastOpen);
    }
}
