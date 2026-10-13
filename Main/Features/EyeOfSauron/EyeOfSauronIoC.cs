using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.EyeOfSauron;

/// <summary>
/// Registers the Eye of Sauron hunt through <see cref="EyeOfSauronModule"/>. One service instance serves the behavior,
/// the console and, through <see cref="IEyeOfSauronHook"/>, the speed and size models. Its dependencies
/// (IAlignmentService, the co-op providers, the logger) are hand-wired earlier in IoC.Configure.
/// </summary>
public static class EyeOfSauronIoC
{
    public static void RegisterEyeOfSauronFeature(IRegistrator container)
    {
        container.Register<IEyeOfSauronSettings, EyeOfSauronSettingsProvider>(Reuse.Singleton);
        container.Register<IHunterPartyAdapter, HunterPartyAdapter>(Reuse.Singleton);
        container.Register<EyeOfSauronService>(Reuse.Singleton);
        container.RegisterMapping<IEyeOfSauronHook, EyeOfSauronService>();
    }
}
