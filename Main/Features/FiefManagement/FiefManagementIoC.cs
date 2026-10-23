using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.FiefManagement;

public static class FiefManagementIoC
{
    public static void RegisterFiefManagementFeature(IContainer container)
    {
        container.Register<IFiefManagementSettingsProvider, FiefManagementSettingsProvider>(Reuse.Singleton);
        container.Register<ISettlementOwnershipAdapter, SettlementOwnershipAdapter>(Reuse.Singleton);
        container.Register<IMapScreenInputAdapter, MapScreenInputAdapter>(Reuse.Singleton);
        container.Register<IRemoteFiefSettlementSwapper, RemoteFiefSettlementSwapper>(Reuse.Singleton);
        container.Register<IFiefHubService, FiefHubService>(Reuse.Singleton);
        container.Register<IFiefHubHostAdapter, FiefHubHostAdapter>(Reuse.Singleton);
        container.Register<FiefHubOpener>(Reuse.Singleton);
        // The "Fiefs" button type is client-only (ADR-013): core's null factory, replaced by TAOM.Client's.
        container.Register<IFiefsNavigationElementFactory, NullFiefsNavigationElementFactory>(Reuse.Singleton);
        container.Register<IFiefHubMenuPresenter, FiefHubMenuPresenter>(Reuse.Singleton);
    }
}
