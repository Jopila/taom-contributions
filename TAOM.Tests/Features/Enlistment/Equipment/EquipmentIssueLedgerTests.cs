using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Equipment;

namespace TAOM.Tests.Features.Enlistment.Equipment;

/// <summary>
/// Ledger semantics: once-per-rank is MONOTONIC (issuing veteran marks recruit/soldier
/// as covered too — a demotion never re-issues), and issued item ids accumulate one
/// entry per physical item instance (duplicates preserved for the later payoff Σ).
/// </summary>
[TestClass]
public class EquipmentIssueLedgerTests
{
    private InMemoryEquipmentIssueLedger _ledger = null!;

    [TestInitialize]
    public void Setup()
    {
        _ledger = new InMemoryEquipmentIssueLedger();
    }

    [TestMethod]
    public void NewLedger_NothingIssued()
    {
        Assert.IsNull(_ledger.HighestIssuedRank);
        Assert.AreEqual(0, _ledger.IssuedItemIds.Count);
        Assert.IsFalse(_ledger.HasIssuedForRank(EnlistmentKitTier.Recruit));
        Assert.IsFalse(_ledger.HasIssuedForRank(EnlistmentKitTier.Sergeant));
    }

    [TestMethod]
    public void RecordIssue_SetsHighestRankAndItems()
    {
        _ledger.RecordIssue(EnlistmentKitTier.Soldier, new[] { "item_a", "item_b" });

        Assert.AreEqual(EnlistmentKitTier.Soldier, _ledger.HighestIssuedRank);
        CollectionAssert.AreEqual(new[] { "item_a", "item_b" }, (System.Collections.ICollection)_ledger.IssuedItemIds);
    }

    [TestMethod]
    public void HasIssuedForRank_TrueForRecordedAndLower_FalseForHigher()
    {
        _ledger.RecordIssue(EnlistmentKitTier.Veteran, new[] { "item_a" });

        Assert.IsTrue(_ledger.HasIssuedForRank(EnlistmentKitTier.Recruit));
        Assert.IsTrue(_ledger.HasIssuedForRank(EnlistmentKitTier.Soldier));
        Assert.IsTrue(_ledger.HasIssuedForRank(EnlistmentKitTier.Veteran));
        Assert.IsFalse(_ledger.HasIssuedForRank(EnlistmentKitTier.Sergeant));
    }

    [TestMethod]
    public void RecordIssue_LowerRankAfterHigher_KeepsHighest()
    {
        _ledger.RecordIssue(EnlistmentKitTier.Veteran, new[] { "item_a" });
        _ledger.RecordIssue(EnlistmentKitTier.Recruit, new[] { "item_b" });

        Assert.AreEqual(EnlistmentKitTier.Veteran, _ledger.HighestIssuedRank);
    }

    [TestMethod]
    public void RecordIssue_AccumulatesDuplicateItemInstances()
    {
        // The same armor piece issued at two ranks = two physical items owed payoff.
        _ledger.RecordIssue(EnlistmentKitTier.Recruit, new[] { "item_a" });
        _ledger.RecordIssue(EnlistmentKitTier.Soldier, new[] { "item_a", "item_c" });

        CollectionAssert.AreEqual(new[] { "item_a", "item_a", "item_c" },
            (System.Collections.ICollection)_ledger.IssuedItemIds);
    }

    [TestMethod]
    public void RecordIssue_NullOrEmptyItemLists_ToleratedButRankStillRecorded()
    {
        _ledger.RecordIssue(EnlistmentKitTier.Recruit, null);
        _ledger.RecordIssue(EnlistmentKitTier.Soldier, new string[0]);

        Assert.AreEqual(EnlistmentKitTier.Soldier, _ledger.HighestIssuedRank);
        Assert.AreEqual(0, _ledger.IssuedItemIds.Count);
    }

    [TestMethod]
    public void Reset_ClearsEverything()
    {
        _ledger.RecordIssue(EnlistmentKitTier.Sergeant, new[] { "item_a" });

        _ledger.Reset();

        Assert.IsNull(_ledger.HighestIssuedRank);
        Assert.AreEqual(0, _ledger.IssuedItemIds.Count);
        Assert.IsFalse(_ledger.HasIssuedForRank(EnlistmentKitTier.Recruit));
    }
}
