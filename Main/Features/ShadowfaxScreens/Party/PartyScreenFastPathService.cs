using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using TAOM.Core.Diagnostics;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Features.ShadowfaxScreens.Party;

/// <summary>The two phases of a party open that run once and are totalled into the open line's detail.</summary>
public enum PartyPhase
{
    InitializeTroopLists,
    RefreshValues,
}

/// <summary>
/// The two per-screen caches behind "Party Fast Open" (docs/features/shadowfax-screens.md). Vanilla rebuilds one
/// CharacterCode per row (the same character appears in several rows and in every rebuild) and walks the whole
/// item roster once per row to count a category. Keys and values are opaque objects compared by reference, so the
/// service holds no TaleWorlds type and two Equals-equal objects never collide.
/// <para>
/// The character-code cache lives for one PartyVM, from its constructor to OnFinalize. The category-count cache lives
/// only inside one InitializeTroopLists or RefreshTroopsUpgradeable call, where the roster cannot change. Outside a
/// session, or when "Party Fast Open" was off at <see cref="BeginScreen"/>, every lookup misses and every store is
/// ignored; Begin and End still run, because a toggle gates input and output, never a state transition.
/// </para>
/// Main thread only (UI), so there are no locks.
/// </summary>
public sealed class PartyScreenFastPathService
{
    private static readonly object NullCategory = new object();

    private readonly IShadowfaxScreensSettingsProvider _settings;
    private readonly IStampClock _clock;
    private readonly Dictionary<CodeKey, object> _codes = new Dictionary<CodeKey, object>();
    private readonly Dictionary<CountKey, int> _counts = new Dictionary<CountKey, int>();

    private bool _timing;
    private bool _reported;
    private long _initTicks;
    private long _refreshTicks;
    private int _rows;
    private int _codeHits;
    private int _codeMisses;
    private int _countHits;
    private int _countMisses;
    private bool _windowOpen;

    public PartyScreenFastPathService(IShadowfaxScreensSettingsProvider settings, IStampClock clock)
    {
        _settings = settings;
        _clock = clock;
    }

    public bool SessionOpen { get; private set; }

    /// <summary>Whether the session opened with the toggle on.</summary>
    public bool Enabled { get; private set; }

    /// <summary>Opens a session for a new PartyVM: clears both caches and counters, snapshots the toggle.</summary>
    public void BeginScreen()
    {
        Clear();
        _rows = _codeHits = _codeMisses = _countHits = _countMisses = 0;
        _initTicks = _refreshTicks = 0;
        _reported = false;
        SessionOpen = true;
        Enabled = Read(() => _settings.PartyFastOpen, true);
        _timing = Read(() => _settings.LogScreenOpenTimes, false);
    }

    /// <summary>Ends the session and drops everything cached. Safe to call twice or with no session.</summary>
    public void EndScreen()
    {
        Clear();
        SessionOpen = false;
        Enabled = false;
    }

    public bool TryGetCode(object character, int type, int side, out object? code)
    {
        code = null;
        if (!SessionOpen) return false;
        _rows++;
        if (Enabled && _codes.TryGetValue(new CodeKey(character, type, side), out code))
        {
            _codeHits++;
            return true;
        }
        _codeMisses++;
        return false;
    }

    public void StoreCode(object character, int type, int side, object? code)
    {
        if (!SessionOpen || !Enabled || code == null) return;
        _codes[new CodeKey(character, type, side)] = code;
    }

    /// <summary>Opens the count window. False when it was already open (nested) or no enabled session exists.</summary>
    public bool OpenCategoryWindow()
    {
        if (!SessionOpen || !Enabled || _windowOpen) return false;
        _windowOpen = true;
        return true;
    }

    /// <summary>Closes the window only when the matching <see cref="OpenCategoryWindow"/> call opened it.</summary>
    public void CloseCategoryWindow(bool opened)
    {
        if (!opened) return;
        _windowOpen = false;
        _counts.Clear();
    }

    public bool TryGetCount(object roster, object? category, out int count)
    {
        count = 0;
        if (!SessionOpen || !Enabled || !_windowOpen) return false;
        if (_counts.TryGetValue(new CountKey(roster, category ?? NullCategory), out count))
        {
            _countHits++;
            return true;
        }
        _countMisses++;
        return false;
    }

    public void StoreCount(object roster, object? category, int count)
    {
        if (!SessionOpen || !Enabled || !_windowOpen) return;
        _counts[new CountKey(roster, category ?? NullCategory)] = count;
    }

    /// <summary>Starts a phase clock; 0 when the timer is off, no session is open or the open was already reported.</summary>
    public long BeginPhase() => SessionOpen && _timing && !_reported ? _clock.Now : 0;

    public void EndPhase(PartyPhase phase, long start)
    {
        if (start == 0 || !SessionOpen || _reported) return;
        var elapsed = _clock.Now - start;
        if (phase == PartyPhase.InitializeTroopLists) _initTicks += elapsed;
        else _refreshTicks += elapsed;
    }

    /// <summary>The detail for the open line. Later phases (a Reset, a later RefreshValues) are not added to it.</summary>
    public string BuildOpenDetail()
    {
        _reported = true;
        return string.Format(CultureInfo.InvariantCulture,
            "rows {0}, codes {1} hit/{2} miss, counts {3} hit/{4} miss, init {5:0.0} ms, refresh {6:0.0} ms",
            _rows, _codeHits, _codeMisses, _countHits, _countMisses,
            LoadTimeStampLines.ToMs(_initTicks, _clock.Frequency), LoadTimeStampLines.ToMs(_refreshTicks, _clock.Frequency));
    }

    private void Clear()
    {
        _codes.Clear();
        _counts.Clear();
        _windowOpen = false;
    }

    private static bool Read(System.Func<bool> getter, bool fallback)
    {
        try { return getter(); }
        catch { return fallback; }
    }

    private readonly struct CodeKey : System.IEquatable<CodeKey>
    {
        private readonly object _character;
        private readonly int _type;
        private readonly int _side;

        public CodeKey(object character, int type, int side)
        {
            _character = character;
            _type = type;
            _side = side;
        }

        public bool Equals(CodeKey other) =>
            ReferenceEquals(_character, other._character) && _type == other._type && _side == other._side;

        public override bool Equals(object? obj) => obj is CodeKey other && Equals(other);

        public override int GetHashCode() =>
            (RuntimeHelpers.GetHashCode(_character) * 397 ^ _type) * 397 ^ _side;
    }

    private readonly struct CountKey : System.IEquatable<CountKey>
    {
        private readonly object _roster;
        private readonly object _category;

        public CountKey(object roster, object category)
        {
            _roster = roster;
            _category = category;
        }

        public bool Equals(CountKey other) =>
            ReferenceEquals(_roster, other._roster) && ReferenceEquals(_category, other._category);

        public override bool Equals(object? obj) => obj is CountKey other && Equals(other);

        public override int GetHashCode() =>
            RuntimeHelpers.GetHashCode(_roster) * 397 ^ RuntimeHelpers.GetHashCode(_category);
    }
}
