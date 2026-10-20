using TAOM.Features.FellFoes.Domain;

namespace TAOM.Features.FellFoes;

/// <summary>
/// Production wire-up of <see cref="IFellFoesSettingsProvider"/> over the MCM <c>TaomSettings</c> singleton.
/// Read once a second, so each read resolves the MCM instance when it has not been taken yet and keeps it
/// (a null is never kept, so a read before MCM is up cannot pin the defaults). Every number is validated by
/// <see cref="FellFoesTunables.Create"/>: a stale json2 value falls back to the shipped default.
/// </summary>
public class FellFoesSettingsProvider : IFellFoesSettingsProvider
{
    private TaomSettings? _settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public FellFoesSettingsProvider() { }

    internal FellFoesSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool CorneredRoutersEnabled => Settings?.FellFoesCorneredRouters ?? true;

    public bool ArchersTargetPlayerEnabled => Settings?.FellFoesArchersTargetPlayer ?? true;

    public bool StragglersLeaveFieldEnabled => Settings?.FellFoesStragglersLeave ?? true;

    public bool DebugEnabled => Settings?.FellFoesDebug ?? false;

    public FellFoesTunables Tunables
    {
        get
        {
            var s = Settings;
            return s == null
                ? FellFoesTunables.Create()
                : FellFoesTunables.Create(
                    corneredMaxFoes: s.FellFoesCorneredMaxFoes,
                    archerChance: s.FellFoesArcherChance,
                    archerMaxHolding: s.FellFoesArcherMaxHolding,
                    stragglerMaxCount: s.FellFoesStragglerMaxCount,
                    stragglerHoldSeconds: s.FellFoesStragglerHoldSeconds);
        }
    }
}
