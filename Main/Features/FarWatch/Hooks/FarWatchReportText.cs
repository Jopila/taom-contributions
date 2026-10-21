using System.Collections.Generic;
using TaleWorlds.Localization;
using TAOM.Adapters;

namespace TAOM.Features.FarWatch.Hooks;

/// <summary>
/// Renders a returned ride (#847, docs/features/far-watch.md "Report") as the title and body of the inquiry that shows it. A
/// pure render of <see cref="ScoutReport"/>: nothing here reads the engine but the text system. Counts arrive as variables
/// (the Hidden Hosts glyphs, "1??", are never written into a default, so the translator's placeholder gate protects them), the
/// lines are joined here and not with a newline in a key, and every line is true on every branch: the sight line prints only
/// when the ride set a bonus, a host with no direction is not listed, a settlement nobody holds says so.
/// </summary>
internal static class FarWatchReportText
{
    internal static string Title(ScoutReport report) =>
        new TextObject("{=taom_fwatch_report_title}{SCOUT} returns").SetTextVariable("SCOUT", FarWatchTexts.Safe(report.ScoutName)).ToString();

    internal static string Close() => new TextObject("{=taom_fwatch_report_close}Close").ToString();

    internal static string Body(ScoutReport report)
    {
        var lines = new List<string>();
        if (report.Kind == ScoutKind.Settlement) SettlementLines(report, lines);
        else SurroundingsLines(report, lines);
        return string.Join("\n", lines);
    }

    private static void SurroundingsLines(ScoutReport report, List<string> lines)
    {
        if (report.Hosts.Count == 0)
        {
            lines.Add(None(report));
        }
        else
        {
            lines.Add(new TextObject("{=taom_fwatch_report_around}Hosts sighted around the camp:").ToString());
            foreach (var host in report.Hosts)
                lines.Add(new TextObject("{=taom_fwatch_report_row}{NAME}: {COUNT} ({KIND}), {DIRECTION}")
                    .SetTextVariable("NAME", FarWatchTexts.Safe(host.Name)).SetTextVariable("COUNT", host.Count)
                    .SetTextVariable("KIND", FarWatchTexts.Kind(host.Composition)).SetTextVariable("DIRECTION", FarWatchTexts.Direction(host.Direction)).ToString());
        }

        if (report.SightHours > 0)
            lines.Add(new TextObject("{=taom_fwatch_report_sight}Your party sees farther for about {TIME}.")
                .SetTextVariable("TIME", FarWatchTexts.HoursText(report.SightHours)).ToString());
    }

    private static void SettlementLines(ScoutReport report, List<string> lines)
    {
        if (report.SiteTotal <= 0)
        {
            lines.Add(None(report));
            return;
        }

        lines.Add(new TextObject("{=taom_fwatch_report_site}{SETTLEMENT} is held by {COUNT} men:")
            .SetTextVariable("SETTLEMENT", FarWatchTexts.Safe(report.SiteName)).SetTextVariable("COUNT", report.SiteTotal).ToString());
        foreach (var d in report.Defenders)
        {
            if (d.Count <= 0) continue;
            lines.Add(Defender(d));
        }
    }

    private static string Defender(ScoutDefenderRow d)
    {
        switch (d.Kind)
        {
            case ScoutDefenderKind.Garrison:
                return new TextObject("{=taom_fwatch_report_garrison}Garrison: {COUNT} ({KIND})")
                    .SetTextVariable("COUNT", d.Count).SetTextVariable("KIND", FarWatchTexts.Kind(d.Composition)).ToString();
            case ScoutDefenderKind.Militia:
                return new TextObject("{=taom_fwatch_report_militia}Militia: {COUNT}").SetTextVariable("COUNT", d.Count).ToString();
            default:
                return new TextObject("{=taom_fwatch_report_lord}{NAME}: {COUNT} ({KIND})")
                    .SetTextVariable("NAME", FarWatchTexts.Safe(d.Name)).SetTextVariable("COUNT", d.Count)
                    .SetTextVariable("KIND", FarWatchTexts.Kind(d.Composition)).ToString();
        }
    }

    private static string None(ScoutReport report) =>
        new TextObject("{=taom_fwatch_report_none}{SCOUT} saw no hosts worth reporting.").SetTextVariable("SCOUT", FarWatchTexts.Safe(report.ScoutName)).ToString();
}
