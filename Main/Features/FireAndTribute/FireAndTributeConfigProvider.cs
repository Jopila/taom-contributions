using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TAOM.Features.EyeOfSauron;
using TAOM.Features.Execution;

namespace TAOM.Features.FireAndTribute;

/// <summary>
/// Loads and validates <c>fire_and_tribute/fire_and_tribute.json</c> (csharp-architecture.md "Config Providers MUST
/// Validate"). A missing file gives the compiled tuning and no hosts with a warning, a parse failure the same with an
/// error. A parseable but invalid tuning field reverts alone to its default (an ordered pair reverts together), an
/// unknown key is warned about and ignored, and an invalid host or host field is skipped or dropped with a warning,
/// never applied. Every float goes through <see cref="FiniteFloatValidator"/> before any range check. Cached for the
/// process (Reuse.Singleton): an edit needs a full restart.
/// </summary>
public sealed class FireAndTributeConfigProvider : IFireAndTributeConfigProvider
{
    /// <summary>The outlaw clans a host may use: the eight that characters/clans.xml defines for this feature.</summary>
    internal static readonly string[] AllowedClans =
    {
        "gundabad_raiders", "rhun_raiders", "harad_raiders", "dunland_raiders",
        "umbar_corsairs", "gondor_soldiers", "erebor_warriors", "mirkwood_stalkers",
    };

    internal const float ShareSumTolerance = 0.01f;
    internal const int MaxNameLength = 120;
    internal const int MaxAnchors = 12;
    internal const int MaxTroops = 12;
    internal const int MaxPreferred = 12;

    private static readonly Regex IdShape = new Regex("^[a-z][a-z0-9_]{0,63}$");
    private static readonly Regex SettlementShape = new Regex("^[A-Za-z][A-Za-z0-9_]{0,63}$");
    private static readonly Regex NameText = new Regex(@"^[^{}]+$");

    private readonly IPathService _pathService;
    private readonly IModLogger _logger;
    private readonly Lazy<FireAndTributeConfig> _config;
    private bool _rejected;

    public FireAndTributeConfigProvider(IPathService pathService, IModLogger logger)
    {
        _pathService = pathService;
        _logger = logger;
        _config = new Lazy<FireAndTributeConfig>(Load);
    }

    public FireAndTributeConfig GetConfig() => _config.Value;

