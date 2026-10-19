using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TAOM.Core.Logging;

namespace TAOM.Features.ShadowfaxScreens.Inventory;

/// <summary>
/// The pure IL rewrite behind the concept memo. The <c>SPItemVM</c> constructor looks up two concepts with
/// <c>Concept.All.SingleOrDefault(predicate)</c>; its two <c>SingleOrDefault&lt;Concept&gt;</c> calls become
/// <see cref="ShadowfaxInventoryCalls.CachedConcept0"/> and <see cref="ShadowfaxInventoryCalls.CachedConcept1"/> in
/// stream order (same stack, mutated in place so labels stay). Each site must be preceded by
/// <c>Concept::get_All</c> within <see cref="AnchorWindow"/> instructions (the predicate's cached delegate sits in
/// between, and a release optimizer can expand it); with exactly two sites, otherwise the IL is returned unmodified
/// with one warning, which makes a second pass a no-op. Never throws.
/// </summary>
internal static class ConceptLookupRewrite
{
    internal const int ExpectedSites = 2;
    internal const int AnchorWindow = 24;

    internal static readonly MethodInfo SingleTarget = typeof(Enumerable).GetMethods()
        .Single(m => m.Name == nameof(Enumerable.SingleOrDefault) && m.GetParameters().Length == 2
            && m.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2)
        .MakeGenericMethod(typeof(Concept));

    internal static readonly MethodInfo GetAllTarget = AccessTools.PropertyGetter(typeof(Concept), nameof(Concept.All))!;

    internal static List<CodeInstruction> Rewrite(List<CodeInstruction> instructions, IModLogger? logger, out int swapped)
    {
        swapped = 0;
        try
        {
            var sites = new List<int>();
            for (var i = 0; i < instructions.Count; i++)
            {
                if (instructions[i].Calls(SingleTarget)) sites.Add(i);
            }

            if (sites.Count != ExpectedSites)
            {
                logger?.LogWarning($"[ShadowfaxInventory] concept lookup: found {sites.Count} SingleOrDefault call sites, expected {ExpectedSites}; IL left vanilla.");
                return instructions;
            }

            if (sites.Any(site => !HasAnchor(instructions, site)))
            {
                logger?.LogWarning("[ShadowfaxInventory] concept lookup: a SingleOrDefault call is not preceded by Concept.get_All; IL left vanilla.");
                return instructions;
            }

            var helpers = ShadowfaxInventoryCalls.ConceptMethods;
            for (var k = 0; k < sites.Count; k++)
            {
                instructions[sites[k]].opcode = OpCodes.Call;
                instructions[sites[k]].operand = helpers[k];
            }
            swapped = sites.Count;
            return instructions;
        }
        catch (Exception ex)
        {
            swapped = 0;
            try { logger?.LogWarning("[ShadowfaxInventory] concept lookup rewrite faulted: " + ex.GetType().Name + ": " + ex.Message); }
            catch { /* diagnostic only */ }
            return instructions;
        }
    }

    private static bool HasAnchor(List<CodeInstruction> instructions, int site)
    {
        for (var i = site - 1; i >= 0 && i >= site - AnchorWindow; i--)
        {
            if (instructions[i].Calls(GetAllTarget)) return true;
            if (instructions[i].Calls(SingleTarget)) return false; // the previous site's anchor is not this one's
        }
        return false;
    }
}
