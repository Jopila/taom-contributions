using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Relations;

namespace TAOM.Tests.Features.Relations;

[TestClass]
public class PersonalRelationServiceTests
{
    private static readonly PersonalRelationSettings Defaults = new(true, 28, 32);

    private IPersonalRelationSettingsProvider _settings = null!;
    private IRandomSource _random = null!;
    private PersonalRelationService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IPersonalRelationSettingsProvider>();
        _settings.Enabled.Returns(true);
        _settings.Get().Returns(Defaults);
        _random = Substitute.For<IRandomSource>();
        _random.Next(Arg.Any<int>(), Arg.Any<int>()).Returns(call => call.ArgAt<int>(0));
        _sut = new PersonalRelationService(_settings, _random);
        _sut.Engage(true);
    }

    // --- IsActive: engaged AND the switch ---

    [TestMethod]
    public void IsActive_NotEngaged_IsFalse_EvenWithTheSwitchOn()
    {
        var fresh = new PersonalRelationService(_settings, _random);

        Assert.IsFalse(fresh.IsActive);
    }

    [TestMethod]
    public void IsActive_Engaged_FollowsTheSwitchLive()
    {
        Assert.IsTrue(_sut.IsActive);

        _settings.Enabled.Returns(false);
        Assert.IsFalse(_sut.IsActive);

        _settings.Enabled.Returns(true);
        Assert.IsTrue(_sut.IsActive);
    }

    [TestMethod]
    public void Engage_False_DisarmsIt()
    {
        _sut.Engage(false);

        Assert.IsFalse(_sut.IsActive);
    }

    [TestMethod]
    public void IsActive_ReadsOnlyTheCachedSwitch_NeverTheWholeGroup()
    {
        _ = _sut.IsActive;
        _sut.Resolve(true, false);

        _settings.DidNotReceive().Get();
    }

    // --- Resolve: every combination of facts, active and inactive ---

    [DataTestMethod]
    [DataRow(true, false, PersonalPairSide.UnmapSecond)]
    [DataRow(false, true, PersonalPairSide.UnmapFirst)]
    [DataRow(false, false, PersonalPairSide.None)]
    [DataRow(true, true, PersonalPairSide.None)]
    public void Resolve_Active_UnmapsOnlyTheOtherSideOfAPlayerPair(bool first, bool second, PersonalPairSide expected)
    {
        Assert.AreEqual(expected, _sut.Resolve(first, second));
    }

    [DataTestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public void Resolve_SwitchOff_IsAlwaysNone(bool first, bool second)
    {
        _settings.Enabled.Returns(false);

        Assert.AreEqual(PersonalPairSide.None, _sut.Resolve(first, second));
    }

    [DataTestMethod]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    [DataRow(true, true)]
    public void Resolve_NotEngaged_IsAlwaysNone(bool first, bool second)
    {
        _sut.Engage(false);

        Assert.AreEqual(PersonalPairSide.None, _sut.Resolve(first, second));
    }

    // --- disband cost: the army leader only ---

    [TestMethod]
    public void DisbandCost_Leader_DrawsFromTheLeaderRange_Inclusive()
    {
        _random.Next(28, 33).Returns(28, 32);

        Assert.AreEqual(-28, _sut.DisbandCost(true));
        Assert.AreEqual(-32, _sut.DisbandCost(true));
    }

    [TestMethod]
    public void DisbandCost_Member_IsVanillasOwn_NoRoll()
    {
        Assert.IsNull(_sut.DisbandCost(false));

        _random.DidNotReceiveWithAnyArgs().Next(default, default);
    }

    [TestMethod]
    public void DisbandCost_UsesTheConfiguredLeaderRange()
    {
        _settings.Get().Returns(Defaults with { LeaderMin = 5, LeaderMax = 9 });
        _random.Next(5, 10).Returns(7);

        Assert.AreEqual(-7, _sut.DisbandCost(true));
    }

    [TestMethod]
    public void DisbandCost_SwitchOff_IsVanilla()
    {
        _settings.Enabled.Returns(false);

        Assert.IsNull(_sut.DisbandCost(true));
    }

    [TestMethod]
    public void DisbandCost_NotEngaged_IsVanilla()
    {
        _sut.Engage(false);

        Assert.IsNull(_sut.DisbandCost(true));
    }

    [TestMethod]
    public void DisbandCost_CostSwitchOff_IsVanilla_EvenWithPersonalRelationsOn()
    {
        _settings.Get().Returns(Defaults with { DisbandCostEnabled = false });

        Assert.IsNull(_sut.DisbandCost(true));
    }

    [TestMethod]
    public void DisbandCost_ARangeOfZero_CostsNothing_NotVanilla()
    {
        _settings.Get().Returns(Defaults with { LeaderMin = 0, LeaderMax = 0 });
        _random.Next(0, 1).Returns(0);

        Assert.AreEqual(0, _sut.DisbandCost(true));
    }

    [TestMethod]
    public void DisbandCost_NeverReadsTheRandomWhenVanillaDecides()
    {
        _sut.Engage(false);

        _sut.DisbandCost(true);

        _random.DidNotReceiveWithAnyArgs().Next(default, default);
    }

    // --- succession ---

    [TestMethod]
    public void InheritedRelationChange_ThePlayer_Active_IsZero()
    {
        Assert.AreEqual(0, _sut.InheritedRelationChange(true));
    }

    [TestMethod]
    public void InheritedRelationChange_AnotherHero_IsVanilla()
    {
        Assert.IsNull(_sut.InheritedRelationChange(false));
    }

    [TestMethod]
    public void InheritedRelationChange_ThePlayer_SwitchOffOrNotEngaged_IsVanilla()
    {
        _settings.Enabled.Returns(false);
        Assert.IsNull(_sut.InheritedRelationChange(true));

        _settings.Enabled.Returns(true);
        _sut.Engage(false);
        Assert.IsNull(_sut.InheritedRelationChange(true));
    }
}
