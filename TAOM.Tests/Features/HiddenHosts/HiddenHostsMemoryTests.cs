using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846 phase 3: the store of remembered counts. Plain data and whole hours, so every rule is checked at its exact hour:
/// the record rules (new, better, equal, worse, fully read), the cap, the visibility clock, the hourly fade at the three
/// Scouting levels the design names, the settlement sight poll and the pruning.
/// </summary>
[TestClass]
public class HiddenHostsMemoryTests
{
    private HiddenHostsMemory _sut = null!;

    [TestInitialize]
    public void SetUp() => _sut = new HiddenHostsMemory();

    private MemoryRow Row(string id, MemoryKind kind = MemoryKind.Party)
    {
        Assert.IsTrue(_sut.TryGet(kind, id, out var row), "no row for " + id);
        return row;
    }

    private static bool? Never(string id) => throw new AssertFailedException("no settlement row exists in this test");

    // ---- Record

    [TestMethod]
    public void Record_NewRow_StoresTheReading_AndIsFreshWhileVisible()
    {
        var row = _sut.Record(MemoryKind.Party, "p1", 150, 2, nowHour: 100, visible: true, scout: 0)!;

        Assert.AreSame(row, Row("p1"));
        Assert.AreEqual(150, row.Count);
        Assert.AreEqual(2, row.Known);
        Assert.AreEqual(100, row.StampHour);
        Assert.AreEqual(-1, row.FadeHour);
        Assert.AreEqual(0, row.XpStage);
        Assert.AreEqual(-1, row.XpDay);
    }

    [DataTestMethod]
    [DataRow(0, 106)]
    [DataRow(150, 112)]
    [DataRow(300, 118)]
    public void Record_NotVisible_StartsTheFadeClockAtTheStampPlusTheHold(int scout, int fadeHour)
    {
        var row = _sut.Record(MemoryKind.Party, "p1", 150, 3, nowHour: 100, visible: false, scout: scout)!;

        Assert.AreEqual(fadeHour, row.FadeHour);
    }

