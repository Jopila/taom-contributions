using System;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.SeasonedHosts;
using TAOM.Features.SeasonedHosts.Hooks;

namespace TAOM.Tests.Features.SeasonedHosts;

/// <summary>
/// Patch118's prefix verdict (#890), driven through the module's statics handshake: false skips vanilla's Steward XP grant,
/// and every other outcome, a fault included, lets vanilla run.
/// </summary>
[TestClass]
public class Patch118ProjectStewardXpTests
{
    private static IModLogger _logger = null!;

    private static void Wire(Action<ISeasonedHostsSettingsProvider> configure, bool defer = false)
    {
        var settings = Substitute.For<ISeasonedHostsSettingsProvider>();
        configure(settings);
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.ShouldDeferToHost.Returns(defer);
        _logger = Substitute.For<IModLogger>();

        using var container = new Container();
        container.RegisterInstance(coop);
        container.RegisterInstance(Substitute.For<IPlayerControlQuery>());
        container.RegisterInstance(_logger);
        new SeasonedHostsModule().RegisterServices(container);
        container.RegisterInstance(settings, IfAlreadyRegistered.Replace);

        new SeasonedHostsModule().InitializeStatics(container);
    }

    [TestMethod]
    public void Prefix_SwitchOnAndNotDeferring_ReturnsFalse()
    {
        Wire(s => s.NoStewardXp.Returns(true));

        Assert.IsFalse(Patch118_ProjectStewardXp.Prefix());
    }

    [TestMethod]
    public void Prefix_SwitchOff_ReturnsTrue()
    {
        Wire(s => s.NoStewardXp.Returns(false));

        Assert.IsTrue(Patch118_ProjectStewardXp.Prefix());
    }

    [TestMethod]
    public void Prefix_SwitchOnButDeferringToTheHost_ReturnsTrue()
    {
        Wire(s => s.NoStewardXp.Returns(true), defer: true);

        Assert.IsTrue(Patch118_ProjectStewardXp.Prefix());
    }

    [TestMethod]
    public void Prefix_TheSettingsProviderThrows_ReturnsTrueAndLogsOnce()
    {
        Wire(s => s.NoStewardXp.Returns(_ => throw new InvalidOperationException("settings torn down")));

        Assert.IsTrue(Patch118_ProjectStewardXp.Prefix());
        Assert.IsTrue(Patch118_ProjectStewardXp.Prefix());

        _logger.Received(1).LogError(NSubstitute.Arg.Any<string>());
    }

    [TestMethod]
    public void Prefix_NoService_ReturnsTrue()
    {
        Patch118_ProjectStewardXp.Initialize(null!, Substitute.For<IModLogger>());

        Assert.IsTrue(Patch118_ProjectStewardXp.Prefix());
    }
}
