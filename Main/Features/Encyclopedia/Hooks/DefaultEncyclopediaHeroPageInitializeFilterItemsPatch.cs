using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TaleWorlds.CampaignSystem.Encyclopedia;
using TaleWorlds.CampaignSystem.Encyclopedia.Pages;
using TAOM.Core.Logging;
using TAOM.Features.Encyclopedia.Adapters;

namespace TAOM.Features.Encyclopedia.Hooks;

/// <summary>
/// Patch119, #907: appends the Race and Age filter groups to the hero page's filters. The page builds its filter list
/// once, in its constructor, so the category applies at OnSubModuleLoad (SandBox builds the pages in its own
/// OnGameInitializationFinished, before TAOM's). The co-op module postfixes the same method with a lazy AddItem, so
/// the result is never cast to a List: it is copied, appended to and handed back. A throw leaves the incoming result.
/// Callers, thread and PatchShield notes: docs/reference/harmony-patch-registry.md "Patch119_EncyclopediaHeroFilters".
/// </summary>
[HarmonyPatch(typeof(DefaultEncyclopediaHeroPage), "InitializeFilterItems")]
[HarmonyPatchCategory(EncyclopediaFiltersModule.PatchCategory)]
public static class DefaultEncyclopediaHeroPageInitializeFilterItemsPatch
{
    private static IEncyclopediaHeroFilterAdapter? _adapter;
    private static IModLogger? _logger;

    internal static void Initialize(IEncyclopediaHeroFilterAdapter? adapter, IModLogger? logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    [HarmonyPostfix]
    public static void Postfix(ref IEnumerable<EncyclopediaFilterGroup> __result)
    {
        try
        {
            if (_adapter == null) return;
            var list = __result?.ToList() ?? new List<EncyclopediaFilterGroup>();
            list.AddRange(_adapter.BuildGroups());
            __result = list;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Patch119 (hero filters): kept the incoming filter list. {ex.GetType().Name}: {ex.Message}");
        }
    }
}
