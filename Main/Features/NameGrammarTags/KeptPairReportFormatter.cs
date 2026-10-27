using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TAOM.Features.NameGrammarTags;

/// <summary>Formats the kept-pair report: per pair and subject, vanilla's line and TAOM's line for each variant, marked SAME, DIFFERENT or FAILED.</summary>
public static class KeptPairReportFormatter
{
    public static string Format(string language, string languageId, string textProcessor,
        CanaryResult canary, int pairCount, IReadOnlyList<string> subjects, IReadOnlyList<RenderedPair> rendered)
    {
        var same = rendered.Count(r => r.Verdict == "SAME");
        var failed = rendered.Count(r => r.Verdict == "FAILED");
        var text = new StringBuilder();
        text.AppendLine("TAOM kept-pair report");
        text.AppendLine($"Language: {language} ({languageId})");
        text.AppendLine($"Text processor: {(string.IsNullOrEmpty(textProcessor) ? "(unreadable)" : textProcessor)} (the processor the language's data builds)");
        text.AppendLine(CanaryLine(canary));
        text.AppendLine($"Kept pairs: {pairCount}");
        text.AppendLine($"Subjects: {string.Join(", ", subjects)}");
        text.AppendLine("Each block renders vanilla's row and TAOM's row with real campaign names bound, once per variant.");
        text.AppendLine("A plain kingdom or clan variable is rendered with its Name and with its InformalName, because vanilla sets either.");
        text.AppendLine("Judge a block by reading the vanilla lines: if they read right, TAOM's row can go.");
        text.AppendLine();
        foreach (var r in rendered)
        {
            text.AppendLine($"--- {r.Pair.Id} [{r.Subject}] {r.Verdict}");
            foreach (var line in r.Lines)
            {
                text.AppendLine($"[{line.Variant}]");
                text.AppendLine($"vanilla: {line.Vanilla}");
                text.AppendLine($"TAOM:    {line.Taom}");
            }
            text.AppendLine();
        }
        text.AppendLine($"Rendered {rendered.Count} block(s): {same} identical, {rendered.Count - same - failed} different, {failed} failed.");
        return text.ToString();
    }

    private static string CanaryLine(CanaryResult canary)
    {
        if (!canary.Applicable)
            return $"Canary: not applicable ({canary.Description})";
        return canary.Passed
            ? $"Canary: PASS ({canary.Description})"
            : $"Canary: FAIL ({canary.Description}); the text processor ignores the grammar tags, so do not trust any line below";
    }
}
