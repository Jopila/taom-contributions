using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.WatchfulHolds;

namespace TAOM.Tests.Features.WatchfulHolds;

[TestClass]
public class WatchfulHoldsSettingsProviderTests
{
    [TestMethod]
    public void McmDefaults_BothSwitchesAreOn()
    {
        var mcm = new TaomSettings();

        Assert.IsTrue(mcm.EnableWatchfulHolds);
        Assert.IsTrue(mcm.WatchfulHoldsHarassAiLords);
    }

    [TestMethod]
    public void Provider_ReadsBothSwitchesFromTheSettings()
    {
        var mcm = new TaomSettings { EnableWatchfulHolds = false, WatchfulHoldsHarassAiLords = false };
        var sut = new WatchfulHoldsSettingsProvider(mcm);

        Assert.IsFalse(sut.Enabled);
        Assert.IsFalse(sut.HarassAiLords);
    }

    [TestMethod]
    public void Provider_TheTwoSwitchesAreIndependent()
    {
        var mcm = new TaomSettings { WatchfulHoldsHarassAiLords = false };
        var sut = new WatchfulHoldsSettingsProvider(mcm);

        Assert.IsTrue(sut.Enabled);
        Assert.IsFalse(sut.HarassAiLords);
    }

    [TestMethod]
    public void Provider_WithNoMcmInstance_FallsBackToOn()
    {
        // Instance is null before MCM is up (and in a test run): the defaults keep the feature working.
        var sut = new WatchfulHoldsSettingsProvider();

        Assert.IsTrue(sut.Enabled);
        Assert.IsTrue(sut.HarassAiLords);
    }
}
