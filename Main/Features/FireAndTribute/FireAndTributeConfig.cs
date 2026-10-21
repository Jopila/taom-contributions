using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.Execution;

namespace TAOM.Features.FireAndTribute;

/// <summary>
/// <c>fire_and_tribute/fire_and_tribute.json</c>: the raiding hosts and the numbers behind them (#870). The tuning's
/// compiled values are the defaults a missing file, a parse failure or a rejected field falls back to; the hosts are
/// data only, so a file with no valid host makes the feature inert. Validated on load by
/// <see cref="FireAndTributeConfigProvider"/>.
/// </summary>
public sealed class FireAndTributeConfig
{
    public FireAndTributeTuning Tuning { get; set; } = new FireAndTributeTuning();

    public IReadOnlyList<RaidHostDef> Hosts { get; set; } = new List<RaidHostDef>();
}

/// <summary>The tuning block. Public setters so Json.NET fills it; <see cref="Unknown"/> catches keys this build does not know.</summary>
public sealed class FireAndTributeTuning
{
    public const int MaxDays = 3650;
    public const int MaxTroopCount = 2000;
    public const int MaxQuota = 100;
    public const float MaxStrengthPerRaid = 10000000f;
    public const float MaxHearth = 10000f;
    public const int MaxNearestVillages = 10;
    public const float MaxEngageRadius = 60f;
    public const float MaxEngageStrengthRatio = 5f;
    public const int MaxHostDaysLimit = 365;
    public const float MaxHomeRadius = 50f;
    public const float MaxSpawnRadiusDays = 3f;
    public const int MaxEnvoyUnitPrice = 1000000;
    public const int MaxEnvoyDelayHours = 720;
    public const int MaxEnvoyDelayDays = 60;

    /// <summary>Days after Full War begins before the first host may ride.</summary>
    public int FirstHostDelayDays { get; set; } = 45;

    /// <summary>The chance a day, per side with no host and no cooldown, that a host rides.</summary>
    public float SpawnChance { get; set; } = 0.05f;

    /// <summary>Days a side rests after its host is broken or goes home.</summary>
    public int CooldownDays { get; set; } = 120;

    /// <summary>With the War of the Ring switched off: the campaign day from which hosts may ride.</summary>
    public int WarlessStartDay { get; set; } = 89;

    public int MinTroops { get; set; } = 80;

    public int MaxTroops { get; set; } = 200;

    /// <summary>The host's strength as a share of the target kingdom's total strength.</summary>
    public float StrengthShare { get; set; } = 0.012f;

    /// <summary>Target strength per planned raid (the quota's base).</summary>
    public float StrengthPerRaid { get; set; } = 2000f;

    public int QuotaMin { get; set; } = 6;

    public int QuotaMax { get; set; } = 15;

    public int QuotaExtraMin { get; set; }

    public int QuotaExtraMax { get; set; } = 3;

    /// <summary>A village below this hearth is left alone.</summary>
    public float MinHearth { get; set; } = 150f;

    /// <summary>The host picks at random among this many of the nearest raidable villages.</summary>
    public int NearestVillages { get; set; } = 3;

    /// <summary>Map units within which the host engages a party of the target kingdom.</summary>
    public float EngageRadius { get; set; } = 8f;

    /// <summary>The host engages a party only up to this multiple of its own strength.</summary>
    public float EngageStrengthRatio { get; set; } = 0.8f;

    /// <summary>Days after which the host turns for home whatever its quota.</summary>
    public int MaxHostDays { get; set; } = 30;

    /// <summary>Map units from its hideout within which a returning host is dismissed.</summary>
    public float HomeRadius { get; set; } = 4f;

    /// <summary>The spawn point is found within this many days of travel of the anchor's gate, then retried around that point to get out of the player's sight, so it can land up to about twice this far away.</summary>
    public float SpawnRadiusDays { get; set; } = 0.25f;

    /// <summary>Denars per price unit of the tribute (a unit is half the player's villages, rounded up, at least one).</summary>
    public int EnvoyUnitPrice { get; set; } = 500;

    /// <summary>The envoy arrives between this many and <see cref="EnvoyMaxDelayHours"/> hours after the host rides.</summary>
    public int EnvoyMinDelayHours { get; set; } = 24;

    public int EnvoyMaxDelayHours { get; set; } = 48;

    /// <summary>Days the envoy waits when the player asks for time; the price stays and nothing is spared meanwhile.</summary>
    public int EnvoyDelayDays { get; set; } = 7;

    /// <summary>The chance a haggle succeeds at Charm 0.</summary>
    public float HaggleBaseChance { get; set; } = 0.20f;

    /// <summary>Added to the haggle chance for each point of the player's Charm.</summary>
    public float HaggleChancePerCharm { get; set; } = 0.002f;

    /// <summary>The haggle chance is kept between these two bounds.</summary>
    public float HaggleMinChance { get; set; } = 0.10f;

    public float HaggleMaxChance { get; set; } = 0.70f;

    [JsonExtensionData]
    public Dictionary<string, JToken>? Unknown { get; set; }
}

/// <summary>One validated host: who rides, from where, against whom, and with which troops.</summary>
public sealed class RaidHostDef
{
    public string Id { get; set; } = string.Empty;

    public FactionSide Side { get; set; }

    public string ClanId { get; set; } = string.Empty;

    /// <summary>The keyed name a host gets when its data carries none that validates (or the data no longer lists it).</summary>
    public const string FallbackName = "{=taom_fat_host_unknown}A raiding host";

    /// <summary>A keyed TextObject string: the key is "taom_fat_host_" plus the host id, then the English text.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Anchor settlement ids (towns or castles of the host's own side): the host appears near the gate of one of them, and
    /// out of the player's sight when the engine can find such a point.
    /// </summary>
    public IReadOnlyList<string> Anchors { get; set; } = new List<string>();

    public IReadOnlyList<FactionSide> TargetSides { get; set; } = new List<FactionSide>();

    /// <summary>Kingdom ids tried, in order, before the strongest.</summary>
    public IReadOnlyList<string> PreferredTargets { get; set; } = new List<string>();

    public IReadOnlyList<HostTroop> Troops { get; set; } = new List<HostTroop>();
}
