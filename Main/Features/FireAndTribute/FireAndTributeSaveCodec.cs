using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Core.Validation;
using TAOM.Features.Execution;

namespace TAOM.Features.FireAndTribute;

/// <summary>
/// The feature's save record as one JSON string: <c>{"v":1,"seen":day,"cool":{"Evil":day},
/// "hosts":[{"p","h","s","t","d","r","q","ret"}]}</c>, plus the tribute envoy's keys, written only when they hold
/// something: per host <c>due</c> (hour), <c>done</c>, <c>vil</c>, <c>price</c>, <c>hag</c> and <c>sp</c> (the spared clan),
/// and at the root <c>grudge</c> (side to kingdom id) and <c>seized</c>. Slice 1 saves have none of them and read as no
/// envoy, no grudge, none seized; the version stays 1. Pure. Parsing is forgiving by section, like the War Chronicle's: a
/// bad section resets alone with a warning, a bad row is skipped alone, and nothing here throws on save content. A host
/// row is skipped for an empty party, host or target id, an unknown side, a day below 0, a raid count outside 0 to 100000,
/// a quota outside 1 to 100, a party already listed, or a second host of one side. Null, empty or whitespace is an empty
/// record, not a fault: a save from before the feature never reaches SyncData.
/// </summary>
/// <remarks>
/// A host row dropped here, or a whole record dropped for an unknown version, leaves its party on the map still pinned: the
/// engine saves <c>DoNotMakeNewDecisions</c> with the party, and nothing is left to steer or release it. So a future version
/// bump must migrate the v1 rows into the new shape and never reset them, and every drop says so in its warning.
/// </remarks>
internal static class FireAndTributeSaveCodec
{
    internal const int Version = 1;
    internal const int MaxRaids = 100000;
    internal const int MaxQuota = 100;
    internal const int MaxVillages = 100000;
    internal const int MaxSeized = 100000;

    private const string PinNote = "; if its party is still on the map it keeps its saved pin and roams unsteered";

    private static readonly Regex IdShape = new Regex("^[A-Za-z0-9_]{1,64}$");

    internal static string Serialize(FireAndTributeState state)
    {
        var cool = new JObject();
        foreach (var pair in state.CooldownUntil.OrderBy(p => p.Key))
            cool[pair.Key.ToString()] = pair.Value;
        var hosts = new JArray();
        foreach (var host in state.Hosts)
        {
            var row = new JObject
            {
                ["p"] = host.PartyId,
                ["h"] = host.HostId,
                ["s"] = host.Side.ToString(),
                ["t"] = host.TargetKingdomId,
                ["d"] = host.StartDay,
                ["r"] = host.Raids,
                ["q"] = host.Quota,
                ["ret"] = host.Returning,
            };
            WriteEnvoy(row, host);
            hosts.Add(row);
        }

        var root = new JObject
        {
            ["v"] = Version,
            ["seen"] = state.WarSeenDay,
            ["cool"] = cool,
            ["hosts"] = hosts,
        };
        if (state.Grudges.Count > 0)
        {
            var grudge = new JObject();
            foreach (var pair in state.Grudges.OrderBy(p => p.Key))
                grudge[pair.Key.ToString()] = pair.Value;
            root["grudge"] = grudge;
        }
        if (state.EnvoysSeized > 0)
            root["seized"] = state.EnvoysSeized;
        return root.ToString(Formatting.None);
    }

    /// <summary>Writes only what differs from "no envoy yet", so a host with none serialises exactly as in slice 1.</summary>
    private static void WriteEnvoy(JObject row, RaidHostRecord host)
    {
        if (host.EnvoyDueHour >= 0)
            row["due"] = host.EnvoyDueHour;
        if (host.EnvoyDone)
            row["done"] = true;
        if (host.EnvoyVillages > 0)
            row["vil"] = host.EnvoyVillages;
        if (host.EnvoyPrice > 0)
            row["price"] = host.EnvoyPrice;
        if (host.EnvoyHaggled)
            row["hag"] = true;
        if (!string.IsNullOrEmpty(host.SparedClanId))
            row["sp"] = host.SparedClanId;
    }

