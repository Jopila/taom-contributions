using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.ViewModelCollection.ArmyManagement;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.RedArrow;
using TAOM.Features.RedArrow.Hooks;

namespace TAOM.Tests.Features.RedArrow;

/// <summary>
/// The two Red Arrow view-model postfixes (#873) on a bare <c>ArmyManagementItemVM</c>. The eligibility postfix writes
/// <c>IsEligible</c> and the private <c>_eligibilityReason</c> only past vanilla's skip branch and only when the feature
/// applies; the constructor postfix writes <c>IsTransferDisabled</c> only when it applies.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class RedArrowPostfixTests
{
    private static readonly TextObject VanillaText = new TextObject("{=vanilla}Vanilla refused");

    private IRedArrowSettingsProvider _settings = null!;
    private IPlayerSiegeStateAdapter _player = null!;
    private IArmyCallCandidateAdapter _candidates = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IRedArrowSettingsProvider>();
        _settings.Enabled.Returns(true);
        _player = Substitute.For<IPlayerSiegeStateAdapter>();
        _player.ReadSiege().Returns(new PlayerSiegeState(true, true));
        _candidates = Substitute.For<IArmyCallCandidateAdapter>();
        var service = new RedArrowService(_settings, Substitute.For<ICoopSessionProvider>(), Substitute.For<IDedicatedServerProvider>());
        RedArrowCalls.Initialize(new RedArrowArmyBoundary(service, _player, _candidates, Substitute.For<IModLogger>()), Substitute.For<IModLogger>());
        Assert.IsTrue(ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch.Prepare());
    }

    [TestCleanup]
    public void Cleanup() => RedArrowCalls.Initialize(null!, null!);

    private static ArmyManagementItemVM Row(bool isMainHero = false)
    {
        var row = (ArmyManagementItemVM)FormatterServices.GetUninitializedObject(typeof(ArmyManagementItemVM));
        row.IsMainHero = isMainHero;
        row.IsEligible = false;
        row.IsTransferDisabled = true;
        SetReason(row, VanillaText);
        return row;
    }

    private static void SetReason(ArmyManagementItemVM row, TextObject text)
    {
        var field = typeof(ArmyManagementItemVM).GetField("_eligibilityReason", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, "ArmyManagementItemVM._eligibilityReason is gone; the test cannot stage the row.");
        field!.SetValue(row, text);
    }

    private static TextObject Reason(ArmyManagementItemVM row) =>
        (TextObject)typeof(ArmyManagementItemVM).GetField("_eligibilityReason", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(row)!;

    private void ACandidateThatPasses() => _candidates.Read(null).ReturnsForAnyArgs(
        new ArmyCallCandidate(true, false, CandidateArmy.None, false, 0.9f, 0.4f, false, false, true, false, 10f, 100f));

    [TestMethod]
    public void Recheck_PastTheSkipBranch_WritesEligibilityAndTheReason()
    {
        ACandidateThatPasses();
        var row = Row();

        ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch.Postfix(row);

        Assert.IsTrue(row.IsEligible);
        Assert.IsTrue(string.IsNullOrEmpty(Reason(row).Value), "the empty text vanilla leaves for an allowed action");
    }

    [TestMethod]
    public void Recheck_ARefusedParty_WritesVanillasTextForIt()
    {
        _candidates.Read(null).ReturnsForAnyArgs(
            new ArmyCallCandidate(true, false, CandidateArmy.OtherArmy, false, 0.9f, 0.4f, false, false, true, false, 10f, 100f));
        var row = Row();
        row.IsEligible = true;

        ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch.Postfix(row);

        Assert.IsFalse(row.IsEligible);
        StringAssert.StartsWith(Reason(row).Value, "{=aROohsat}");
    }

    [TestMethod]
    public void Recheck_WritesNothing_WhenVanillaSkippedTheRow()
    {
        ACandidateThatPasses();
        var free = Row();
        free.CanJoinBackWithoutCost = true;
        var added = Row();
        added.IsInCart = true;
        added.IsAlreadyWithPlayer = false;

        ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch.Postfix(free);
        ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch.Postfix(added);

        Assert.IsFalse(free.IsEligible);
        Assert.AreSame(VanillaText, Reason(free));
        Assert.IsFalse(added.IsEligible);
        Assert.AreSame(VanillaText, Reason(added));
    }

    [TestMethod]
    public void Recheck_WritesNothing_WhenTheFeatureDoesNotApply()
    {
        ACandidateThatPasses();
        _settings.Enabled.Returns(false);
        var row = Row();

        ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch.Postfix(row);

        Assert.IsFalse(row.IsEligible);
        Assert.AreSame(VanillaText, Reason(row));
    }

    [TestMethod]
    public void Recheck_WritesNothing_WhenNoBoundaryWasHandedOver()
    {
        RedArrowCalls.Initialize(null!, null!);
        var row = Row();

        ArmyManagementItemVM_UpdateEligibility_RedArrow_Patch.Postfix(row);

        Assert.IsFalse(row.IsEligible);
        Assert.AreSame(VanillaText, Reason(row));
    }

    [TestMethod]
    public void Transfer_TheMainRowStaysDisabled_EveryOtherRowIsEnabled()
    {
        var main = Row(isMainHero: true);
        var other = Row(isMainHero: false);

        ArmyManagementItemVM_Constructor_RedArrow_Patch.Postfix(main);
        ArmyManagementItemVM_Constructor_RedArrow_Patch.Postfix(other);

        Assert.IsTrue(main.IsTransferDisabled);
        Assert.IsFalse(other.IsTransferDisabled);
    }

    [TestMethod]
    public void Transfer_WithTheFeatureOff_LeavesVanillasValue()
    {
        _settings.Enabled.Returns(false);
        var other = Row(isMainHero: false);

        ArmyManagementItemVM_Constructor_RedArrow_Patch.Postfix(other);

        Assert.IsTrue(other.IsTransferDisabled, "vanilla disabled it for the siege and nothing lifted that");
    }
}
