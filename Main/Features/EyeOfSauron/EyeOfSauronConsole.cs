using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Features.DevConsole;
using TAOM.Features.Execution;

namespace TAOM.Features.EyeOfSauron;

/// <summary>
/// The Eye of Sauron hunt's console commands (#765 M8; docs/features/dev-console.md). <c>taom.eye_status</c> is tier
/// A; the others are tier B: a score edit is reversible with a negative amount, and a started hunt ends with
/// <c>taom.eye_end_hunt</c> (a spawned hunter's party stays on the map as an ordinary lord party). Every body runs
/// through <see cref="TaomConsole.RunInCampaign"/>; the service keeps the single-player and war gates.
/// </summary>
public static class EyeOfSauronConsole
{
    private const float MaxScoreEdit = 1000f;

    private const string StatusUsage = "Format is \"taom.eye_status\". No arguments. Prints the deed scores, the cooldown and the hunt.";

    private const string AddScoreUsage =
        "Format is \"taom.eye_add_score <evil|free> <amount>\".\n"
        + "'evil' adds to the score for beating Evil kingdoms (it draws one of the Nine onto a Free player);\n"
        + "'free' adds to the score for beating Free kingdoms (it draws Glorfindel onto an Evil player).\n"
        + "The amount may be negative; a score never drops below zero.";

    private const string StartUsage =
        "Format is \"taom.eye_start_hunt\". Starts a hunt now, ignoring the score and the cooldown. Your kingdom must\n"
        + "still be at war with the hunter's, and the game must be single-player.";

    private const string EndUsage = "Format is \"taom.eye_end_hunt\". Ends the running hunt and releases the hunter.";

    [CommandLineFunctionality.CommandLineArgumentFunction("eye_status", "taom")]
    public static string EyeStatus(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, StatusUsage, _ => string.Join("\n", IoC.Resolve<EyeOfSauronService>().Describe()));

    [CommandLineFunctionality.CommandLineArgumentFunction("eye_add_score", "taom")]
    public static string EyeAddScore(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, AddScoreUsage, args =>
        {
            if (args.Count != 2 || !TryParseSide(args[0], out var side))
                return "Expected 'evil' or 'free', then an amount.\n" + AddScoreUsage;
            if (!DevConsoleArgs.TryParseAmount(args[1], out float amount, out string error))
                return error;
            if (Math.Abs(amount) > MaxScoreEdit)
                return $"Amount must be between -{MaxScoreEdit:0} and {MaxScoreEdit:0} (got {amount}).";

            var service = IoC.Resolve<EyeOfSauronService>();
            float before = service.GetScore(side);
            service.AddScore(side, amount);
            return $"Score against {side}: {before:0.0} -> {service.GetScore(side):0.0}.";
        });

    [CommandLineFunctionality.CommandLineArgumentFunction("eye_start_hunt", "taom")]
    public static string EyeStartHunt(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, StartUsage, _ => IoC.Resolve<EyeOfSauronService>().TryStartHunt(ignoreScoreAndCooldown: true));

    [CommandLineFunctionality.CommandLineArgumentFunction("eye_end_hunt", "taom")]
    public static string EyeEndHunt(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, EndUsage, _ =>
        {
            var service = IoC.Resolve<EyeOfSauronService>();
            if (!service.IsHunting) return "No hunt is running.";
            string hunter = service.HunterHeroId ?? "?";
            service.EndHunt(HuntEndReason.Manual);
            return $"Hunt ended; {hunter} is released and the cooldown has started.";
        });

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
