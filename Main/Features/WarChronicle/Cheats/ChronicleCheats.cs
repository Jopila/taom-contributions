using System.Collections.Generic;
using TaleWorlds.Library;
using TAOM.Adapters;
using TAOM.Features.ArmyTargeting;
using TAOM.Features.CoopInterop;
using TAOM.Features.DevConsole;
using TAOM.Features.WarChronicle.Events;

namespace TAOM.Features.WarChronicle.Cheats;

/// <summary>
/// The chronicle's console commands (docs/features/dev-console.md, #765), thin over
/// <see cref="ChronicleCheatReport"/> and <see cref="ChronicleService"/>. <c>taom.chronicle_open</c> changes
/// save-backed world state, so it runs only on the co-op authority, like the daily step.
/// <para>
/// <c>taom.chronicle_open &lt;id&gt; [confirm]</c> is tier C: a resolved event's row, its effects and its
/// momentum award are saved, and no command reverts them. The bare id is a dry run through the same checks;
/// only the literal <c>confirm</c> acts, and the arguments are validated before any lookup. Event states:
/// Pending is the only one that opens (narrative: Occurred; siege: Active, or Fell when an attacker already
/// holds the target; a failed precondition: stays Pending); Active, Occurred, Held, Fell, Lapsed and Skipped
/// are refused; a row not yet adopted is adopted first on confirm and only reported by the dry run.
/// </para>
/// </summary>
public static class ChronicleCheats
{
    [CommandLineFunctionality.CommandLineArgumentFunction("chronicle_status", "taom")]
    public static string ChronicleStatus(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, ChronicleCheatReport.StatusUsage, _ =>
            ChronicleCheatReport.FormatStatus(
                IoC.Resolve<ChronicleService>().Status(),
                IoC.Resolve<ArmyTargetFocusRegistry>().Snapshot(),
                System.Math.Floor(IoC.Resolve<IChronicleWorldAdapter>().GetElapsedDays()),
                IoC.Resolve<IWarChronicleSettingsProvider>().ChronicleEnabled,
                IoC.Resolve<IArmyTargetingSettingsProvider>().EnableArmyStrategicIntelligence));

    [CommandLineFunctionality.CommandLineArgumentFunction("chronicle_open", "taom")]
    public static string ChronicleOpen(List<string> strings) =>
        TaomConsole.RunInCampaign(strings, ChronicleCheatReport.OpenUsage, args =>
        {
            var coop = IoC.Resolve<ICoopSessionProvider>();
            if (!coop.IsAuthority || !CoopSessionPolicy.MayWriteSaveBackedState(coop.IsCoopClient))
                return "Refused: only the co-op host runs the War Chronicle.";

            if (!ChronicleCheatReport.TryReadOpenArgs(args, out var id, out var confirmed, out var error))
                return error;

            var acted = IoC.Resolve<ChronicleService>().OpenChronicleEventNow(id, dryRun: !confirmed, out var reason);
            return ChronicleCheatReport.FormatOpen(id, dryRun: !confirmed, acted, reason);
        });
}
