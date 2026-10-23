using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Namegiving;

/// <summary>
/// Source pins for the engine side of Namegiving. The adapter and the mixin need a running campaign, so what matters about
/// each engine call is pinned in the comment-stripped source (docs/features/namegiving.md).
/// </summary>
[TestClass]
public class FiefNamingAdapterPinTests
{
    private static string Adapter() => RepoPaths.ReadSource("Main/Adapters/FiefNamingAdapter.cs", stripComments: true);

    private static string Mixin() => RepoPaths.ReadSource("Client/Features/Namegiving/UI/FiefNamingMixin.cs", stripComments: true);

    [TestMethod]
    public void ApplyPlayerName_NamesTheSettlementFirst_ThenChargesTheInfluence()
    {
        var src = Adapter();

        var name = src.IndexOf("SetCustomName(", System.StringComparison.Ordinal);
        var charge = src.IndexOf("ChangeClanInfluenceAction.Apply(", System.StringComparison.Ordinal);
        Assert.IsTrue(name >= 0 && charge > name, "the name is written before the influence is charged, so a throw in the charge leaves a named fief, never a charge for nothing");
        StringAssert.Contains(src, "ChangeClanInfluenceAction.Apply(Clan.PlayerClan, -cost)");
        Assert.AreEqual(1, Regex.Matches(src, @"ChangeClanInfluenceAction\.Apply\(").Count);
    }

    [TestMethod]
    public void ApplyPlayerName_StoresTheNameWithTheNonLocalizedPrefix_InAFreshTextObject()
    {
        var src = Adapter();

        StringAssert.Contains(src, "new TextObject(\"{=!}\" + name)");
        Assert.IsFalse(Regex.IsMatch(src, @"SetCustomName\(\s*(settlement|party)\.Name"), "SetCustomName mutates the TextObject it is given: never pass a shared one");
    }

    [TestMethod]
    public void ApplyLore_StoresTheLiteralAsGiven_AndRestoreClearsWithNull()
    {
        var src = Adapter();

        StringAssert.Contains(src, "SetCustomName(new TextObject(text))");
        Assert.IsFalse(src.Contains("english"), "the adapter writes the harvested literal as given, never rebuilds it from parts");
        Assert.IsTrue(Regex.IsMatch(src, @"SetCustomName\(null\)"), "Restore clears the custom name");
    }

    [TestMethod]
    public void Snapshot_ReadsTheRawCustomNameFromTheParty_NotTheDisplayName()
    {
        var src = Adapter();

        StringAssert.Contains(src, "Party.CustomName");
        StringAssert.Contains(src, ".Value");
    }

    [TestMethod]
    public void Snapshot_SaysARealmFiefIsOneInTheKingdomOfTheMainHero()
    {
        var src = Adapter();

        StringAssert.Contains(src, "Hero.MainHero.MapFaction");
        StringAssert.Contains(src, "IsKingdomFaction");
        StringAssert.Contains(src, "settlement.MapFaction");
    }

    [TestMethod]
    public void Snapshot_AcceptsOnlyTownsCastlesAndVillages()
    {
        var src = Adapter();

        StringAssert.Contains(src, "IsTown");
        StringAssert.Contains(src, "IsCastle");
        StringAssert.Contains(src, "IsVillage");
        StringAssert.Contains(src, "BoundVillages");
    }

    [TestMethod]
    public void MapAction_UsesTheEnginesOwnAnnexGate()
    {
        StringAssert.Contains(Adapter(), "CampaignUIHelper.GetMapScreenActionIsEnabledWithReason(out ");
    }

    [TestMethod]
    public void Ruler_IsTheMainHeroKingdomLeader()
    {
        StringAssert.Contains(Adapter(), "Hero.MainHero.IsKingdomLeader");
    }

    [TestMethod]
    public void Influence_ReadsThePlayerClan()
    {
        StringAssert.Contains(Adapter(), "Clan.PlayerClan.Influence");
    }

    [TestMethod]
    public void TheMixin_ListensForPropertyChangedWithValue_NotPropertyChanged()
    {
        // ViewModel.OnPropertyChangedWithValue<T> raises only PropertyChangedWithValue (ViewModel.cs): a PropertyChanged handler never fires.
        var src = Mixin();

        StringAssert.Contains(src, "PropertyChangedWithValue +=");
        StringAssert.Contains(src, "PropertyChangedWithValue -=");
        Assert.IsFalse(Regex.IsMatch(src, @"\.PropertyChanged\s*[+-]="), "a PropertyChanged subscription would never fire for CurrentSelectedSettlement");
        StringAssert.Contains(src, "\"CurrentSelectedSettlement\"");
    }

    [TestMethod]
    public void TheMixin_NeverRebuildsTheSettlementList()
    {
        // RefreshSettlementList clears the list and resets the selection.
        Assert.IsFalse(Mixin().Contains("RefreshSettlementList"));
    }
}
