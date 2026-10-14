using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.EyeOfSauron;

namespace TAOM.Tests.Features.EyeOfSauron;

/// <summary>
/// #765 M8 review E30: <c>ApplyHunterSpeedFloor</c> is the one hook member that writes an engine number. In sight it
/// raises the hunter's <c>ExplainedNumber</c> to the floor through <c>LimitMin</c>; out of sight, or for another party,
/// the number is untouched.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // constructs an ExplainedNumber
public class EyeOfSauronSpeedFloorTests
{
    private const string Party = "wk_party";

    private IHunterPartyAdapter _adapter = null!;
    private EyeOfSauronService _sut = null!;

    [TestInitialize]
    public void SetUp()
    {
        _adapter = Substitute.For<IHunterPartyAdapter>();
        _adapter.NowDays.Returns(1000d);
        _adapter.ReadParty(Arg.Any<string>()).Returns(HuntPartySnapshot.Missing);
        var settings = Substitute.For<IEyeOfSauronSettings>();
        settings.HunterSpeedMultiplier.Returns(1.3f);
        _sut = new EyeOfSauronService(_adapter, EyeOfSauronServiceTests.NewAlignment(), settings,
            Substitute.For<ICoopSessionProvider>(), Substitute.For<IDedicatedServerProvider>(), Substitute.For<IModLogger>());
        _sut.LoadFrom(new EyeOfSauronState { HunterHeroId = HuntTroopLists.WitchKingId, HunterPartyId = Party, HuntStartDay = 1000f });
    }

    private void Sight(float distance) =>
        _adapter.ReadSight(Party, Arg.Any<float>()).Returns(new HuntSightReading
        {
            Valid = true, HunterActive = true, PlayerActive = true, SeeingRange = 10f, Distance = distance, PlayerSpeed = 4f,
        });

    [TestMethod]
    public void ApplyHunterSpeedFloor_InSight_RaisesTheResultToTheFloor()
    {
        Sight(5f);
        _sut.OnTick();
        var speed = new ExplainedNumber(3f);

        _sut.ApplyHunterSpeedFloor(Party, ref speed);

        Assert.AreEqual(5.2f, speed.ResultNumber, 1e-4f);
    }

    [TestMethod]
    public void ApplyHunterSpeedFloor_OutOfSight_OrAnotherParty_LeavesTheResult()
    {
        Sight(50f);
        _sut.OnTick();
        var hunter = new ExplainedNumber(3f);
        _sut.ApplyHunterSpeedFloor(Party, ref hunter);
        Assert.AreEqual(3f, hunter.ResultNumber, 1e-4f);

        Sight(5f);
        _sut.OnTick();
        var other = new ExplainedNumber(3f);
        _sut.ApplyHunterSpeedFloor("someone_else", ref other);
        Assert.AreEqual(3f, other.ResultNumber, 1e-4f);
    }
}
