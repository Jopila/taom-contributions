using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Features.TaomBuildings;

namespace TAOM.Tests.Features.TaomBuildings;

/// <summary>A two-variant family for the catalog and seeding tests: a default and one culture variant.</summary>
internal static class TaomBuildingFixtures
{
    public const string Family = "test_wall";
    public const string DefaultId = "building_taom_test_wall";
    public const string ElfId = "building_taom_test_wall_elf";

    public static TaomBuildingDefinition Wall() => new(
        Family,
        new[]
        {
            new TaomBuildingVariant(DefaultId, "{=t_wall}Wall", "{=t_wall_d}A wall.", new string[0]),
            new TaomBuildingVariant(ElfId, "{=t_wall_e}Way", "{=t_wall_ed}A way.", new[] { "rivendell", "lindon" }),
        },
        new[] { 100, 200, 300 },
        true,
        "taom_building_test_wall",
        level => level is >= 1 and <= 3 ? new BuildingEffectText("{=t_effect}{PERCENT}% less", "PERCENT", (level * 10).ToString()) : null);

    public static TaomBuildingCatalog Catalog() => new(new[] { Wall() });

    /// <summary>A family with no default variant, so a culture it does not list has no variant at all.</summary>
    public static TaomBuildingDefinition Restricted() => new(
        "test_restricted",
        new[] { new TaomBuildingVariant("building_taom_test_restricted_elf", "{=t_r}Way", "{=t_rd}A way.", new[] { "lindon" }) },
        new[] { 100, 200, 300 },
        true,
        "taom_building_test_restricted",
        _ => null);

    public static FiefSnapshot Fief(string id, string? culture, params string[] typeIds) =>
        new(id, culture, new List<string>(typeIds));
}