    private FireAndTributeConfig Load()
    {
        var path = Path.Combine(_pathService.ModuleDataPath, "fire_and_tribute", "fire_and_tribute.json");
        if (!File.Exists(path))
        {
            _logger.LogWarning($"FireAndTributeConfigProvider: fire_and_tribute.json not found at {path}, using the compiled tuning and no hosts");
            return new FireAndTributeConfig();
        }

        RawFile? parsed;
        try
        {
            parsed = JsonConvert.DeserializeObject<RawFile>(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex)
        {
            _logger.LogError($"FireAndTributeConfigProvider: Failed to parse fire_and_tribute.json: {ex.Message}");
            return new FireAndTributeConfig();
        }

        return Validate(parsed ?? new RawFile());
    }

    private FireAndTributeConfig Validate(RawFile raw)
    {
        _rejected = false;
        WarnUnknown("the file", raw.Unknown);
        var tuning = ValidateTuning(raw.Tuning ?? new FireAndTributeTuning());
        var hosts = ValidateHosts(raw.Hosts);

        if (hosts.Count == 0)
        {
            _logger.LogWarning("FireAndTributeConfigProvider: fire_and_tribute.json holds no valid host, so no host will ride");
            _rejected = true;
        }
        if (_rejected)
            _logger.LogWarning("FireAndTributeConfigProvider: fire_and_tribute.json contained invalid values. See prior warnings for details.");
        else
            _logger.LogInfo($"FireAndTributeConfigProvider: Loaded fire_and_tribute.json ({hosts.Count} host(s))");
        return new FireAndTributeConfig { Tuning = tuning, Hosts = hosts };
    }

    // ---- tuning ----------------------------------------------------------------------------------------------------

    private FireAndTributeTuning ValidateTuning(FireAndTributeTuning t)
    {
        WarnUnknown("tuning", t.Unknown);
        var d = new FireAndTributeTuning();
        var s = new FireAndTributeTuning
        {
            FirstHostDelayDays = Count("firstHostDelayDays", t.FirstHostDelayDays, 0, FireAndTributeTuning.MaxDays, d.FirstHostDelayDays),
            SpawnChance = Float("spawnChance", t.SpawnChance, 0f, 1f, false, d.SpawnChance),
            CooldownDays = Count("cooldownDays", t.CooldownDays, 0, FireAndTributeTuning.MaxDays, d.CooldownDays),
            WarlessStartDay = Count("warlessStartDay", t.WarlessStartDay, 0, FireAndTributeTuning.MaxDays, d.WarlessStartDay),
            MinTroops = Count("minTroops", t.MinTroops, 1, FireAndTributeTuning.MaxTroopCount, d.MinTroops),
            MaxTroops = Count("maxTroops", t.MaxTroops, 1, FireAndTributeTuning.MaxTroopCount, d.MaxTroops),
            StrengthShare = Float("strengthShare", t.StrengthShare, 0f, 1f, true, d.StrengthShare),
            StrengthPerRaid = Float("strengthPerRaid", t.StrengthPerRaid, 0f, FireAndTributeTuning.MaxStrengthPerRaid, true, d.StrengthPerRaid),
            QuotaMin = Count("quotaMin", t.QuotaMin, 1, FireAndTributeTuning.MaxQuota, d.QuotaMin),
            QuotaMax = Count("quotaMax", t.QuotaMax, 1, FireAndTributeTuning.MaxQuota, d.QuotaMax),
            QuotaExtraMin = Count("quotaExtraMin", t.QuotaExtraMin, 0, FireAndTributeTuning.MaxQuota, d.QuotaExtraMin),
            QuotaExtraMax = Count("quotaExtraMax", t.QuotaExtraMax, 0, FireAndTributeTuning.MaxQuota, d.QuotaExtraMax),
            MinHearth = Float("minHearth", t.MinHearth, 0f, FireAndTributeTuning.MaxHearth, false, d.MinHearth),
            NearestVillages = Count("nearestVillages", t.NearestVillages, 1, FireAndTributeTuning.MaxNearestVillages, d.NearestVillages),
            EngageRadius = Float("engageRadius", t.EngageRadius, 0f, FireAndTributeTuning.MaxEngageRadius, true, d.EngageRadius),
            EngageStrengthRatio = Float("engageStrengthRatio", t.EngageStrengthRatio, 0f, FireAndTributeTuning.MaxEngageStrengthRatio, true, d.EngageStrengthRatio),
            MaxHostDays = Count("maxHostDays", t.MaxHostDays, 1, FireAndTributeTuning.MaxHostDaysLimit, d.MaxHostDays),
            HomeRadius = Float("homeRadius", t.HomeRadius, 0f, FireAndTributeTuning.MaxHomeRadius, true, d.HomeRadius),
            SpawnRadiusDays = Float("spawnRadiusDays", t.SpawnRadiusDays, 0f, FireAndTributeTuning.MaxSpawnRadiusDays, true, d.SpawnRadiusDays),
            EnvoyUnitPrice = Count("envoyUnitPrice", t.EnvoyUnitPrice, 1, FireAndTributeTuning.MaxEnvoyUnitPrice, d.EnvoyUnitPrice),
            EnvoyMinDelayHours = Count("envoyMinDelayHours", t.EnvoyMinDelayHours, 1, FireAndTributeTuning.MaxEnvoyDelayHours, d.EnvoyMinDelayHours),
            EnvoyMaxDelayHours = Count("envoyMaxDelayHours", t.EnvoyMaxDelayHours, 1, FireAndTributeTuning.MaxEnvoyDelayHours, d.EnvoyMaxDelayHours),
            EnvoyDelayDays = Count("envoyDelayDays", t.EnvoyDelayDays, 1, FireAndTributeTuning.MaxEnvoyDelayDays, d.EnvoyDelayDays),
            HaggleBaseChance = Float("haggleBaseChance", t.HaggleBaseChance, 0f, 1f, false, d.HaggleBaseChance),
            HaggleChancePerCharm = Float("haggleChancePerCharm", t.HaggleChancePerCharm, 0f, 1f, false, d.HaggleChancePerCharm),
            HaggleMinChance = Float("haggleMinChance", t.HaggleMinChance, 0f, 1f, false, d.HaggleMinChance),
            HaggleMaxChance = Float("haggleMaxChance", t.HaggleMaxChance, 0f, 1f, false, d.HaggleMaxChance),
        };

        if (s.MinTroops > s.MaxTroops)
        {
            Reject("minTroops/maxTroops", $"{s.MinTroops}/{s.MaxTroops}", "an ordered pair (min at most max)", $"{d.MinTroops}/{d.MaxTroops}");
            s.MinTroops = d.MinTroops;
            s.MaxTroops = d.MaxTroops;
        }
        if (s.QuotaMin > s.QuotaMax)
        {
            Reject("quotaMin/quotaMax", $"{s.QuotaMin}/{s.QuotaMax}", "an ordered pair (min at most max)", $"{d.QuotaMin}/{d.QuotaMax}");
            s.QuotaMin = d.QuotaMin;
            s.QuotaMax = d.QuotaMax;
        }
        if (s.QuotaExtraMin > s.QuotaExtraMax)
        {
            Reject("quotaExtraMin/quotaExtraMax", $"{s.QuotaExtraMin}/{s.QuotaExtraMax}", "an ordered pair (min at most max)", $"{d.QuotaExtraMin}/{d.QuotaExtraMax}");
            s.QuotaExtraMin = d.QuotaExtraMin;
            s.QuotaExtraMax = d.QuotaExtraMax;
        }
        if (s.EnvoyMinDelayHours > s.EnvoyMaxDelayHours)
        {
            Reject("envoyMinDelayHours/envoyMaxDelayHours", $"{s.EnvoyMinDelayHours}/{s.EnvoyMaxDelayHours}", "an ordered pair (min at most max)", $"{d.EnvoyMinDelayHours}/{d.EnvoyMaxDelayHours}");
            s.EnvoyMinDelayHours = d.EnvoyMinDelayHours;
            s.EnvoyMaxDelayHours = d.EnvoyMaxDelayHours;
        }
        if (s.HaggleMinChance > s.HaggleMaxChance)
        {
            var shown = System.Globalization.CultureInfo.InvariantCulture;
            Reject("haggleMinChance/haggleMaxChance", $"{s.HaggleMinChance.ToString(shown)}/{s.HaggleMaxChance.ToString(shown)}", "an ordered pair (min at most max)",
                $"{d.HaggleMinChance.ToString(shown)}/{d.HaggleMaxChance.ToString(shown)}");
            s.HaggleMinChance = d.HaggleMinChance;
            s.HaggleMaxChance = d.HaggleMaxChance;
        }
        return s;
    }

    /// <summary>Finite first, then in [min, max]; <paramref name="aboveMin"/> makes the lower bound exclusive.</summary>
    private float Float(string name, float value, float min, float max, bool aboveMin, float fallback)
    {
        if (FiniteFloatValidator.IsFiniteInRange(value, min, max) && !(aboveMin && value <= min))
            return value;
        string range = aboveMin ? $"above {min} and at most {max}" : $"in [{min}, {max}]";
        return Reject(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture), $"a finite value {range}", fallback);
    }

