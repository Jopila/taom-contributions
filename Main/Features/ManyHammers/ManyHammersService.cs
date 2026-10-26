using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.ManyHammers;

/// <summary>
/// Many Hammers (docs/features/many-hammers.md, #911): repeats the smithy's own smelt or refine so one click can do several.
/// The service never replaces the engine's last unit. It runs every unit but one through the engine's crafting behavior and
/// leaves the final unit to the vanilla button handler, which then refreshes the lists and the screen itself, so a throw
/// here at worst leaves the player with fewer units done than they asked for.
/// </summary>
public sealed class ManyHammersService
{
    private readonly IManyHammersSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    public ManyHammersService(IManyHammersSettingsProvider settings, ICoopSessionProvider coop, IModLogger logger)
    {
        _settings = settings;
        _coop = coop;
        _logger = logger;
    }

    /// <summary>
    /// Whether Shift and Ctrl do anything: the switch is on and this peer owns the smithing state. A co-op guest's roster and
    /// stamina belong to the host, so a guest stays at one unit a click.
    /// </summary>
    public bool BulkActive => _settings.BulkEnabled && !_coop.ShouldDeferToHost;

    /// <summary>Whether locked items are left out of the smelting list. A guest hides them too: it is a view filter only.</summary>
    public bool HidesLockedItems => _settings.HideLockedEnabled;

    /// <summary>
    /// How many units a click asks for. A locked selection is always one: vanilla asks "Are you sure you want to smelt this
    /// weapon? It is locked in the inventory." before it smelts, and bulk must not run extra units behind that question.
    /// </summary>
    public int DesiredCount(bool shift, bool ctrl, int stack, bool selectionLocked) =>
        selectionLocked ? 1 : BulkCraftRules.ResolveCount(BulkActive, shift, ctrl, stack);

    /// <summary>
    /// Runs the units before the last one. The opening plan fixes how many units fit, and one of them is reserved for
    /// vanilla. Before each extra unit the numbers are read again and the plan redone, which is cheap and stays right if a
    /// model ever prices a unit by skill; the next two units are checked, and the loop goes on only while this unit and
    /// vanilla's still fit. The opening plan also bounds the loop, so a snapshot that never moves cannot run it away. A throw
    /// is logged and ends the loop; the caller then lets vanilla do its unit.
    /// </summary>
    /// <returns>The units done here, not counting the one left for vanilla.</returns>
    public int RunExtras(ICraftBenchUnit unit, int desired)
    {
        if (unit == null || desired <= 1) return 0;

        var done = 0;
        try
        {
            var planned = BulkCraftRules.PlanUnits(desired, unit.Read());
            while (done < planned - 1)
            {
                if (BulkCraftRules.PlanUnits(2, unit.Read()) < 2) break; // this unit and vanilla's last one
                unit.DoOne();
                done++;
            }
        }
        catch (Exception ex)
        {
            Log($"[ManyHammers] stopped after {done} extra unit(s); vanilla still does the last one: {ex}");
        }

        return done;
    }

    private void Log(string message)
    {
        try { _logger.LogError(message); }
        catch { /* the log line must never reach the smithy screen */ }
    }
}
