using DryIoc;
using TAOM.Composition;

namespace TAOM.Features.HallOfFire;

/// <summary>
/// Hall of Fire (#826, docs/features/hall-of-fire.md): TAOM settlements borrow a vanilla culture's conversation
/// backdrop, by wrapping the engine's map conversation provider at the first main menu. No patch, behavior,
/// model or save data.
/// </summary>
internal sealed class HallOfFireModule : TaomFeatureModule
{
    public override string Id => "HallOfFire";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHallOfFireConfigProvider, HallOfFireConfigProvider>(Reuse.Singleton);
        registrator.Register<HallOfFireService>(Reuse.Singleton);
        registrator.Register<IMapConversationProviderSlot, MapConversationProviderSlot>(Reuse.Singleton);
        registrator.Register<IMapConversationProviderWrapper, NullMapConversationProviderWrapper>(Reuse.Singleton);
        registrator.Register<HallOfFireInstaller>(Reuse.Singleton);
    }

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase == ApplyPhase.MainMenu)
            resolver.Resolve<HallOfFireInstaller>().Install();
    }
}
