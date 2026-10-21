using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
using TAOM.Features.FromTheAshes;
using TAOM.Features.FromTheAshes.Hooks;

namespace TAOM.Tests.Features.FromTheAshes;

[TestClass]
public class FromTheAshesServiceTests
{
    private const string Village = "village_A1";
    private const string Town = "town_A1";

    private IFromTheAshesConfigProvider _config = null!;
    private IFromTheAshesSettingsProvider _settings = null!;
    private IAshesWorldAdapter _world = null!;
    private IEnlistmentStateQuery _enlistment = null!;
    private ICoopSessionProvider _coop = null!;
    private IDedicatedServerProvider _dedicated = null!;
    private IAlignmentService _alignment = null!;
    private IHeroSkillXpAdapter _xp = null!;
    private IModLogger _logger = null!;
    private AshesJobStore _store = null!;
    private FromTheAshesService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = Substitute.For<IFromTheAshesConfigProvider>();
        _config.Rebuild.Returns(FromTheAshesConfigProvider.DefaultRebuild());
        _config.Thralls.Returns(FromTheAshesConfigProvider.DefaultThralls());
        _config.Project.Returns(FromTheAshesConfigProvider.DefaultProject());
        _settings = Substitute.For<IFromTheAshesSettingsProvider>();
        _settings.Enabled.Returns(true);
        _world = Substitute.For<IAshesWorldAdapter>();
        _world.CountMaterial(Arg.Any<string>()).Returns(1000);
        _world.TakeMaterial(Arg.Any<string>(), Arg.Any<int>()).Returns(true);
        _world.RestoreVillage(Arg.Any<string>()).Returns(true);
        _world.AddConstruction(Arg.Any<string>(), Arg.Any<float>()).Returns(true);
        _world.PartyHeroIds().Returns(new[] { "hero_main", "hero_companion" });
        _enlistment = Substitute.For<IEnlistmentStateQuery>();
        _coop = Substitute.For<ICoopSessionProvider>();
        _coop.IsAuthority.Returns(true);
        _dedicated = Substitute.For<IDedicatedServerProvider>();
        _alignment = Substitute.For<IAlignmentService>();
        _alignment.ResolveSide(Arg.Any<string>(), Arg.Any<string>()).Returns(FactionSide.Free);
        _xp = Substitute.For<IHeroSkillXpAdapter>();
        _logger = Substitute.For<IModLogger>();
        _store = new AshesJobStore(_logger);
        _sut = new FromTheAshesService(_config, _settings, _world, _enlistment, _coop, _dedicated, _alignment, _xp, _store, _logger);
    }

    private static AshesSiteSnapshot LootedVillage() => new()
    {
        SettlementId = Village,
        SettlementName = "Ashford",
        IsVillage = true,
        IsLooted = true,
        PlayerClanOwns = false,
        SameMapFactionAsPlayer = true,
        HealthyTroops = 50,
        Prisoners = 0,
    };

    private static AshesSiteSnapshot Fief() => new()
    {
        SettlementId = Town,
        SettlementName = "Minas Test",
        IsFortification = true,
        PlayerClanOwns = true,
        SameMapFactionAsPlayer = true,
        HasProjectInProgress = true,
        ProjectName = "Granary",
        HealthyTroops = 80,
    };

    /// <summary>Makes the alignment service resolve the snapshot's kingdom and culture pair to <paramref name="side"/>.</summary>
    private void Side(AshesSiteSnapshot site, FactionSide side)
    {
        site.PlayerSide = new SideIds("kingdom_" + side, "culture_" + side);
        _alignment.ResolveSide(site.PlayerSide.FactionId, site.PlayerSide.CultureId).Returns(side);
    }

    private static AshesJob RebuildJob(double start = 100d, float total = 24f, int credited = 0) =>
        new() { Kind = AshesJobKind.VillageRebuild, SettlementId = Village, StartHours = start, TotalHours = total, HoursCredited = credited };

    private static AshesJob ProjectJob(double start = 100d, float total = 20f, int credited = 0) =>
        new() { Kind = AshesJobKind.ProjectHelp, SettlementId = Town, StartHours = start, TotalHours = total, HoursCredited = credited };

    // --- CanStart ---------------------------------------------------------------------------------------------------

    [TestMethod]
    public void CanStart_VillageNotLooted_Blocked()
    {
        var site = LootedVillage();
        site.IsLooted = false;

        Assert.AreEqual(AshesBlock.NotLooted, _sut.CanStart(AshesJobKind.VillageRebuild, site).Block);
    }

    [TestMethod]
    public void CanStart_OtherFaction_Blocked()
    {
        var site = LootedVillage();
        site.SameMapFactionAsPlayer = false;

        Assert.AreEqual(AshesBlock.NotYours, _sut.CanStart(AshesJobKind.VillageRebuild, site).Block);
    }

    [TestMethod]
    public void CanStart_SameFaction_Allowed()
    {
        var check = _sut.CanStart(AshesJobKind.VillageRebuild, LootedVillage());

        Assert.IsTrue(check.Allowed);
        Assert.AreEqual(AshesVariant.Free, check.Variant);
    }

    [TestMethod]
    public void CanStart_TooFewHealthyTroops_Blocked()
    {
        var site = LootedVillage();
        site.HealthyTroops = 19;

        var check = _sut.CanStart(AshesJobKind.VillageRebuild, site);

        Assert.AreEqual(AshesBlock.NeedLabour, check.Block);
        Assert.AreEqual(20, check.Count);
    }

    [TestMethod]
    public void CanStart_ExactlyTheLabour_Allowed()
    {
        var site = LootedVillage();
        site.HealthyTroops = 20;

        Assert.IsTrue(_sut.CanStart(AshesJobKind.VillageRebuild, site).Allowed);
    }

    [TestMethod]
    public void CanStart_Evil_CountsPrisonersNotTroops()
    {
        var site = LootedVillage();
        Side(site, FactionSide.Evil);
        site.HealthyTroops = 500;
        site.Prisoners = 19;
        var short1 = _sut.CanStart(AshesJobKind.VillageRebuild, site);
        site.Prisoners = 20;
        site.HealthyTroops = 0;
        var enough = _sut.CanStart(AshesJobKind.VillageRebuild, site);

        Assert.AreEqual(AshesBlock.NeedLabour, short1.Block);
        Assert.AreEqual(AshesVariant.Thralls, short1.Variant);
        Assert.IsTrue(enough.Allowed, "twenty captives and no troops is enough");
    }

    [TestMethod]
    public void CanStart_Neutral_UsesFreeVariant()
    {
        var site = LootedVillage();
        Side(site, FactionSide.Neutral);
        site.Prisoners = 0;

        var check = _sut.CanStart(AshesJobKind.VillageRebuild, site);

        Assert.IsTrue(check.Allowed);
        Assert.AreEqual(AshesVariant.Free, check.Variant);
        _alignment.Received().ResolveSide(site.PlayerSide.FactionId, site.PlayerSide.CultureId);
    }

    [TestMethod]
    public void CanStart_Project_NotOwner_Blocked()
    {
        var site = Fief();
        site.PlayerClanOwns = false;

        Assert.AreEqual(AshesBlock.NotOwner, _sut.CanStart(AshesJobKind.ProjectHelp, site).Block);
    }

    [TestMethod]
    public void CanStart_Project_OwnerUnassigned_Blocked()
    {
        var site = Fief();
        site.OwnerUnassigned = true;

        Assert.AreEqual(AshesBlock.NotOwner, _sut.CanStart(AshesJobKind.ProjectHelp, site).Block);
    }

    [TestMethod]
    public void CanStart_Project_EmptyQueue_Blocked()
    {
        var site = Fief();
        site.HasProjectInProgress = false;

        Assert.AreEqual(AshesBlock.NoProject, _sut.CanStart(AshesJobKind.ProjectHelp, site).Block);
    }

    [TestMethod]
    public void CanStart_Project_WithAProject_Allowed()
    {
        var check = _sut.CanStart(AshesJobKind.ProjectHelp, Fief());

        Assert.IsTrue(check.Allowed);
        Assert.AreEqual(AshesVariant.Project, check.Variant);
    }

    [TestMethod]
    public void CanStart_Project_NeedsFortyHealthyTroops_EvenForAnEvilPlayer()
    {
        var site = Fief();
        Side(site, FactionSide.Evil);
        site.HealthyTroops = 39;
        site.Prisoners = 100;

        var check = _sut.CanStart(AshesJobKind.ProjectHelp, site);

        Assert.AreEqual(AshesBlock.NeedLabour, check.Block);
        Assert.AreEqual(40, check.Count);
    }

    [TestMethod]
    public void CanStart_Enlisted_Blocked()
    {
        _enlistment.IsEnlisted.Returns(true);

        Assert.AreEqual(AshesBlock.Enlisted, _sut.CanStart(AshesJobKind.VillageRebuild, LootedVillage()).Block);
    }

    [TestMethod]
    public void CanStart_ForeignArmy_Blocked()
    {
        var site = LootedVillage();
        site.InForeignArmy = true;

        Assert.AreEqual(AshesBlock.ForeignArmy, _sut.CanStart(AshesJobKind.VillageRebuild, site).Block);
    }

    [DataTestMethod]
    [DataRow(true, false, false)]
    [DataRow(false, true, false)]
    [DataRow(false, false, true)]
    public void CanStart_NotSinglePlayer_Blocked(bool sessionActive, bool deferToHost, bool dedicated)
    {
        _coop.IsSessionActive.Returns(sessionActive);
        _coop.ShouldDeferToHost.Returns(deferToHost);
        _dedicated.IsDedicatedServer.Returns(dedicated);

        Assert.AreEqual(AshesBlock.NotSinglePlayer, _sut.CanStart(AshesJobKind.VillageRebuild, LootedVillage()).Block);
    }

    [TestMethod]
    public void CanStart_NoAuthority_Blocked()
    {
        _coop.IsAuthority.Returns(false);

        Assert.AreEqual(AshesBlock.NotSinglePlayer, _sut.CanStart(AshesJobKind.VillageRebuild, LootedVillage()).Block);
    }

    [TestMethod]
    public void CanStart_SwitchOff_Disabled()
    {
        _settings.Enabled.Returns(false);

        Assert.AreEqual(AshesBlock.Disabled, _sut.CanStart(AshesJobKind.VillageRebuild, LootedVillage()).Block);
    }

    [TestMethod]
    public void CanStart_NoSite_NotApplicable()
    {
        Assert.AreEqual(AshesBlock.NotApplicable, _sut.CanStart(AshesJobKind.VillageRebuild, null).Block);
    }

    [TestMethod]
    public void CanStart_RebuildAtAFief_NotApplicable()
    {
        Assert.AreEqual(AshesBlock.NotApplicable, _sut.CanStart(AshesJobKind.VillageRebuild, Fief()).Block);
    }

    [TestMethod]
    public void CanStart_ProjectAtAVillage_NotApplicable()
    {
        Assert.AreEqual(AshesBlock.NotApplicable, _sut.CanStart(AshesJobKind.ProjectHelp, LootedVillage()).Block);
    }

    [TestMethod]
    public void CanStart_UnderSiegeOrInAMapEvent_Busy()
    {
        var siege = Fief();
        siege.UnderSiegeOrRaid = true;
        var battle = LootedVillage();
        battle.MainPartyInMapEvent = true;

        Assert.AreEqual(AshesBlock.Busy, _sut.CanStart(AshesJobKind.ProjectHelp, siege).Block);
        Assert.AreEqual(AshesBlock.Busy, _sut.CanStart(AshesJobKind.VillageRebuild, battle).Block);
    }

    [TestMethod]
    public void CanStart_MissingMaterial_BlockedNamesItem()
    {
        _world.CountMaterial("hardwood").Returns(39);

        var check = _sut.CanStart(AshesJobKind.VillageRebuild, LootedVillage());

        Assert.AreEqual(AshesBlock.NeedMaterial, check.Block);
        var missing = check.Missing.Single();
        Assert.AreEqual("hardwood", missing.ItemId);
        Assert.AreEqual(40, missing.Count, "the full amount, so the message reads 'You need 40 hardwood'");
    }

    [TestMethod]
    public void CanStart_Project_NamesEveryMissingItem()
    {
        _world.CountMaterial("clay").Returns(9);
        _world.CountMaterial("iron").Returns(0);

        var check = _sut.CanStart(AshesJobKind.ProjectHelp, Fief());

        CollectionAssert.AreEqual(new[] { "clay", "iron" }, check.Missing.Select(m => m.ItemId).ToArray());
    }

    [TestMethod]
    public void CanStart_UsesTheMaterialsTheConfigNames()
    {
        var rebuild = FromTheAshesConfigProvider.DefaultRebuild();
        rebuild.Hardwood = 0;
        _config.Rebuild.Returns(rebuild);
        _world.CountMaterial(Arg.Any<string>()).Returns(0);

        Assert.IsTrue(_sut.CanStart(AshesJobKind.VillageRebuild, LootedVillage()).Allowed, "a zero material is not asked for");
    }

    [TestMethod]
    public void CheckStart_ReadsTheSiteTheAdapterReports()
    {
        _world.CurrentSite().Returns(LootedVillage());
        Assert.IsTrue(_sut.CheckStart(AshesJobKind.VillageRebuild).Allowed);

        _world.CurrentSite().Returns((AshesSiteSnapshot?)null);
        Assert.AreEqual(AshesBlock.NotApplicable, _sut.CheckStart(AshesJobKind.VillageRebuild).Block);
    }

    // --- Progress and hours -----------------------------------------------------------------------------------------

    [TestMethod]
    public void Progress_DerivesFromStart_ClampsZeroToOne()
    {
        var job = RebuildJob(start: 100d, total: 24f);

        Assert.AreEqual(0f, _sut.Progress(job, 90d), "before the start");
        Assert.AreEqual(0f, _sut.Progress(job, 100d));
        Assert.AreEqual(0.5f, _sut.Progress(job, 112d), 1e-6f);
        Assert.AreEqual(1f, _sut.Progress(job, 124d));
        Assert.AreEqual(1f, _sut.Progress(job, 99999d), "far past the end");
    }

    [TestMethod]
    public void Progress_NaNNow_FailsClosed()
    {
        var job = RebuildJob();

        Assert.AreEqual(0f, _sut.Progress(job, double.NaN));
        Assert.AreEqual(0f, _sut.Progress(job, double.PositiveInfinity));
    }

    [TestMethod]
    public void Progress_NoLength_IsComplete()
    {
        Assert.AreEqual(1f, _sut.Progress(RebuildJob(total: 0f), 100d));
    }

    [TestMethod]
    public void HoursDue_RerunSameTime_ReturnsZero()
    {
        var job = ProjectJob(start: 100d, total: 20f);
        var first = _sut.HoursDue(job, 105.5d);
        job.HoursCredited += first;

        Assert.AreEqual(5, first);
        Assert.AreEqual(0, _sut.HoursDue(job, 105.5d));
    }

    [TestMethod]
    public void HoursDue_CapsAtTotal()
    {
        var job = ProjectJob(start: 100d, total: 20f);

        Assert.AreEqual(20, _sut.HoursDue(job, 10000d));
        job.HoursCredited = 20;
        Assert.AreEqual(0, _sut.HoursDue(job, 10000d));
    }

    [TestMethod]
    public void HoursDue_FractionalTotal_CreditsTheLastPartialHourOnce()
    {
        var job = ProjectJob(start: 100d, total: 20.5f);

        Assert.AreEqual(20, _sut.HoursDue(job, 120.4d), "the partial hour waits for the end");
        Assert.AreEqual(21, _sut.HoursDue(job, 120.5d));
    }

    [TestMethod]
    public void HoursDue_SkippedHours_CreditedOnce()
    {
        // A fast-forward or a load can jump several hours between two looks; every hour is paid exactly once.
        var job = ProjectJob(start: 100d, total: 20f);
        var due = _sut.HoursDue(job, 107.2d);
        job.HoursCredited += due;

        Assert.AreEqual(7, due);
        Assert.AreEqual(0, _sut.HoursDue(job, 107.9d));
        Assert.AreEqual(1, _sut.HoursDue(job, 108.0d));
    }

    [TestMethod]
    public void HoursDue_NaNNow_IsZero()
    {
        Assert.AreEqual(0, _sut.HoursDue(ProjectJob(), double.NaN));
    }

    [TestMethod]
    public void Begin_TakesItsLengthFromTheConfig()
    {
        var job = _sut.Begin(AshesJobKind.ProjectHelp, Fief(), 321.5d);

        Assert.AreEqual(AshesJobKind.ProjectHelp, job.Kind);
        Assert.AreEqual(Town, job.SettlementId);
        Assert.AreEqual(321.5d, job.StartHours);
        Assert.AreEqual(20f, job.TotalHours);
        Assert.AreEqual(0, job.HoursCredited);
    }

    // --- Validate ---------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Validate_HealthyJob_Continues()
    {
        Assert.AreEqual(AshesVerdict.Continue, _sut.Validate(RebuildJob(), LootedVillage()));
        Assert.AreEqual(AshesVerdict.Continue, _sut.Validate(ProjectJob(), Fief()));
    }

    [TestMethod]
    public void Validate_VillageRecovered_CancelsNoPenalty()
    {
        var site = LootedVillage();
        site.IsLooted = false;

        Assert.AreEqual(AshesVerdict.CancelRecovered, _sut.Validate(RebuildJob(), site));
        _world.DidNotReceive().ChangeNotableRelations(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Validate_UnderSiege_Cancels()
    {
        var village = LootedVillage();
        village.UnderSiegeOrRaid = true;
        var fief = Fief();
        fief.UnderSiegeOrRaid = true;

        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(RebuildJob(), village));
        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(ProjectJob(), fief));
    }

    [TestMethod]
    public void Validate_OwnerChanged_Cancels()
    {
        var village = LootedVillage();
        village.SameMapFactionAsPlayer = false;
        var fief = Fief();
        fief.PlayerClanOwns = false;
        var unassigned = Fief();
        unassigned.OwnerUnassigned = true;

        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(RebuildJob(), village));
        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(ProjectJob(), fief));
        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(ProjectJob(), unassigned));
    }

    [TestMethod]
    public void Validate_PartyLeftTheSettlement_Cancels()
    {
        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(RebuildJob(), null));
        var elsewhere = LootedVillage();
        elsewhere.SettlementId = "village_B2";
        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(RebuildJob(), elsewhere));
    }

    [TestMethod]
    public void Validate_MapEvent_ForeignArmy_Enlisted_NoAuthority_Cancel()
    {
        var battle = LootedVillage();
        battle.MainPartyInMapEvent = true;
        var army = Fief();
        army.InForeignArmy = true;

        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(RebuildJob(), battle));
        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(ProjectJob(), army));
        _enlistment.IsEnlisted.Returns(true);
        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(RebuildJob(), LootedVillage()));
        _enlistment.IsEnlisted.Returns(false);
        _coop.IsAuthority.Returns(false);
        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(RebuildJob(), LootedVillage()));
    }

    [TestMethod]
    public void Validate_ProjectQueueEmptied_Completes()
    {
        var site = Fief();
        site.HasProjectInProgress = false;

        Assert.AreEqual(AshesVerdict.Complete, _sut.Validate(ProjectJob(), site));
    }

    [TestMethod]
    public void Validate_SwitchOffMidJob_StillContinues()
    {
        _settings.Enabled.Returns(false);

        Assert.AreEqual(AshesVerdict.Continue, _sut.Validate(RebuildJob(), LootedVillage()));
    }

    // --- Rewards and costs ------------------------------------------------------------------------------------------

    [TestMethod]
    public void RewardsFor_Free_GivesNotableOwnerAndXp()
    {
        var rewards = _sut.RewardsFor(AshesJobKind.VillageRebuild, LootedVillage());

        Assert.AreEqual((3, 5, 200), (rewards.NotableRelation, rewards.OwnerRelation, rewards.AthleticsXp));
    }

    [TestMethod]
    public void RewardsFor_Evil_NoNotableGain()
    {
        var site = LootedVillage();
        Side(site, FactionSide.Evil);

        var rewards = _sut.RewardsFor(AshesJobKind.VillageRebuild, site);

        Assert.AreEqual(0, rewards.NotableRelation);
        Assert.AreEqual(5, rewards.OwnerRelation, "the owner relation is still given");
        Assert.AreEqual(200, rewards.AthleticsXp);
    }

    [TestMethod]
    public void RewardsFor_OwnerIsPlayer_NoOwnerGain()
    {
        var site = LootedVillage();
        site.PlayerClanOwns = true;

        var rewards = _sut.RewardsFor(AshesJobKind.VillageRebuild, site);

        Assert.AreEqual(0, rewards.OwnerRelation);
        Assert.AreEqual(3, rewards.NotableRelation);
    }

    [TestMethod]
    public void RewardsFor_Project_OneNotableNoOwner()
    {
        var rewards = _sut.RewardsFor(AshesJobKind.ProjectHelp, Fief());

        Assert.AreEqual((1, 0, 150), (rewards.NotableRelation, rewards.OwnerRelation, rewards.AthleticsXp));
    }

    [TestMethod]
    public void CostFor_ProjectPaysAtStart_RebuildAtCompletion()
    {
        var project = _sut.CostFor(AshesJobKind.ProjectHelp, FactionSide.Evil);
        var rebuild = _sut.CostFor(AshesJobKind.VillageRebuild, FactionSide.Free);
        var thralls = _sut.CostFor(AshesJobKind.VillageRebuild, FactionSide.Evil);

        Assert.IsTrue(project.PayAtStart);
        Assert.IsFalse(rebuild.PayAtStart);
        Assert.IsFalse(project.LabourFromPrisoners);
        Assert.IsFalse(rebuild.LabourFromPrisoners);
        Assert.IsTrue(thralls.LabourFromPrisoners);
        CollectionAssert.AreEqual(new[] { "hardwood", "clay", "tools", "iron" }, project.Materials.Select(m => m.ItemId).ToArray());
        CollectionAssert.AreEqual(new[] { 30, 10, 5, 5 }, project.Materials.Select(m => m.Count).ToArray());
        Assert.AreEqual("hardwood", rebuild.Materials.Single().ItemId);
        Assert.AreEqual(15f, project.ConstructionPerHour);
    }

    // --- Orchestration ----------------------------------------------------------------------------------------------

    [TestMethod]
    public void TryStart_Rebuild_StoresTheJob_AndTakesNothingYet()
    {
        _world.CurrentSite().Returns(LootedVillage());

        var check = _sut.TryStart(AshesJobKind.VillageRebuild, 500d);

        Assert.IsTrue(check.Allowed);
        Assert.AreEqual(Village, _store.Current!.SettlementId);
        Assert.AreEqual(500d, _store.Current.StartHours);
        _world.DidNotReceive().TakeMaterial(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TryStart_Project_PaysMaterialsAtOnce()
    {
        _world.CurrentSite().Returns(Fief());

        var check = _sut.TryStart(AshesJobKind.ProjectHelp, 500d);

        Assert.IsTrue(check.Allowed);
        _world.Received(1).TakeMaterial("hardwood", 30);
        _world.Received(1).TakeMaterial("clay", 10);
        _world.Received(1).TakeMaterial("tools", 5);
        _world.Received(1).TakeMaterial("iron", 5);
        Assert.IsNotNull(_store.Current);
    }

    [TestMethod]
    public void TryStart_Blocked_StoresNothing_TakesNothing()
    {
        var site = Fief();
        site.PlayerClanOwns = false;
        _world.CurrentSite().Returns(site);

        var check = _sut.TryStart(AshesJobKind.ProjectHelp, 500d);

        Assert.AreEqual(AshesBlock.NotOwner, check.Block);
        Assert.IsNull(_store.Current);
        _world.DidNotReceive().TakeMaterial(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void TryStart_WhileAJobRuns_Busy()
    {
        _store.Set(RebuildJob());
        _world.CurrentSite().Returns(LootedVillage());

        Assert.AreEqual(AshesBlock.Busy, _sut.TryStart(AshesJobKind.VillageRebuild, 500d).Block);
    }

    [TestMethod]
    public void Advance_NoJob_IsNone()
    {
        Assert.AreEqual(AshesOutcome.None, _sut.Advance(500d, false).Outcome);
    }

    [TestMethod]
    public void Advance_Rebuild_ReportsProgress_AndWritesNothing()
    {
        _store.Set(RebuildJob(start: 100d, total: 24f));
        _world.CurrentSite().Returns(LootedVillage());

        var result = _sut.Advance(112d, false);

        Assert.AreEqual(AshesOutcome.Running, result.Outcome);
        Assert.AreEqual(0.5f, result.Progress, 1e-6f);
        Assert.AreEqual(24f, result.TotalHours);
        Assert.AreEqual(AshesVariant.Free, result.Variant);
        Assert.AreEqual("Ashford", result.SettlementName);
        _world.DidNotReceive().RestoreVillage(Arg.Any<string>());
    }

    [TestMethod]
    public void Advance_Project_CreditsHoursDue_ThenNoMore()
    {
        _store.Set(ProjectJob(start: 100d, total: 20f));
        _world.CurrentSite().Returns(Fief());

        var first = _sut.Advance(105.5d, false);
        var again = _sut.Advance(105.5d, false);

        Assert.AreEqual(AshesOutcome.Running, first.Outcome);
        Assert.AreEqual("Granary", first.ProjectName);
        _world.Received(1).AddConstruction(Town, 75f);
        Assert.AreEqual(5, _store.Current!.HoursCredited);
        Assert.AreEqual(AshesOutcome.Running, again.Outcome);
        _world.Received(1).AddConstruction(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Advance_Project_QueueEmptiesMidJob_CompletesEarly()
    {
        _store.Set(ProjectJob(start: 100d, total: 20f));
        _world.CurrentSite().Returns(Fief());
        _world.AddConstruction(Town, Arg.Any<float>()).Returns(false);

        var result = _sut.Advance(105.5d, false);

        Assert.AreEqual(AshesOutcome.Completed, result.Outcome);
        Assert.IsNull(_store.Current);
        _xp.Received(1).AddSkillXp("hero_main", "Athletics", 150f);
    }

    [TestMethod]
    public void Advance_VillageRecovered_CancelsWithoutPenalty()
    {
        _store.Set(RebuildJob());
        var site = LootedVillage();
        site.IsLooted = false;
        _world.CurrentSite().Returns(site);

        var result = _sut.Advance(110d, true);

        Assert.AreEqual(AshesOutcome.CancelledRecovered, result.Outcome);
        Assert.IsNull(_store.Current);
        _world.DidNotReceive().ChangeNotableRelations(Arg.Any<string>(), Arg.Any<int>());
        _world.DidNotReceive().TakeMaterial(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Advance_PartyLeft_Cancels()
    {
        _store.Set(RebuildJob());
        _world.CurrentSite().Returns((AshesSiteSnapshot?)null);

        Assert.AreEqual(AshesOutcome.Cancelled, _sut.Advance(110d, true).Outcome);
        Assert.IsNull(_store.Current);
    }

    [TestMethod]
    public void Advance_Hourly_FinishesAJobThatIsDue()
    {
        _store.Set(RebuildJob(start: 100d, total: 24f));
        _world.CurrentSite().Returns(LootedVillage());

        var result = _sut.Advance(125d, finishWhenDue: true);

        Assert.AreEqual(AshesOutcome.Completed, result.Outcome);
        _world.Received(1).RestoreVillage(Village);
    }

    [TestMethod]
    public void Advance_FromTheMenuTick_DoesNotFinishItself()
    {
        _store.Set(RebuildJob(start: 100d, total: 24f));
        _world.CurrentSite().Returns(LootedVillage());

        var result = _sut.Advance(125d, finishWhenDue: false);

        Assert.AreEqual(AshesOutcome.Running, result.Outcome, "the engine ends the wait and calls Complete");
        Assert.IsNotNull(_store.Current);
    }

    [TestMethod]
    public void Complete_Rebuild_TakesMaterials_Restores_AndPaysTheRewards()
    {
        _store.Set(RebuildJob(start: 100d, total: 24f));
        _world.CurrentSite().Returns(LootedVillage());

        var result = _sut.Complete(124d);

        Assert.AreEqual(AshesOutcome.Completed, result.Outcome);
        Assert.AreEqual("Ashford", result.SettlementName);
        Received.InOrder(() =>
        {
            _world.RestoreVillage(Village);
            _world.TakeMaterial("hardwood", 40);
            _world.ChangeNotableRelations(Village, 3);
            _world.ChangeOwnerRelation(Village, 5);
        });
        _xp.Received(1).AddSkillXp("hero_main", "Athletics", 200f);
        _xp.Received(1).AddSkillXp("hero_companion", "Athletics", 200f);
        Assert.IsNull(_store.Current);
    }

    [TestMethod]
    public void Complete_Evil_GivesOwnerRelationOnly()
    {
        var site = LootedVillage();
        Side(site, FactionSide.Evil);
        site.Prisoners = 30;
        _store.Set(RebuildJob(start: 100d, total: 24f));
        _world.CurrentSite().Returns(site);

        _sut.Complete(124d);

        _world.DidNotReceive().ChangeNotableRelations(Arg.Any<string>(), Arg.Any<int>());
        _world.Received(1).ChangeOwnerRelation(Village, 5);
    }

    [TestMethod]
    public void Complete_MaterialsGoneSinceTheStart_TakesNothing_RebuildsNothing()
    {
        _store.Set(RebuildJob(start: 100d, total: 24f));
        _world.CurrentSite().Returns(LootedVillage());
        _world.CountMaterial("hardwood").Returns(12);

        var result = _sut.Complete(124d);

        Assert.AreEqual(AshesOutcome.ShortOfMaterials, result.Outcome);
        Assert.AreEqual("hardwood", result.Missing.Single().ItemId);
        _world.DidNotReceive().TakeMaterial(Arg.Any<string>(), Arg.Any<int>());
        _world.DidNotReceive().RestoreVillage(Arg.Any<string>());
        Assert.IsNull(_store.Current, "the job ends either way; no loop of retries");
    }

    [TestMethod]
    public void Complete_ClearsTheJobBeforeTheWorldWrites()
    {
        // A throw out of a world call must not leave a job that re-throws on every tick.
        _store.Set(RebuildJob(start: 100d, total: 24f));
        _world.CurrentSite().Returns(LootedVillage());
        _world.RestoreVillage(Village).Returns(_ => throw new System.InvalidOperationException("engine"));

        Assert.ThrowsException<System.InvalidOperationException>(() => _sut.Complete(124d));
        Assert.IsNull(_store.Current);
    }

    [TestMethod]
    public void Complete_NoAuthority_WritesNothing()
    {
        _store.Set(RebuildJob(start: 100d, total: 24f));
        _world.CurrentSite().Returns(LootedVillage());
        _coop.IsAuthority.Returns(false);

        var result = _sut.Complete(124d);

        Assert.AreEqual(AshesOutcome.Cancelled, result.Outcome);
        _world.DidNotReceive().RestoreVillage(Arg.Any<string>());
        _xp.DidNotReceive().AddSkillXp(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Complete_RestoreFails_TakesAndPaysNothing_LogsOnce()
    {
        _store.Set(RebuildJob(start: 100d, total: 24f));
        _world.CurrentSite().Returns(LootedVillage());
        _world.RestoreVillage(Village).Returns(false);

        var result = _sut.Complete(124d);

        Assert.AreEqual(AshesOutcome.Cancelled, result.Outcome);
        _world.DidNotReceive().ChangeNotableRelations(Arg.Any<string>(), Arg.Any<int>());
        _world.DidNotReceive().TakeMaterial(Arg.Any<string>(), Arg.Any<int>());
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Complete_Project_CreditsTheLastHours_ThenRewards()
    {
        _store.Set(ProjectJob(start: 100d, total: 20f, credited: 18));
        _world.CurrentSite().Returns(Fief());

        var result = _sut.Complete(120d);

        Assert.AreEqual(AshesOutcome.Completed, result.Outcome);
        Assert.AreEqual("Granary", result.ProjectName);
        _world.Received(1).AddConstruction(Town, 30f);
        _world.Received(1).ChangeNotableRelations(Town, 1);
        _world.DidNotReceive().ChangeOwnerRelation(Arg.Any<string>(), Arg.Any<int>());
        _world.DidNotReceive().TakeMaterial(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Stop_Rebuild_AppliesThePenalty_TakesNothing()
    {
        _store.Set(RebuildJob());
        _world.CurrentSite().Returns(LootedVillage());

        var result = _sut.Stop();

        Assert.AreEqual(AshesOutcome.Stopped, result.Outcome);
        _world.Received(1).ChangeNotableRelations(Village, -1);
        _world.DidNotReceive().TakeMaterial(Arg.Any<string>(), Arg.Any<int>());
        _world.DidNotReceive().RestoreVillage(Arg.Any<string>());
        Assert.IsNull(_store.Current);
    }

    [TestMethod]
    public void Stop_Thralls_NoPenalty()
    {
        var site = LootedVillage();
        Side(site, FactionSide.Evil);
        _store.Set(RebuildJob());
        _world.CurrentSite().Returns(site);

        _sut.Stop();

        _world.DidNotReceive().ChangeNotableRelations(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Stop_Project_NoPenalty_CreditedHoursStay()
    {
        _store.Set(ProjectJob(credited: 6));
        _world.CurrentSite().Returns(Fief());

        var result = _sut.Stop();

        Assert.AreEqual(AshesOutcome.Stopped, result.Outcome);
        _world.DidNotReceive().ChangeNotableRelations(Arg.Any<string>(), Arg.Any<int>());
        _world.DidNotReceive().AddConstruction(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Stop_NoJob_IsNone()
    {
        Assert.AreEqual(AshesOutcome.None, _sut.Stop().Outcome);
    }

    [TestMethod]
    public void Abandon_ClearsTheJob_WithoutAWorldWrite()
    {
        _store.Set(RebuildJob());

        _sut.Abandon();

        Assert.IsNull(_store.Current);
        Assert.AreEqual(0, _world.ReceivedCalls().Count());
    }

    // --- Review fixes (1.3, 1.5, 1.6, 1.7, 2.5, 2.7) ----------------------------------------------------------------

    [TestMethod]
    public void Advance_QueueEmptiedSinceLastLook_ResultNamesTheLastBuilding()
    {
        _store.Set(ProjectJob(start: 100d, total: 20f));
        _world.CurrentSite().Returns(Fief());
        Assert.AreEqual("Granary", _sut.Advance(101d, false).ProjectName);
        var emptied = Fief();
        emptied.HasProjectInProgress = false;
        emptied.ProjectName = "";
        _world.CurrentSite().Returns(emptied);

        var result = _sut.Advance(102d, false);

        Assert.AreEqual(AshesOutcome.Completed, result.Outcome);
        Assert.AreEqual("Granary", result.ProjectName, "the message must not read 'hastens the .'");
    }

    [TestMethod]
    public void CheckStart_WhileAJobIsStored_IsBusy()
    {
        _store.Set(RebuildJob());
        _world.CurrentSite().Returns(LootedVillage());

        Assert.AreEqual(AshesBlock.Busy, _sut.CheckStart(AshesJobKind.VillageRebuild).Block);
    }

    [TestMethod]
    public void CanStart_NotSinglePlayer_StillHidesWhereSinglePlayerHides()
    {
        _coop.IsSessionActive.Returns(true);
        var notLooted = LootedVillage();
        notLooted.IsLooted = false;
        var notOwner = Fief();
        notOwner.PlayerClanOwns = false;
        var siege = Fief();
        siege.UnderSiegeOrRaid = true;

        Assert.AreEqual(AshesBlock.NotLooted, _sut.CanStart(AshesJobKind.VillageRebuild, notLooted).Block);
        Assert.AreEqual(AshesBlock.NotOwner, _sut.CanStart(AshesJobKind.ProjectHelp, notOwner).Block);
        Assert.AreEqual(AshesBlock.Busy, _sut.CanStart(AshesJobKind.ProjectHelp, siege).Block);
        Assert.AreEqual(AshesBlock.NotApplicable, _sut.CanStart(AshesJobKind.ProjectHelp, LootedVillage()).Block);
        Assert.AreEqual(AshesBlock.NotSinglePlayer, _sut.CanStart(AshesJobKind.VillageRebuild, LootedVillage()).Block,
            "only an option single-player would show is explained as unavailable");
    }

    [TestMethod]
    public void Validate_InACoopSession_CancelsNoPenalty()
    {
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(AshesVerdict.Cancel, _sut.Validate(RebuildJob(), LootedVillage()));
        _world.DidNotReceive().ChangeNotableRelations(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void CanStart_Project_EmptyQueueWhileEnlisted_ReportsEnlisted()
    {
        _enlistment.IsEnlisted.Returns(true);
        var site = Fief();
        site.HasProjectInProgress = false;

        Assert.AreEqual(AshesBlock.Enlisted, _sut.CanStart(AshesJobKind.ProjectHelp, site).Block,
            "CanStart and Validate share one site rule, and the empty queue is its last check");
    }

    [TestMethod]
    public void Complete_NoJob_IsNone()
    {
        Assert.AreEqual(AshesOutcome.None, _sut.Complete(500d).Outcome);
    }

    [TestMethod]
    public void Advance_ThenComplete_AtTheSameInstant_PaysTheLastHourOnce()
    {
        _store.Set(ProjectJob(start: 100d, total: 20f));
        _world.CurrentSite().Returns(Fief());

        _sut.Advance(120d, false);
        var result = _sut.Complete(120d);

        Assert.AreEqual(AshesOutcome.Completed, result.Outcome);
        _world.Received(1).AddConstruction(Town, 300f);
        _world.Received(1).AddConstruction(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void IsHidden_IsExactlyTheFiveSilentBlocks()
    {
        var hidden = System.Enum.GetValues(typeof(AshesBlock)).Cast<AshesBlock>().Where(FromTheAshesTexts.IsHidden).ToArray();

        CollectionAssert.AreEquivalent(
            new[] { AshesBlock.Disabled, AshesBlock.NotApplicable, AshesBlock.NotLooted, AshesBlock.NotOwner, AshesBlock.Busy }, hidden);
    }

    // --- Review fixes, second batch (1.5, 1.9, 1.13, 1.14) -----------------------------------------------------------

    [TestMethod]
    public void CanStart_VassalOfAnEvilKingdom_ResolvesTheKingdomWithItsOwnCulture()
    {
        var site = LootedVillage();
        site.PlayerSide = new SideIds("mordor", "mordor_culture");
        _alignment.ResolveSide("mordor", "mordor_culture").Returns(FactionSide.Evil);
        site.HealthyTroops = 0;
        site.Prisoners = 30;

        var check = _sut.CanStart(AshesJobKind.VillageRebuild, site);

        Assert.AreEqual(AshesVariant.Thralls, check.Variant);
        Assert.IsTrue(check.Allowed);
        _alignment.DidNotReceive().ResolveSide(Arg.Any<string>(), "gondor_culture");
    }

    [TestMethod]
    public void CanStart_IndependentEvilCulturePlayer_ResolvesTheClanWithItsOwnCulture()
    {
        var site = LootedVillage();
        site.PlayerSide = new SideIds("player_clan", "orc_culture");
        _alignment.ResolveSide("player_clan", "orc_culture").Returns(FactionSide.Evil);

        var check = _sut.CanStart(AshesJobKind.VillageRebuild, site);

        Assert.AreEqual(AshesVariant.Thralls, check.Variant);
    }

    [TestMethod]
    public void CanStart_NeutralPlayer_GetsTheFreeVariant()
    {
        var site = LootedVillage();
        site.PlayerSide = new SideIds("player_clan", "vlandia");
        _alignment.ResolveSide("player_clan", "vlandia").Returns(FactionSide.Neutral);

        Assert.AreEqual(AshesVariant.Free, _sut.CanStart(AshesJobKind.VillageRebuild, site).Variant);
        // The default stub already answers Free, so only the arguments prove the neutral player was looked up.
        _alignment.Received().ResolveSide("player_clan", "vlandia");
    }

    private static readonly (string Id, int Count)[] ProjectMaterials = { ("hardwood", 30), ("clay", 10), ("tools", 5), ("iron", 5) };

    private void AssertRefunded(bool refunded)
    {
        foreach (var (id, count) in ProjectMaterials)
            if (refunded)
                _world.Received(1).ReturnMaterial(id, count);
            else
                _world.DidNotReceive().ReturnMaterial(id, Arg.Any<int>());
    }

    [TestMethod]
    public void Advance_ProjectCancelledByTheRules_RefundsTheMaterials_KeepsTheCreditedHours()
    {
        _store.Set(ProjectJob(start: 100d, total: 20f, credited: 6));
        var site = Fief();
        site.PlayerClanOwns = false;
        _world.CurrentSite().Returns(site);

        var result = _sut.Advance(110d, false);

        Assert.AreEqual(AshesOutcome.Cancelled, result.Outcome);
        AssertRefunded(true);
        _world.DidNotReceive().AddConstruction(Arg.Any<string>(), Arg.Any<float>());
    }

    [TestMethod]
    public void Advance_InACoopSession_CancelsWithNoPenalty_AndRefundsAProject()
    {
        _store.Set(ProjectJob());
        _world.CurrentSite().Returns(Fief());
        _coop.IsSessionActive.Returns(true);

        Assert.AreEqual(AshesOutcome.Cancelled, _sut.Advance(110d, false).Outcome);
        AssertRefunded(true);
        _world.DidNotReceive().ChangeNotableRelations(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Advance_ProjectCancelled_WithoutAuthority_WritesNothing()
    {
        _store.Set(ProjectJob());
        _world.CurrentSite().Returns(Fief());
        _coop.IsAuthority.Returns(false);

        Assert.AreEqual(AshesOutcome.Cancelled, _sut.Advance(110d, false).Outcome);
        AssertRefunded(false);
    }

    [TestMethod]
    public void Advance_RebuildCancelled_RefundsNothing()
    {
        _store.Set(RebuildJob());
        _world.CurrentSite().Returns((AshesSiteSnapshot?)null);

        _sut.Advance(110d, false);

        _world.DidNotReceive().ReturnMaterial(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Stop_Project_KeepsTheMaterialsSpent()
    {
        _store.Set(ProjectJob(credited: 6));
        _world.CurrentSite().Returns(Fief());

        _sut.Stop();

        AssertRefunded(false);
    }

    [TestMethod]
    public void Stop_WithoutAuthority_WritesNothing()
    {
        _store.Set(RebuildJob());
        _world.CurrentSite().Returns(LootedVillage());
        _coop.IsAuthority.Returns(false);

        var result = _sut.Stop();

        Assert.AreEqual(AshesOutcome.Stopped, result.Outcome);
        Assert.IsNull(_store.Current);
        _world.DidNotReceive().ChangeNotableRelations(Arg.Any<string>(), Arg.Any<int>());
        AssertRefunded(false);
    }

    [TestMethod]
    public void Abandon_AProject_RefundsTheMaterials()
    {
        _store.Set(ProjectJob());

        _sut.Abandon();

        Assert.IsNull(_store.Current);
        AssertRefunded(true);
    }

    [TestMethod]
    public void Abandon_AProject_ASecondTimeRefundsNothing()
    {
        _store.Set(ProjectJob());
        _sut.Abandon();

        _sut.Abandon();

        _world.Received(4).ReturnMaterial(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Abandon_AProject_ARefundThatThrowsIsLoggedNotRethrown()
    {
        _store.Set(ProjectJob());
        _world.When(w => w.ReturnMaterial(Arg.Any<string>(), Arg.Any<int>())).Do(_ => throw new System.InvalidOperationException("engine"));

        _sut.Abandon();

        Assert.IsNull(_store.Current);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Interrupt_ARunningProject_CancelsRefundsAndPenalisesNothing()
    {
        _store.Set(ProjectJob(credited: 3));
        _world.CurrentSite().Returns(Fief());

        var result = _sut.Interrupt();

        Assert.AreEqual(AshesOutcome.Cancelled, result.Outcome);
        Assert.IsNull(_store.Current);
        AssertRefunded(true);
        _world.DidNotReceive().ChangeNotableRelations(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Interrupt_ARunningRebuild_CancelsWithNoPenalty()
    {
        _store.Set(RebuildJob());
        _world.CurrentSite().Returns(LootedVillage());

        Assert.AreEqual(AshesOutcome.Cancelled, _sut.Interrupt().Outcome);
        Assert.IsNull(_store.Current);
        _world.DidNotReceive().ChangeNotableRelations(Arg.Any<string>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Interrupt_NoJob_IsNone()
    {
        Assert.AreEqual(AshesOutcome.None, _sut.Interrupt().Outcome);
    }
}
