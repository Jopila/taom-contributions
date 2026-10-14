using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;
using TAOM.Features.Execution;

namespace TAOM.Features.EyeOfSauron.Cheats;

/// <summary>
/// The Eye of Sauron hunt's console commands (#765 M8; docs/features/dev-console.md). <c>taom.print_eye_of_sauron</c>
/// is tier A. <c>taom.add_eye_score</c> and <c>taom.clear_eye_hunt</c> are tier B: a score edit is reversible with a
/// negative amount, and clearing a hunt releases the hunter and starts the cooldown.
/// <para>
/// <c>taom.start_eye_hunt [confirm]</c> is tier C: a start can remove a hunter as governor, spawn a party, add up to
/// <c>HunterMaxTroops</c> men and grain, all saved with the campaign and none undone by <c>clear_eye_hunt</c>. The bare
/// command is a dry run through the same decision path; only the literal <c>confirm</c> starts the hunt, and its
/// arguments are validated before any lookup. Entity states: the hunter's are the service's gates (alive, free, not a
/// prisoner, at war, his party not busy). The player's: refused when he has no kingdom or a Neutral one, and when he
/// is a prisoner or enlisted (the engage needs an active main party). In a settlement, a siege or another lord's army
/// the start is allowed: the hunter rides, and his out-of-sight steps pause until the player is on the open map.
/// </para>
/// Every body runs through <see cref="TaomConsole.RunInCampaign"/>; the service keeps the single-player and war gates.
/// </summary>
public static class EyeOfSauronCheats
{
    internal const float MaxScoreEdit = 1000f;

    private const string StatusUsage =
        "Format is \"taom.print_eye_of_sauron\". No arguments. Prints the deed scores, the cooldown and the hunt.";

    private const string AddScoreUsage =
        "Format is \"taom.add_eye_score <evil|free> <amount>\".\n"
        + "'evil' adds to the score for beating Evil kingdoms (it draws one of the Nine onto a Free player);\n"
        + "'free' adds to the score for beating Free kingdoms (it draws Glorfindel onto an Evil player).\n"
        + "The amount may be negative, between -1000 and 1000; a score never drops below zero.";

    private const string StartUsage =
        "Format is \"taom.start_eye_hunt [confirm]\". Without 'confirm' it only reports who would ride, whether his\n"
        + "party is re-tasked or a new one spawned, and the top-up. With 'confirm' it starts the hunt now, ignoring the\n"
        + "score and the cooldown; your kingdom must still be at war with the hunter's, and the game single-player.\n"
        + "The party, its troops and grain, and a lost governorship remain after taom.clear_eye_hunt.";

    private const string EndUsage = "Format is \"taom.clear_eye_hunt\". Ends the running hunt and releases the hunter.";

    [CommandLineFunctionality.CommandLineArgumentFunction("print_eye_of_sauron", "taom")]
    public static string PrintEyeOfSauron(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, StatusUsage, _ => string.Join("\n", IoC.Resolve<EyeOfSauronService>().Describe()));

    [CommandLineFunctionality.CommandLineArgumentFunction("add_eye_score", "taom")]
    public static string AddEyeScore(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, AddScoreUsage, args =>
        {
            if (!TryParseScoreEdit(args, out var side, out float amount, out string error))
                return error;

            var service = IoC.Resolve<EyeOfSauronService>();
            float before = service.GetScore(side);
            service.AddScore(side, amount);
            return $"Score against {side}: {before:0.0} -> {service.GetScore(side):0.0}.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("start_eye_hunt", "taom")]
    public static string StartEyeHunt(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, StartUsage, args =>
        {
            if (!TryParseConfirm(args, out bool confirmed))
                return "Expected no argument (a dry run) or the literal 'confirm'.\n" + StartUsage;
            return IoC.Resolve<EyeOfSauronService>().TryStartHunt(ignoreScoreAndCooldown: true, dryRun: !confirmed);
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("clear_eye_hunt", "taom")]
    public static string ClearEyeHunt(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, EndUsage, _ =>
        {
            var service = IoC.Resolve<EyeOfSauronService>();
            if (!service.IsHunting) return "No hunt is running.";
            string hunter = service.HunterHeroId ?? "?";
            service.EndHunt(HuntEndReason.Manual);
            return $"Hunt ended; {hunter} is released and the cooldown has started.";
        });

    /// <summary>No argument is a dry run; exactly the literal <c>confirm</c> (ordinal) starts; anything else is refused.</summary>
    internal static bool TryParseConfirm(List<string> args, out bool confirmed)
    {
        confirmed = false;
        if (args == null || args.Count == 0) return true;
        if (args.Count != 1 || !string.Equals(args[0], "confirm", StringComparison.Ordinal)) return false;
        confirmed = true;
        return true;
    }

    /// <summary>A side, then a finite amount within <see cref="MaxScoreEdit"/>; the error says what was wrong.</summary>
    internal static bool TryParseScoreEdit(List<string> args, out FactionSide side, out float amount, out string error)
    {
        amount = 0f;
        side = FactionSide.Neutral;
        if (args == null || args.Count != 2 || !TryParseSide(args[0], out side))
        {
            error = "Expected 'evil' or 'free', then an amount.\n" + AddScoreUsage;
            return false;
        }
        if (!DevConsoleArgs.TryParseAmount(args[1], out amount, out error))
            return false;
        if (Math.Abs(amount) > MaxScoreEdit)
        {
            error = $"Amount must be between -{MaxScoreEdit:0} and {MaxScoreEdit:0} (got {amount}).";
            return false;
        }
        error = string.Empty;
        return true;
    }

    /// <summary>'evil' or 'free', any case; anything else is refused, never a default side.</summary>
    internal static bool TryParseSide(string raw, out FactionSide side)
    {
        switch ((raw ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "evil":
                side = FactionSide.Evil;
                return true;
            case "free":
                side = FactionSide.Free;
                return true;
            default:
                side = FactionSide.Neutral;
                return false;
        }
    }
}
