using System;
using System.Linq;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Tests.Composition;

/// <summary>
/// ClientComposition.Apply on a bare container (ADR-013): the client's registrations replace core's by
/// default, land in the container core resolves from, and are logged.
/// </summary>
[TestClass]
public class ClientCompositionTests
{
    public interface IProbe { }

    public sealed class CoreProbe : IProbe { }

    public sealed class ClientProbe : IProbe { }

    public sealed class OtherCoreProbe : IProbe { }

    public interface IClientOnly { }

    public sealed class ClientOnly : IClientOnly { }

    private sealed class Registering : IClientComposition
    {
        private readonly Action<IRegistrator> _register;

        public Registering(Action<IRegistrator> register) => _register = register;

        public void RegisterServices(IRegistrator registrator) => _register(registrator);
    }

    [TestCleanup]
    public void Cleanup()
    {
        ClientComposition.Current = null;
        ClientComposition.IsDedicatedServer = ClientComposition.ReadDedicatedServer;
    }

    private static Container CoreContainer()
    {
        var container = new Container();
        container.Register<IProbe, CoreProbe>(Reuse.Singleton);
        return container;
    }

    [TestMethod]
    public void Apply_EmptySlot_LeavesCoreRegistrations()
    {
        var container = CoreContainer();
        ClientComposition.Current = null;

        ClientComposition.Apply(container);

        Assert.IsInstanceOfType(container.Resolve<IProbe>(), typeof(CoreProbe));
    }

    [TestMethod]
    public void Apply_SlotRegistersACoreService_ReplacesTheCoreRegistration()
    {
        var container = CoreContainer();
        ClientComposition.Current = new Registering(r => r.Register<IProbe, ClientProbe>());

        ClientComposition.Apply(container);

        Assert.IsInstanceOfType(container.Resolve<IProbe>(), typeof(ClientProbe));
        Assert.AreEqual(1, container.ResolveMany<IProbe>().Count());
    }

    [TestMethod]
    public void Apply_SlotRegistersANewService_ResolvesThroughTheCoreContainer()
    {
        var container = CoreContainer();
        ClientComposition.Current = new Registering(r => r.Register<IClientOnly, ClientOnly>());

        ClientComposition.Apply(container);

        Assert.IsInstanceOfType(container.Resolve<IClientOnly>(), typeof(ClientOnly));
    }

    [TestMethod]
    public void Apply_SlotAppendsExplicitly_KeepsBothRegistrations()
    {
        var container = CoreContainer();
        ClientComposition.Current = new Registering(r =>
            r.Register<IProbe, ClientProbe>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed));

        ClientComposition.Apply(container);

