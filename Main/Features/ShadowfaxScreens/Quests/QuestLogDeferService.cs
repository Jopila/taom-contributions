using System;
using System.Runtime.CompilerServices;
using System.Threading;
using TAOM.Adapters;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Features.ShadowfaxScreens.Quests;

/// <summary>
/// Quest log fast open (docs/features/shadowfax-screens.md): decides which finished quests skip building their stages
/// inside the QuestsVM constructor, builds them when one is selected, and keeps the updated dot and the date sort right
/// for the ones still unbuilt. Items are opaque keys (a <c>QuestItemVM</c> behind <see cref="IQuestJournalAdapter"/>), so
/// the service holds no TaleWorlds type. The open's scope, its snapshot of the toggle and of the adapter's bound state, and
/// the replay flag are per thread: the UI thread opens the screen, and nothing else may defer. Every public member answers
/// "do nothing" on a failure rather than throw.
/// </summary>
public sealed class QuestLogDeferService
{
    private readonly IShadowfaxScreensSettingsProvider _settings;
    private readonly IQuestJournalAdapter _adapter;
    private readonly IStampClock _clock;
    private readonly IModLogger _logger;
    private readonly ThreadLocal<OpenScope> _scope = new(() => new OpenScope());
    private readonly ConditionalWeakTable<object, Mark> _marks = new();
    private int _disarmed;

    public QuestLogDeferService(IShadowfaxScreensSettingsProvider settings, IQuestJournalAdapter adapter, IStampClock clock, IModLogger logger)
    {
        _settings = settings;
        _adapter = adapter;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>True after a rebuild threw: no later open defers anything this process.</summary>
    public bool Disarmed => Volatile.Read(ref _disarmed) != 0;

    /// <summary>Binds the adapter and writes one line saying whether the fast open is installed. Returns the reason it is not, or null.</summary>
    public string? Install()
    {
        string? problem;
        try
        {
            problem = _adapter.Bind();
        }
        catch (Exception ex)
        {
            problem = ex.GetType().Name + ": " + ex.Message;
        }

        try
        {
            if (problem == null)
                _logger.LogInfo("[QuestLog] fast open installed; finished quests build when selected (fast open " + (ToggleOn() ? "on" : "off") + ").");
            else
                _logger.LogWarning("[QuestLog] fast open unavailable (" + problem + "); vanilla builds the quest log.");
        }
        catch
        {
            // A log line must not break game start.
        }

        return problem;
    }

    /// <summary>Opens the scope on this thread. The outermost call snapshots the toggle and the bound state and zeroes the counts.</summary>
    public void BeginOpen()
    {
        var scope = _scope.Value!;
        if (scope.Depth++ > 0) return;
        scope.Reset();
        try
        {
            scope.Timing = _settings.LogScreenOpenTimes;
            scope.FastOpen = _settings.QuestLogFastOpen;
            scope.Bound = _adapter.IsBound;
        }
        catch
        {
            scope.Timing = false;
            scope.FastOpen = false;
            scope.Bound = false;
        }
    }

    /// <summary>Closes the scope. The outermost close returns the detail for the open line; a nested close or one without a begin returns null.</summary>
    public string? EndOpen()
    {
        var scope = _scope.Value!;
        if (scope.Depth <= 0)
        {
            scope.Depth = 0;
            return null;
        }

        if (--scope.Depth > 0) return null;
        return string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "old={0}, deferred={1}, entries={2}, updated={3}, fast open {4}, {5}, journal items {6:0.0} ms",
            scope.OldItems, scope.DeferredItems, scope.DeferredEntries, scope.Updated,
            scope.FastOpen ? "on" : "off", scope.Bound ? "bound" : "unbound",
            LoadTimeStampLines.ToMs(scope.ItemTicks, SafeFrequency()));
    }

