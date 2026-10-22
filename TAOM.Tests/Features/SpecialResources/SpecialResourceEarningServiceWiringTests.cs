using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// SpecialResourceEarningService is registered Transient, so every Resolve builds a new instance. One instance
/// per campaign is the intended wiring: SubModule resolves it once, inside the method that builds the
/// behavior, and nothing else resolves a second copy.
/// </summary>
[TestClass]
public class SpecialResourceEarningServiceWiringTests
{
    private const string Resolve = "Resolve<SpecialResourceEarningService>(";

    [TestMethod]
    public void EarningService_IsResolvedOnceInSubModule_AndNowhereElse()
    {
        var holders = RepoPaths.CSharpFiles()
            .Where(path => RepoPaths.StripComments(File.ReadAllText(path)).Contains(Resolve))
            .Select(path => Path.GetFileName(path))
            .ToList();

        CollectionAssert.AreEqual(new[] { "SubModule.cs" }, holders, "only SubModule resolves the earning service");

        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        Assert.AreEqual(1, subModule.Split(new[] { Resolve }, StringSplitOptions.None).Length - 1,
            "SubModule resolves it once per campaign start");

        var method = subModule.IndexOf("private static void RegisterSpecialResourcesAndCareers(", StringComparison.Ordinal);
        var resolve = subModule.IndexOf(Resolve, StringComparison.Ordinal);
        var behavior = subModule.IndexOf("new SpecialResourcesBehavior(", StringComparison.Ordinal);
        Assert.IsTrue(method >= 0 && method < behavior && behavior < resolve,
            "the one resolve is an argument of the behavior built in RegisterSpecialResourcesAndCareers");
    }
}
