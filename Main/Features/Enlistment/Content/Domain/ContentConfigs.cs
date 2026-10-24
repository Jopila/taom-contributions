using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Content.Domain;

/// <summary>Aggregate for enlistment_config.json (designer tunables — thresholds, tables, weights).</summary>
public sealed class EnlistmentContentConfig
{
    public ProgressionTables Progression { get; set; } = new ProgressionTables();
    public WagePolicyConfig WagePolicy { get; set; } = new WagePolicyConfig();
    public MeritScoringConfig MeritScoring { get; set; } = new MeritScoringConfig();
    public List<MeritBand> MeritBands { get; set; } = new List<MeritBand>();
    public SchedulerConfig Scheduler { get; set; } = new SchedulerConfig();
    public List<PromotionRequirement> Promotions { get; set; } = new List<PromotionRequirement>();
    public SitOutConfig SitOut { get; set; } = new SitOutConfig();

    /// <summary>
    /// Relation with the commander lost by deserting during the minimum term (#833). In [-100, 0];
    /// a value outside that range reverts to the default. Shipped in enlistment_config.json.
    /// </summary>
    public int DesertionRelationPenalty { get; set; } = -15;

    /// <summary>Honor trait XP lost by the same desertion (#833). In [-1000, 0], same validation.</summary>
    public int DesertionHonorXpPenalty { get; set; } = -100;

    /// <summary>Days a past contract stays in the service history (#837). In [1, 3650].</summary>
    public int ServiceHistoryExpiryDays { get; set; } = 730;

    /// <summary>Most past contracts kept; the oldest go first (#837). In [1, 50].</summary>
    public int ServiceHistoryMaxEntries { get; set; } = 10;

    /// <summary>Days after a desertion that the same commander refuses the oath (#837). In [0, 365]; 0 turns it off.</summary>
    public int DesertionCooldownDays { get; set; } = 60;

    /// <summary>Days of the current rank's wage paid as a bonus when the player signs on for another term (#836). In [0, 365].</summary>
    public int RenewalBonusWageDays { get; set; } = 30;

    /// <summary>Trust added when the player signs on for another term (#836). In [0, 100].</summary>
    public int RenewalTrust { get; set; } = 10;

    /// <summary>Most personal spoils (items from his own kills) a won battle can pay (#834). In [0, 10].</summary>
    public int SpoilsMaxItemsPerBattle { get; set; } = 3;

    /// <summary>Per-rank-ordinal raise of the spoils target value: target x (1 + scale x rank). Finite, in [0, 2].</summary>
    public double SpoilsValueScalePerRank { get; set; } = 0.25;

    /// <summary>Lower edge of the random factor on the spoils target value. Finite, 0 &lt;= min &lt;= max &lt;= 2.</summary>
    public double SpoilsRandomMin { get; set; } = 0.1;

    /// <summary>Upper edge of the random factor on the spoils target value. Finite, 0 &lt;= min &lt;= max &lt;= 2.</summary>
    public double SpoilsRandomMax { get; set; } = 0.9;
}

/// <summary>
/// The 'Leave it to the others' rule (#830). Shipped values are in the <c>sitOut</c> section of
/// enlistment_config.json and must equal these defaults (pinned by the config provider tests).
/// </summary>
public sealed class SitOutConfig
{
    /// <summary>Player side strength share at or above which a healthy soldier may stay back. In (0.5, 1].</summary>
    public float OverwhelmingShare { get; set; } = 0.85f;

    /// <summary>Trust docked when a healthy soldier uses that allowance. At least 0.</summary>
    public int TrustCost { get; set; } = 1;

    /// <summary>Multiplier on the base win/loss XP of a battle he sat out. In [0, 1].</summary>
    public float BaseXpFactor { get; set; } = 0.5f;
}

/// <summary>Aggregate for enlistment_duties.json (the duty/incident content tables).</summary>
public sealed class EnlistmentDutiesConfig
{
    public List<DutyDefinition> FieldDuties { get; set; } = new List<DutyDefinition>();
    public List<InteractiveDutyDefinition> InteractiveDuties { get; set; } = new List<InteractiveDutyDefinition>();
    public List<IncidentDefinition> Incidents { get; set; } = new List<IncidentDefinition>();
}

/// <summary>
/// Hourly-cached world snapshot for the duty scheduler (replaces the donor's
/// GetCurrentArmyRhythmState — 2 full MobileParty.All scans × 11 call sites per tick chain).
/// Pure data; the snapshot service owns the engine reads.
/// </summary>
public sealed class ArmyRhythmSnapshot
{
    public ServicePhase Phase { get; set; }
    public bool InArmy { get; set; }
    public bool SiegePressure { get; set; }
    public bool Naval { get; set; }
    public bool Blockade { get; set; }
    public bool LowSupplies { get; set; }
    public bool RecoveryState { get; set; }
    public bool QuietGarrison { get; set; }
    public bool Marching { get; set; }
    public bool PreBattle { get; set; }

    /// <summary>Faction-level war check (the donor's party-scan version was effectively always true).</summary>
    public bool ActiveCampaign { get; set; }

    public bool HighScrutiny { get; set; }
    public int WoundedCount { get; set; }
    public int RecruitCount { get; set; }
}

/// <summary>Progress counters the promotion evaluator + scheduler read (a view over the content store section).</summary>
public sealed class ServiceProgressSnapshot
{
    public ServiceRank Rank { get; set; }
    public ServiceAssignment Assignment { get; set; }
    public int DaysServed { get; set; }
    public int ServiceXp { get; set; }
    public int LeadershipSkill { get; set; }
    public int DutySuccesses { get; set; }
    public int DutyFailures { get; set; }
    public int Trust { get; set; }
    public int DeferredWages { get; set; }
}