    /// <summary>Counts one journal item built in this open; returns its start tick, or 0 when the timer is off or the clock fails.</summary>
    public long BeginItem()
    {
        var scope = _scope.Value!;
        if (scope.Depth <= 0) return 0;
        scope.OldItems++;
        if (!scope.Timing) return 0;
        try
        {
            return Math.Max(_clock.Now, 1);
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Adds the item's elapsed time to the open's total. A zero start (no prefix ran, or the timer is off) adds nothing.</summary>
    public void EndItem(long start)
    {
        var scope = _scope.Value!;
        if (start <= 0 || scope.Depth <= 0) return;
        try
        {
            scope.ItemTicks += _clock.Now - start;
        }
        catch
        {
            // The measurement must never break the screen.
        }
    }

    /// <summary>
    /// True when PopulateQuestLog should be skipped for this item's entry; the item is then marked as deferred. Only inside an
    /// open whose snapshot had the toggle on and the adapter bound, for an item with a journal entry, outside a replay, and
    /// while armed.
    /// </summary>
    public bool TryDefer(object item, bool hasJournalEntry)
    {
        var scope = _scope.Value!;
        if (scope.Depth <= 0 || scope.Replaying || !scope.FastOpen || !scope.Bound || !hasJournalEntry || Disarmed)
            return false;
        if (!_marks.TryGetValue(item, out _))
        {
            _marks.Add(item, new Mark());
            scope.DeferredItems++;
        }

        scope.DeferredEntries++;
        return true;
    }

    public bool IsDeferred(object item) => _marks.TryGetValue(item, out _);

    /// <summary>
    /// Builds a deferred item's stages once, whatever the toggle says now. A rebuild that throws is logged once, disarms
    /// deferral for the process and leaves the item marked (so sorting keeps reading its entries); the item is not retried.
    /// </summary>
    public void EnsureBuilt(object? item)
    {
        if (item == null || !_marks.TryGetValue(item, out var mark) || mark.Failed) return;
        var scope = _scope.Value!;
        if (scope.Replaying) return;
        scope.Replaying = true;
        try
        {
            _adapter.Rebuild(item);
            _marks.Remove(item);
        }
        catch (Exception ex)
        {
            mark.Failed = true;
            Disarm(ex);
        }
        finally
        {
            scope.Replaying = false;
        }
    }

    /// <summary>The updated dot for a deferred item (any journal entry unexamined), or null to leave vanilla's value.</summary>
    public bool? UpdatedFor(object item)
    {
        if (!IsDeferred(item)) return null;
        try
        {
            var updated = _adapter.AnyUnexamined(item);
            var scope = _scope.Value!;
            if (updated && scope.Depth > 0) scope.Updated++;
            return updated;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>The journal entry vanilla's sort would read from a built item's stages: the first or last. Null for any item that is not deferred.</summary>
    public object? SortFallback(object item, bool last)
    {
        if (!IsDeferred(item)) return null;
        try
        {
            return _adapter.FirstOrLastEntry(item, last);
        }
        catch
        {
            return null;
        }
    }

    private void Disarm(Exception ex)
    {
        if (Interlocked.Exchange(ref _disarmed, 1) != 0) return;
        try
        {
            _logger.LogWarning("[QuestLog] building a finished quest on demand threw " + ex.GetType().Name + ": " + ex.Message
                + "; fast open is off until the game restarts.");
        }
        catch
        {
            // A log line must not break the screen.
        }
    }

    private bool ToggleOn()
    {
        try { return _settings.QuestLogFastOpen; }
        catch { return false; }
    }

    private long SafeFrequency()
    {
        try { return _clock.Frequency; }
        catch { return 0; }
    }

    private sealed class Mark
    {
        public bool Failed;
    }

    private sealed class OpenScope
    {
        public int Depth;
        public bool Timing;
        public bool FastOpen;
        public bool Bound;
        public bool Replaying;
        public int OldItems;
        public int DeferredItems;
        public int DeferredEntries;
        public int Updated;
        public long ItemTicks;

        public void Reset()
        {
            OldItems = 0;
            DeferredItems = 0;
            DeferredEntries = 0;
            Updated = 0;
            ItemTicks = 0;
            Replaying = false;
        }
    }
}
