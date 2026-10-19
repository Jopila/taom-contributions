using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.ShadowfaxScreens;
using TAOM.Features.ShadowfaxScreens.Party;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ShadowfaxScreens.Party;

[TestClass]
public class PartyScreenFastPathServiceTests
{
    private IShadowfaxScreensSettingsProvider _settings = null!;
    private FakeStampClock _clock = null!;
    private PartyScreenFastPathService _sut = null!;

    private sealed class EqualsAll
    {
        public override bool Equals(object? obj) => obj is EqualsAll;

        public override int GetHashCode() => 1;
    }

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IShadowfaxScreensSettingsProvider>();
        _settings.PartyFastOpen.Returns(true);
        _clock = new FakeStampClock();
        _sut = new PartyScreenFastPathService(_settings, _clock);
    }

    [TestMethod]
    public void NoSession_EveryLookupMisses_AndStoreIsIgnored()
    {
        var ch = new object();

        _sut.StoreCode(ch, 0, 1, new object());

        Assert.IsFalse(_sut.TryGetCode(ch, 0, 1, out _));
        _sut.BeginScreen();
        Assert.IsFalse(_sut.TryGetCode(ch, 0, 1, out _));
    }

    [TestMethod]
    public void StoreThenGet_ReturnsTheSameReference()
    {
        var ch = new object();
        var code = new object();
        _sut.BeginScreen();

        _sut.StoreCode(ch, 0, 1, code);

        Assert.IsTrue(_sut.TryGetCode(ch, 0, 1, out var got));
        Assert.AreSame(code, got);
    }

    [TestMethod]
    public void DifferentTypeSideOrCharacter_Misses()
    {
        var ch = new object();
        _sut.BeginScreen();
        _sut.StoreCode(ch, 0, 1, new object());

        Assert.IsFalse(_sut.TryGetCode(ch, 1, 1, out _));
        Assert.IsFalse(_sut.TryGetCode(ch, 0, 0, out _));
        Assert.IsFalse(_sut.TryGetCode(new object(), 0, 1, out _));
    }

    [TestMethod]
    public void EqualsEqualButDistinctCharacters_DoNotCollide()
    {
        var a = new EqualsAll();
        var b = new EqualsAll();
        _sut.BeginScreen();
        _sut.StoreCode(a, 0, 0, new object());

        Assert.IsTrue(_sut.TryGetCode(a, 0, 0, out _));
        Assert.IsFalse(_sut.TryGetCode(b, 0, 0, out _));
    }

    [TestMethod]
    public void BeginScreen_ClearsThePreviousSession()
    {
        var ch = new object();
        _sut.BeginScreen();
        _sut.StoreCode(ch, 0, 0, new object());

        _sut.BeginScreen();

        Assert.IsFalse(_sut.TryGetCode(ch, 0, 0, out _));
    }

    [TestMethod]
    public void EndScreen_ClearsAndIsIdempotent()
    {
        var ch = new object();
        _sut.BeginScreen();
        _sut.StoreCode(ch, 0, 0, new object());

        _sut.EndScreen();
        _sut.EndScreen();

        Assert.IsFalse(_sut.TryGetCode(ch, 0, 0, out _));
        _sut.StoreCode(ch, 0, 0, new object());
        Assert.IsFalse(_sut.TryGetCode(ch, 0, 0, out _));
    }

    [TestMethod]
    public void ToggleOffAtBegin_Misses_ButBeginAndEndStillTransition()
    {
        _settings.PartyFastOpen.Returns(false);
        var ch = new object();
        _sut.BeginScreen();

        _sut.StoreCode(ch, 0, 0, new object());
        Assert.IsFalse(_sut.TryGetCode(ch, 0, 0, out _));
        Assert.IsTrue(_sut.SessionOpen);
        Assert.IsFalse(_sut.Enabled);

        _sut.EndScreen();
        Assert.IsFalse(_sut.SessionOpen);
    }

    [TestMethod]
    public void ToggleFlippedMidSession_DoesNotChangeTheSession()
    {
        var ch = new object();
        _sut.BeginScreen();
        _settings.PartyFastOpen.Returns(false);

        _sut.StoreCode(ch, 0, 0, new object());

        Assert.IsTrue(_sut.TryGetCode(ch, 0, 0, out _));
    }

    [TestMethod]
    public void NullCode_IsNeverStored()
    {
        var ch = new object();
        _sut.BeginScreen();

        _sut.StoreCode(ch, 0, 0, null);

        Assert.IsFalse(_sut.TryGetCode(ch, 0, 0, out _));
    }

    [TestMethod]
    public void Count_OutsideAWindow_Misses_AndStoreIsIgnored()
    {
        var roster = new object();
        var cat = new object();
        _sut.BeginScreen();

        _sut.StoreCount(roster, cat, 5);

        Assert.IsFalse(_sut.TryGetCount(roster, cat, out _));
    }

    [TestMethod]
    public void Count_InsideAWindow_StoresAndGets_AndCloseClears()
    {
        var roster = new object();
        var cat = new object();
        _sut.BeginScreen();

        var opened = _sut.OpenCategoryWindow();
        _sut.StoreCount(roster, cat, 7);

        Assert.IsTrue(opened);
        Assert.IsTrue(_sut.TryGetCount(roster, cat, out var n));
        Assert.AreEqual(7, n);

        _sut.CloseCategoryWindow(opened);
        Assert.IsFalse(_sut.TryGetCount(roster, cat, out _));
        _sut.OpenCategoryWindow();
        Assert.IsFalse(_sut.TryGetCount(roster, cat, out _));
    }

    [TestMethod]
    public void Count_NullCategory_Works()
    {
        var roster = new object();
        _sut.BeginScreen();
        _sut.OpenCategoryWindow();

        _sut.StoreCount(roster, null, 3);

        Assert.IsTrue(_sut.TryGetCount(roster, null, out var n));
        Assert.AreEqual(3, n);
        Assert.IsFalse(_sut.TryGetCount(roster, new object(), out _));
    }

    [TestMethod]
    public void Count_RostersAreSeparate()
    {
        var cat = new object();
        var a = new EqualsAll();
        var b = new EqualsAll();
        _sut.BeginScreen();
        _sut.OpenCategoryWindow();

        _sut.StoreCount(a, cat, 1);
        _sut.StoreCount(b, cat, 2);

        Assert.IsTrue(_sut.TryGetCount(a, cat, out var na));
        Assert.IsTrue(_sut.TryGetCount(b, cat, out var nb));
        Assert.AreEqual(1, na);
        Assert.AreEqual(2, nb);
    }

    [TestMethod]
    public void NestedOpen_ReturnsFalse_AndInnerCloseKeepsTheOuterWindow()
    {
        var roster = new object();
        var cat = new object();
        _sut.BeginScreen();

        var outer = _sut.OpenCategoryWindow();
        _sut.StoreCount(roster, cat, 4);
        var inner = _sut.OpenCategoryWindow();
        _sut.CloseCategoryWindow(inner);

        Assert.IsTrue(outer);
        Assert.IsFalse(inner);
        Assert.IsTrue(_sut.TryGetCount(roster, cat, out _));

        _sut.CloseCategoryWindow(outer);
        Assert.IsFalse(_sut.TryGetCount(roster, cat, out _));
    }

    [TestMethod]
    public void NoWindowWithoutAnEnabledSession()
    {
        var roster = new object();
        Assert.IsFalse(_sut.OpenCategoryWindow());
        _sut.StoreCount(roster, null, 1);
        Assert.IsFalse(_sut.TryGetCount(roster, null, out _));

        _settings.PartyFastOpen.Returns(false);
        _sut.BeginScreen();
        Assert.IsFalse(_sut.OpenCategoryWindow());
        _sut.StoreCount(roster, null, 1);
        Assert.IsFalse(_sut.TryGetCount(roster, null, out _));
    }

    [TestMethod]
    public void EndScreen_ClosesAnOpenWindow()
    {
        var roster = new object();
        _sut.BeginScreen();
        _sut.OpenCategoryWindow();
        _sut.StoreCount(roster, null, 1);

        _sut.EndScreen();
        _sut.BeginScreen();

        Assert.IsFalse(_sut.TryGetCount(roster, null, out _));
        Assert.IsTrue(_sut.OpenCategoryWindow());
    }

    [TestMethod]
    public void Detail_ReportsCountersAndPhases()
    {
        _settings.LogScreenOpenTimes.Returns(true);
        var ch = new object();
        var roster = new object();
        _sut.BeginScreen();
        _sut.TryGetCode(ch, 0, 0, out _);
        _sut.StoreCode(ch, 0, 0, new object());
        _sut.TryGetCode(ch, 0, 0, out _);
        _sut.OpenCategoryWindow();
        _sut.TryGetCount(roster, null, out _);
        _sut.StoreCount(roster, null, 2);
        _sut.TryGetCount(roster, null, out _);
        _sut.TryGetCount(roster, null, out _);
        _clock.Now = 100;
        var start = _sut.BeginPhase();
        _clock.Advance(25);
        _sut.EndPhase(PartyPhase.InitializeTroopLists, start);

        var detail = _sut.BuildOpenDetail();

        Assert.AreEqual("rows 2, codes 1 hit/1 miss, counts 2 hit/1 miss, init 25.0 ms, refresh 0.0 ms", detail);
    }

    [TestMethod]
    public void Phase_IgnoredWithoutASession_OrAfterTheOpenWasReported()
    {
        _settings.LogScreenOpenTimes.Returns(true);
        Assert.AreEqual(0, _sut.BeginPhase());

        _sut.BeginScreen();
        _clock.Now = 50;
        var start = _sut.BeginPhase();
        _clock.Advance(10);
        _sut.EndPhase(PartyPhase.RefreshValues, start);
        StringAssert.Contains(_sut.BuildOpenDetail(), "refresh 10.0 ms");

        Assert.AreEqual(0, _sut.BeginPhase());
    }
}
