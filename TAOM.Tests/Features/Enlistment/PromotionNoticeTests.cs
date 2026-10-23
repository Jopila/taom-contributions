using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Equipment;
using TAOM.Features.Enlistment.Presentation;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>#835: the promotion notice and the "a kit waits" rule.</summary>
[TestClass]
public class PromotionNoticeTests
{
    // ---- a kit waits: the quartermaster's own ledger has not covered the rank's kit tier ----

    [DataTestMethod]
    [DataRow(ServiceRank.Private, null, true, DisplayName = "nothing drawn yet: the first kit waits")]
    [DataRow(ServiceRank.Private, 0, false, DisplayName = "private kit drawn")]
    [DataRow(ServiceRank.Specialist, 0, true, DisplayName = "promoted past the drawn tier")]
    [DataRow(ServiceRank.Specialist, 1, false, DisplayName = "specialist kit drawn")]
    [DataRow(ServiceRank.Sergeant, 1, true, DisplayName = "sergeant above specialist tier")]
    [DataRow(ServiceRank.StaffSergeant, 2, true, DisplayName = "staff sergeant: tier 3 above 2")]
    [DataRow(ServiceRank.StaffSergeant, 3, false, DisplayName = "top tier drawn")]
    [DataRow(ServiceRank.SergeantFirstClass, 3, false, DisplayName = "inside the top tier promises no kit")]
    [DataRow(ServiceRank.MasterSergeant, 3, false, DisplayName = "inside the top tier promises no kit (4)")]
    [DataRow(ServiceRank.SergeantMajor, 3, false, DisplayName = "inside the top tier promises no kit (5)")]
    [DataRow(ServiceRank.SergeantMajor, 2, true, DisplayName = "top rank, only tier 2 drawn")]
    [DataRow(ServiceRank.Private, 3, false, DisplayName = "a higher tier drawn earlier never re-promises")]
    [DataRow(ServiceRank.Private, 99, true, DisplayName = "an undefined stored tier counts as nothing drawn")]
    public void KitWaits_AsksTheQuartermastersLedger(ServiceRank rank, int? drawn, bool expected)
    {
        var content = new EnlistmentContentStore(Substitute.For<IModLogger>());
        content.Record.HighestIssuedKitTier = drawn;
        var ledger = new PersistedEquipmentIssueLedger(content);

        Assert.AreEqual(expected, EnlistmentKitTiers.KitWaits(rank, ledger));
    }

    // ---- the portrait notice needs a commander who can still hold a command ----

    [DataTestMethod]
    [DataRow(true, true, false, true, DisplayName = "alive, free: portrait notice")]
    [DataRow(false, false, false, false, DisplayName = "missing: neutral text")]
    [DataRow(true, false, false, false, DisplayName = "dead: neutral text")]
    [DataRow(true, true, true, false, DisplayName = "prisoner: neutral text")]
    public void UsePortraitNotice_OnlyForAnAliveFreeCommander(bool exists, bool alive, bool prisoner, bool expected)
    {
        var snapshot = new CommanderSnapshot(exists: exists, isAlive: alive, isPrisoner: prisoner, name: "Boromir");

        Assert.AreEqual(expected, ServiceDailyAnnouncer.UsePortraitNotice(snapshot));
    }

    [TestMethod]
    public void UsePortraitNotice_NullSnapshot_False() => Assert.IsFalse(ServiceDailyAnnouncer.UsePortraitNotice(null));

    // ---- source shapes (the announcer and the board writer touch engine text) ----

    [TestMethod]
    public void TheAnnouncer_UsesTheNewKeys_AndFallsBackToTheOldMessage()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Presentation/ServiceDailyAnnouncer.cs", stripComments: true);
        StringAssert.Contains(src, "{=taom_enlist_promoted_notice}");
        StringAssert.Contains(src, "{=taom_enlist_promoted_kit}");
        StringAssert.Contains(src, "{=taom_enlist_promoted}");
        StringAssert.Contains(src, "EnlistmentKitTiers.KitWaits");
        Assert.IsFalse(src.Contains("PromotionNoticeGate"), "the gate is gone: every promotion is exactly one rank up");
    }

    [TestMethod]
    public void TheBoardWriter_RendersTheKitLineWithItsOwnNewline()
    {
        var src = RepoPaths.ReadSource("Main/Features/Enlistment/Presentation/ServiceStatusTextWriter.cs", stripComments: true);
        StringAssert.Contains(src, "{=taom_enlist_board_kit}{NEWLINE}Kit to draw: the quartermaster holds gear for your rank.");
        StringAssert.Contains(src, "{=!}{DUTY_LINE}{KIT_LINE}");
        StringAssert.Contains(src, "KitWaits");
    }

    [TestMethod]
    public void TheQuickNoticeAdapter_FailsSafe_WithThreeSecondsExtra()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/QuickNoticeAdapter.cs", stripComments: true);
        StringAssert.Contains(src, "AddQuickInformation");
        StringAssert.Contains(src, "3000");
        StringAssert.Contains(src, "catch");
    }
}
