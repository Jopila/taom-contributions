using System;
using System.Collections.Generic;

namespace TAOM.Features.TaomBuildings;

/// <summary>
/// One line of per-level effect text for a TAOM building: a <c>{=KEY}Default</c> template and the one text variable it
/// fills (the engine's own effect lines work the same way, with BONUS_AMOUNT_PERCENT). Kept as plain strings so the
/// catalog stays free of engine text types; the UI patch builds the TextObject.
/// </summary>
public sealed record BuildingEffectText(string Template, string Variable, string Value);

/// <summary>
/// One concrete building type of a family: its id (lowercase, because the town screen derives the sprite code from the
/// lowercased id), its name and description as <c>{=KEY}Default</c> strings, and the cultures that receive it. An empty
/// culture list marks the family's default, given to every culture no sibling lists.
/// </summary>
public sealed record TaomBuildingVariant(string Id, string Name, string Explanation, IReadOnlyList<string> CultureIds);

/// <summary>
/// A family of TAOM-added town buildings that differ by name and description only (a Hidden Door and a Sally Port are
/// the same building for different cultures). Each fief holds at most one member. Costs are the three level costs
/// (engine <c>productionCosts</c>), <see cref="SpriteName"/> is the sprite the town screen shows (its small variant adds
/// <c>_t</c>), and <see cref="LevelText"/> gives the effect line for levels 1 to 3.
/// </summary>
public sealed record TaomBuildingDefinition(
    string FamilyId,
    IReadOnlyList<TaomBuildingVariant> Variants,
    IReadOnlyList<int> Costs,
    bool IsMilitary,
    string SpriteName,
    Func<int, BuildingEffectText?> LevelText);
