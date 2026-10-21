using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Pins when CollectAssemblyTypesShim may hand back a partial type list. A BannerlordCoop assembly
/// that fails to load fully must surface the failure while the BannerlordCoop module (id "Coop") is
/// active: a partial list makes the mod register half its types and desync quietly. Any other
/// co-op module, and the force flag alone, keep the historic swallow.
/// </summary>
[TestClass]
public class CollectAssemblyTypesPolicyTests
{
    private static readonly string[] CoopActive = { "Coop" };
    private static readonly string[] BannerlordTogetherOnly = { "BannerlordTogether" };
    private static readonly string[] ForcedOnly = { CoopPresencePolicy.ForcedMarkerId };

    [TestMethod]
    public void ShouldSwallow_BannerlordTogetherOnly_CoopNamedAssembly_ReturnsTrue()
    {
        // BannerlordTogether is a different mod: an assembly that happens to be named "Common" is
        // not Coop's, so the swallow stays.
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("Common", BannerlordTogetherOnly));
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("Coop.Core", BannerlordTogetherOnly));
    }

    [TestMethod]
    public void ShouldSwallow_CoopActive_AutoSync_ReturnsFalse()
    {
        // Coop compiles this assembly at runtime (GameInterface AutoSyncBuilder).
        Assert.IsFalse(CollectAssemblyTypesPolicy.ShouldSwallow("AutoSync", CoopActive));
    }

    [TestMethod]
    public void ShouldSwallow_ForcedByFlagOnly_ReturnsTrue()
    {
        // The force flag reports a marker, not the Coop module id, so it never counts as Coop.
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("Coop.Core", ForcedOnly));
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("AutoSync", ForcedOnly));
    }

    [TestMethod]
    public void ShouldSwallow_NoCoop_AnyAssembly_ReturnsTrue()
    {
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("Coop.Core", Array.Empty<string>()));
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("SomeOtherMod", Array.Empty<string>()));
    }

    [TestMethod]
    public void ShouldSwallow_CoopActive_NonCoopAssembly_ReturnsTrue()
    {
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("SomeOtherMod", CoopActive));
    }

    [TestMethod]
    public void ShouldSwallow_CoopActive_EveryCoopAssembly_ReturnsFalse()
    {
        foreach (var name in new[] { "Coop", "Coop.Core", "GameInterface", "Common", "Missions", "Coop.Steam", "Coop.CrashReporter", "AutoSync" })
        {
            Assert.IsFalse(CollectAssemblyTypesPolicy.ShouldSwallow(name, CoopActive), name);
        }
    }

    [TestMethod]
    public void ShouldSwallow_CoopActive_NameMatchIsCaseInsensitive()
    {
        Assert.IsFalse(CollectAssemblyTypesPolicy.ShouldSwallow("coop.core", CoopActive));
        Assert.IsFalse(CollectAssemblyTypesPolicy.ShouldSwallow("coop.core", new[] { "coop" }));
    }

    [TestMethod]
    public void ShouldSwallow_CoopActive_NameMatchIsExactNotPrefix()
    {
        // "Coop.Extras" and "CommonLib" are other assemblies that merely start like a co-op one.
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("Coop.Extras", CoopActive));
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("CommonLib", CoopActive));
    }

    [TestMethod]
    public void ShouldSwallow_CoopActive_UnknownAssemblyName_ReturnsTrue()
    {
        // An unreadable name keeps today's behaviour rather than turning into a throw.
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow(null, CoopActive));
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("", CoopActive));
    }

    [TestMethod]
    public void ShouldSwallow_NullIdList_ReturnsTrue()
    {
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("Coop.Core", null!));
    }
}
