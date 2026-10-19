using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ShadowfaxScreens.Diagnostics;

namespace TAOM.Features.ShadowfaxScreens.Inventory;

/// <summary>
/// The static helpers the two IL rewrites call, and the wiring the Harmony hooks reach (handed over by
/// ShadowfaxScreensModule.InitializeStatics; every helper is vanilla's own call while unwired or switched off).
/// </summary>
internal static class ShadowfaxInventoryCalls
{
    internal static InventoryOpenFastPathService? Service { get; private set; }

    internal static IInventoryOpenAdapter? Adapter { get; private set; }

    internal static IModLogger? Logger { get; private set; }

    internal static MethodInfo SortByNameMethod { get; } =
        typeof(ShadowfaxInventoryCalls).GetMethod(nameof(SortByNameOrPassThrough))!;

    internal static IReadOnlyList<MethodInfo> ConceptMethods { get; } = new[]
    {
        typeof(ShadowfaxInventoryCalls).GetMethod(nameof(CachedConcept0))!,
        typeof(ShadowfaxInventoryCalls).GetMethod(nameof(CachedConcept1))!,
    };

    internal static void Initialize(InventoryOpenFastPathService? service, IInventoryOpenAdapter? adapter, IModLogger? logger)
    {
        Service = service;
        Adapter = adapter;
        Logger = logger;
    }

    /// <summary>Starts the timed open (null while "Log Screen Open Times" is off) and the service's totals for it.</summary>
    internal static ScreenOpenWindow? BeginOpen()
    {
        var window = ScreenOpenTiming.Begin("Inventory", "open");
        if (window != null) Service?.BeginOpen();
        return window;
    }

    /// <summary>Ends the timed open with the mode, the side counts and the service's phase totals as its detail.</summary>
    internal static void EndOpen(ScreenOpenWindow? window, Exception? exception)
    {
        if (window == null) return;
        string? detail = null;
        try
        {
            var mode = "n/a";
            int left = -1, right = -1;
            Adapter?.TryRead(out mode, out left, out right);
            detail = Service?.EndOpen(mode, left, right);
        }
        catch
        {
            // the detail is optional; the open's own time still logs
        }
        ScreenOpenTiming.End(window, exception, detail);
    }

    internal static long? BeginRows() => Service?.BeginRows();

    internal static void EndRows(long? start) => Service?.EndRows(start);

    /// <summary>Replaces <c>OrderBy(key)</c> in InitializeInventory. The constructor and AfterReset sort the lists afterwards.</summary>
    public static IEnumerable<ItemRosterElement> SortByNameOrPassThrough(
        IEnumerable<ItemRosterElement> source, Func<ItemRosterElement, string> key)
    {
        var service = Service;
        return service != null && service.ShouldSkipNameSort() ? source : source.OrderBy(key);
    }

    public static Concept? CachedConcept0(IEnumerable<Concept> source, Func<Concept, bool> predicate)
        => Lookup(0, source, predicate);

    public static Concept? CachedConcept1(IEnumerable<Concept> source, Func<Concept, bool> predicate)
        => Lookup(1, source, predicate);

    // The source is Campaign.Current.Concepts, one list per campaign; a null result is cached, an exception
    // (a duplicate id) propagates as vanilla's does and is never stored.
    private static Concept? Lookup(int site, IEnumerable<Concept> source, Func<Concept, bool> predicate)
    {
        var service = Service;
        if (service == null || source == null)
            return source!.SingleOrDefault(predicate);

        if (service.TryGetConcept(site, source, out var cached))
            return cached as Concept;

        var found = source.SingleOrDefault(predicate);
        service.StoreConcept(site, source, found);
        return found;
    }
}
