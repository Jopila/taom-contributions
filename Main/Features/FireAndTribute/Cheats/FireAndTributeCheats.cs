using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Core.Validation;
using TAOM.Features.DevConsole;
using TAOM.Features.Execution;

namespace TAOM.Features.FireAndTribute.Cheats;

/// <summary>
/// Fire and Tribute's console commands (#870; docs/features/dev-console.md). <c>taom.print_fire_and_tribute</c> is tier A:
/// it reads the switches, the spawn window, each side's host and cooldown, and today's counters.
/// <para>
/// <c>taom.spawn_raid_host &lt;free|neutral|evil&gt; [confirm]</c> is tier C: a spawn creates a bandit-clan party with
/// up to 200 men, saved with the campaign, that raids villages until it goes home. The bare command is a dry run through
/// the same decision path (target, anchor, hideout, roster); only the literal <c>confirm</c> sends the host, ignoring
/// the timers, the chance and the cooldown, and its arguments are validated before any lookup. The service keeps the
/// single-player, switch and one-host-per-side gates.
/// </para>
/// Every body runs through <see cref="TaomConsole.RunInCampaign"/>.
/// </summary>
public static class FireAndTributeCheats
{
    private const string StatusUsage =
        "Format is \"taom.print_fire_and_tribute\". No arguments. Prints the switches, the spawn window, each side's\n"
        + "host (party, target, raids against its quota), cooldown, and today's counters.";

    private const string SpawnUsage =
        "Format is \"taom.spawn_raid_host <free|neutral|evil> [confirm]\". Without 'confirm' it only reports which host\n"
        + "would ride, against whom, from where, with how many men and what quota. With 'confirm' it sends the side's\n"
        + "host now, ignoring the War of the Ring phase, the daily chance and the cooldown; the MCM switches,\n"
        + "single-player and one host per side still hold.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_fire_and_tribute", "taom")]
    public static string PrintFireAndTribute(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, StatusUsage, _ => string.Join("\n", IoC.Resolve<IFireAndTributeService>().Describe()));

    [CommandLineFunctionality.CommandLineArgumentFunction("spawn_raid_host", "taom")]
    public static string SpawnRaidHost(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, SpawnUsage, args =>
        {
            if (!TryParseSpawn(args, out var side, out bool confirmed, out string error))
                return error;
            return IoC.Resolve<IFireAndTributeService>().TrySpawn(side, dryRun: !confirmed);
        });

    /// <summary>A side, then optionally the literal ordinal <c>confirm</c>; anything else is refused with the usage.</summary>
    internal static bool TryParseSpawn(List<string> args, out FactionSide side, out bool confirmed, out string error)
    {
        side = FactionSide.Neutral;
        confirmed = false;
        if (args == null || args.Count < 1 || args.Count > 2 || !EnumNames.TryParse(args[0], out side))
        {
            error = "Expected 'free', 'neutral' or 'evil', then optionally 'confirm'.\n" + SpawnUsage;
            return false;
        }
        if (args.Count == 2)
        {
            if (!string.Equals(args[1], "confirm", StringComparison.Ordinal))
            {
                error = "The second argument must be the literal 'confirm'.\n" + SpawnUsage;
                return false;
            }
            confirmed = true;
        }
        error = string.Empty;
        return true;
    }
}
