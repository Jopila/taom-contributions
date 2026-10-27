using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.NameGrammarTags;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>`taom.print_kept_pairs &lt;lang&gt; [subjects]`: renders vanilla's row and TAOM's row of every kept pair with real kingdoms, clans and settlements bound, and writes the report to the Logs folder (#1023).</summary>
public static class KeptPairCheats
{
    private const int DefaultSubjects = 4;
    private const int MaxSubjects = 12;

    private const string Usage =
        "Format is \"taom.print_kept_pairs <lang> [subjects]\", for example \"taom.print_kept_pairs IT 4\".\n"
        + "<lang> is the language folder (BR, IT, ...). The game must be running in that language.\n"
        + "Renders vanilla's row and TAOM's row of every kept pair for up to [subjects] kingdoms (default 4)\n"
        + "and writes both lines of each pair to a report in the game's Logs folder.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_kept_pairs", "taom")]
    public static string PrintKeptPairs(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            if (args.Count < 1) return Usage;
            var subjects = DefaultSubjects;
            if (args.Count > 1 && !DevConsoleArgs.TryParseCount(args[1], 1, MaxSubjects, out subjects, out var error))
                return error + "\n" + Usage;

            // Built here, not resolved from IoC: the three adapters keep no state, so there is nothing to register or share.
            var service = new KeptPairRenderService(new KeptPairSourceAdapter(), new GrammarRendererAdapter(), new ReportFileAdapter());
            return service.Run(args[0].Trim(), subjects);
        });
}