    internal static FireAndTributeState Parse(string? json)
    {
        var state = new FireAndTributeState();
        if (string.IsNullOrWhiteSpace(json))
            return state;

        JToken token;
        try
        {
            using (var reader = new JsonTextReader(new StringReader(json!)) { DateParseHandling = DateParseHandling.None })
                token = JToken.Load(reader);
        }
        catch (JsonException ex)
        {
            state.Warnings.Add("the save record is not readable JSON, so everything is reset: " + ex.Message);
            return state;
        }

        if (!(token is JObject root))
        {
            state.Warnings.Add("the save record is not a JSON object, so everything is reset");
            return state;
        }
        // A pattern, never JToken's explicit long conversion: that throws OverflowException on an integer past 64 bits.
        if (!(root["v"] is JValue { Value: long version } && version == Version))
        {
            state.Warnings.Add("the save record has an unknown version, so everything is reset; any host party it listed keeps its saved pin and roams unsteered");
            return state;
        }

        ReadSeen(root, state);
        ReadCooldowns(root, state);
        ReadHosts(root, state);
        ReadGrudges(root, state);
        ReadSeized(root, state);
        return state;
    }

    private static void ReadSeized(JObject root, FireAndTributeState state)
    {
        var token = root["seized"];
        if (token == null)
            return;
        if (TryInt(token, 0, MaxSeized, out int seized))
            state.EnvoysSeized = seized;
        else
            state.Warnings.Add($"seized = {token} is not a count from 0 to {MaxSeized}, so it is reset");
    }

    private static void ReadGrudges(JObject root, FireAndTributeState state)
    {
        var section = root["grudge"];
        if (section == null)
            return;
        if (!(section is JObject map))
        {
            state.Warnings.Add("the grudge section is not an object, so it is reset");
            return;
        }
        foreach (var property in map.Properties())
        {
            if (!EnumNames.TryParse(property.Name, out FactionSide side))
                state.Warnings.Add($"grudge '{property.Name}' skipped: not a side");
            else if (!TryId(property.Value, out var kingdom))
                state.Warnings.Add($"grudge '{property.Name}' skipped: {property.Value} is not a kingdom id");
            else
                state.Grudges[side] = kingdom;
        }
    }

    private static void ReadSeen(JObject root, FireAndTributeState state)
    {
        var token = root["seen"];
        if (token == null)
            return;
        if (TryInt(token, -1, int.MaxValue, out int day))
            state.WarSeenDay = day;
        else
            state.Warnings.Add($"seen = {token} is not a day from -1 up, so it is reset");
    }

    private static void ReadCooldowns(JObject root, FireAndTributeState state)
    {
        var section = root["cool"];
        if (section == null)
            return;
        if (!(section is JObject map))
        {
            state.Warnings.Add("the cooldown section is not an object, so it is reset");
            return;
        }
        foreach (var property in map.Properties())
        {
            if (!EnumNames.TryParse(property.Name, out FactionSide side))
                state.Warnings.Add($"cooldown '{property.Name}' skipped: not a side");
            else if (!TryInt(property.Value, 0, int.MaxValue, out int day))
                state.Warnings.Add($"cooldown '{property.Name}' skipped: {property.Value} is not a day from 0 up");
            else
                state.CooldownUntil[side] = day;
        }
    }

    private static void ReadHosts(JObject root, FireAndTributeState state)
    {
        var section = root["hosts"];
        if (section == null)
            return;
        if (!(section is JArray rows))
        {
            state.Warnings.Add("the hosts section is not an array, so it is reset");
            return;
        }
        for (int i = 0; i < rows.Count; i++)
        {
            // A dropped row's party, if it is still on the map, keeps its saved pin (see the class remarks).
            if (!TryReadHost(rows[i], out var host, out var problem))
                state.Warnings.Add($"host row {i} skipped: {problem}{PinNote}");
            else if (state.Hosts.Any(h => h.PartyId == host!.PartyId))
                state.Warnings.Add($"host row {i} skipped: party '{host!.PartyId}' is already listed{PinNote}");
            else if (state.Hosts.Any(h => h.Side == host!.Side))
                state.Warnings.Add($"host row {i} skipped: {host!.Side} already has a host{PinNote}");
            else
            {
                ReadEnvoy(rows[i], i, host!, state);
                state.Hosts.Add(host!);
            }
        }
    }

