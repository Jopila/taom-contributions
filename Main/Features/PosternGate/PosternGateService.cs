using System.Collections.Generic;
using TAOM.Features.TaomBuildings;
using TAOM.Adapters;

namespace TAOM.Features.PosternGate;

/// <summary>
/// The Postern Gate's rule (docs/features/postern-gate.md): a fief with the building cuts the troops lost when a party
/// breaks into or out of that fief's siege by the level's reduction (50, 67 and 75 percent by default). The cut is a
/// factor added to the engine's number, so it composes with the Improviser perk by multiplication: the engine adds
/// factors, so the cut is taken of what is left after the factors already on the number.
/// </summary>
public sealed class PosternGateService
{
    private readonly IPosternGateConfigProvider _config;
    private readonly IPosternGateSettingsProvider _settings;
    private readonly ITownBuildingsAdapter _towns;
    private readonly IReadOnlyList<string> _typeIds;

    public PosternGateService(IPosternGateConfigProvider config, IPosternGateSettingsProvider settings,
        ITownBuildingsAdapter towns, TaomBuildingCatalog catalog)
    {
        _config = config;
        _settings = settings;
        _towns = towns;
        _typeIds = catalog.VariantIds(PosternGateDefinition.FamilyId);
    }

    /// <summary>
    /// The factor to add: <c>-r(level) * (1 + existingSumOfFactors)</c>. Zero when the switch is off, at level 0 and
    /// for a level outside 1 to 3.
    /// </summary>
    public float FactorFor(int level, float existingSumOfFactors)
    {
        var reductions = _config.Reductions;
        if (!_settings.CutEnabled || level < 1 || level > reductions.Count)
            return 0f;
        return -reductions[level - 1] * (1f + existingSumOfFactors);
    }

    /// <summary>
    /// The factor for the besieged settlement's Postern Gate, zero when there is none, the switch is off or the id is
    /// missing. The settlement is the besieged one, never the main party's.
    /// </summary>
    public float CutFor(string? settlementId, float existingSumOfFactors) =>
        string.IsNullOrEmpty(settlementId) || !_settings.CutEnabled
            ? 0f
            : FactorFor(_towns.GetLevel(settlementId!, _typeIds), existingSumOfFactors);
}
