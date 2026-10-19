using System;
using System.Globalization;
using TAOM.Core.Diagnostics;
using TAOM.Features.CoopInterop;
using TAOM.Features.LoadTimeStamps;

namespace TAOM.Features.ShadowfaxScreens.Clan;

/// <summary>
/// Clan fast open (docs/features/shadowfax-screens.md "Clan fast open"). Vanilla builds the clan screen's party lists
/// three times per open; the second and third builds are the ones <c>ClanPartiesVM.RefreshValues</c> triggers for the
/// same instance a moment after the first. This service decides which build to skip: only the one made inside that
/// instance's own <c>RefreshValues</c>. A direct call to the list builder (the screen's category refresh, a co-op
/// handler) is never inside a token, so it always builds. No TaleWorlds types: the hooks pass the view model as an object.
/// The owner is thread-static and every call runs on the UI main thread.
/// </summary>
internal sealed class ClanScreenFastPathService
{
    [ThreadStatic] private static RefreshToken? _current;

    private readonly IShadowfaxScreensSettingsProvider _settings;
    private readonly ICoopPresenceProvider _coop;
    private readonly IStampClock _clock;

    private bool _windowOpen;
    private bool _windowFastOn;
    private int _run;
    private int _skipped;
    private long _runTicks;

    public ClanScreenFastPathService(IShadowfaxScreensSettingsProvider settings, ICoopPresenceProvider coop, IStampClock clock)
    {
        _settings = settings;
        _coop = coop;
        _clock = clock;
    }

    /// <summary>Opens a refresh scope for <paramref name="partiesVm"/>, or returns null when the fast path is off.</summary>
    public object? EnterRefresh(object partiesVm)
    {
        if (!FastPathOn()) return null;
        var token = new RefreshToken(partiesVm, _current);
        _current = token;
        return token;
    }

    /// <summary>True only for the instance whose refresh scope is the innermost one; the build is then skipped.</summary>
    public bool ShouldSkipListBuild(object partiesVm)
    {
        var token = _current;
        if (token == null || !ReferenceEquals(token.Owner, partiesVm)) return false;
        token.Skipped = true;
        if (_windowOpen) _skipped++;
        return true;
    }

    /// <summary>Closes the scope the token opened and returns whether a build was skipped inside it. A null token does nothing.</summary>
    public bool ExitRefresh(object? token)
    {
        if (token is not RefreshToken scope) return false;
        _current = scope.Previous;
        return scope.Skipped;
    }

    /// <summary>Starts counting list builds for one screen open.</summary>
    public void BeginOpen()
    {
        _run = 0;
        _skipped = 0;
        _runTicks = 0;
        _windowFastOn = FastPathOn();
        _windowOpen = true;
    }

    /// <summary>Closes the counting window and returns the detail text for the open line (empty when no window was open).</summary>
    public string EndOpen()
    {
        if (!_windowOpen) return "";
        _windowOpen = false;
        var ms = LoadTimeStampLines.ToMs(_runTicks, _clock.Frequency);
        return string.Format(CultureInfo.InvariantCulture, "fast open {0}; list builds: {1} run, {2} skipped, {3:0.0} ms",
            _windowFastOn ? "on" : "off", _run, _skipped, ms);
    }

    /// <summary>The start stamp of a build that runs, or 0 when no window is open (nothing is then counted).</summary>
    public long BeginBuild() => _windowOpen ? _clock.Now : 0;

    public void EndBuild(long startTick)
    {
        if (startTick == 0 || !_windowOpen) return;
        _run++;
        _runTicks += _clock.Now - startTick;
    }

    private bool FastPathOn()
    {
        try { return _settings.ClanFastOpen && !_coop.IsCoopActive; }
        catch { return false; }
    }

    private sealed class RefreshToken
    {
        public RefreshToken(object owner, RefreshToken? previous)
        {
            Owner = owner;
            Previous = previous;
        }

        public object Owner { get; }
        public RefreshToken? Previous { get; }
        public bool Skipped { get; set; }
    }
}
