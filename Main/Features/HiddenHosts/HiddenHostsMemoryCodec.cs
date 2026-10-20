using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TAOM.Features.HiddenHosts;

/// <summary>What a decode did (#846 phase 3).</summary>
internal readonly struct MemoryDecodeReport
{
    internal MemoryDecodeReport(int loaded, int invalid, bool unknownFormat, bool capReached)
    {
        Loaded = loaded;
        Invalid = invalid;
        UnknownFormat = unknownFormat;
        CapReached = capReached;
    }

    /// <summary>Rows stored.</summary>
    internal int Loaded { get; }

    /// <summary>Rows skipped because a field was missing, malformed or out of range.</summary>
    internal int Invalid { get; }

    /// <summary>The payload did not start with the header this build writes: a foreign or future format, nothing was loaded.</summary>
    internal bool UnknownFormat { get; }

    /// <summary>A table was full: the rest of the payload was not read.</summary>
    internal bool CapReached { get; }
}

/// <summary>
/// The save text of the memory (#846 phase 3). One header line, then one row per line:
/// <c>p|id|count|known|stampHour|fadeHour|xpStage|xpDay</c> for a party, <c>s|...</c> for a settlement. Pure and strict: a
/// save is foreign input (a downgrade, an edit, a truncation), so a bad row is skipped and counted, an unknown header loads
/// nothing, and no row can carry a value the store would not itself produce. Only the id is escaped (<c>%</c>, <c>|</c>,
/// newline and carriage return); every other character, non-ASCII included, passes through.
/// </summary>
internal static class HiddenHostsMemoryCodec
{
    /// <summary>The version tag. A different header is a foreign or future format.</summary>
    internal const string Header = "HH1";

    internal const int MaxIdLength = 256;
    internal const int MaxCount = 1_000_000;
    private const int FieldCount = 8;
    private const int MaxXpStage = 3;

    internal static string Encode(HiddenHostsMemory memory)
    {
        var text = new StringBuilder(Header);
        Append(text, 'p', memory.Rows(MemoryKind.Party));
        Append(text, 's', memory.Rows(MemoryKind.Settlement));
        return text.ToString();
    }

    /// <summary>Stores every valid row of <paramref name="payload"/> into <paramref name="into"/>, which the caller has cleared.</summary>
    internal static MemoryDecodeReport Decode(string? payload, HiddenHostsMemory into)
    {
        if (string.IsNullOrEmpty(payload)) return default;

        var lines = payload!.Split('\n');
        if (lines[0] != Header) return new MemoryDecodeReport(0, 0, unknownFormat: true, capReached: false);

        int loaded = 0, invalid = 0;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;
            if (!TryParseRow(lines[i], out var kind, out var id, out var row))
            {
                invalid++;
                continue;
            }
            if (!into.Put(kind, id, row)) return new MemoryDecodeReport(loaded, invalid, unknownFormat: false, capReached: true);
            loaded++;
        }
        return new MemoryDecodeReport(loaded, invalid, unknownFormat: false, capReached: false);
    }

    /// <summary>Escapes the four characters a row cannot carry raw. <c>%</c> is its own escape.</summary>
    internal static string EscapeId(string id)
    {
        var text = new StringBuilder(id.Length + 8);
        foreach (char c in id)
        {
            switch (c)
            {
                case '%': text.Append("%25"); break;
                case '|': text.Append("%7C"); break;
                case '\n': text.Append("%0A"); break;
                case '\r': text.Append("%0D"); break;
                default: text.Append(c); break;
            }
        }
        return text.ToString();
    }

    /// <summary>Reverses <see cref="EscapeId"/>; any <c>%</c> that does not start one of its four sequences makes the id invalid.</summary>
    internal static bool TryUnescapeId(string text, out string id)
    {
        id = string.Empty;
        var result = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c != '%')
            {
                result.Append(c);
                continue;
            }
            if (i + 2 >= text.Length) return false;
            switch (text.Substring(i + 1, 2))
            {
                case "25": result.Append('%'); break;
                case "7C": result.Append('|'); break;
                case "0A": result.Append('\n'); break;
                case "0D": result.Append('\r'); break;
                default: return false;
            }
            i += 2;
        }
        id = result.ToString();
        return true;
    }

    /// <summary>True when <paramref name="id"/> and <paramref name="row"/> are in range, so the decoder would accept the line the encoder writes for them.</summary>
    internal static bool IsValid(string id, MemoryRow row) =>
        id != null && id.Length >= 1 && id.Length <= MaxIdLength
        && row.Count >= 0 && row.Count <= MaxCount
        && row.Known >= 0 && row.Known <= HiddenHostsMath.DigitCount(row.Count)
        && row.StampHour >= 0
        && row.FadeHour >= -1
        && row.XpStage >= 0 && row.XpStage <= MaxXpStage
        && row.XpDay >= -1;

    private static void Append(StringBuilder text, char kind, IEnumerable<KeyValuePair<string, MemoryRow>> rows)
    {
        foreach (var pair in rows)
        {
            var row = pair.Value;
            if (!IsValid(pair.Key, row)) continue;
            text.Append('\n').Append(kind).Append('|').Append(EscapeId(pair.Key))
                .Append('|').Append(row.Count.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(row.Known.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(row.StampHour.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(row.FadeHour.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(row.XpStage.ToString(CultureInfo.InvariantCulture))
                .Append('|').Append(row.XpDay.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static bool TryParseRow(string line, out MemoryKind kind, out string id, out MemoryRow row)
    {
        kind = MemoryKind.Party;
        id = string.Empty;
        row = new MemoryRow();

        var fields = line.Split('|');
        if (fields.Length != FieldCount) return false;

        if (fields[0] == "p") kind = MemoryKind.Party;
        else if (fields[0] == "s") kind = MemoryKind.Settlement;
        else return false;

        if (!TryUnescapeId(fields[1], out id)) return false;
        if (!TryNumber(fields[2], false, out int count) || !TryNumber(fields[3], false, out int known)
            || !TryNumber(fields[4], false, out int stamp) || !TryNumber(fields[5], true, out int fade)
            || !TryNumber(fields[6], false, out int xpStage) || !TryNumber(fields[7], true, out int xpDay))
            return false;

        row = new MemoryRow { Count = count, Known = known, StampHour = stamp, FadeHour = fade, XpStage = xpStage, XpDay = xpDay };
        return IsValid(id, row);
    }

    /// <summary>Digits only (no sign, no space), except that a field which may be unset also accepts exactly "-1".</summary>
    private static bool TryNumber(string text, bool allowMinusOne, out int value)
    {
        if (allowMinusOne && text == "-1")
        {
            value = -1;
            return true;
        }
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }
}
