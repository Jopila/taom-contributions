using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Features.ShadowfaxScreens.Party;

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
        new ShadowfaxScreensModule().RegisterServices(_container);
    }

    [TestCleanup]
    public void Cleanup()
    {
        ScreenOpenTiming.Initialize(null);
        PartyFastOpenCalls.Initialize(null);
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
