using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TAOM.Features.Enlistment.Content;

namespace TAOM.Features.DevConsole.Cheats;

/// <summary>
/// `taom.set_term_end`: end the enlisted player's current term today, so the next daily tick makes
/// the end-of-term offer (#836). Exists for the in-game check: the term is a year long.
///
/// Boundary class, so <c>IoC.Resolve</c> is correct here (the engine constructs the console command and
/// there is no injection point), same precedent as <c>EnlistmentRescueCheats</c>. Host only: the service
/// refuses on a co-op client.
/// </summary>
public static class EnlistmentTermCheats
{
    private const string Usage = "Format is \"taom.set_term_end\". No arguments.";

    [CommandLineFunctionality.CommandLineArgumentFunction("set_term_end", "taom")]
    public static string EnlistEndTerm(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, Usage, args =>
        {
            var renewal = IoC.Resolve<ContractRenewalService>();
            if (renewal == null)
                return "Enlistment services are not loaded.";
            return renewal.ForceTermEnd(CampaignTime.Now.ToDays)
                ? "The term ends today. The offer comes with the next daily tick."
                : "Not enlisted, or not the host: nothing changed.";
        });
}
