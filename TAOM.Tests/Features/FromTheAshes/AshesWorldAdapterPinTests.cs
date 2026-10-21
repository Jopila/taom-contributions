using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.FromTheAshes;

/// <summary>
/// Source pins for the engine side of From the Ashes. <c>AshesWorldAdapter</c> needs a running campaign, so what matters
/// about each engine call is pinned in the comment-stripped source (docs/features/from-the-ashes.md).
/// </summary>
[TestClass]
public class AshesWorldAdapterPinTests
{
    private static string Adapter() => RepoPaths.ReadSource("Main/Adapters/AshesWorldAdapter.cs", stripComments: true);

    [TestMethod]
    public void RestoreVillage_UsesTheEnginesHealPath_NeverTheStateChangeAlone()
    {
        var src = Adapter();

        StringAssert.Contains(src, "IncreaseSettlementHealthAction.Apply(settlement, 1f);");
        Assert.IsFalse(src.Contains("ApplyBySettingToNormal"), "the state change alone leaves the health where the looting put it");
    }

    [TestMethod]
    public void AddConstruction_MirrorsTheEnginesQueueTick()
    {
        var src = Adapter();

        StringAssert.Contains(src, "head.CurrentLevel < 3");
        StringAssert.Contains(src, "BuildingHelper.CheckIfBuildingIsComplete(head);");
        StringAssert.Contains(src, "town.Settlement.IsUnderSiege");
        StringAssert.Contains(src, "queue.Peek()");
    }

    [TestMethod]
    public void TheStash_NeedsAPlayerOwnedFortification_WithAnAssignedOwner()
    {
        var src = Adapter();
        var stash = src.Substring(src.IndexOf("ItemRoster? UsableStash()", System.StringComparison.Ordinal));

        StringAssert.Contains(stash, "fortification.OwnerClan != Clan.PlayerClan");
        StringAssert.Contains(stash, "fortification.Town.IsOwnerUnassigned");
        StringAssert.Contains(stash, "settlement.Village.Bound");
    }

    [TestMethod]
    public void ThrallLabour_CountsRegularHealthyCaptivesOnly()
    {
        var src = Adapter();

        StringAssert.Contains(src, "Prisoners = HealthyCaptives(party.PrisonRoster)");
        StringAssert.Contains(src, "prisoners.TotalRegulars - prisoners.TotalWoundedRegulars");
        Assert.IsFalse(src.Contains("NumberOfPrisoners"), "that count includes captive heroes and the wounded");
    }

    [TestMethod]
    public void TheSnapshot_CarriesThePlayersSidePair_ForTheServiceToResolve()
    {
        var src = Adapter();

        StringAssert.Contains(src, "PlayerSide = FactionSides.OfHero(hero)");
        Assert.IsFalse(src.Contains("ResolveSide") || src.Contains("IAlignmentService"), "the adapter reports facts; the service resolves the side");
    }

    [TestMethod]
    public void TheNames_AreRenderedOncePerObjectAndLanguage()
    {
        var src = Adapter();

        StringAssert.Contains(src, "MBTextManager.GetActiveTextLanguageIndex()");
        StringAssert.Contains(src, "ReferenceEquals(name, _source)");
        StringAssert.Contains(src, "_settlementNames.Of(settlement.Name)");
        StringAssert.Contains(src, "_buildingNames.Of(head.Name)");
        Assert.IsFalse(Regex.IsMatch(src, @"\.Name\?\.ToString\(\)"), "a name is never re-rendered on the per-frame path");
    }

    [TestMethod]
    public void CurrentSite_GuardsAMissingCampaign()
    {
        var src = Adapter();
        var site = src.Substring(src.IndexOf("CurrentSite()", System.StringComparison.Ordinal));

        Assert.IsTrue(site.IndexOf("Campaign.Current == null", System.StringComparison.Ordinal) < site.IndexOf("Hero.MainHero", System.StringComparison.Ordinal),
            "the campaign check comes before the static hero and party reads");
    }

    [TestMethod]
    public void TheServiceAndTypes_UseSharedEmptyArrays()
    {
        var types = RepoPaths.ReadSource("Main/Features/FromTheAshes/AshesTypes.cs", stripComments: true);

        Assert.AreEqual(3, Regex.Matches(types, @"Array\.Empty<AshesMaterial>\(\)").Count);
        Assert.IsFalse(types.Contains("new AshesMaterial[0]"));
    }

    [TestMethod]
    public void TheConfigLoader_HasOneCatchAroundTheWholeLoad()
    {
        var src = RepoPaths.ReadSource("Main/Features/FromTheAshes/FromTheAshesConfigProvider.cs", stripComments: true);
        var load = src.Substring(src.IndexOf("Load()", System.StringComparison.Ordinal));
        load = load.Substring(0, load.IndexOf("private static AshesVariantConfig ReadVariant", System.StringComparison.Ordinal));

        Assert.AreEqual(1, Regex.Matches(load, @"\btry\b").Count, "the inner try/catch duplicated the outer one");
        Assert.AreEqual(1, Regex.Matches(load, @"catch \(Exception ex\)").Count);
    }
}
