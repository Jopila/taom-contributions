using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;

namespace TAOM.Features.FromTheAshes;

/// <summary>
/// The rules of From the Ashes (docs/features/from-the-ashes.md): the player's company either raises a looted village of
/// his own realm (hours of labour, then hardwood and goodwill), or lends its hands to the head of a build queue in a fief
/// he owns (construction points every hour). The rule methods (<see cref="CanStart"/>, <see cref="Validate"/>, the
/// progress and cost methods) write nothing: they read their arguments, the config and the live gates (the switch, the
/// co-op session, enlistment, the stored job). The job methods move the one job in <see cref="AshesJobStore"/> through the
/// adapter. Time is always passed in, as campaign hours. Single-player only, and every world write also needs
/// <see cref="ICoopSessionProvider.IsAuthority"/>.
/// </summary>
public sealed class FromTheAshesService
{
    private const string Athletics = "Athletics";

    private readonly IFromTheAshesConfigProvider _config;
    private readonly IFromTheAshesSettingsProvider _settings;
    private readonly IAshesWorldAdapter _world;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IAlignmentService _alignment;
    private readonly IHeroSkillXpAdapter _xp;
    private readonly AshesJobStore _store;
    private readonly IModLogger _logger;

    public FromTheAshesService(IFromTheAshesConfigProvider config, IFromTheAshesSettingsProvider settings, IAshesWorldAdapter world,
        IEnlistmentStateQuery enlistment, ICoopSessionProvider coop, IDedicatedServerProvider dedicated, IAlignmentService alignment,
        IHeroSkillXpAdapter xp, AshesJobStore store, IModLogger logger)
    {
        _config = config;
        _settings = settings;
        _world = world;
        _enlistment = enlistment;
        _coop = coop;
        _dedicated = dedicated;
        _alignment = alignment;
        _xp = xp;
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Read live on every use (ICoopSessionProvider's contract). Stands down while a co-op session is live, while a co-op mod
    /// TAOM cannot probe is loaded, on a dedicated server and on a peer that may not write the world (the Eye of Sauron's rule).
    /// </summary>
    private bool IsSinglePlayer =>
        !_coop.IsSessionActive && !_coop.ShouldDeferToHost && !_dedicated.IsDedicatedServer && _coop.IsAuthority;

    // --- Rules ------------------------------------------------------------------------------------------------------

    /// <summary>Whether a job of this kind may start at this site, and if not, the first reason. Writes nothing.</summary>
    public AshesStartCheck CanStart(AshesJobKind kind, AshesSiteSnapshot? site)
    {
        if (!_settings.Enabled)
            return Blocked(AshesBlock.Disabled, kind, site);
        if (_store.Current != null)
            return Blocked(AshesBlock.Busy, kind, site);
        if (site == null)
            return Blocked(AshesBlock.NotApplicable, kind, site);

        // The site rules come first so a co-op peer sees no option exactly where single-player sees none.
        var block = SiteBlock(kind, site);
        if (block != AshesBlock.None)
            return Blocked(block, kind, site);
        if (!IsSinglePlayer)
            return Blocked(AshesBlock.NotSinglePlayer, kind, site);

        var cost = CostFor(kind, SideFor(kind, site));
        var have = cost.LabourFromPrisoners ? site.Prisoners : site.HealthyTroops;
        if (have < cost.Labour)
            return new AshesStartCheck { Block = AshesBlock.NeedLabour, Variant = cost.Variant, Count = cost.Labour };

        var missing = MissingFor(cost);
        if (missing.Count > 0)
            return new AshesStartCheck { Block = AshesBlock.NeedMaterial, Variant = cost.Variant, Missing = missing };

        return new AshesStartCheck { Block = AshesBlock.None, Variant = cost.Variant };
    }

    /// <summary><see cref="CanStart"/> for the settlement the party is in now: what a menu condition asks.</summary>
    public AshesStartCheck CheckStart(AshesJobKind kind) => CanStart(kind, _world.CurrentSite());

    /// <summary>A new job starting now. Callers have already passed <see cref="CanStart"/>.</summary>
    public AshesJob Begin(AshesJobKind kind, AshesSiteSnapshot site, double nowHours) => new()
    {
        Kind = kind,
        SettlementId = site.SettlementId,
        StartHours = nowHours,
        TotalHours = CostFor(kind, SideFor(kind, site)).Hours,
        HoursCredited = 0,
    };

    /// <summary>0 to 1 from the stored start. A time that is not finite gives 0, so a bad clock never completes a job.</summary>
    public float Progress(AshesJob job, double nowHours)
    {
        if (job.TotalHours <= 0f)
            return 1f;
        if (!FiniteFloatValidator.IsFinite(nowHours))
            return 0f;
        var elapsed = nowHours - job.StartHours;
        if (!(elapsed > 0d))
            return 0f;
        var progress = elapsed / job.TotalHours;
        return progress >= 1d ? 1f : (float)progress;
    }

    /// <summary>
    /// The whole hours not yet paid out: elapsed hours rounded down, or the length rounded up once it is over, less
    /// <see cref="AshesJob.HoursCredited"/>. Never negative; the caller adds the result to the credited count.
    /// </summary>
    public int HoursDue(AshesJob job, double nowHours)
    {
        if (!FiniteFloatValidator.IsFinite(nowHours) || job.TotalHours <= 0f)
            return 0;
        var elapsed = nowHours - job.StartHours;
        if (!(elapsed > 0d))
            return 0;
        var through = elapsed >= job.TotalHours ? (int)Math.Ceiling(job.TotalHours) : (int)Math.Floor(elapsed);
        return Math.Max(0, through - job.HoursCredited);
    }

    /// <summary>
    /// What a running job should do given where the party is now. The switch is deliberately not read: turning it off hides
    /// the start options only. The menu id is not read either: another menu on top (an encounter) is not a reason to cancel.
    /// A job carried into a co-op session cancels, because the start gate would not have let it begin there.
    /// </summary>
    public AshesVerdict Validate(AshesJob job, AshesSiteSnapshot? site)
    {
        if (!IsSinglePlayer || site == null || site.SettlementId != job.SettlementId)
            return AshesVerdict.Cancel;

        switch (SiteBlock(job.Kind, site))
        {
            case AshesBlock.None:
                return AshesVerdict.Continue;
            case AshesBlock.NotLooted:
                return AshesVerdict.CancelRecovered;
            case AshesBlock.NoProject:
                return AshesVerdict.Complete;
            default:
                return AshesVerdict.Cancel;
        }
    }

    /// <summary>The goodwill and experience a finished job pays. The owner gives nothing to himself.</summary>
    public AshesRewards RewardsFor(AshesJobKind kind, AshesSiteSnapshot site)
    {
        var config = ConfigFor(VariantFor(kind, SideFor(kind, site)));
        return new AshesRewards
        {
            NotableRelation = config.NotableRelation,
            OwnerRelation = kind == AshesJobKind.ProjectHelp || site.PlayerClanOwns ? 0 : config.OwnerRelation,
            AthleticsXp = config.AthleticsXp,
        };
    }

    /// <summary>What a job asks for, from the config. An evil player's rebuild is the thrall variant.</summary>
    public AshesCost CostFor(AshesJobKind kind, FactionSide side)
    {
        var variant = VariantFor(kind, side);
        var config = ConfigFor(variant);
        var materials = new List<AshesMaterial>();
        AddMaterial(materials, "hardwood", config.Hardwood);
        AddMaterial(materials, "clay", config.Clay);
        AddMaterial(materials, "tools", config.Tools);
        AddMaterial(materials, "iron", config.Iron);
        return new AshesCost
        {
            Variant = variant,
            Hours = config.Hours,
            Labour = config.Labour,
            LabourFromPrisoners = variant == AshesVariant.Thralls,
            Materials = materials,
            PayAtStart = variant == AshesVariant.Project,
            ConstructionPerHour = config.ConstructionPerHour,
            StopPenalty = config.StopPenalty,
        };
    }

    // --- The job ----------------------------------------------------------------------------------------------------

    /// <summary>Starts a job at the party's settlement. Project help pays its materials now; a rebuild pays at the end.</summary>
    public AshesStartCheck TryStart(AshesJobKind kind, double nowHours)
    {
        var site = _world.CurrentSite();
        var check = CanStart(kind, site);
        if (!check.Allowed)
            return check;

        var cost = CostFor(kind, SideFor(kind, site));
        if (cost.PayAtStart)
            Pay(cost);
        _store.Set(Begin(kind, site!, nowHours));
        return check;
    }

    /// <summary>
    /// One look at the running job: validates it, credits project hours, and reports progress. The menu's tick passes
    /// <paramref name="finishWhenDue"/> false (the engine ends the wait and calls <see cref="Complete"/>); the hourly
    /// reconcile passes true, so a job whose menu was pre-empted still ends. A project's construction points are the only
    /// world write of a look that ends in <see cref="AshesOutcome.Running"/>.
    /// </summary>
    public AshesResult Advance(double nowHours, bool finishWhenDue)
    {
        var job = _store.Current;
        if (job == null)
            return AshesResult.Of(AshesOutcome.None);

        var site = _world.CurrentSite();
        if (site != null && site.SettlementId == job.SettlementId && !string.IsNullOrEmpty(site.ProjectName))
            job.LastProjectName = site.ProjectName;
        var verdict = Validate(job, site);
        if (verdict != AshesVerdict.Continue)
            return End(job, site, verdict, nowHours);

        if (job.Kind == AshesJobKind.ProjectHelp)
        {
            var due = HoursDue(job, nowHours);
            if (due > 0)
            {
                var points = due * CostFor(job.Kind, FactionSide.Neutral).ConstructionPerHour;
                var advanced = points <= 0f || _world.AddConstruction(site!.SettlementId, points);
                job.HoursCredited += due;
                if (!advanced)
                    return End(job, site, AshesVerdict.Complete, nowHours);
            }
        }

        var progress = Progress(job, nowHours);
        if (finishWhenDue && progress >= 1f)
            return End(job, site, AshesVerdict.Continue, nowHours);

        var running = Describe(AshesOutcome.Running, job, site);
        running.Progress = progress;
        running.TotalHours = job.TotalHours;
        return running;
    }

    /// <summary>The wait ran its course: pays the end of the job. Validates again, so a job that went wrong in the last frame cancels.</summary>
    public AshesResult Complete(double nowHours)
    {
        var job = _store.Current;
        if (job == null)
            return AshesResult.Of(AshesOutcome.None);

        var site = _world.CurrentSite();
        return End(job, site, Validate(job, site), nowHours);
    }

    /// <summary>
    /// The player lays down his tools. A rebuild costs goodwill with the notables; project help costs nothing and keeps its
    /// credited hours, and its materials stay spent (a choice to stop is not a forced cancel).
    /// </summary>
    public AshesResult Stop()
    {
        var job = _store.Current;
        if (job == null)
            return AshesResult.Of(AshesOutcome.None);

        var site = _world.CurrentSite();
        _store.Clear();
        if (job.Kind == AshesJobKind.VillageRebuild && _coop.IsAuthority)
        {
            var penalty = CostFor(job.Kind, SideFor(job.Kind, site)).StopPenalty;
            if (penalty != 0)
                _world.ChangeNotableRelations(job.SettlementId, penalty);
        }

        return Describe(AshesOutcome.Stopped, job, site);
    }

    /// <summary>A map event took the main party: the job ends as a cancel, with no penalty and a project's materials back.</summary>
    public AshesResult Interrupt()
    {
        var job = _store.Current;
        return job == null ? AshesResult.Of(AshesOutcome.None) : End(job, _world.CurrentSite(), AshesVerdict.Cancel, 0d);
    }

    /// <summary>Whether a job is stored, for a fault log to say if it dropped one.</summary>
    public bool HasJob => _store.Current != null;

    /// <summary>
    /// Drops the job: the guard's answer to an exception, so a fault cannot loop. A fault is a forced end, so a project's
    /// materials go back; nothing else is written.
    /// </summary>
    public void Abandon()
    {
        var job = _store.Current;
        _store.Clear();
        if (job != null)
            Refund(job);
    }

    // --- Internals --------------------------------------------------------------------------------------------------

    /// <summary>
    /// The one site rule, shared by <see cref="CanStart"/> and <see cref="Validate"/>: the first reason the job cannot
    /// run at this site, or None. The empty queue is last, so a project's "nothing to do" never hides a more basic block.
    /// </summary>
    private AshesBlock SiteBlock(AshesJobKind kind, AshesSiteSnapshot site)
    {
        if (kind == AshesJobKind.VillageRebuild)
        {
            if (!site.IsVillage)
                return AshesBlock.NotApplicable;
            if (!site.IsLooted)
                return AshesBlock.NotLooted;
            if (!site.SameMapFactionAsPlayer)
                return AshesBlock.NotYours;
        }
        else
        {
            if (!site.IsFortification)
                return AshesBlock.NotApplicable;
            if (!site.PlayerClanOwns || site.OwnerUnassigned)
                return AshesBlock.NotOwner;
        }

        if (site.UnderSiegeOrRaid || site.MainPartyInMapEvent)
            return AshesBlock.Busy;
        if (_enlistment.IsEnlisted)
            return AshesBlock.Enlisted;
        if (site.InForeignArmy)
            return AshesBlock.ForeignArmy;
        if (kind == AshesJobKind.ProjectHelp && !site.HasProjectInProgress)
            return AshesBlock.NoProject;
        return AshesBlock.None;
    }

    /// <summary>Ends the job for <paramref name="verdict"/>: a cancel writes nothing but a refund; anything else pays the end.</summary>
    private AshesResult End(AshesJob job, AshesSiteSnapshot? site, AshesVerdict verdict, double nowHours)
    {
        // Clear first: an exception out of a world write below must not leave a job that throws again on every tick.
        _store.Clear();
        if (verdict == AshesVerdict.Cancel)
        {
            Refund(job);
            return Describe(AshesOutcome.Cancelled, job, site);
        }

        if (verdict == AshesVerdict.CancelRecovered)
            return Describe(AshesOutcome.CancelledRecovered, job, site);

        var cost = CostFor(job.Kind, SideFor(job.Kind, site));
        if (job.Kind == AshesJobKind.VillageRebuild)
        {
            var missing = MissingFor(cost);
            if (missing.Count > 0)
            {
                var shortOfMaterials = Describe(AshesOutcome.ShortOfMaterials, job, site);
                shortOfMaterials.Missing = missing;
                return shortOfMaterials;
            }

            // Restore before paying: a village the engine will not raise costs the player nothing.
            if (!_world.RestoreVillage(job.SettlementId))
            {
                _logger.LogWarning($"[FromTheAshes] '{job.SettlementId}' did not return to the normal state; nothing was taken");
                return Describe(AshesOutcome.Cancelled, job, site);
            }

            Pay(cost);
        }
        else if (verdict == AshesVerdict.Continue)
        {
            // The last hours: the wait is over, so every remaining hour is due.
            var due = HoursDue(job, nowHours);
            var points = due * cost.ConstructionPerHour;
            if (due > 0 && points > 0f)
                _world.AddConstruction(job.SettlementId, points);
        }

        var rewards = RewardsFor(job.Kind, site!);
        if (rewards.NotableRelation != 0)
            _world.ChangeNotableRelations(job.SettlementId, rewards.NotableRelation);
        if (rewards.OwnerRelation != 0)
            _world.ChangeOwnerRelation(job.SettlementId, rewards.OwnerRelation);
        if (rewards.AthleticsXp > 0)
            foreach (var heroId in _world.PartyHeroIds())
                _xp.AddSkillXp(heroId, Athletics, rewards.AthleticsXp);

        return Describe(AshesOutcome.Completed, job, site);
    }

    private IReadOnlyList<AshesMaterial> MissingFor(AshesCost cost)
    {
        var missing = new List<AshesMaterial>();
        foreach (var need in cost.Materials)
            if (_world.CountMaterial(need.ItemId) < need.Count)
                missing.Add(need);

        return missing;
    }

    private void Pay(AshesCost cost)
    {
        foreach (var material in cost.Materials)
            if (!_world.TakeMaterial(material.ItemId, material.Count))
                _logger.LogWarning($"[FromTheAshes] could not take {material.Count} {material.ItemId} after the check passed");
    }

    /// <summary>
    /// Returns a project's materials to the party after a forced end (a cancel verdict or a fault). A rebuild paid nothing at
    /// its start. Needs <see cref="ICoopSessionProvider.IsAuthority"/> like every write, and never throws.
    /// </summary>
    private void Refund(AshesJob job)
    {
        if (job.Kind != AshesJobKind.ProjectHelp || !_coop.IsAuthority)
            return;
        foreach (var material in CostFor(AshesJobKind.ProjectHelp, FactionSide.Neutral).Materials)
        {
            try
            {
                _world.ReturnMaterial(material.ItemId, material.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[FromTheAshes] could not return {material.Count} {material.ItemId}: {ex.Message}");
            }
        }
    }

    private AshesStartCheck Blocked(AshesBlock block, AshesJobKind kind, AshesSiteSnapshot? site) =>
        new() { Block = block, Variant = VariantFor(kind, SideFor(kind, site)) };

    /// <summary>
    /// The player's side for a rebuild, read from the pair the adapter built for the player clan's map faction (#744, #957).
    /// Project help does not care, and a missing site is Neutral.
    /// </summary>
    private FactionSide SideFor(AshesJobKind kind, AshesSiteSnapshot? site)
    {
        if (kind == AshesJobKind.ProjectHelp || site == null)
            return FactionSide.Neutral;
        return _alignment.ResolveSide(site.PlayerSide);
    }

    private static AshesVariant VariantFor(AshesJobKind kind, FactionSide side) =>
        kind == AshesJobKind.ProjectHelp ? AshesVariant.Project
        : side == FactionSide.Evil ? AshesVariant.Thralls
        : AshesVariant.Free;

    private AshesVariantConfig ConfigFor(AshesVariant variant) =>
        variant == AshesVariant.Project ? _config.Project
        : variant == AshesVariant.Thralls ? _config.Thralls
        : _config.Rebuild;

    private static void AddMaterial(List<AshesMaterial> list, string itemId, int count)
    {
        if (count > 0)
            list.Add(new AshesMaterial(itemId, count));
    }

    private AshesResult Describe(AshesOutcome outcome, AshesJob job, AshesSiteSnapshot? site) => new()
    {
        Outcome = outcome,
        Kind = job.Kind,
        Variant = VariantFor(job.Kind, SideFor(job.Kind, site)),
        SettlementName = site?.SettlementName ?? "",
        ProjectName = string.IsNullOrEmpty(site?.ProjectName) ? job.LastProjectName : site!.ProjectName,
    };
}
