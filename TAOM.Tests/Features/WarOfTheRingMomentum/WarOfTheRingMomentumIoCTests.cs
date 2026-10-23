using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.WarOfTheRingMomentum;
using TAOM.Features.WarOfTheRingMomentum.UI;

namespace TAOM.Tests.Features.WarOfTheRingMomentum;

/// <summary>ADR-013, B4b. The map meter's service is client-only: core registers the null one and TAOM.Client replaces it.</summary>
[TestClass]
public class WarOfTheRingMomentumIoCTests
{
    [TestMethod]
    public void Register_TheMapMeterService_IsTheNullOne()
    {
        using var container = new Container();

        WarOfTheRingMomentumIoC.RegisterWarOfTheRingMomentumFeature(container);

        Assert.IsInstanceOfType(container.Resolve<IMomentumUIService>(), typeof(NullMomentumUIService));
    }
}
