using System;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.ServerSettings;

/// <summary>
/// Where the four settings classes' <c>X.Instance</c> hides look first. MCM is not loaded on a dedicated server,
/// so there MCM's <c>Instance</c> is null; this loads <c>taom-server-settings.json</c> into fresh instances on the
/// first read, under a lock, once per process, and hands those out. On a client the first read caches "no
/// override" without touching the file, and each later read costs one volatile field read.
/// <para>The load is lazy because services resolve during <c>IoC.Configure</c> and some keep the settings
/// reference from their constructor: whichever reads first, the file is applied before the read returns.
/// Never throws: any failure is a report line and the MCM fallback (null on a server).</para>
/// </summary>
internal static class ServerSettingsSource
{
    private static readonly object Gate = new();
    private static volatile Loaded? _loaded;
    private static bool _loading;
    private static IServerSettingsFileStore? _store;

    /// <summary>Test seam; the default reads <c>GameNetwork.IsDedicatedServer</c>.</summary>
    internal static Func<bool> IsDedicatedServer { get; set; } = DefaultIsDedicatedServer;

    /// <summary>Test seam; the default is the real file store, built on first use.</summary>
    internal static IServerSettingsFileStore Store
    {
        get => _store ??= new ServerSettingsFileStore();
        set => _store = value;
    }

    /// <summary>The server's load report; null before the first read and on a client.</summary>
    internal static ServerSettingsReport? Report => _loaded?.Report;

    /// <summary>The file-backed instance on a dedicated server; null on a client, during the load itself, or after a failed load.</summary>
    internal static T? Get<T>() where T : class => (_loaded ?? Load())?.Set?.Get<T>();

    internal static void ResetForTests()
    {
        lock (Gate)
        {
            _loaded = null;
            _loading = false;
            _store = null;
            IsDedicatedServer = DefaultIsDedicatedServer;
        }
    }

    private static Loaded? Load()
    {
        lock (Gate)
        {
            if (_loaded != null) return _loaded;
            // Monitor is re-entrant: a settings read made by the load itself (a constructor, a store) lands here.
            if (_loading) return null;
            _loading = true;
            try
            {
                _loaded = LoadOnce();
                return _loaded;
            }
            finally
            {
                _loading = false;
            }
        }
    }

    private static Loaded LoadOnce()
    {
        ServerSettingsLocation? location = null;
        try
        {
            if (!IsDedicatedServer()) return Loaded.NoOverride;

            var store = Store;
            location = store.Locate();
            var read = store.TryRead(location.Path);
            var set = new ServerSettingsSet();
            ServerSettingsReport report;
            if (read.State == ServerFileState.Loaded)
            {
                report = ServerSettingsReader.Read(read.Text, set);
            }
            else
            {
                report = new ServerSettingsReport(read.State);
                if (read.State == ServerFileState.Unreadable)
                    report.Add(ServerSettingsLevel.Error,
                        "could not read " + location.Path + " (" + read.Error + "); every setting keeps its server default");
            }

            report.Location = location;
            return new Loaded(set, report);
        }
        catch (Exception ex)
        {
            var failed = new ServerSettingsReport(ServerFileState.LoaderFailed) { Location = location };
            failed.Add(ServerSettingsLevel.Error,
                "loading the server settings failed (" + ex.GetType().Name + ": " + ex.Message + "); no settings instance is served, so each provider's own fallback applies");
            return new Loaded(null, failed);
        }
    }

    // A constant getter in both DLLs (false on the client, true on the server); LoadOnce catches anyway.
    private static bool DefaultIsDedicatedServer() => GameNetwork.IsDedicatedServer;

    private sealed class Loaded
    {
        internal static readonly Loaded NoOverride = new(null, null);

        internal Loaded(ServerSettingsSet? set, ServerSettingsReport? report)
        {
            Set = set;
            Report = report;
        }

        internal ServerSettingsSet? Set { get; }

        internal ServerSettingsReport? Report { get; }
    }
}
