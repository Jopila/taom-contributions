using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// Pins the player-control decision and the reflection bind to Coop's player registry:
/// the bind happens once like <see cref="CoopSessionProvider"/>, and an answer is never cached.
/// </summary>
[TestClass]
public class CoopPlayerControlQueryTests
{
    public static class FakePlayerManager
    {
        public static object? Registered;

        public static bool TryGetControlledObjectInfo(object obj, out object info)
        {
            info = null!;
            return ReferenceEquals(obj, Registered);
        }
    }

    public static class ThrowingPlayerManager
    {
        public static bool TryGetControlledObjectInfo(object obj, out object info)
            => throw new InvalidOperationException("registry torn down");
    }

    private static ICoopSessionProvider Session(bool active)
    {
        var s = Substitute.For<ICoopSessionProvider>();
        s.IsSessionActive.Returns(active);
        return s;
    }

    [TestMethod]
    public void Classify_LocalPlayer_NeverScans()
    {
        var scans = 0;
        var q = new CoopPlayerControlQuery(Session(true), Substitute.For<IModLogger>(),
            _ => { scans++; return null; });

        Assert.AreEqual(PlayerControl.Player, q.Classify(new object(), isLocalPlayer: true));
        Assert.AreEqual(0, scans);
    }

    [TestMethod]
    public void Classify_NoSession_NotPlayerWithoutScan()
    {
        var scans = 0;
        var q = new CoopPlayerControlQuery(Session(false), Substitute.For<IModLogger>(),
            _ => { scans++; return typeof(FakePlayerManager); });

        Assert.AreEqual(PlayerControl.NotPlayer, q.Classify(new object(), isLocalPlayer: false));
        Assert.AreEqual(0, scans);
    }

    [TestMethod]
    public void Classify_SessionRegistryUnbound_IsUnknown()
    {
        var q = new CoopPlayerControlQuery(Session(true), Substitute.For<IModLogger>(), _ => null);

        Assert.AreEqual(PlayerControl.Unknown, q.Classify(new object(), isLocalPlayer: false));
    }

    [TestMethod]
    public void Classify_RegistryAnswersLive_NotCached()
    {
        var q = new CoopPlayerControlQuery(Session(true), Substitute.For<IModLogger>(),
            _ => typeof(FakePlayerManager));
        var entity = new object();

        FakePlayerManager.Registered = entity;
        Assert.AreEqual(PlayerControl.Player, q.Classify(entity, false));

        FakePlayerManager.Registered = null;
        Assert.AreEqual(PlayerControl.NotPlayer, q.Classify(entity, false));
    }

    [TestMethod]
    public void Classify_RegistryThrows_IsUnknownAndLogsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        var q = new CoopPlayerControlQuery(Session(true), logger, _ => typeof(ThrowingPlayerManager));

        Assert.AreEqual(PlayerControl.Unknown, q.Classify(new object(), false));
        Assert.AreEqual(PlayerControl.Unknown, q.Classify(new object(), false));

        logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Classify_FailedBind_ScansOnlyOnce()
    {
        var scans = 0;
        var q = new CoopPlayerControlQuery(Session(true), Substitute.For<IModLogger>(),
            _ => { scans++; return null; });

        Assert.AreEqual(PlayerControl.Unknown, q.Classify(new object(), false));
        Assert.AreEqual(PlayerControl.Unknown, q.Classify(new object(), false));

        Assert.AreEqual(1, scans);
    }

    [TestMethod]
    public void Classify_FinderThrows_IsUnknownAndWarnsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        var q = new CoopPlayerControlQuery(Session(true), logger,
            _ => throw new InvalidOperationException("assembly scan failed"));

        Assert.AreEqual(PlayerControl.Unknown, q.Classify(new object(), false));
        Assert.AreEqual(PlayerControl.Unknown, q.Classify(new object(), false));

        logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Classify_CleanBind_LogsNoWarning()
    {
        var logger = Substitute.For<IModLogger>();
        var q = new CoopPlayerControlQuery(Session(true), logger, _ => typeof(FakePlayerManager));

        FakePlayerManager.Registered = null;
        Assert.AreEqual(PlayerControl.NotPlayer, q.Classify(new object(), false));

        logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    [TestCategory("RequiresGame")] // OfParty and OfClan take engine types
    public void OfPartyAndOfClan_Null_AreNotPlayer()
    {
        var q = new CoopPlayerControlQuery(Session(true), Substitute.For<IModLogger>(),
            _ => typeof(FakePlayerManager));

        Assert.AreEqual(PlayerControl.NotPlayer, q.OfParty(null));
        Assert.AreEqual(PlayerControl.NotPlayer, q.OfClan(null));
    }
}
