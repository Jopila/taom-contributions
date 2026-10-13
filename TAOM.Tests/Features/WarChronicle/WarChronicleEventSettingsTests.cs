using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.WarChronicle;

namespace TAOM.Tests.Features.WarChronicle;

/// <summary>
/// The MCM group "War of the Ring/War Chronicle" (#765): Enable Chronicle, Show Announcements and the Army
/// Focus Multiplier, read through <see cref="WarChronicleSettingsProvider"/>'s lazy settings accessor.
/// </summary>
[TestClass]
public class WarChronicleEventSettingsTests
{
    [TestMethod]
    public void NoMcm_CompiledDefaultsHold()
    {
        var sut = new WarChronicleSettingsProvider();

        Assert.IsTrue(sut.ChronicleEnabled);
        Assert.IsTrue(sut.ChronicleAnnouncements);
        Assert.AreEqual(1.4f, sut.ArmyFocusMultiplier, 0.0001f);
    }

    [TestMethod]
    public void DefaultSettings_MatchTheCompiledDefaults()
    {
        var sut = new WarChronicleSettingsProvider(new TaomSettings());

        Assert.IsTrue(sut.ChronicleEnabled);
        Assert.IsTrue(sut.ChronicleAnnouncements);
        Assert.AreEqual(1.4f, sut.ArmyFocusMultiplier, 0.0001f);
    }

    [DataTestMethod]
    [DataRow(1f, 1f)]
    [DataRow(2.5f, 2.5f)]
    [DataRow(1.7f, 1.7f)]
    [DataRow(0.5f, 1f)]
    [DataRow(-3f, 1f)]
    [DataRow(9f, 2.5f)]
    [DataRow(float.NaN, 1.4f)]
    [DataRow(float.PositiveInfinity, 1.4f)]
    [DataRow(float.NegativeInfinity, 1.4f)]
    public void ArmyFocusMultiplier_IsClampedAndNonFiniteFallsBackToTheDefault(float stored, float expected)
    {
        var sut = new WarChronicleSettingsProvider(new TaomSettings { WarChronicleArmyFocus = stored });

        Assert.AreEqual(expected, sut.ArmyFocusMultiplier, 0.0001f);
    }

    [TestMethod]
    public void Switches_ReadThroughTheCachedSettings()
    {
        var mcm = new TaomSettings();
        var sut = new WarChronicleSettingsProvider(mcm);

        mcm.WarChronicleEnabled = false;
        mcm.WarChronicleAnnouncements = false;
        mcm.WarChronicleArmyFocus = 2f;

        Assert.IsFalse(sut.ChronicleEnabled);
        Assert.IsFalse(sut.ChronicleAnnouncements);
        Assert.AreEqual(2f, sut.ArmyFocusMultiplier, 0.0001f);
    }
}
