using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.FireAndTribute;

/// <summary>
/// Registers Fire and Tribute through <see cref="FireAndTributeModule"/>. Its dependencies on other features are
/// registered elsewhere: <c>IKingdomWarSnapshotAdapter</c> by the War Chronicle module, <c>IWarOfTheRingService</c> by
/// Diplomacy, <c>IAlignmentService</c> by Execution, <c>IEnlistmentStateQuery</c> by Enlistment (a null one from Field
/// Commission when Enlistment is absent), <c>ICareerPurseAdapter</c> by the Career system, <c>IRandomSource</c>, the co-op
/// providers, <c>IPathService</c> and the logger by <c>IoC.Configure</c>. The dedicated-server and co-op checks are the service's, read live.
/// </summary>
public static class FireAndTributeIoC
{
    public static void RegisterFireAndTributeFeature(IRegistrator registrator)
    {
        registrator.Register<IFireAndTributeConfigProvider, FireAndTributeConfigProvider>(Reuse.Singleton);
        registrator.Register<IFireAndTributeSettings, FireAndTributeSettingsProvider>(Reuse.Singleton);
        registrator.Register<IRaidHostAdapter, RaidHostAdapter>(Reuse.Singleton);
        registrator.Register<IFireAndTributeNoticeAdapter, FireAndTributeNoticeAdapter>(Reuse.Singleton);
        registrator.Register<ITributeEnvoyAdapter, TributeEnvoyAdapter>(Reuse.Singleton);
        registrator.Register<IEnvoyPresenterAdapter, EnvoyPresenterAdapter>(Reuse.Singleton);
        registrator.Register<IFireAndTributeService, FireAndTributeService>(Reuse.Singleton);
    }
}
