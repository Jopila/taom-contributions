using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BlowDiagnostics;
using TAOM.Features.CrashReport;
using TAOM.Features.ServerSettings;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.ServerSettings;

/// <summary>
/// The hide of <c>X.Instance</c> on the four settings classes and the lazy, once-per-process load behind it.
/// A client never touches the file; a dedicated server loads it before the first read returns.
/// </summary>
[TestClass]
public class ServerSettingsSourceTests
{
    private static readonly Type[] Classes =
    {
        typeof(TaomSettings), typeof(BattleLoadDiagnosticsSettings),
        typeof(BlowDiagnosticsSettings), typeof(CrashReportSettings),
    };

    private static bool _gameLoaded;

    private FakeServerSettingsFileStore _store = null!;

    // The IL gate reads bodies that name view and sandbox types; without the game folders on the resolver they
    // come back unreadable (the HowdahCrewLookupBanTests pattern).
    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        ServerSettingsSource.ResetForTests();
        _store = FakeServerSettingsFileStore.WithText("{ \"CastleNotablesPerCastle\": 5 }");
        ServerSettingsSource.Store = _store;
    }

    [TestCleanup]
    public void Cleanup() => ServerSettingsSource.ResetForTests();

    private static void AsServer() => ServerSettingsSource.IsDedicatedServer = () => true;

    [TestMethod]
    public void EverySettingsClass_DeclaresItsOwnStaticInstance()
    {
        foreach (var type in Classes)
        {
            var instance = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            Assert.IsNotNull(instance, type.Name + " must declare its own static Instance (the hide over MCM's).");
            Assert.AreEqual(type, instance.PropertyType, type.Name + ".Instance must return its own type.");
        }
    }

    [TestMethod]
    public void NoTaomCode_CallsMcmInstanceAroundTheSource()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // GlobalSettings<X>.get_Instance is MCM's own. Only the four hide getters may call it, after asking the source.
        bool IsMcmInstance(MethodBase m) =>
            m.Name == "get_Instance" && m.DeclaringType is { IsGenericType: true }
            && m.DeclaringType.GetGenericTypeDefinition().FullName == "MCM.Abstractions.Base.Global.GlobalSettings`1";

        var callers = TaomAssemblies.FindCallers(IsMcmInstance, out var unreadable, out var scanned);

        Assert.AreEqual(0, unreadable.Count, "unreadable bodies: " + string.Join(", ", unreadable));
        Assert.IsTrue(scanned > 1000, "the scan walked only " + scanned + " bodies");
        var allowed = Classes.Select(t => t.FullName + ".get_Instance").OrderBy(n => n, StringComparer.Ordinal).ToList();
        CollectionAssert.AreEqual(allowed, callers.OrderBy(n => n, StringComparer.Ordinal).ToList(),
            "only the four hide getters may read MCM's Instance; every other read must go through X.Instance.");
    }

    [TestMethod]
    public void Client_InstanceIsMcmsOwn_AndTheFileIsNeverRead()
    {
        ServerSettingsSource.IsDedicatedServer = () => false;
        using var mcm = new CountingMcm();
        var registered = new TaomSettings();
        mcm.Register(registered);

        Assert.AreSame(registered, TaomSettings.Instance);
        Assert.AreSame(registered, TaomSettings.Instance);
        Assert.AreEqual(0, _store.ReadCount);
        Assert.AreEqual(0, _store.MissingWrites.Count + _store.ChangedWrites.Count);
    }

    [TestMethod]
    public void TestHost_InstanceIsNull()
    {
        ServerSettingsSource.ResetForTests();

        Assert.IsNull(TaomSettings.Instance);
        Assert.IsNull(CrashReportSettings.Instance);
    }

    [TestMethod]
    public void Server_FirstRead_LoadsBeforeReturning()
    {
        AsServer();

        Assert.AreEqual(5, TaomSettings.Instance!.CastleNotablesPerCastle);
        Assert.AreEqual(1, _store.ReadCount);
    }

    [TestMethod]
    public void Server_LoadsOncePerProcess_AndHandsOutTheSameObject()
    {
        AsServer();

        var first = TaomSettings.Instance;
        for (int i = 0; i < 10; i++)
            Assert.AreSame(first, TaomSettings.Instance);

        Assert.AreEqual(1, _store.ReadCount);
    }

    [TestMethod]
    public void Server_ConcurrentFirstReads_LoadOnceAndAgree()
    {
        AsServer();

        var seen = new TaomSettings?[16];
        Parallel.For(0, seen.Length, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => seen[i] = TaomSettings.Instance);

        Assert.AreEqual(1, _store.ReadCount);
        Assert.IsTrue(seen.All(s => ReferenceEquals(s, seen[0]) && s != null));
    }

    [TestMethod]
    public void Server_AllFourClasses_ComeFromTheFile()
    {
        AsServer();
        _store.Read = new ServerFileRead(ServerFileState.Loaded,
            "{ \"CastleNotablesPerCastle\": 4, \"MapViewReleaseInterval\": 30, \"EnableBlowDiagnostics\": true, \"EnableCrashCapture\": false }", null);

        Assert.AreEqual(4, TaomSettings.Instance!.CastleNotablesPerCastle);
        Assert.AreEqual(30, BattleLoadDiagnosticsSettings.Instance!.MapViewReleaseInterval);
        Assert.IsTrue(BlowDiagnosticsSettings.Instance!.EnableBlowDiagnostics);
        Assert.IsFalse(CrashReportSettings.Instance!.EnableCrashCapture);
    }

    [TestMethod]
    public void Server_LazyProviderAccessor_SeesFileValues()
    {
        AsServer();
        _store.Read = new ServerFileRead(ServerFileState.Loaded, "{ \"EnableBlowDiagnostics\": true }", null);

        // BlowDiagnosticsSettingsProvider caches BlowDiagnosticsSettings.Instance on its first read, as the other hot-path providers do.
        Assert.IsTrue(new BlowDiagnosticsSettingsProvider().IsEnabled);
    }

    [TestMethod]
    public void Server_HitchProbe_DefaultsOff()
    {
        AsServer();
        _store.Read = new ServerFileRead(ServerFileState.Loaded, "{}", null);

        Assert.IsFalse(new BattleLoadDiagnosticsSettingsProvider().HitchProbeEnabled,
            "a server without the key must not install Patch98, as before the settings file existed");
    }

    [TestMethod]
    public void Server_HitchProbe_FileCanEnable()
    {
        AsServer();
        _store.Read = new ServerFileRead(ServerFileState.Loaded, "{ \"EnableHitchProbe\": true }", null);

        Assert.IsTrue(new BattleLoadDiagnosticsSettingsProvider().HitchProbeEnabled);
    }

    [TestMethod]
    public void Server_Report_NamesTheLocationAndCounts()
    {
        AsServer();
        _store.Read = new ServerFileRead(ServerFileState.Loaded, "{ \"CastleNotablesPerCastle\": 4, \"Nope\": 1 }", null);

        _ = TaomSettings.Instance;

        var report = ServerSettingsSource.Report!;
        Assert.AreEqual(ServerFileState.Loaded, report.State);
        Assert.AreEqual(1, report.Applied);
        Assert.AreEqual(1, report.Unknown);
        Assert.AreEqual(_store.Where.Path, report.Location!.Path);
    }

    [TestMethod]
    public void ReentrantReadDuringLoad_ReturnsNull()
    {
        AsServer();
        TaomSettings? inner = new TaomSettings();
        _store.OnTryRead = () => inner = ServerSettingsSource.Get<TaomSettings>();

        var outer = TaomSettings.Instance;

        Assert.IsNull(inner, "a read made while the file is being loaded must not recurse or deadlock");
        Assert.IsNotNull(outer);
    }

    [TestMethod]
    public void LoaderThrow_FallsBackToMcmNull()
    {
        AsServer();
        _store.OnTryRead = () => throw new InvalidOperationException("boom");

        Assert.IsNull(TaomSettings.Instance);
        Assert.IsNull(TaomSettings.Instance);
        Assert.AreEqual(1, _store.ReadCount, "a failed load must not retry on every read");
        Assert.IsTrue(ServerSettingsSource.Report!.Lines.Any(l => l.Level == ServerSettingsLevel.Error && l.Text.Contains("boom")));
    }

    [TestMethod]
    public void LoaderThrow_ReportsLoaderFailed_NotUnreadable()
    {
        AsServer();
        _store.OnTryRead = () => throw new InvalidOperationException("boom");

        _ = TaomSettings.Instance;

        Assert.AreEqual(ServerFileState.LoaderFailed, ServerSettingsSource.Report!.State);
    }

    [TestMethod]
    public void IoError_StillReportsUnreadable_AndServesTheSet()
    {
        AsServer();
        _store.Read = new ServerFileRead(ServerFileState.Unreadable, null, "IOException: locked");

        Assert.IsNotNull(TaomSettings.Instance);
        Assert.AreEqual(ServerFileState.Unreadable, ServerSettingsSource.Report!.State);
        StringAssert.Contains(ServerSettingsSource.Report.BootLine(), "every setting is at its server default");
    }

    // A server override that nobody documents is a silent behaviour split between client and server.
    [TestMethod]
    public void ServerSet_DiffersFromFreshInstances_OnlyInEnableHitchProbe()
    {
        var set = new ServerSettingsSet();
        var differences = new System.Collections.Generic.List<string>();
        foreach (var type in Classes)
        {
            var fresh = Activator.CreateInstance(type)!;
            var server = set.For(type);
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);
            foreach (var p in properties)
            {
                if (!SameValue(p.GetValue(fresh), p.GetValue(server)))
                    differences.Add(type.Name + "." + p.Name);
            }
        }

        CollectionAssert.AreEqual(new[] { "BattleLoadDiagnosticsSettings.EnableHitchProbe" }, differences,
            "the server settings set differs from the compiled defaults in: " + string.Join(", ", differences)
            + ". Document any new server override in docs/features/server-settings.md and ServerSettingsSet.cs, then add it here.");
    }

    // Dropdowns are fresh objects per instance, so compare what they select, not the reference.
    private static bool SameValue(object? a, object? b)
    {
        if (a is MCM.Common.Dropdown<string> da && b is MCM.Common.Dropdown<string> db)
            return da.SelectedIndex == db.SelectedIndex && da.Count == db.Count;
        return Equals(a, b);
    }

    [TestMethod]
    public void DefaultSeam_ReadsGameNetworkIsDedicatedServer()
    {
        var method = typeof(ServerSettingsSource).GetMethod("DefaultIsDedicatedServer", BindingFlags.NonPublic | BindingFlags.Static)!;
        var expected = typeof(GameNetwork).GetProperty(nameof(GameNetwork.IsDedicatedServer))!.GetMethod!;

        var calls = IlCallScanner.ExtractCalledMethods(method, method.GetMethodBody()!.GetILAsByteArray()!).ToList();

        Assert.IsTrue(calls.Any(c => IlCallScanner.SameMethod(c, expected)),
            "DefaultIsDedicatedServer must read GameNetwork.IsDedicatedServer; called: "
            + string.Join(", ", calls.Select(c => c.DeclaringType?.Name + "." + c.Name)));
    }
}
