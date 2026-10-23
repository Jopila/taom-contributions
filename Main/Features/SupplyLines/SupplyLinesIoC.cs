using DryIoc;

namespace TAOM.Features.SupplyLines;

public static class SupplyLinesIoC
{
    public static void RegisterSupplyLinesFeature(IContainer container)
    {
        container.Register<ISupplyLinesSettingsProvider, SupplyLinesSettingsProvider>(Reuse.Singleton);
        container.Register<ISupplyPricingService, SupplyPricingService>(Reuse.Singleton);
        container.Register<ISupplyOrderEngine, SupplyOrderEngine>(Reuse.Singleton);
        container.Register<ISupplySourceService, SupplySourceService>(Reuse.Singleton);
        container.Register<ISupplyCaravanService, SupplyCaravanService>(Reuse.Singleton);
        // Client-only implementation (ADR-013): core's null, replaced by TAOM.Client's.
        container.Register<ISupplyRouteVisualService, NullSupplyRouteVisualService>(Reuse.Singleton);

        // Singleton: the order book lives here between SyncData calls; a transient would lose
        // every order between the behavior's save and load halves.
        container.Register<ISupplyOrderService, SupplyOrderService>(Reuse.Singleton);
    }
}
