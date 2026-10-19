using DryIoc;

namespace TAOM.Features.TelcharsMark;

public static class TelcharsMarkIoC
{
    public static void RegisterTelcharsMarkFeature(IContainer container)
    {
        container.Register<ITelcharsMarkSettingsProvider, TelcharsMarkSettingsProvider>(Reuse.Singleton);
        container.Register<ItemUpgradeScorer>(Reuse.Singleton);
        container.Register<TelcharsMarkService>(Reuse.Singleton);
    }
}
