using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Core.Validation;
using TAOM.Features.DevConsole;
using TAOM.Features.Execution;

namespace TAOM.Features.FireAndTribute.Cheats;

/// <summary>
/// Fire and Tribute's console commands (#870; docs/features/dev-console.md). <c>taom.print_fire_and_tribute</c> is tier A:
/// it reads the switches, the spawn window, the tribute envoy state, each side's host and cooldown, and today's counters.
/// <para>
/// <c>taom.spawn_raid_host &lt;free|neutral|evil&gt; [confirm]</c> is tier C: a spawn creates a bandit-clan party with
/// up to 200 men, saved with the campaign, that raids villages until it goes home. The bare command is a dry run through
/// the same decision path (target, anchor, hideout, roster); only the literal <c>confirm</c> sends the host, ignoring
/// the timers, the chance and the cooldown, and its arguments are validated before any lookup. The service keeps the
/// single-player, switch and one-host-per-side gates.
/// </para>
/// <para>
/// <c>taom.set_envoy_due [confirm]</c> is tier C: it sets a live host's tribute envoy due now, so the popup and its
/// options can be tested without waiting for the scheduled hour. The bare command is a dry run that reports the quote and
/// what blocks the envoy; only the literal <c>confirm</c> sets it due and tries to show it. The switches and single-player
/// gates hold.
/// </para>
/// Every body runs through <see cref="TaomConsole.RunInCampaign"/>.
/// </summary>
public static class FireAndTributeCheats
{
    private const string StatusUsage =
        "Format is \"taom.print_fire_and_tribute\". No arguments. Prints the switches, the spawn window, the tribute\n"
        + "envoy switch and seized count, any grudges, each side's host (party, target, raids against its quota, its envoy:\n"
        + "due hour, quote, spared clan), cooldown, and today's counters.";

    private const string SpawnUsage =
        "Format is \"taom.spawn_raid_host <free|neutral|evil> [confirm]\". Without 'confirm' it only reports which host\n"
        + "would ride, against whom, from where, with how many men and what quota. With 'confirm' it sends the side's\n"
        + "host now, ignoring the War of the Ring phase, the daily chance and the cooldown; the MCM switches,\n"
        + "single-player and one host per side still hold.";

    private const string EnvoyUsage =
        "Format is \"taom.set_envoy_due [confirm]\". Without 'confirm' it only reports the tribute (villages, price) and\n"
        + "what blocks the envoy of a live host now. With 'confirm' it sets that envoy due at once and shows the popup if\n"
        + "nothing blocks it, else every hour until nothing does. The MCM switches and single-player still hold, and so\n"
        + "does one envoy per host.";

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

    [CommandLineFunctionality.CommandLineArgumentFunction("set_envoy_due", "taom")]
    public static string SetEnvoyDue(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, EnvoyUsage, args =>
        {
            if (!TryParseSetEnvoyDue(args, out bool confirmed, out string error))
                return error;
            return IoC.Resolve<IFireAndTributeService>().BringEnvoyNow(dryRun: !confirmed);
        });

    /// <summary>No arguments (a dry run) or the literal ordinal <c>confirm</c>; anything else is refused with the usage.</summary>
    internal static bool TryParseSetEnvoyDue(List<string> args, out bool confirmed, out string error)
    {
        confirmed = false;
        error = string.Empty;
        if (args == null || args.Count > 1 || (args.Count == 1 && !string.Equals(args[0], "confirm", StringComparison.Ordinal)))
        {
            error = "Expected no argument, or the literal 'confirm'.\n" + EnvoyUsage;
            return false;
        }
        confirmed = args.Count == 1;
        return true;
    }

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
