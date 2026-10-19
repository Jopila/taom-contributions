using System;
using System.Collections.Generic;
using System.Globalization;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Features.ShadowfaxScreens.Inventory;

/// <summary>
/// The decisions and counters behind the Inventory fast open (docs/features/shadowfax-screens.md): the toggle
/// gate, one install or bail line per rewritten call site, the per-site concept memo, and the per-open totals
/// that go on the screen's [ScreenOpen] line. Holds no TaleWorlds type (ADR-007). UI main thread only. No method
/// throws: a diagnostic or a cache must never be what breaks the screen.
/// </summary>
internal sealed class InventoryOpenFastPathService
{
    internal const int ConceptSites = 2;

    private readonly IShadowfaxScreensSettingsProvider _settings;
    private readonly IStampClock _clock;
    private readonly IModLogger _logger;
    private readonly HashSet<string> _reportedSites = new();
    private readonly SourceScopedMemo<object, object?>[] _concepts = NewMemos();

    private bool _openActive;
    private long _rowTicks;
    private int _sortsSkipped;
    private int _conceptLookups;
    private int _conceptHits;

    public InventoryOpenFastPathService(IShadowfaxScreensSettingsProvider settings, IStampClock clock, IModLogger logger)
    {
        _settings = settings;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Read per call, so the MCM switch applies to the next open. An unreadable setting is off.</summary>
    public bool Enabled
    {
        get
        {
            try { return _settings.InventoryFastOpen; }
            catch { return false; }
        }
    }

    public bool OpenActive => _openActive;

    /// <summary>Logs one line per site name, however often it is reported (a re-applied category installs again).</summary>
    public void ReportSite(string site, int swapped, int expected)
    {
        try
        {
            if (!_reportedSites.Add(site)) return;
            var counts = string.Format(CultureInfo.InvariantCulture, "swapped {0} of {1} call sites", swapped, expected);
            if (swapped == expected) _logger.LogInfo($"[ShadowfaxInventory] {site}: {counts}");
            else _logger.LogWarning($"[ShadowfaxInventory] {site}: left vanilla ({counts})");
        }
        catch
        {
            // diagnostic only
        }
    }

    /// <summary>Whether InitializeInventory's name sort may pass its source through (the ctor re-sorts the lists).</summary>
    public bool ShouldSkipNameSort()
    {
        if (!Enabled) return false;
        if (_openActive) _sortsSkipped++;
        return true;
    }

    public bool TryGetConcept(int site, object source, out object? value)
    {
        value = null;
        try
        {
            if (!Enabled || site < 0 || site >= ConceptSites) return false;
            if (_openActive) _conceptLookups++;
            if (!_concepts[site].TryGet(source, out value)) return false;
            if (_openActive) _conceptHits++;
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    public void StoreConcept(int site, object source, object? value)
    {
        try
        {
            if (Enabled && site >= 0 && site < ConceptSites) _concepts[site].Store(source, value);
        }
        catch
        {
            // a missed store only costs a recompute
        }
    }

    /// <summary>Starts the totals of one screen open. Called only when the open is being timed.</summary>
    public void BeginOpen()
    {
        _openActive = true;
        _rowTicks = 0;
        _sortsSkipped = 0;
        _conceptLookups = 0;
        _conceptHits = 0;
    }

    /// <summary>Starts the InitializeInventory phase, or returns null outside a timed open (a Reset re-runs it).</summary>
    public long? BeginRows()
    {
        if (!_openActive) return null;
        try { return _clock.Now; }
        catch { return null; }
    }

    public void EndRows(long? start)
    {
        if (start == null || !_openActive) return;
        try { _rowTicks += _clock.Now - start.Value; }
        catch
        {
            // the phase is simply under-reported
        }
    }

    /// <summary>Closes the open and returns the detail for its [ScreenOpen] line. A negative count prints n/a.</summary>
    public string EndOpen(string? mode, int left, int right)
    {
        _openActive = false;
        try
        {
            var ms = LoadTimeStampLines.ToMs(_rowTicks, _clock.Frequency);
            return string.Format(CultureInfo.InvariantCulture,
                "mode={0}, left={1}, right={2}, fast={3}, rows={4:0.0} ms, sorts skipped={5}, concepts cached={6}/{7}",
                mode ?? "n/a", Count(left), Count(right), Enabled ? "on" : "off", ms, _sortsSkipped, _conceptHits, _conceptLookups);
        }
        catch
        {
            return "detail unavailable";
        }
    }

    private static string Count(int value) => value < 0 ? "n/a" : value.ToString(CultureInfo.InvariantCulture);

    private static SourceScopedMemo<object, object?>[] NewMemos()
    {
        var memos = new SourceScopedMemo<object, object?>[ConceptSites];
        for (var i = 0; i < memos.Length; i++) memos[i] = new SourceScopedMemo<object, object?>();
        return memos;
    }
}
