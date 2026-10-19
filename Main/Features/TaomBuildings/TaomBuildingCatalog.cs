using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.TaomBuildings;

/// <summary>The narrow seam the two town-screen patches call: sprite and effect line by building type id.</summary>
public interface ITaomBuildingUiHook
{
    /// <summary>The sprite name for a TAOM building type, or null when the id is not TAOM's.</summary>
    string? SpriteFor(string typeId, bool small);

    /// <summary>The effect line for a TAOM building type at level 1 to 3, or null (not TAOM's, or no such level).</summary>
    BuildingEffectText? LevelText(string typeId, int level);
}

/// <summary>
/// Everything the engine-free code asks about TAOM's building families, built once from the registered definitions. A
/// family or variant id registered twice, or a definition without exactly three costs, is a programming error and throws.
/// </summary>
public sealed class TaomBuildingCatalog : ITaomBuildingUiHook
{
    private readonly Dictionary<string, TaomBuildingDefinition> _byFamily = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TaomBuildingDefinition> _byVariant = new(StringComparer.Ordinal);

    public TaomBuildingCatalog(IEnumerable<TaomBuildingDefinition> definitions)
    {
        var list = new List<TaomBuildingDefinition>();
        foreach (var definition in definitions)
        {
            if (definition.Costs.Count != 3)
                throw new ArgumentException($"Building family '{definition.FamilyId}' must have exactly three costs, one per level.", nameof(definitions));
            if (_byFamily.ContainsKey(definition.FamilyId))
                throw new ArgumentException($"Building family '{definition.FamilyId}' is registered twice.", nameof(definitions));
            _byFamily.Add(definition.FamilyId, definition);
            foreach (var variant in definition.Variants)
            {
                if (_byVariant.ContainsKey(variant.Id))
                    throw new ArgumentException($"Building type '{variant.Id}' is registered twice.", nameof(definitions));
                _byVariant.Add(variant.Id, definition);
            }

            list.Add(definition);
        }

        Definitions = list;
    }

    public IReadOnlyList<TaomBuildingDefinition> Definitions { get; }

    /// <summary>The variant id a fief of this culture receives, or null for an unknown family.</summary>
    public string? VariantFor(string familyId, string? cultureId)
    {
        if (!_byFamily.TryGetValue(familyId, out var definition))
            return null;

        TaomBuildingVariant? fallback = null;
        foreach (var variant in definition.Variants)
        {
            if (variant.CultureIds.Count == 0)
                fallback ??= variant;
            else if (cultureId != null && variant.CultureIds.Contains(cultureId))
                return variant.Id;
        }

        return fallback?.Id;
    }

    /// <summary>True when the type is the variant the culture receives, false for another variant, null when not TAOM's (defer to the base model).</summary>
    public bool? CanAdd(string typeId, string? cultureId)
    {
        if (!_byVariant.TryGetValue(typeId, out var definition))
            return null;
        return string.Equals(VariantFor(definition.FamilyId, cultureId), typeId, StringComparison.Ordinal);
    }

    /// <summary>Every variant id of a family (empty for an unknown family).</summary>
    public IReadOnlyList<string> VariantIds(string familyId) =>
        _byFamily.TryGetValue(familyId, out var definition)
            ? definition.Variants.Select(v => v.Id).ToList()
            : Array.Empty<string>();

    public string? SpriteFor(string typeId, bool small) =>
        _byVariant.TryGetValue(typeId, out var definition)
            ? small ? definition.SpriteName + "_t" : definition.SpriteName
            : null;

    public BuildingEffectText? LevelText(string typeId, int level) =>
        level is >= 1 and <= 3 && _byVariant.TryGetValue(typeId, out var definition)
            ? definition.LevelText(level)
            : null;
}