    [TestMethod]
    public void Record_ABetterReading_Overwrites()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 1, 100, true, 0);

        var row = _sut.Record(MemoryKind.Party, "p1", 160, 2, 110, true, 0)!;

        Assert.AreEqual(160, row.Count);
        Assert.AreEqual(2, row.Known);
        Assert.AreEqual(110, row.StampHour);
    }

    [TestMethod]
    public void Record_AnEqualReading_RefreshesTheCountAndTheStamp()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 2, 100, true, 0);

        var row = _sut.Record(MemoryKind.Party, "p1", 190, 2, 130, true, 0)!;

        Assert.AreEqual(190, row.Count);
        Assert.AreEqual(130, row.StampHour);
    }

    [TestMethod]
    public void Record_AWorseReading_KeepsTheRowAsItWas()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 3, 100, true, 0);

        var row = _sut.Record(MemoryKind.Party, "p1", 190, 1, 130, true, 0)!;

        Assert.AreEqual(150, row.Count);
        Assert.AreEqual(3, row.Known);
        Assert.AreEqual(100, row.StampHour);
        Assert.AreEqual(-1, row.FadeHour);
    }

    [TestMethod]
    public void Record_AWorseReadingOfACountReadInFull_Overwrites_SoAShrunkenHostIsNotRemembered()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 3, 100, true, 0);

        var row = _sut.Record(MemoryKind.Party, "p1", 5, 1, 130, true, 0)!;

        Assert.AreEqual(5, row.Count, "one digit of a one-digit count is the whole number");
        Assert.AreEqual(1, row.Known);
    }

    // ---- A live view that contradicts the memory wins (decision 1 of the deep review)

    [TestMethod]
    public void Record_AReadingOfAnotherSize_Overwrites_EvenWithOneDigit()
    {
        _sut.Record(MemoryKind.Party, "p1", 80, 2, 100, true, 0);

        var row = _sut.Record(MemoryKind.Party, "p1", 300, 1, 110, true, 0)!;

        Assert.AreEqual(300, row.Count, "a 300-man army read with one digit is not the 80 men remembered");
        Assert.AreEqual(1, row.Known);
    }

    [TestMethod]
    public void Record_ACountReadInFull_ThenALargerHostReadWithOneDigit_Overwrites()
    {
        _sut.Record(MemoryKind.Party, "p1", 123, 3, 100, true, 0);

        var row = _sut.Record(MemoryKind.Party, "p1", 1500, 1, 110, true, 0)!;

        Assert.AreEqual(1500, row.Count);
        Assert.AreEqual(1, row.Known);
    }

    [TestMethod]
    public void Record_AReadingWithADifferentLeadingDigit_Overwrites()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 3, 100, true, 0);

        var row = _sut.Record(MemoryKind.Party, "p1", 260, 1, 110, true, 0)!;

        Assert.AreEqual(260, row.Count);
    }

    [TestMethod]
    public void Record_ANoDigitReadingOfAnotherSize_EmptiesTheRow_SoTheLiveViewWins()
    {
        var first = _sut.Record(MemoryKind.Party, "p1", 123, 3, 100, true, 0)!;
        first.XpStage = 2;
        first.XpDay = 4;

        var row = _sut.Record(MemoryKind.Party, "p1", 7, 0, 110, true, 0)!;

        Assert.AreEqual(0, row.Known);
        Assert.AreEqual(2, row.XpStage, "the Scouting latch survives");
        Assert.AreEqual(4, row.XpDay);
    }

    [TestMethod]
    public void Record_ANoDigitReadingOfTheSameSize_KeepsTheRow()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 3, 100, true, 0);

        var row = _sut.Record(MemoryKind.Party, "p1", 190, 0, 110, true, 0)!;

        Assert.AreEqual(150, row.Count);
        Assert.AreEqual(3, row.Known);
        Assert.AreEqual(100, row.StampHour);
    }

    [TestMethod]
    public void Record_AReadingThatAgreesWithTheMemoryButShowsLess_KeepsTheRow()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 3, 100, true, 0);

        var row = _sut.Record(MemoryKind.Party, "p1", 150, 2, 110, true, 0)!;

        Assert.AreEqual(3, row.Known, "'~150' stays over '15?'");
    }

    [TestMethod]
    public void Record_NoDigit_AndNoRow_MakesNone()
    {
        Assert.IsNull(_sut.Record(MemoryKind.Party, "p1", 123, 0, 100, true, 0));
        Assert.AreEqual(0, _sut.Count(MemoryKind.Party));
    }

    [TestMethod]
    public void Record_Overwrite_KeepsTheXpLatch()
    {
        var first = _sut.Record(MemoryKind.Party, "p1", 150, 1, 100, true, 0)!;
        first.XpStage = 2;
        first.XpDay = 4;

        var row = _sut.Record(MemoryKind.Party, "p1", 150, 2, 110, true, 0)!;

        Assert.AreEqual(2, row.XpStage);
        Assert.AreEqual(4, row.XpDay);
    }

    [TestMethod]
    public void Record_KnownIsClampedToTheDigitsOfTheCount()
    {
        Assert.AreEqual(3, _sut.Record(MemoryKind.Party, "a", 150, 9, 1, true, 0)!.Known);
        Assert.IsNull(_sut.Record(MemoryKind.Party, "b", 150, -4, 1, true, 0), "a reading of no digit makes no new row");
    }

    [TestMethod]
    public void Record_PartiesAndSettlementsAreSeparateTables()
    {
        _sut.Record(MemoryKind.Party, "x", 10, 2, 1, true, 0);
        _sut.Record(MemoryKind.Settlement, "x", 99, 2, 1, true, 0);

        Assert.AreEqual(10, Row("x").Count);
        Assert.AreEqual(99, Row("x", MemoryKind.Settlement).Count);
        Assert.AreEqual(1, _sut.Count(MemoryKind.Party));
        Assert.AreEqual(1, _sut.Count(MemoryKind.Settlement));
    }

    [TestMethod]
    public void TryGet_NoRow_IsFalse() => Assert.IsFalse(_sut.TryGet(MemoryKind.Party, "nobody", out _));

    // ---- The cap

    [TestMethod]
    public void Record_AtTheCap_EvictsTheStaleRowWithTheSmallestStamp()
    {
        for (int i = 0; i < HiddenHostsMemory.MaxParties; i++)
            _sut.Record(MemoryKind.Party, "p" + i, 100, 3, nowHour: 1000 + i, visible: i != 7 && i != 9, scout: 0);

        var added = _sut.Record(MemoryKind.Party, "new", 100, 3, 5000, true, 0);

        Assert.IsNotNull(added);
        Assert.AreEqual(HiddenHostsMemory.MaxParties, _sut.Count(MemoryKind.Party));
        Assert.IsFalse(_sut.TryGet(MemoryKind.Party, "p7", out _), "p7 is stale and the oldest stale row");
        Assert.IsTrue(_sut.TryGet(MemoryKind.Party, "p9", out _));
        Assert.IsTrue(_sut.TryGet(MemoryKind.Party, "new", out _));
    }

    [TestMethod]
    public void Record_AtTheCapWithEveryRowFresh_SkipsTheInsert_AndCountsIt()
    {
        for (int i = 0; i < HiddenHostsMemory.MaxParties; i++)
            _sut.Record(MemoryKind.Party, "p" + i, 100, 3, 1000 + i, true, 0);

        var added = _sut.Record(MemoryKind.Party, "new", 100, 3, 5000, true, 0);

        Assert.IsNull(added);
        Assert.AreEqual(HiddenHostsMemory.MaxParties, _sut.Count(MemoryKind.Party));
        Assert.IsFalse(_sut.TryGet(MemoryKind.Party, "new", out _));
        Assert.AreEqual(1, _sut.SkippedInserts);
    }

    [TestMethod]
    public void Record_AtTheCap_AnExistingIdIsOverwrittenWithoutEvicting()
    {
        for (int i = 0; i < HiddenHostsMemory.MaxParties; i++)
            _sut.Record(MemoryKind.Party, "p" + i, 100, 1, 1000 + i, false, 0);

        _sut.Record(MemoryKind.Party, "p3", 100, 2, 6000, false, 0);

        Assert.AreEqual(HiddenHostsMemory.MaxParties, _sut.Count(MemoryKind.Party));
        Assert.AreEqual(6000, Row("p3").StampHour);
        Assert.IsTrue(_sut.TryGet(MemoryKind.Party, "p0", out _), "no eviction for an overwrite");
    }

    [TestMethod]
    public void Record_TheSettlementTableHasItsOwnSmallerCap()
    {
        for (int i = 0; i < HiddenHostsMemory.MaxSettlements; i++)
            _sut.Record(MemoryKind.Settlement, "s" + i, 100, 3, 1000 + i, true, 0);

        Assert.IsNull(_sut.Record(MemoryKind.Settlement, "extra", 100, 3, 9000, true, 0));
        Assert.IsNotNull(_sut.Record(MemoryKind.Party, "p", 100, 3, 9000, true, 0), "the party table is unaffected");
    }

    // ---- Put

    [TestMethod]
    public void Put_ADuplicateId_KeepsTheLast()
    {
        _sut.Put(MemoryKind.Party, "p1", new MemoryRow { Count = 10, Known = 1 });
        _sut.Put(MemoryKind.Party, "p1", new MemoryRow { Count = 20, Known = 2 });

        Assert.AreEqual(20, Row("p1").Count);
        Assert.AreEqual(1, _sut.Count(MemoryKind.Party));
    }

    [TestMethod]
    public void Put_AtTheCap_RefusesANewId_ButTakesAnExistingOne()
    {
        for (int i = 0; i < HiddenHostsMemory.MaxParties; i++)
            Assert.IsTrue(_sut.Put(MemoryKind.Party, "p" + i, new MemoryRow { Count = 1, Known = 1 }));

        Assert.IsFalse(_sut.Put(MemoryKind.Party, "extra", new MemoryRow { Count = 1, Known = 1 }));
        Assert.IsTrue(_sut.Put(MemoryKind.Party, "p5", new MemoryRow { Count = 2, Known = 1 }));
    }

    // ---- Visibility

    [TestMethod]
    public void SetVisible_Gone_StartsTheFadeClockAtTheStampPlusTheHold()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 2, 100, true, 0);

        _sut.SetVisible("p1", false, 150);

        Assert.AreEqual(112, Row("p1").FadeHour, "stamp 100 + hold 12: information ages from when it was counted");
    }

    [TestMethod]
    public void SetVisible_Gone_LongAfterTheStamp_LeavesTheClockInThePast()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 2, 100, true, 0);

        _sut.SetVisible("p1", false, 0);

        Assert.AreEqual(106, Row("p1").FadeHour);
    }

    [TestMethod]
    public void SetVisible_Back_MakesTheRowFreshAgain()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 2, 100, false, 0);

        _sut.SetVisible("p1", true, 0);

        Assert.AreEqual(-1, Row("p1").FadeHour);
    }

    [TestMethod]
    public void SetVisible_NoRow_DoesNothing_AndASettlementIdIsNotAParty()
    {
        _sut.Record(MemoryKind.Settlement, "s1", 150, 2, 100, true, 0);

        _sut.SetVisible("s1", false, 0);
        _sut.SetVisible("ghost", false, 0);

        Assert.AreEqual(-1, Row("s1", MemoryKind.Settlement).FadeHour);
        Assert.AreEqual(0, _sut.Count(MemoryKind.Party));
    }

    // ---- The hourly fade

    [TestMethod]
    public void FadeHourly_BeforeTheFadeHour_ChangesNothing()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 3, 100, false, 0);

        _sut.FadeHourly(105, 0, Never);

        Assert.AreEqual(3, Row("p1").Known);
        Assert.AreEqual(106, Row("p1").FadeHour);
    }

    [DataTestMethod]
    [DataRow(0, 106, 114)]
    [DataRow(150, 112, 124)]
    [DataRow(300, 118, 134)]
    public void FadeHourly_AtTheFadeHour_LosesOneDigit_AndSchedulesTheNextAtTheStep(int scout, int fadeAt, int next)
    {
        var row = _sut.Record(MemoryKind.Party, "p1", 150, 3, 100, false, scout)!;
        Assert.AreEqual(fadeAt, row.FadeHour, "stamp 100 plus the hold");

        _sut.FadeHourly(fadeAt - 1, scout, Never);
        Assert.AreEqual(3, row.Known, "one hour early");
        _sut.FadeHourly(fadeAt, scout, Never);

        Assert.AreEqual(2, row.Known);
        Assert.AreEqual(next, row.FadeHour, "the step is 8, 12 and 16 hours");
    }

    [TestMethod]
    public void FadeHourly_ADigitEveryStepHours_AtScoutZero_UntilNothingIsLeft()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 3, 100, false, 0);
        var seen = new List<(int Hour, int Known)>();

        for (int hour = 100; hour <= 130; hour++)
        {
            _sut.FadeHourly(hour, 0, Never);
            if (_sut.TryGet(MemoryKind.Party, "p1", out var row)) seen.Add((hour, row.Known));
        }

        Assert.AreEqual(3, seen.First(s => s.Hour == 105).Known);
        Assert.AreEqual(2, seen.First(s => s.Hour == 106).Known, "stamp 100 + hold 6");
        Assert.AreEqual(2, seen.First(s => s.Hour == 113).Known);
        Assert.AreEqual(1, seen.First(s => s.Hour == 114).Known, "106 + step 8");
        Assert.AreEqual(1, seen.First(s => s.Hour == 121).Known);
        Assert.IsFalse(_sut.TryGet(MemoryKind.Party, "p1", out _), "122 loses the last digit and the empty row is dropped");
        Assert.IsFalse(seen.Any(s => s.Hour >= 122));
    }

    [TestMethod]
    public void FadeHourly_APastDueClock_LosesOnlyOneDigitPerTick()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 3, 100, true, 0);
        _sut.SetVisible("p1", false, 0);

        _sut.FadeHourly(500, 0, Never);

        Assert.AreEqual(2, Row("p1").Known);
        Assert.AreEqual(508, Row("p1").FadeHour);
    }

    [TestMethod]
    public void FadeHourly_AFreshRow_NeverFades()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 3, 100, true, 0);

        _sut.FadeHourly(9999, 0, Never);

        Assert.AreEqual(3, Row("p1").Known);
    }

    [TestMethod]
    public void FadeHourly_ARowWithNothingKnown_IsDropped_UnlessItsXpLatchIsToday()
    {
        _sut.Put(MemoryKind.Party, "latch", new MemoryRow { Count = 150, Known = 0, XpDay = 5, XpStage = 1 });
        _sut.Put(MemoryKind.Party, "gone", new MemoryRow { Count = 150, Known = 0, XpDay = 4 });
        _sut.Put(MemoryKind.Party, "never", new MemoryRow { Count = 150, Known = 0, XpDay = -1 });

        _sut.FadeHourly(5 * 24 + 3, 0, Never);

        Assert.IsTrue(_sut.TryGet(MemoryKind.Party, "latch", out _), "its XP latch counts for today and must survive");
        Assert.IsFalse(_sut.TryGet(MemoryKind.Party, "gone", out _));
        Assert.IsFalse(_sut.TryGet(MemoryKind.Party, "never", out _));
    }

    [TestMethod]
    public void FadeHourly_TheLastDigitOfARowWithATodayLatch_LeavesAnEmptyRowThatIsDroppedTomorrow()
    {
        var row = _sut.Record(MemoryKind.Party, "p1", 5, 1, 24, false, 0)!;
        row.XpDay = 1;

        _sut.FadeHourly(30, 0, Never);
        Assert.AreEqual(0, Row("p1").Known);
        _sut.FadeHourly(47, 0, Never);
        Assert.IsTrue(_sut.TryGet(MemoryKind.Party, "p1", out _), "still day 1");
        _sut.FadeHourly(48, 0, Never);

        Assert.IsFalse(_sut.TryGet(MemoryKind.Party, "p1", out _), "day 2");
    }

    // ---- Settlements: the sight poll

    [TestMethod]
    public void FadeHourly_ASettlementNoLongerInTheCampaign_IsRemoved()
    {
        _sut.Record(MemoryKind.Settlement, "s1", 300, 3, 100, true, 0);

        _sut.FadeHourly(101, 0, id => null);

        Assert.AreEqual(0, _sut.Count(MemoryKind.Settlement));
    }

    [TestMethod]
    public void FadeHourly_ASettlementInSight_IsFresh_EvenWhenItWasStale()
    {
        _sut.Record(MemoryKind.Settlement, "s1", 300, 3, 100, false, 0);

        _sut.FadeHourly(200, 0, id => true);

        Assert.AreEqual(3, Row("s1", MemoryKind.Settlement).Known, "in sight: it does not fade");
        Assert.AreEqual(-1, Row("s1", MemoryKind.Settlement).FadeHour);
    }

    [TestMethod]
    public void FadeHourly_ASettlementOutOfSight_StartsItsClockAtTheStampPlusTheHold_ThenFades()
    {
        _sut.Record(MemoryKind.Settlement, "s1", 300, 3, 100, true, 0);

        _sut.FadeHourly(103, 0, id => false);
        Assert.AreEqual(106, Row("s1", MemoryKind.Settlement).FadeHour);
        Assert.AreEqual(3, Row("s1", MemoryKind.Settlement).Known);

        _sut.FadeHourly(106, 0, id => false);
        Assert.AreEqual(2, Row("s1", MemoryKind.Settlement).Known);
        Assert.AreEqual(114, Row("s1", MemoryKind.Settlement).FadeHour);
    }

    [TestMethod]
    public void FadeHourly_ASettlementAlreadyFading_KeepsItsClock()
    {
        _sut.Record(MemoryKind.Settlement, "s1", 300, 3, 100, false, 0);

        _sut.FadeHourly(103, 0, id => false);

        Assert.AreEqual(106, Row("s1", MemoryKind.Settlement).FadeHour);
    }

    [TestMethod]
    public void FadeHourly_APartyRowIsNeverAskedAboutSettlementSight()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 3, 100, false, 0);

        _sut.FadeHourly(103, 0, Never);

        Assert.AreEqual(3, Row("p1").Known);
    }

    // ---- Prune, Remove, Clear, Rows

    [TestMethod]
    public void Prune_DropsEmptyRowsWithoutATodayLatch_OfBothTables()
    {
        _sut.Put(MemoryKind.Party, "a", new MemoryRow { Count = 150, Known = 0 });
        _sut.Put(MemoryKind.Settlement, "b", new MemoryRow { Count = 150, Known = 0 });
        _sut.Put(MemoryKind.Party, "c", new MemoryRow { Count = 150, Known = 0, XpDay = 7 });
        _sut.Record(MemoryKind.Party, "d", 150, 1, 1, true, 0);

        _sut.Prune(7);

        CollectionAssert.AreEqual(new[] { "c", "d" }, _sut.Rows(MemoryKind.Party).Select(r => r.Key).OrderBy(x => x).ToArray());
        Assert.AreEqual(0, _sut.Count(MemoryKind.Settlement));
    }

    [TestMethod]
    public void Remove_DropsOneRow_AndSaysWhetherItExisted()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 2, 1, true, 0);

        Assert.IsTrue(_sut.Remove(MemoryKind.Party, "p1"));
        Assert.IsFalse(_sut.Remove(MemoryKind.Party, "p1"));
    }

    [TestMethod]
    public void Clear_EmptiesBothTables_AndTheSkippedCount()
    {
        _sut.Record(MemoryKind.Party, "p1", 150, 2, 1, true, 0);
        _sut.Record(MemoryKind.Settlement, "s1", 150, 2, 1, true, 0);

        _sut.Clear();

        Assert.AreEqual(0, _sut.Count(MemoryKind.Party));
        Assert.AreEqual(0, _sut.Count(MemoryKind.Settlement));
        Assert.AreEqual(0, _sut.SkippedInserts);
    }
}
