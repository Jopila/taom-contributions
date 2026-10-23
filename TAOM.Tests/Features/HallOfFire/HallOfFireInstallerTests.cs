using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using SandBox.View.Map;
using TAOM.Client.Features.HallOfFire;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.HallOfFire;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.HallOfFire;

/// <summary>
/// The install decision against a fake engine slot: wrap the vanilla provider once, never twice, never into a null
/// slot, never on a dedicated server. The slot interface names an engine type, so the class is tagged <c>RequiresGame</c>.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class HallOfFireInstallerTests
{
    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    private IMapConversationProviderSlot _slot = null!;
    private IMapConversationDataProvider _vanilla = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IModLogger _logger = null!;
    private HallOfFireService _service = null!;
    private HallOfFireInstaller _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _slot = Substitute.For<IMapConversationProviderSlot>();
        _vanilla = Substitute.For<IMapConversationDataProvider>();
        _slot.Current.Returns(_vanilla);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _logger = Substitute.For<IModLogger>();
        var config = Substitute.For<IHallOfFireConfigProvider>();
        config.GetMap().Returns(HallOfFireMap.Default());
        _service = new HallOfFireService(config);
        _sut = new HallOfFireInstaller(_slot, new HallOfFireConversationWrapper(_service, _logger), _dedicated, _logger);
    }

    [TestMethod]
    public void Install_WrapsTheCurrentProviderOnce_AndLogsOneInfoLine()
    {
        _sut.Install();

        _slot.Received(1).Set(Arg.Is<IMapConversationDataProvider>(p =>
            p is TaomMapConversationDataProvider && ((TaomMapConversationDataProvider)p).Inner == _vanilla));
        _logger.Received(1).LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void Install_WhenTheSlotAlreadyHoldsTheWrapper_DoesNotWrapAgain()
    {
        _slot.Current.Returns(new TaomMapConversationDataProvider(_vanilla, _service, _logger));

        _sut.Install();

        _slot.DidNotReceive().Set(Arg.Any<IMapConversationDataProvider>());
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    [TestMethod]
    public void Install_WithoutAClientWrapper_SetsNothingAndLogsOneWarning()
    {
        var sut = new HallOfFireInstaller(_slot, new NullMapConversationProviderWrapper(), _dedicated, _logger);

        sut.Install();

        _slot.DidNotReceive().Set(Arg.Any<IMapConversationDataProvider>());
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("TAOM.Client")));
    }

    [TestMethod]
    public void Install_WhenTheSlotIsEmpty_NeverSetsNull()
    {
        _slot.Current.Returns((IMapConversationDataProvider?)null);

        _sut.Install();

        _slot.DidNotReceive().Set(Arg.Any<IMapConversationDataProvider>());
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Install_OnADedicatedServer_TouchesNothing()
    {
        _dedicated.IsDedicatedServer.Returns(true);

        _sut.Install();

        _slot.DidNotReceive().Set(Arg.Any<IMapConversationDataProvider>());
        _ = _slot.DidNotReceive().Current;
    }

    [TestMethod]
    public void Install_WhenTheSlotThrows_LogsAndDoesNotPropagate()
    {
        _slot.Current.Returns(_ => throw new InvalidOperationException("no instance"));

        _sut.Install();

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("no instance")));
    }
}
