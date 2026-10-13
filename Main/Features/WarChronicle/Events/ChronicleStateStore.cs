using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Core.Logging;
using TAOM.Core.Validation;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// The chronicle's per-campaign state: one <see cref="ChronicleEventRecord"/> per event, saved as the
/// <c>chronicle</c> array of the War Chronicle payload (<see cref="WarChronicleStateService"/>). Row shape:
/// <c>{"id","s","open","close","a","res"}</c>, the state as its int, absent days left out.
///
/// <para>Forgiving by row, like the codec's other sections: a row that is not an object, has no id, an
/// undefined state, an Active state without a finite window, or a non-number day is skipped alone with a
/// warning. The adoption rule then re-creates it at session launch. A row for an id the configuration no
/// longer lists is kept, so a save from a later release survives a load in an earlier one.</para>
///
/// <para>A process-lifetime singleton holding campaign state: <see cref="ResetForNewSession"/> runs from
/// <c>WarChronicleStateService.ResetForNewSession</c>, which the behavior's constructor calls.</para>
/// </summary>
public sealed class ChronicleStateStore
{
    private readonly IModLogger _logger;
    private readonly Dictionary<string, ChronicleEventRecord> _records = new Dictionary<string, ChronicleEventRecord>(StringComparer.Ordinal);

    public ChronicleStateStore(IModLogger logger)
    {
        _logger = logger;
    }

    public IReadOnlyDictionary<string, ChronicleEventRecord> Records => _records;

    public ChronicleEventRecord? Get(string? id) =>
        id != null && _records.TryGetValue(id, out var record) ? record : null;

    /// <summary>Adds the record, or replaces the one with the same id.</summary>
    public void Set(ChronicleEventRecord record)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.Id))
            return;
        _records[record.Id] = record;
    }

    public string ToJson()
    {
        var rows = new JArray();
        foreach (var record in _records.Values)
        {
            var row = new JObject { ["id"] = record.Id, ["s"] = (int)record.State };
            Day(row, "open", record.OpenDay);
            Day(row, "close", record.CloseDay);
            if (record.Assaulted)
                row["a"] = true;
            Day(row, "res", record.ResolvedDay);
            rows.Add(row);
        }

        return rows.ToString(Formatting.None);
    }

    /// <summary>Replaces the store with the saved rows; null, empty or "[]" is a save with no chronicle section.</summary>
    public void RestoreFromJson(string? json)
    {
        _records.Clear();
        if (string.IsNullOrWhiteSpace(json))
            return;

        JToken token;
        try
        {
            using (var reader = new JsonTextReader(new StringReader(json!)) { DateParseHandling = DateParseHandling.None })
                token = JToken.Load(reader);
        }
        catch (JsonException ex)
        {
            Warn("the chronicle section is not readable JSON, so it is reset: " + ex.Message);
            return;
        }

        if (!(token is JArray rows))
        {
            Warn("the chronicle section is not an array, so it is reset");
            return;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            if (!TryRead(rows[i], out var record, out var problem))
            {
                Warn($"chronicle row {i} skipped: {problem}");
                continue;
            }

            if (_records.ContainsKey(record!.Id))
            {
                Warn($"chronicle row {i} skipped: a duplicate of '{record.Id}'");
                continue;
            }

            _records[record.Id] = record;
        }
    }

    public void ResetForNewSession() => _records.Clear();

    private static void Day(JObject row, string key, double? value)
    {
        if (value.HasValue && FiniteFloatValidator.IsFinite(value.Value))
            row[key] = value.Value;
    }

    private static bool TryRead(JToken token, out ChronicleEventRecord? record, out string problem)
    {
        record = null;
        problem = string.Empty;
        if (!(token is JObject row))
        {
            problem = "it is not an object";
            return false;
        }

        var id = row["id"] is JValue idValue && idValue.Type == JTokenType.String ? (string?)idValue.Value : null;
        if (string.IsNullOrWhiteSpace(id))
        {
            problem = "its id is missing or empty";
            return false;
        }

        var stateToken = row["s"];
        if (stateToken == null || stateToken.Type != JTokenType.Integer
            || !(((JValue)stateToken).Value is long raw) || raw < int.MinValue || raw > int.MaxValue
            || !Enum.IsDefined(typeof(ChronicleEventState), (int)raw))
        {
            problem = $"'{id}' has a state this build does not know ({stateToken})";
            return false;
        }

        if (!TryDay(row, "open", out var open) || !TryDay(row, "close", out var close) || !TryDay(row, "res", out var resolved))
        {
            problem = $"'{id}' has a window or resolution day (open, close or res) that is not a finite number";
            return false;
        }

        var state = (ChronicleEventState)(int)raw;
        if (state == ChronicleEventState.Active && (!open.HasValue || !close.HasValue))
        {
            problem = $"'{id}' is Active without a finite window";
            return false;
        }

        var assaulted = row["a"] is JValue a && a.Type == JTokenType.Boolean && (bool)a.Value!;
        record = new ChronicleEventRecord(id!, state) { OpenDay = open, CloseDay = close, Assaulted = assaulted, ResolvedDay = resolved };
        return true;
    }

    /// <summary>False when the key holds something other than a finite number; an absent key is a null day.</summary>
    private static bool TryDay(JObject row, string key, out double? day)
    {
        day = null;
        var token = row[key];
        if (token == null || token.Type == JTokenType.Null)
            return true;
        if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float)
            return false;

        double value;
        try
        {
            value = Convert.ToDouble(((JValue)token).Value, CultureInfo.InvariantCulture);
        }
        catch (InvalidCastException)
        {
            return false;
        }

        if (!FiniteFloatValidator.IsFinite(value))
            return false;
        day = value;
        return true;
    }

    private void Warn(string message) => _logger.LogWarning("[WarChronicle] Save: " + message + ".");
}
