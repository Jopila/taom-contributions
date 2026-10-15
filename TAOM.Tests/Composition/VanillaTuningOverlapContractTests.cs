using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;

namespace TAOM.Tests.Composition;

/// <summary>
/// Pins the contract yotthani's VanillaTuning v0.1.1 reads in <c>HoN/VanillaTuning/Core/TaomOverlap.cs</c>:
/// it stands down for a feature when assembly <c>TAOM</c> contains the matching module type by full name.
/// A rename restores double patching; parking or unlisting a module leaves the work undone by both mods.
/// The four names stand for: nameplate culling, map view release, mission start guard and the skeleton
/// buffer guard; SkeletonBufferModule stands for both frame-buffer-guard and frame-buffer-watch.
/// </summary>
[TestClass]
public class VanillaTuningOverlapContractTests
{
    private static readonly string[] ContractTypeNames =
    {
        "TAOM.Features.NameplateCull.NameplateCullModule",
        "TAOM.Features.MapViewRelease.MapViewReleaseModule",
        "TAOM.Features.MissionStartGuard.MissionStartGuardModule",
        "TAOM.Features.SkeletonBuffer.SkeletonBufferModule",
    };

    [TestMethod]
    public void TaomAssembly_Name_IsTaom()
    {
        Assert.AreEqual("TAOM", typeof(FeatureModules).Assembly.GetName().Name);
    }

    [TestMethod]
    public void ContractTypes_LookedUpByFullName_EachResolves()
    {
        var assembly = typeof(FeatureModules).Assembly;
        foreach (var name in ContractTypeNames)
            Assert.IsNotNull(assembly.GetType(name, false), name + " no longer resolves; VanillaTuning would patch the same code again.");
    }

    [TestMethod]
    public void ContractTypes_InFeatureModulesAll_EachListedOnceAndNotParked()
    {
        foreach (var name in ContractTypeNames)
        {
            var listed = FeatureModules.All.Where(m => m.GetType().FullName == name).ToList();
            Assert.AreEqual(1, listed.Count, name + " must be listed exactly once in FeatureModules.All.");
            Assert.IsNull(listed[0].ParkedReason, name + " is parked, but VanillaTuning stands down for it.");
        }
    }
}