    private static bool TryReadHost(JToken token, out RaidHostRecord? host, out string problem)
    {
        host = null;
        problem = string.Empty;
        if (!(token is JObject row))
        {
            problem = "it is not an object";
            return false;
        }
        if (!TryId(row["p"], out var party) || !TryId(row["h"], out var hostId) || !TryId(row["t"], out var target))
        {
            problem = "its party, host or target id is missing or not an id";
            return false;
        }
        if (!(row["s"] is JValue { Type: JTokenType.String } sv) || !EnumNames.TryParse((string?)sv.Value, out FactionSide side))
        {
            problem = $"its side ({row["s"]}) is not Free, Neutral or Evil";
            return false;
        }
        if (!TryInt(row["d"], 0, int.MaxValue, out int day))
        {
            problem = "its start day is not a day from 0 up";
            return false;
        }
        if (!TryInt(row["r"], 0, MaxRaids, out int raids) || !TryInt(row["q"], 1, MaxQuota, out int quota))
        {
            problem = "its raid count or quota is out of range";
            return false;
        }
        bool returning = row["ret"] is JValue { Type: JTokenType.Boolean } rv && (bool)rv.Value!;
        host = new RaidHostRecord
        {
            PartyId = party, HostId = hostId, Side = side, TargetKingdomId = target,
            StartDay = day, Raids = raids, Quota = quota, Returning = returning,
        };
        return true;
    }

    /// <summary>
    /// The envoy keys of a kept host row. Each resets alone with a warning when it does not read (the row is never dropped
    /// for them: a dropped row leaves its party pinned), so an envoy lost to a bad key simply never comes.
    /// </summary>
    private static void ReadEnvoy(JToken token, int index, RaidHostRecord host, FireAndTributeState state)
    {
        var row = (JObject)token;
        if (row["due"] != null)
        {
            if (TryInt(row["due"], 0, int.MaxValue, out int due))
                host.EnvoyDueHour = due;
            else
                Reset(state, index, "due", row["due"], "an hour from 0 up");
        }
        if (row["vil"] != null)
        {
            if (TryInt(row["vil"], 0, MaxVillages, out int villages))
                host.EnvoyVillages = villages;
            else
                Reset(state, index, "vil", row["vil"], $"a count from 0 to {MaxVillages}");
        }
        if (row["price"] != null)
        {
            if (TryInt(row["price"], 0, int.MaxValue, out int price))
                host.EnvoyPrice = price;
            else
                Reset(state, index, "price", row["price"], "a price from 0 up");
        }
        host.EnvoyDone = ReadFlag(row, "done", index, state);
        host.EnvoyHaggled = ReadFlag(row, "hag", index, state);
        if (row["sp"] != null)
        {
            if (TryId(row["sp"], out var clan))
                host.SparedClanId = clan;
            else
                Reset(state, index, "sp", row["sp"], "a clan id");
        }
    }

    private static bool ReadFlag(JObject row, string key, int index, FireAndTributeState state)
    {
        var token = row[key];
        if (token == null)
            return false;
        if (token is JValue { Type: JTokenType.Boolean } flag)
            return (bool)flag.Value!;
        Reset(state, index, key, token, "true or false");
        return false;
    }

    private static void Reset(FireAndTributeState state, int index, string key, JToken? value, string expected) =>
        state.Warnings.Add($"host row {index}: {key} = {value} is not {expected}, so it is reset");

    private static bool TryInt(JToken? token, int min, int max, out int number)
    {
        number = 0;
        if (!(token is JValue { Type: JTokenType.Integer } v) || !(v.Value is long raw) || raw < min || raw > max)
            return false;
        number = (int)raw;
        return true;
    }

    private static bool TryId(JToken? token, out string id)
    {
        id = string.Empty;
        if (!(token is JValue { Type: JTokenType.String } v) || !(v.Value is string text) || !IdShape.IsMatch(text))
            return false;
        id = text;
        return true;
    }
}
