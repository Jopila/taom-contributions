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

            if (!ChronicleCheatReport.TryReadOpenArgs(args, out var id, out var error))
                return error;

            var opened = IoC.Resolve<ChronicleService>().OpenChronicleEventNow(id, out var reason);
            return (opened ? "[WarChronicle] Opened: " : "[WarChronicle] Not opened: ") + reason;
        });
}
