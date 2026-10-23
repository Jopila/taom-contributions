using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using MCM.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TAOM.Core.Validation;

namespace TAOM.Features.ServerSettings;

/// <summary>
/// Applies the operator's JSON to a fresh <see cref="ServerSettingsSet"/>. Pure: no IO, no logging, never throws.
/// Parse success is not validation success ("Config Providers MUST Validate"): each value is checked against its
/// setting's kind and MCM range, a bad one keeps the server default with a warning, and one summary warning
/// carries the count. A file that is not one JSON object changes nothing.
/// </summary>
internal static class ServerSettingsReader
{
    /// <summary>The longest Text value the file may set. MCM text settings here are hotkey names and short labels.</summary>
    internal const int MaxTextLength = 256;

    private static readonly JsonLoadSettings LoadSettings = new()
    {
        CommentHandling = CommentHandling.Ignore,
        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
        LineInfoHandling = LineInfoHandling.Load,
    };

    public static ServerSettingsReport Read(string? json, ServerSettingsSet target)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ServerSettingsReport(ServerFileState.Empty);

        JObject root;
        try
        {
            root = Parse(json!);
        }
        catch (Exception ex)
        {
            var malformed = new ServerSettingsReport(ServerFileState.Malformed);
            malformed.Add(ServerSettingsLevel.Error,
                "the settings file is not one valid JSON object (" + ex.Message + "); every setting keeps its server default");
            return malformed;
        }

        var report = new ServerSettingsReport(ServerFileState.Loaded);
        foreach (var property in root.Properties())
            Apply(property, target, report);

        if (report.Reverted > 0)
            report.Add(ServerSettingsLevel.Warning,
                report.Reverted + " value(s) reverted to their server defaults; the warnings above name each one");
        return report;
    }

    /// <summary>Null when <paramref name="token"/> is a valid value for <paramref name="d"/>, else the reason.</summary>
    /// <param name="value">The typed value to set: bool, int, float, string, or the dropdown index.</param>
    public static string? Validate(ServerSettingDescriptor d, JToken token, out object? value)
    {
        value = null;
        switch (d.Kind)
        {
            case ServerSettingKind.Bool:
                if (token.Type != JTokenType.Boolean) return "expects true or false, got " + Describe(token);
                value = token.Value<bool>();
                return null;

            case ServerSettingKind.Int:
                if (token.Type != JTokenType.Integer) return "expects a whole number, got " + Describe(token);
                if (((JValue)token).Value is BigInteger) return "is beyond the 32-bit integer range";
                var asLong = token.Value<long>();
                if (asLong < int.MinValue || asLong > int.MaxValue) return "is beyond the 32-bit integer range";
                if (asLong < d.Min || asLong > d.Max) return "must be from " + Range(d);
                value = (int)asLong;
                return null;

            case ServerSettingKind.Float:
                if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                    return "expects a number, got " + Describe(token);
                var raw = ((JValue)token).Value;
                var asDouble = raw is BigInteger big ? (double)big : Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                if (!FiniteFloatValidator.IsFinite(asDouble)) return "must be a finite number";
                var asFloat = (float)asDouble;
                if (!FiniteFloatValidator.IsFinite(asFloat)) return "is beyond the range of a float";
                // Compared as floats: the MCM bounds are float literals, so a compiled default equal to a bound passes.
                if (!FiniteFloatValidator.IsFiniteInRange(asFloat, (float)d.Min, (float)d.Max)) return "must be from " + Range(d);
                value = asFloat;
                return null;

            case ServerSettingKind.Text:
                if (token.Type != JTokenType.String) return "expects a string, got " + Describe(token);
                var text = token.Value<string>()!;
                if (text.Length > MaxTextLength) return "is longer than " + MaxTextLength + " characters";
                value = text;
                return null;

            case ServerSettingKind.Dropdown:
                if (token.Type != JTokenType.String)
                    return "expects one of " + Options(d) + " as a string, got " + Describe(token);
                var index = IndexOf(d, token.Value<string>()!);
                if (index < 0) return "must be one of " + Options(d) + " (case-sensitive)";
                value = index;
                return null;

            default:
                return "is a " + d.Kind.ToString().ToLowerInvariant() + " setting, which the file cannot set";
        }
    }

    private static JObject Parse(string json)
    {
        using var reader = new JsonTextReader(new StringReader(json))
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double,
        };
        var token = JToken.ReadFrom(reader, LoadSettings);
        while (reader.Read())
        {
            if (reader.TokenType != JsonToken.Comment)
                throw new JsonReaderException("content after the closing brace");
        }

        return token as JObject ?? throw new JsonReaderException("the top level is a " + token.Type + ", not an object");
    }

    private static void Apply(JProperty property, ServerSettingsSet target, ServerSettingsReport report)
    {
        if (!ServerSettingsSchema.ByName.TryGetValue(property.Name, out var d))
        {
            report.Unknown++;
            report.Add(ServerSettingsLevel.Warning, "unknown key '" + property.Name + "' ignored (keys are case-sensitive)");
            return;
        }

        if (d.Kind == ServerSettingKind.Button)
        {
            report.Add(ServerSettingsLevel.Warning, d.Name + " is a button, not a value; ignored");
            return;
        }

        if (d.Kind == ServerSettingKind.Parked)
        {
            report.Add(ServerSettingsLevel.Warning, d.Name + " is parked (MCM does not load it either); ignored");
            return;
        }

        // Keys are unique (duplicates are malformed), so the target still holds this key's server default.
        var serverDefault = Render(ServerSettingsDefaultsRenderer.DefaultToken(d, target));
        var instance = target.For(d.Owner);
        var reason = Validate(d, property.Value, out var value);
        if (reason != null)
        {
            report.Reverted++;
            report.Add(ServerSettingsLevel.Warning,
                d.Name + " = " + Render(property.Value) + " " + reason + "; kept the server default " + serverDefault);
            return;
        }

        if (d.Kind == ServerSettingKind.Dropdown)
            ((Dropdown<string>)d.Property.GetValue(instance)!).SelectedIndex = (int)value!;
        else
            d.Property.SetValue(instance, value);
        report.Applied++;
        report.Add(ServerSettingsLevel.Info, "set " + d.Name + " = " + Render(property.Value) + " (server default " + serverDefault + ")");
    }

    private static int IndexOf(ServerSettingDescriptor d, string option)
    {
        for (int i = 0; i < d.Options.Count; i++)
        {
            if (string.Equals(d.Options[i], option, StringComparison.Ordinal)) return i;
        }

        return -1;
    }

    private static string Range(ServerSettingDescriptor d) =>
        d.Min.ToString(CultureInfo.InvariantCulture) + " to " + d.Max.ToString(CultureInfo.InvariantCulture);

    private static string Options(ServerSettingDescriptor d) => "\"" + string.Join("\", \"", d.Options) + "\"";

    private static string Describe(JToken token) => token.Type.ToString().ToLowerInvariant();

    private static string Render(JToken token)
    {
        var text = token.ToString(Formatting.None);
        return text.Length <= 80 ? text : text.Substring(0, 77) + "...";
    }
}
