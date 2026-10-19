using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FromTheAshes;

namespace TAOM.Tests.Features.FromTheAshes;

[TestClass]
public class AshesJobStoreTests
{
    private IModLogger _logger = null!;
    private AshesJobStore _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new AshesJobStore(_logger);
    }

    [TestMethod]
    public void Load_ValidRecord_RestoresTheJob()
    {
        _sut.LoadFrom("village_A1", (int)AshesJobKind.ProjectHelp, 1234.5, 20f, 7);

        Assert.IsNotNull(_sut.Current);
        Assert.AreEqual(AshesJobKind.ProjectHelp, _sut.Current!.Kind);
        Assert.AreEqual("village_A1", _sut.Current.SettlementId);
        Assert.AreEqual(1234.5, _sut.Current.StartHours);
        Assert.AreEqual(20f, _sut.Current.TotalHours);
        Assert.AreEqual(7, _sut.Current.HoursCredited);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Load_MissingKey_Empty()
    {
        // A missing SyncData key leaves every local at its default: no job, and nothing to warn about.
        _sut.Set(new AshesJob { SettlementId = "stale", TotalHours = 5f });

        _sut.LoadFrom(null, 0, 0d, 0f, 0);

        Assert.IsNull(_sut.Current, "the load replaces the previous session's job");
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(2)]
    [DataRow(-1)]
    [DataRow(99)]
    public void Load_UnknownKind_Dropped(int kind)
    {
        _sut.LoadFrom("village_A1", kind, 10d, 24f, 0);

        Assert.IsNull(_sut.Current);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("village_A1")));
    }

    [DataTestMethod]
    [DataRow(double.NaN, 24f)]
    [DataRow(double.PositiveInfinity, 24f)]
    [DataRow(double.NegativeInfinity, 24f)]
    [DataRow(10d, float.NaN)]
    [DataRow(10d, float.PositiveInfinity)]
    [DataRow(10d, 0f)]
    [DataRow(10d, -3f)]
    public void Load_NaNStart_OrBadLength_Dropped(double start, float total)
    {
        _sut.LoadFrom("village_A1", 0, start, total, 0);

        Assert.IsNull(_sut.Current);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(-1, 20f)]
    [DataRow(21, 20f)]
    [DataRow(22, 20.5f)]
    public void Load_CreditedOutOfRange_Dropped(int credited, float total)
    {
        _sut.LoadFrom("village_A1", 1, 10d, total, credited);

        Assert.IsNull(_sut.Current);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(168.5f)]
    [DataRow(1000f)]
    [DataRow(3.4e38f)]
    public void Load_LengthBeyondTheConfigCap_Dropped(float total)
    {
        _sut.LoadFrom("village_A1", 0, 10d, total, 0);

        Assert.IsNull(_sut.Current, "the config caps a job at 168 hours, so a longer record is corrupt");
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Load_LengthAtTheConfigCap_IsKept()
    {
        _sut.LoadFrom("village_A1", 0, 10d, 168f, 168);

        Assert.AreEqual(168f, _sut.Current!.TotalHours);
    }

    [TestMethod]
    public void Load_CreditedAtTheCeiling_IsKept()
    {
        _sut.LoadFrom("village_A1", 1, 10d, 20.5f, 21);

        Assert.AreEqual(21, _sut.Current!.HoursCredited, "a fractional length credits its last, partial hour");
    }

    [TestMethod]
    public void Load_EmptyIdWithAJobBody_Dropped_WithOneWarning()
    {
        _sut.LoadFrom("", 1, 10d, 20f, 3);

        Assert.IsNull(_sut.Current);
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Load_BadRecord_ClearsTheStaleJob()
    {
        _sut.Set(new AshesJob { SettlementId = "stale", TotalHours = 5f });

        _sut.LoadFrom("village_A1", 7, 10d, 24f, 0);

        Assert.IsNull(_sut.Current);
    }

    [TestMethod]
    public void SaveDoesNotMutateLive()
    {
        var job = new AshesJob { Kind = AshesJobKind.ProjectHelp, SettlementId = "town_A1", StartHours = 99.25, TotalHours = 20f, HoursCredited = 4 };
        _sut.Set(job);

        _sut.SaveInto(out var site, out var kind, out var start, out var total, out var credited);
        site = "changed";
        kind = 9;
        credited = 99;

        Assert.AreSame(job, _sut.Current);
        Assert.AreEqual("town_A1", job.SettlementId);
        Assert.AreEqual(AshesJobKind.ProjectHelp, job.Kind);
        Assert.AreEqual(4, job.HoursCredited);
        Assert.AreEqual(99.25, start);
        Assert.AreEqual(20f, total);
        Assert.AreEqual("changed", site);
    }

    [TestMethod]
    public void Save_Empty_WritesAnEmptyRecord_ThatLoadsBackEmpty()
    {
        _sut.SaveInto(out var site, out var kind, out var start, out var total, out var credited);

        Assert.AreEqual("", site);
        var again = new AshesJobStore(_logger);
        again.LoadFrom(site, kind, start, total, credited);
        Assert.IsNull(again.Current);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Save_Then_Load_RoundTrips()
    {
        _sut.Set(new AshesJob { Kind = AshesJobKind.VillageRebuild, SettlementId = "village_B2", StartHours = 5000.125, TotalHours = 24f, HoursCredited = 0 });
        _sut.SaveInto(out var site, out var kind, out var start, out var total, out var credited);

        var other = new AshesJobStore(_logger);
        other.LoadFrom(site, kind, start, total, credited);

        Assert.AreEqual("village_B2", other.Current!.SettlementId);
        Assert.AreEqual(5000.125, other.Current.StartHours);
        Assert.AreNotSame(_sut.Current, other.Current);
    }

    [TestMethod]
    public void Clear_RemovesTheJob()
    {
        _sut.Set(new AshesJob { SettlementId = "x", TotalHours = 1f });

        _sut.Clear();

        Assert.IsNull(_sut.Current);
    }
}
