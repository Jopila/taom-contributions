using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.InnRounds;

/// <summary>
/// Registers Inn Rounds (docs/features/inn-rounds.md); every shared piece it reads is registered by another feature.
/// </summary>
public static class InnRoundsIoC
{
    public static void RegisterInnRoundsFeature(IRegistrator container)
    {
        container.Register<IInnRoundsConfigProvider, InnRoundsConfigProvider>(Reuse.Singleton);
        container.Register<IInnRoundsSettingsProvider, InnRoundsSettingsProvider>(Reuse.Singleton);
        container.Register<IInnRoundsAdapter, InnRoundsAdapter>(Reuse.Singleton);
        container.Register<InnRoundsService>(Reuse.Singleton);
    }
}
