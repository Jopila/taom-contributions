using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.TaomBuildings;

/// <summary>One building the seeding pass will add: the settlement and the variant id.</summary>
public sealed record SeedAction(string SettlementId, string VariantId);

/// <summary>
/// Gives every existing fortification the TAOM building of its culture, so a save made before the building existed
/// gets it (a new game gets it from the engine's own new-game pass through <c>TaomBuildingModel</c>). Idempotent and
/// writes no save data: it asks each fief what it holds and adds only what is absent. A fief that already holds any
/// member of a family is skipped, which also keeps a culture change after seeding from adding a second member.
/// </summary>
public sealed class TaomBuildingSeedingService
{
    private readonly TaomBuildingCatalog _catalog;
    private readonly ITownBuildingsAdapter _towns;
    private readonly IModLogger _logger;

    public TaomBuildingSeedingService(TaomBuildingCatalog catalog, ITownBuildingsAdapter towns, IModLogger logger)
    {
        _catalog = catalog;
        _towns = towns;
        _logger = logger;
    }

    /// <summary>What to add. Pure: the caller decides whether this peer may write.</summary>
    public IReadOnlyList<SeedAction> Plan(IEnumerable<FiefSnapshot> fiefs)
    {
        var plan = new List<SeedAction>();
        foreach (var fief in fiefs)
        {
            foreach (var definition in _catalog.Definitions)
            {
                if (definition.Variants.Any(v => fief.BuildingTypeIds.Contains(v.Id)))
                    continue;

                var variantId = _catalog.VariantFor(definition.FamilyId, fief.CultureId);
                if (variantId != null)
                    plan.Add(new SeedAction(fief.SettlementId, variantId));
            }
        }

        return plan;
    }

    /// <summary>
    /// Plans and adds. Main thread, once per game load. <paramref name="authority"/> false (a peer without authority, a
    /// co-op client) reads and writes nothing: the building list is shared state, which only the host writes. It fails
    /// open: an exception out of an OnGameLoaded handler loops the loading screen, so a fief that cannot take its
    /// building is counted and skipped, and the next load tries again (the pass is idempotent).
    /// </summary>
    public void Apply(bool authority)
    {
        if (!authority)
            return;

        IReadOnlyList<SeedAction> plan;
        try
        {
            plan = Plan(_towns.Fortifications());
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[TaomBuildings] seeding could not read the fiefs ({ex.GetType().Name}: {ex.Message}); it retries on the next load");
            return;
        }

        var failed = 0;
        foreach (var action in plan)
        {
            try
            {
                _towns.Add(action.SettlementId, action.VariantId);
            }
            catch (Exception)
            {
                failed++;
            }
        }

        if (failed > 0)
            _logger.LogWarning($"[TaomBuildings] seeding failed for {failed} of {plan.Count} fief(s); it retries on the next load");
        if (plan.Count - failed > 0)
            _logger.LogInfo($"[TaomBuildings] seeded {plan.Count - failed} building(s) into existing fiefs");
    }
}
