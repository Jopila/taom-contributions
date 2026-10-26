using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.KeepWardens;

/// <summary>
/// The castle twin of vanilla's town Guard House upgrade refresh (#888 follow-up). Vanilla's
/// <c>PatrolPartiesCampaignBehavior.OnBuildingLevelChanged</c> bumps the patrol roster version only for the town Guard House, so a
/// castle patrol kept a limit cached on <c>MemberRoster.VersionNo</c>. The handler needs a live campaign, so it is a source pin plus
/// a binding check against the installed engine.
/// </summary>
[TestClass]
public class CastleGuardHouseUpgradeBehaviorTests
{
    private static string Source() =>
        RepoPaths.ReadSource("Main/Features/KeepWardens/CastleGuardHouseUpgradeBehavior.cs", stripComments: true);

    [TestMethod]
    public void TheListener_IsNonSerialized_OnTheBuildingLevelChangedEvent()
    {
        StringAssert.Contains(Source(), "CampaignEvents.OnBuildingLevelChangedEvent.AddNonSerializedListener(this, OnBuildingLevelChanged)");
    }

    [TestMethod]
    public void TheHandler_ChecksTheCastleGuardHouse_ThenBumpsThePatrolRosterVersion_NullSafe()
    {
        var src = Source();
        var check = src.IndexOf("building.BuildingType == DefaultBuildingTypes.CastleGuardHouse", System.StringComparison.Ordinal);
        var bump = src.IndexOf("town.Settlement.PatrolParty?.Party.MemberRoster.UpdateVersion()", System.StringComparison.Ordinal);

        Assert.IsTrue(check >= 0 && bump > check, "the building check must come before the version bump, and the patrol may be absent");
        Assert.IsFalse(src.Contains("SettlementGuardHouse"), "the town Guard House is vanilla's own branch");
    }

    [TestMethod]
    public void TheHandler_IsGuarded_AndLogsOnce_AndTheBehaviorSavesNothing()
    {
        var src = Source();

        StringAssert.Contains(src, "catch (Exception");
        Assert.AreEqual(1, Regex.Matches(src, @"_logger\.LogError").Count, "one logged error");
        Assert.IsFalse(Regex.IsMatch(src, @"IsEnabled|Settings|Enabled"), "no settings gate: the bump only invalidates a cache");
        Assert.IsFalse(src.Contains("dataStore.SyncData"), "no save data");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheEngine_StillHasTheEvent_AndTheMembersTheHandlerUses()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var events = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignEvents");
        var evt = AccessTools.PropertyGetter(events, "OnBuildingLevelChangedEvent");
        Assert.IsNotNull(evt, "CampaignEvents.OnBuildingLevelChangedEvent is gone; the castle patrol limit would stay stale.");
        var args = evt.ReturnType.GetGenericArguments();
        Assert.AreEqual(3, args.Length, "the event no longer carries three arguments");
        CollectionAssert.AreEqual(new[] { "Town", "Building", "Int32" }, new[] { args[0].Name, args[1].Name, args[2].Name });

        var types = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Settlements.Buildings.DefaultBuildingTypes");
        Assert.IsNotNull(AccessTools.PropertyGetter(types, "CastleGuardHouse"), "DefaultBuildingTypes.CastleGuardHouse is gone");
        var roster = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Roster.TroopRoster");
        Assert.IsNotNull(AccessTools.Method(roster, "UpdateVersion"), "TroopRoster.UpdateVersion is gone");
        var settlement = AccessTools.TypeByName("TaleWorlds.CampaignSystem.Settlements.Settlement");
        Assert.IsNotNull(AccessTools.PropertyGetter(settlement, "PatrolParty"), "Settlement.PatrolParty is gone");
    }
}
