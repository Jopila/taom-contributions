using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.HardQuestions;

/// <summary>
/// Registers Hard Questions (docs/features/hard-questions.md) through its feature module. Everything is a
/// singleton; the campaign state is reset by the campaign behavior's constructor. The alignment services, the
/// armour gate, the player adapter, the skill adapter and the random source are registered by their own
/// features; DryIoc resolves lazily, so order does not matter.
/// </summary>
public static class HardQuestionsIoC
{
    public static void RegisterHardQuestionsFeature(IRegistrator container)
    {
        container.Register<IHardQuestionsConfigProvider, HardQuestionsConfigProvider>(Reuse.Singleton);
        container.Register<IHardQuestionsSettingsProvider, HardQuestionsSettingsProvider>(Reuse.Singleton);
        container.Register<HardQuestionsState>(Reuse.Singleton);
        container.Register<HardQuestionsPenaltyApplier>(Reuse.Singleton);
        container.Register<StripService>(Reuse.Singleton);
        container.Register<InterrogationService>(Reuse.Singleton);
        container.Register<ICaptiveLordAdapter, CaptiveLordAdapter>(Reuse.Singleton);
        container.Register<IPlayerTraitAdapter, PlayerTraitAdapter>(Reuse.Singleton);
        container.Register<IHardQuestionsActionAdapter, HardQuestionsActionAdapter>(Reuse.Singleton);
        container.Register<IMapRevealAdapter, MapRevealAdapter>(Reuse.Singleton);
        container.Register<ILordEquipmentAdapter, LordEquipmentAdapter>(Reuse.Singleton);
    }
}
