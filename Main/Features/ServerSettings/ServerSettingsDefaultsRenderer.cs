using System;
using System.Globalization;
using System.Linq;
using System.Text;
using MCM.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TAOM.Features.ServerSettings;

/// <summary>
/// The two texts TAOM writes beside the settings file: the one-time template, and the defaults reference that
/// lists every key at its server default. Pure and deterministic, so the store can skip an unchanged reference.
/// </summary>
internal static class ServerSettingsDefaultsRenderer
{
    private const string NewLine = "\n";

    /// <summary>Written once, when no settings file exists. Reads as zero keys.</summary>
    public static string Template() =>
        "// TAOM server settings. A dedicated server reads this file once at start: restart the server after an edit." + NewLine
        + "// Add only the keys you change, as \"Name\": value. Every key, its server default and its range is listed in" + NewLine
        + "// " + ServerSettingsFileStore.DefaultsFileName + " beside this file. Keys are case-sensitive." + NewLine
        + "{}" + NewLine;

    /// <summary>Every settable key at the server default in <paramref name="defaults"/>, each with a kind comment.</summary>
    public static string DefaultsReference(ServerSettingsSet defaults)
    {
        var settable = ServerSettingsSchema.All.Where(d => d.IsSettableFromFile).ToList();
        var sb = new StringBuilder();
        sb.Append("// ").Append(ServerSettingsFileStore.DefaultsFileName)
            .Append(": TAOM generates this file on every dedicated server boot and never reads it.").Append(NewLine);
        sb.Append("// It lists every setting at its server default (the compiled MCM default, except EnableHitchProbe, off on a server).").Append(NewLine);
        sb.Append("// Copy only the keys you change into ")
            .Append(ServerSettingsFileStore.FileName).Append(NewLine);
        sb.Append("// so a later TAOM default still reaches every key you did not set.").Append(NewLine);
        sb.Append('{').Append(NewLine);

        Type? owner = null;
        for (int i = 0; i < settable.Count; i++)
        {
            var d = settable[i];
            if (d.Owner != owner)
            {
                owner = d.Owner;
                sb.Append("  // ").Append(owner.Name).Append(NewLine);
            }

            sb.Append("  ").Append(JsonConvert.ToString(d.Name)).Append(": ")
                .Append(DefaultToken(d, defaults).ToString(Formatting.None))
                .Append(i < settable.Count - 1 ? "," : "")
                .Append(" /* ").Append(Comment(d)).Append(" */").Append(NewLine);
        }

        sb.Append('}').Append(NewLine);
        return sb.ToString();
    }

    /// <summary>The value <paramref name="set"/> holds for <paramref name="d"/>, as the JSON token the file would carry.</summary>
    public static JToken DefaultToken(ServerSettingDescriptor d, ServerSettingsSet set)
    {
        var value = d.Property.GetValue(set.For(d.Owner));
        switch (d.Kind)
        {
            case ServerSettingKind.Bool: return new JValue((bool)value!);
            case ServerSettingKind.Int: return new JValue((long)(int)value!);
            case ServerSettingKind.Float: return new JValue((float)value!);
            case ServerSettingKind.Text: return new JValue((string?)value);
            case ServerSettingKind.Dropdown:
                var dropdown = (Dropdown<string>?)value;
                return dropdown != null && dropdown.SelectedIndex >= 0 && dropdown.SelectedIndex < dropdown.Count
                    ? new JValue(dropdown[dropdown.SelectedIndex])
                    : JValue.CreateNull();
            default: return JValue.CreateNull();
        }
    }

    private static string Comment(ServerSettingDescriptor d)
    {
        string kind = d.Kind switch
        {
            ServerSettingKind.Bool => "bool",
            ServerSettingKind.Int => "int, " + Range(d),
            ServerSettingKind.Float => "float, " + Range(d),
            ServerSettingKind.Text => "text, at most " + ServerSettingsReader.MaxTextLength + " characters",
            ServerSettingKind.Dropdown => "one of " + string.Join(" | ", d.Options.Select(o => JsonConvert.ToString(o))),
            _ => d.Kind.ToString().ToLowerInvariant(),
        };
        var text = d.Group.Length == 0 ? kind : kind + ", " + d.Group;
        // A "*/" inside a group or option name would end the comment early.
        return text.Replace("*/", "* /");
    }

    private static string Range(ServerSettingDescriptor d) =>
        d.Min.ToString(CultureInfo.InvariantCulture) + " to " + d.Max.ToString(CultureInfo.InvariantCulture);
}
