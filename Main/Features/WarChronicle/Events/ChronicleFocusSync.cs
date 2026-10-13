using System.Collections.Generic;
using TAOM.Features.ArmyTargeting;

namespace TAOM.Features.WarChronicle.Events;

/// <summary>
/// Writes the chronicle's Active siege windows into ArmyTargeting's <see cref="ArmyTargetFocusRegistry"/>
/// (#765 M4): the target, the attackers, the defender and the MCM Army Focus Multiplier. The whole set is
/// replaced each time, so a resolved window drops out by not being listed. The multiplier is read here
/// (daily, at session launch, on each change) and baked into the registry, so the scoring path never
/// reads a setting.
/// </summary>
public sealed class ChronicleFocusSync
{
    private readonly ArmyTargetFocusRegistry _registry;
    private readonly IWarChronicleSettingsProvider _settings;

    public ChronicleFocusSync(ArmyTargetFocusRegistry registry, IWarChronicleSettingsProvider settings)
    {
        _registry = registry;
        _settings = settings;
    }

    public void Push(IEnumerable<ChronicleEventDefinition> activeSieges)
    {
        var multiplier = _settings.ArmyFocusMultiplier;
        var focuses = new List<ArmyTargetFocus>();
        foreach (var siege in activeSieges)
            focuses.Add(new ArmyTargetFocus(siege.TargetSettlementId, siege.Attackers, siege.Defender, multiplier));
        _registry.Replace(focuses);
    }

    public void Clear() => _registry.Clear();
}
