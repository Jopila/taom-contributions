using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.ArmyTargeting.HoldTheWalls;

/// <summary>
/// Registers Hold the Walls through <see cref="HoldTheWallsModule"/>. Its dependencies on other features are registered
/// elsewhere: <c>IArmyTargetingConfigProvider</c> and <c>IArmyTargetingSettingsProvider</c> (the MCM switch) by <c>ArmyTargetingIoC</c>, <c>IAlignmentService</c> by Execution, and the
/// co-op providers and the logger by <c>IoC.Configure</c>. The single-player gate is the service's, read live.
/// </summary>
public static class HoldTheWallsIoC
{
    public static void RegisterHoldTheWallsFeature(IRegistrator registrator)
    {
        registrator.Register<IFortHoldAdapter, FortHoldAdapter>(Reuse.Singleton);
        registrator.Register<IHoldTheWallsService, HoldTheWallsService>(Reuse.Singleton);
    }
}
