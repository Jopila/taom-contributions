using TAOM.Core.Validation;
using TAOM.Features.WarOfTheRingMomentum.Domain;

namespace TAOM.Features.WarOfTheRingMomentum;

/// <summary>
/// The scripted momentum award (#765, M6; docs/features/war-of-the-ring-momentum.md "Chronicle awards").
/// Units are displayed momentum, scaled ×100 into the store like every config value
/// (<see cref="MomentumEventService"/>), and the event lasts <c>durationsHours.chronicle</c>. Only the War
/// Chronicle calls it, on the co-op authority, from a resolution that is itself written once.
/// </summary>
public sealed class MomentumAwardService : IMomentumAwardService
{
    /// <summary>The largest single award, in displayed units: the size of the shipped battle cap.</summary>
    public const int MaxScriptedUnits = 300;

    private readonly IMomentumStateStore _store;
    private readonly IMomentumConfigProvider _config;
    private readonly IMomentumSettingsProvider _settings;

    public MomentumAwardService(IMomentumStateStore store, IMomentumConfigProvider config, IMomentumSettingsProvider settings)
    {
        _store = store;
        _config = config;
        _settings = settings;
    }

    public bool TryAwardScripted(MomentumSide side, int units, string description, double nowHours)
    {
        if (!_settings.MomentumEnabled || units < 1 || !FiniteFloatValidator.IsFinite(nowHours))
            return false;

        var state = _store.State;
        if (!state.HasWarStarted || state.HasWarEnded)
            return false;

        var clamped = units > MaxScriptedUnits ? MaxScriptedUnits : units;
        var endHours = nowHours + _config.GetConfig().DurationsHours.GetHours(MomentumActionType.Chronicle);
        state.GetSide(side).AddEvent(new MomentumEvent(
            clamped * MomentumWarState.MomentumScale, description, MomentumActionType.Chronicle, endHours));
        _store.NotifyMomentumChanged();
        return true;
    }
}
