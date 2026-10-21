using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Dependencies.Foundation;

namespace TAOM.Tests.Infrastructure.Dependencies;

/// <summary>
/// Pins when CollectAssemblyTypesShim may hand back a partial type list. A co-op assembly that
/// fails to load fully must surface the failure while a co-op module is active: a partial list
/// makes the co-op mod register half its types and desync quietly.
/// </summary>
[TestClass]
public class CollectAssemblyTypesPolicyTests
{
    [TestMethod]
    public void ShouldSwallow_NoCoop_AnyAssembly_ReturnsTrue()
    {
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("Coop.Core", coopActive: false));
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("SomeOtherMod", coopActive: false));
    }

    [TestMethod]
    public void ShouldSwallow_CoopActive_NonCoopAssembly_ReturnsTrue()
    {
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("SomeOtherMod", coopActive: true));
    }

    [TestMethod]
    public void ShouldSwallow_CoopActive_EveryCoopAssembly_ReturnsFalse()
    {
        foreach (var name in new[] { "Coop", "Coop.Core", "GameInterface", "Common", "Missions", "Coop.Steam", "Coop.CrashReporter" })
        {
            Assert.IsFalse(CollectAssemblyTypesPolicy.ShouldSwallow(name, coopActive: true), name);
        }
    }

    [TestMethod]
    public void ShouldSwallow_CoopActive_NameMatchIsCaseInsensitive()
    {
        Assert.IsFalse(CollectAssemblyTypesPolicy.ShouldSwallow("coop.core", coopActive: true));
    }

    [TestMethod]
    public void ShouldSwallow_CoopActive_NameMatchIsExactNotPrefix()
    {
        // "Coop.Extras" and "CommonLib" are other assemblies that merely start like a co-op one.
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("Coop.Extras", coopActive: true));
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("CommonLib", coopActive: true));
    }

    [TestMethod]
    public void ShouldSwallow_CoopActive_UnknownAssemblyName_ReturnsTrue()
    {
        // An unreadable name keeps today's behaviour rather than turning into a throw.
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow(null, coopActive: true));
        Assert.IsTrue(CollectAssemblyTypesPolicy.ShouldSwallow("", coopActive: true));
    }
}
