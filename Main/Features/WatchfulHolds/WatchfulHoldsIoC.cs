using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.WatchfulHolds;

/// <summary>
/// Registers Watchful Holds through <see cref="WatchfulHoldsModule"/>. Its dependencies on other features
/// (<c>IRandomSource</c>, <c>IEnlistmentStateQuery</c>, <c>ICoopSessionProvider</c>, <c>IPathService</c>, the logger) are
/// hand-wired earlier in IoC.Configure, and <c>RealmTerritoryService</c> and <c>IRealmBordersSettings</c> come from the Realm Borders module.
/// </summary>
public static class WatchfulHoldsIoC
{
    public static void RegisterWatchfulHoldsFeature(IRegistrator registrator)
    {
        registrator.Register<IWatchfulHoldsConfigProvider, WatchfulHoldsConfigProvider>(Reuse.Singleton);
        registrator.Register<IWatchfulHoldsSettings, WatchfulHoldsSettingsProvider>(Reuse.Singleton);
        registrator.Register<IWatchfulHoldsAdapter, WatchfulHoldsAdapter>(Reuse.Singleton);
        registrator.Register<IWatchfulHoldsNoticeAdapter, WatchfulHoldsNoticeAdapter>(Reuse.Singleton);
        registrator.Register<IWatchfulProvinceLookup, RealmProvinceLookup>(Reuse.Singleton);
        registrator.Register<IWatchfulHoldsService, WatchfulHoldsService>(Reuse.Singleton);
    }
}