    private float Reject(string name, string shown, string expected, float fallback)
    {
        Reject(name, shown, expected, fallback.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return fallback;
    }

    private void Reject(string name, string shown, string expected, string fallback)
    {
        _logger.LogWarning($"FireAndTributeConfigProvider: {name} = {shown} is not {expected}, reverting to {fallback}");
        _rejected = true;
    }

    private int Count(string name, int value, int min, int max, int fallback)
    {
        if (value >= min && value <= max)
            return value;
        Reject(name, value.ToString(System.Globalization.CultureInfo.InvariantCulture), $"in [{min}, {max}]", fallback.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return fallback;
    }

    private void WarnUnknown(string where, Dictionary<string, JToken>? unknown)
    {
        if (unknown == null)
            return;
        foreach (var key in unknown.Keys)
        {
            _logger.LogWarning($"FireAndTributeConfigProvider: unknown key '{key}' in {where}, ignoring it");
            _rejected = true;
        }
    }

    // ---- hosts -----------------------------------------------------------------------------------------------------

    private List<RaidHostDef> ValidateHosts(List<RawHost?>? raw)
    {
        var hosts = new List<RaidHostDef>();
        if (raw == null)
            return hosts;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < raw.Count; i++)
        {
            var row = raw[i];
            if (row == null)
            {
                SkipHost($"#{i}", "the row is null");
                continue;
            }
            WarnUnknown($"host '{row.Id}'", row.Unknown);
            var def = ValidateHost(row, i);
            if (def == null)
                continue;
            if (!seen.Add(def.Id))
            {
                SkipHost(def.Id, "its id is already used by an earlier host");
                continue;
            }
            hosts.Add(def);
        }
        return hosts;
    }

    private void SkipHost(string id, string why)
    {
        _logger.LogWarning($"FireAndTributeConfigProvider: host {id} skipped: {why}");
        _rejected = true;
    }

    private RaidHostDef? ValidateHost(RawHost row, int index)
    {
        string label = string.IsNullOrEmpty(row.Id) ? $"#{index}" : row.Id!;
        if (row.Id == null || !IdShape.IsMatch(row.Id))
        {
            SkipHost(label, "its id is missing or not lowercase letters, digits and underscores");
            return null;
        }
        if (!EnumNames.TryParse(row.Side, out FactionSide side))
        {
            SkipHost(label, $"its side '{row.Side}' is not Free, Neutral or Evil");
            return null;
        }
        if (row.Clan == null || !AllowedClans.Contains(row.Clan, StringComparer.Ordinal))
        {
            SkipHost(label, $"its clan '{row.Clan}' is not one of {string.Join(", ", AllowedClans)}");
            return null;
        }

        var anchors = Ids(label, "anchors", row.Anchors, SettlementShape, MaxAnchors);
        if (anchors.Count == 0)
        {
            SkipHost(label, "it names no usable anchor settlement");
            return null;
        }
        var targetSides = TargetSides(label, side, row.TargetSides);
        if (targetSides.Count == 0)
        {
            SkipHost(label, "it has no usable target side");
            return null;
        }
        var troops = Troops(label, row.Troops);
        if (troops == null)
            return null;

        return new RaidHostDef
        {
            Id = row.Id,
            Side = side,
            ClanId = row.Clan,
            Name = Name(row.Id, row.Name),
            Anchors = anchors,
            TargetSides = targetSides,
            PreferredTargets = Ids(label, "preferredTargets", row.PreferredTargets, IdShape, MaxPreferred),
            Troops = troops,
        };
    }

    /// <summary>The host's own key (a copied key would show another host's text), then plain English text.</summary>
    private string Name(string id, string? name)
    {
        string key = $"{{=taom_fat_host_{id}}}";
        if (name != null && name.Length <= MaxNameLength && name.StartsWith(key, StringComparison.Ordinal)
            && NameText.IsMatch(name.Substring(key.Length)))
            return name;
        _logger.LogWarning($"FireAndTributeConfigProvider: host {id}: name '{name}' is not '{key}English text' within {MaxNameLength} characters, using a generic name");
        _rejected = true;
        return RaidHostDef.FallbackName;
    }

    private List<string> Ids(string label, string field, List<string?>? values, Regex shape, int cap)
    {
        var kept = new List<string>();
        if (values == null)
            return kept;
        foreach (var value in values)
        {
            var id = value?.Trim();
            if (id == null || !shape.IsMatch(id))
                Drop(label, field, $"'{value}' is not a valid id");
            else if (kept.Contains(id, StringComparer.Ordinal))
                Drop(label, field, $"'{id}' is listed twice");
            else if (kept.Count >= cap)
                Drop(label, field, $"'{id}' is past the {cap} entries allowed");
            else
                kept.Add(id);
        }
        return kept;
    }

    private void Drop(string label, string field, string why)
    {
        _logger.LogWarning($"FireAndTributeConfigProvider: host {label}: {field} entry dropped: {why}");
        _rejected = true;
    }

    private List<FactionSide> TargetSides(string label, FactionSide own, List<string?>? values)
    {
        var kept = new List<FactionSide>();
        if (values == null)
            return kept;
        foreach (var value in values)
        {
            if (!EnumNames.TryParse(value, out FactionSide side))
                Drop(label, "targetSides", $"'{value}' is not Free, Neutral or Evil");
            else if (side == FactionSide.Neutral)
                Drop(label, "targetSides", "a host never raids a Neutral realm");
            else if (side == own)
                Drop(label, "targetSides", "a host never raids its own side");
            else if (!kept.Contains(side))
                kept.Add(side);
        }
        return kept;
    }

    private List<HostTroop>? Troops(string label, List<RawTroop?>? values)
    {
        var kept = new List<HostTroop>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var troop in values ?? new List<RawTroop?>())
        {
            if (troop == null)
            {
                Drop(label, "troops", "a row is null");
                continue;
            }
            WarnUnknown($"a troop of host '{label}'", troop.Unknown);
            if (troop.Id == null || !IdShape.IsMatch(troop.Id))
                Drop(label, "troops", $"'{troop.Id}' is not a valid troop id");
            else if (!FiniteFloatValidator.IsFiniteInRange(troop.Share, 0f, 1f) || troop.Share <= 0f)
                Drop(label, "troops", $"'{troop.Id}' has share {troop.Share}, which is not a finite value above 0 and at most 1");
            else if (!seen.Add(troop.Id))
                Drop(label, "troops", $"'{troop.Id}' is listed twice");
            else if (kept.Count >= MaxTroops)
                Drop(label, "troops", $"'{troop.Id}' is past the {MaxTroops} troops allowed");
            else
                kept.Add(new HostTroop(troop.Id, troop.Share));
        }

        if (kept.Count == 0)
        {
            SkipHost(label, "it has no usable troop");
            return null;
        }
        float sum = kept.Sum(t => t.Share);
        if (!(Math.Abs(sum - 1f) <= ShareSumTolerance))
        {
            SkipHost(label, $"its troop shares sum to {sum}, not 1");
            return null;
        }
        return kept;
    }

    // ---- raw JSON shapes -------------------------------------------------------------------------------------------

    private sealed class RawFile
    {
        public FireAndTributeTuning? Tuning { get; set; }

        public List<RawHost?>? Hosts { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JToken>? Unknown { get; set; }
    }

    private sealed class RawHost
    {
        public string? Id { get; set; }

        public string? Side { get; set; }

        public string? Clan { get; set; }

        public string? Name { get; set; }

        public List<string?>? Anchors { get; set; }

        public List<string?>? TargetSides { get; set; }

        public List<string?>? PreferredTargets { get; set; }

        public List<RawTroop?>? Troops { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JToken>? Unknown { get; set; }
    }

    private sealed class RawTroop
    {
        public string? Id { get; set; }

        public float Share { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JToken>? Unknown { get; set; }
    }
}
