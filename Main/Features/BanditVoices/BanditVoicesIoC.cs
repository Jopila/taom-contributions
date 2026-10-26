using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.BanditVoices;

/// <summary>
/// Registers Bandit Voices through <see cref="BanditVoicesModule"/>.
/// </summary>
public static class BanditVoicesIoC
{
    public static void RegisterBanditVoicesFeature(IRegistrator registrator)
    {
        registrator.Register<IBanditVoicesSettingsProvider, BanditVoicesSettingsProvider>(Reuse.Singleton);
        registrator.Register<IBanditEncounterTextAdapter, BanditEncounterTextAdapter>(Reuse.Singleton);
        registrator.Register<BanditVoicesService>(Reuse.Singleton);
    }
}
