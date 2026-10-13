using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Features.WarChronicle.Cheats;

/// <summary>
/// The pure half of the chronicle's console commands (#765): the text of <c>taom.chronicle_status</c> and
/// the argument check of <c>taom.chronicle_open</c>. Invariant culture throughout.
/// </summary>
internal static class ChronicleCheatReport
{
    internal const string StatusUsage =
        "Format is \"taom.chronicle_status\".\n"
        + "Prints every War Chronicle event with its state (Pending, Active, Occurred, Held, Fell, Lapsed, Skipped), "
        + "its days, and the army focus the open siege windows set. Read-only.";

    internal const string OpenUsage =
        "Format is \"taom.chronicle_open <id>\".\n"
        + "Opens a Pending chronicle event now, ignoring its day but still checking its preconditions: a narrative "
        + "event occurs, a siege window opens. Ids are listed by taom.chronicle_status.";

    internal static bool TryReadOpenArgs(IReadOnlyList<string> args, out string id, out string error)
    {
        id = string.Empty;
        error = string.Empty;
        if (args == null || args.Count != 1 || string.IsNullOrWhiteSpace(args[0]))
        {
            error = OpenUsage;
            return false;
        }

        id = args[0].Trim();
        return true;
    }

    internal static string FormatStatus(
        IReadOnlyList<ChronicleStatusRow> rows, IReadOnlyList<ArmyTargetFocus> focus, double nowDay, bool enabled)
    {
        var sb = new StringBuilder();
        sb.Append("[WarChronicle] Chronicle on campaign day ").Append(Day(nowDay))
          .Append(enabled ? "" : " (the chronicle is off: nothing opens or resolves)").Append('\n');

        if (rows.Count == 0)
        {
            sb.Append("No chronicle events loaded (see war_chronicle.json warnings in the log).");
            return sb.ToString();
        }

        foreach (var row in rows)
        {
            sb.Append("  ").Append(row.Id).Append(" (").Append(row.Kind).Append(") ").Append(row.State);
            if (row.Target != null)
                sb.Append(" at ").Append(row.Target);
            sb.Append(": ").Append(Days(row)).Append('\n');
        }

        sb.Append("Army focus:");
        if (focus.Count == 0)
            sb.Append(" none");
        foreach (var f in focus)
        {
            sb.Append("\n  ").Append(f.SettlementId).Append(" x").Append(f.Multiplier.ToString("0.00", CultureInfo.InvariantCulture))
              .Append(": besieged by ").Append(string.Join(", ", f.AttackerFactionIds))
              .Append("; defended by ").Append(f.DefenderFactionId ?? "none");
        }

        return sb.ToString();
    }

    private static string Days(ChronicleStatusRow row)
    {
        switch (row.State)
        {
            case ChronicleEventState.Pending:
                return row.OpenDay.HasValue ? "opens day " + Day(row.OpenDay.Value) : "opens when its anchor is known";
            case ChronicleEventState.Active:
                return "opened day " + Day(row.OpenDay) + ", closes day " + Day(row.CloseDay)
                       + (row.Assaulted ? ", assaulted" : ", not assaulted yet");
            default:
                return "resolved day " + Day(row.ResolvedDay);
        }
    }

    private static string Day(double? day) =>
        day.HasValue ? day.Value.ToString("0.#", CultureInfo.InvariantCulture) : "?";
}
