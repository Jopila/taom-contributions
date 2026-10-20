using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.LordCourtesies;

/// <summary>
/// Registers Lord Courtesies (docs/features/lord-courtesies.md) through its feature module. Everything is a
/// singleton. The map reveal adapter is registered by Hard Questions, the alignment service by Execution, the
/// player adapter by Armour Acquisition and the co-op providers by Co-op Interop; DryIoc resolves lazily, so
/// order does not matter.
/// </summary>
public static class LordCourtesiesIoC
{
    public static void RegisterLordCourtesiesFeature(IRegistrator container)
    {
        container.Register<ILordCourtesiesConfigProvider, LordCourtesiesConfigProvider>(Reuse.Singleton);
        container.Register<ILordCourtesiesSettingsProvider, LordCourtesiesSettingsProvider>(Reuse.Singleton);
        container.Register<ILordCourtesyAdapter, LordCourtesyAdapter>(Reuse.Singleton);
        container.Register<LordCourtesiesService>(Reuse.Singleton);
    }
}