        Assert.AreEqual(2, container.ResolveMany<IProbe>().Count());
    }

    public sealed class Holder
    {
        public IProbe Probe { get; }

        public Holder(IProbe probe) => Probe = probe;
    }

    // Pins the limit IClientComposition documents: a replacement never reaches a graph built before Apply
    // (the services core resolves during registration: BannerInjectionIoC, HeroRaceIoC).
    [TestMethod]
    public void Apply_ReplacesAServiceResolvedBeforeApply_EarlierGraphKeepsTheCoreInstance()
    {
        var container = CoreContainer();
        container.Register<Holder>(Reuse.Singleton);
        var holder = container.Resolve<Holder>();
        ClientComposition.Current = new Registering(r => r.Register<IProbe, ClientProbe>());

        ClientComposition.Apply(container);

        Assert.IsInstanceOfType(container.Resolve<IProbe>(), typeof(ClientProbe));
        Assert.IsInstanceOfType(holder.Probe, typeof(CoreProbe));
        Assert.AreSame(holder, container.Resolve<Holder>());
    }

    // Pins the other documented limit: a client registration without AppendNotKeyed replaces EVERY core
    // contributor of that service type, not just the last one.
    [TestMethod]
    public void Apply_RegistersACollectionServiceWithoutAppend_ReplacesEveryCoreContributor()
    {
        var container = new Container();
        container.Register<IProbe, CoreProbe>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        container.Register<IProbe, OtherCoreProbe>(ifAlreadyRegistered: IfAlreadyRegistered.AppendNotKeyed);
        Assert.AreEqual(2, container.ResolveMany<IProbe>().Count());
        ClientComposition.Current = new Registering(r => r.Register<IProbe, ClientProbe>());

        ClientComposition.Apply(container);

        var all = container.ResolveMany<IProbe>().ToList();
        Assert.AreEqual(1, all.Count);
        Assert.IsInstanceOfType(all[0], typeof(ClientProbe));
    }

    [TestMethod]
    public void Apply_SlotThrows_PropagatesTheException()
    {
        var container = CoreContainer();
        ClientComposition.Current = new Registering(_ => throw new InvalidOperationException("boom"));

        Assert.ThrowsException<InvalidOperationException>(() => ClientComposition.Apply(container));
    }

    [TestMethod]
    public void Apply_SlotSet_LogsTheCompositionType()
    {
        var container = CoreContainer();
        var logger = Substitute.For<IModLogger>();
        container.RegisterInstance(logger);
        ClientComposition.Current = new Registering(_ => { });

        ClientComposition.Apply(container);

        logger.Received(1).LogInfo(NSubstitute.Arg.Is<string>(s =>
            s.Contains("[Client] composition applied") && s.Contains(typeof(Registering).FullName!)
            && s.Contains("client=") && s.Contains("core=")));
    }

    [TestMethod]
    public void Apply_EmptySlot_LogsTheCoreOnlyLine()
    {
        var container = CoreContainer();
        var logger = Substitute.For<IModLogger>();
        container.RegisterInstance(logger);
        ClientComposition.Current = null;

        ClientComposition.Apply(container);

        logger.Received(1).LogInfo(NSubstitute.Arg.Is<string>(s => s.Contains("[Client] no client composition")));
    }

    [TestMethod]
    public void Apply_EmptySlotOnARenderingClient_LogsAnErrorNamingTheMissingClient()
    {
        var container = CoreContainer();
        var logger = Substitute.For<IModLogger>();
        container.RegisterInstance(logger);
        ClientComposition.Current = null;
        ClientComposition.IsDedicatedServer = () => false;

        ClientComposition.Apply(container);

        logger.Received(1).LogError(NSubstitute.Arg.Is<string>(s =>
            s.Contains("TAOM.Client") && s.Contains("faction map") && s.Contains("front-end art")
            && s.Contains("Custom Battle")));
    }

    [TestMethod]
    public void Apply_EmptySlotOnADedicatedServer_LogsNoError()
    {
        var container = CoreContainer();
        var logger = Substitute.For<IModLogger>();
        container.RegisterInstance(logger);
        ClientComposition.Current = null;
        ClientComposition.IsDedicatedServer = () => true;

        ClientComposition.Apply(container);

        logger.DidNotReceive().LogError(NSubstitute.Arg.Any<string>());
    }

    [TestMethod]
    public void Apply_SlotSetOnAClient_LogsNoMissingClientError()
    {
        var container = CoreContainer();
        var logger = Substitute.For<IModLogger>();
        container.RegisterInstance(logger);
        ClientComposition.Current = new Registering(_ => { });
        ClientComposition.IsDedicatedServer = () => false;

        ClientComposition.Apply(container);

        logger.DidNotReceive().LogError(NSubstitute.Arg.Any<string>());
    }

    [TestMethod]
    public void DescribeStampMismatch_PairedStamps_ReturnsNull()
    {
        Assert.IsNull(ClientComposition.DescribeStampMismatch(
            "v2.0.35.0 build.20261022-100000Z+abc", "v2.0.35.0 build.20261022-100500Z+abc"));
    }

    [TestMethod]
    public void DescribeStampMismatch_StampsDaysApart_NamesTheClientAndCoreStamps()
    {
        var line = ClientComposition.DescribeStampMismatch(
            "v2.0.35.0 build.20261001-100000Z+abc", "v2.0.35.0 build.20261022-100000Z+def");

        Assert.IsNotNull(line);
        StringAssert.Contains(line, "TAOM.Client");
        StringAssert.Contains(line, "build.20261001-100000Z");
        StringAssert.Contains(line, "build.20261022-100000Z");
    }

    [TestMethod]
    public void DescribeStampMismatch_HoursApart_ReturnsAWarning()
    {
        Assert.IsNotNull(ClientComposition.DescribeStampMismatch(
            "v1 build.20261022-010000Z+a", "v1 build.20261022-100000Z+a"));
    }

    [TestMethod]
    public void DescribeStampMismatch_NoStampOnOneSide_ReturnsNull()
    {
        Assert.IsNull(ClientComposition.DescribeStampMismatch("v1", "v1 build.20261022-100000Z+a"));
    }

    [TestMethod]
    public void Apply_LoggerThrows_DoesNotThrow()
    {
        var container = CoreContainer();
        var logger = Substitute.For<IModLogger>();
        logger.When(l => l.LogInfo(NSubstitute.Arg.Any<string>())).Do(_ => throw new InvalidOperationException("log down"));
        container.RegisterInstance(logger);
        ClientComposition.Current = new Registering(_ => { });

        ClientComposition.Apply(container);
    }

    [TestMethod]
    public void Apply_NoLoggerRegistered_DoesNotThrow()
    {
        var container = CoreContainer();
        ClientComposition.Current = new Registering(_ => { });

        ClientComposition.Apply(container);
    }
}
