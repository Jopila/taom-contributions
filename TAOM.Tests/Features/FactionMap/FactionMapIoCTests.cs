using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Hooks;

namespace TAOM.Tests.Features.FactionMap;

/// <summary>
/// ADR-013, B4a. The culture-stage hooks hold adapters that TAOM.Client replaces, and DryIoc keeps the
/// instances it built when a hook was resolved: a hook resolved while FactionMap registers (before
/// ClientComposition.Apply) would keep core's null adapters on a client. So registration resolves
/// nothing, and IoC.cs initializes the patch statics after the client composition.
/// </summary>
[TestClass]
public class FactionMapIoCTests
{
    [TestMethod]
    public void RegisterFactionMapFeature_ResolvesNothing()
    {
        var container = new Container();

        // A bare container has no ICultureStageMovieOverride, so resolving any hook here would throw: the
        // call returning is the proof that registration builds nothing (IoC.Configure's own order is
        // pinned by FeatureModulesTests).
        FactionMapIoC.RegisterFactionMapFeature(container);

        Assert.IsTrue(container.IsRegistered<IOnCultureStageViewCreated>());
        Assert.IsTrue(container.IsRegistered<IOnCultureStageViewTick>());
        Assert.IsTrue(container.IsRegistered<IOnCultureStageViewFinalize>());
    }
}
