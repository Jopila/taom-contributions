using System;
using System.Collections.Generic;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BlowDiagnostics;
using TAOM.Features.CrashReport;

namespace TAOM.Features.ServerSettings;

/// <summary>
/// One fresh instance of each of the four TAOM MCM settings classes, at the compiled defaults except
/// <c>EnableHitchProbe</c>, which a server keeps off. On a dedicated server
/// the file's values are applied to these, and <c>X.Instance</c> hands them out for the rest of the process.
/// </summary>
internal sealed class ServerSettingsSet
{
    /// <summary>The four classes, in the order the schema and the defaults reference list them.</summary>
    internal static readonly Type[] Classes =
    {
        typeof(TaomSettings), typeof(BattleLoadDiagnosticsSettings),
        typeof(BlowDiagnosticsSettings), typeof(CrashReportSettings),
    };

    private readonly Dictionary<Type, object> _instances = new()
    {
        [typeof(TaomSettings)] = new TaomSettings(),
        // The hitch probe installs Harmony patches (Patch98) that a server never ran before this file existed: off unless the file sets it.
        [typeof(BattleLoadDiagnosticsSettings)] = new BattleLoadDiagnosticsSettings { EnableHitchProbe = false },
        [typeof(BlowDiagnosticsSettings)] = new BlowDiagnosticsSettings(),
        [typeof(CrashReportSettings)] = new CrashReportSettings(),
    };

    public T? Get<T>() where T : class =>
        _instances.TryGetValue(typeof(T), out var instance) ? (T)instance : null;

    /// <summary>The instance that owns <paramref name="owner"/>'s properties.</summary>
    public object For(Type owner) => _instances[owner];
}
