using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.Relations;

/// <summary>
/// The group is user-editable and MCM deserializes a hand-edited json verbatim (the slider range is a UI hint), so every
/// value is clamped. The values are ints, so there is no NaN case; sign and range are the rules, and a leader range given the
/// wrong way round is swapped.
/// </summary>
[TestClass]
public class PersonalRelationSettingsProviderTests
{
    [TestMethod]
    public void From_NoSettingsYet_IsTheCompiledDefaults()
    {
        Assert.AreEqual(new PersonalRelationSettings(true, 28, 32), PersonalRelationSettingsProvider.From(null));
    }

    [TestMethod]
    public void From_TheMcmDefaults_MatchTheNoSettingsFallback()
    {
        Assert.AreEqual(PersonalRelationSettingsProvider.From(null), PersonalRelationSettingsProvider.From(new TaomSettings()));
    }

    [TestMethod]
    public void From_ValuesInRange_PassThrough()
    {
        var s = PersonalRelationSettingsProvider.From(new TaomSettings
        {
            PersonalDisbandCostEnabled = false,
            PersonalDisbandLeaderMin = 10,
            PersonalDisbandLeaderMax = 20,
        });

        Assert.AreEqual(new PersonalRelationSettings(false, 10, 20), s);
    }

    [DataTestMethod]
    [DataRow(-1, 0)]
    [DataRow(int.MinValue, 0)]
    [DataRow(0, 0)]
    [DataRow(50, 50)]
    [DataRow(51, 50)]
    [DataRow(int.MaxValue, 50)]
    public void From_EachCostValue_IsClampedToZeroToFifty(int value, int expected)
    {
        var s = PersonalRelationSettingsProvider.From(new TaomSettings
        {
            PersonalDisbandLeaderMin = value, PersonalDisbandLeaderMax = value,
        });

        Assert.AreEqual(expected, s.LeaderMin);
        Assert.AreEqual(expected, s.LeaderMax);
    }

    [TestMethod]
    public void From_ALeaderRangeTheWrongWayRound_IsSwapped()
    {
        var s = PersonalRelationSettingsProvider.From(new TaomSettings { PersonalDisbandLeaderMin = 40, PersonalDisbandLeaderMax = 30 });

        Assert.AreEqual(30, s.LeaderMin);
        Assert.AreEqual(40, s.LeaderMax);
    }

    [TestMethod]
    public void From_ALeaderRangeTheWrongWayRound_IsSwapped_AfterClamping()
    {
        var s = PersonalRelationSettingsProvider.From(new TaomSettings { PersonalDisbandLeaderMin = 99, PersonalDisbandLeaderMax = -4 });

        Assert.AreEqual(0, s.LeaderMin);
        Assert.AreEqual(50, s.LeaderMax);
    }

    [TestMethod]
    public void Enabled_WithoutMcm_IsTheCompiledDefault()
    {
        // TaomSettings.Instance is null in a test host.
        Assert.IsTrue(new PersonalRelationSettingsProvider().Enabled);
        Assert.AreEqual(PersonalRelationSettingsProvider.From(null), new PersonalRelationSettingsProvider().Get());
    }
}
