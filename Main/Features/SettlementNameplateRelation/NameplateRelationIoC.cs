using DryIoc;

namespace TAOM.Features.SettlementNameplateRelation;

public static class NameplateRelationIoC
{
    public static void RegisterSettlementNameplateRelationFeature(IContainer container)
    {
        container.Register<INameplateRelationSettingsProvider, NameplateRelationSettingsProvider>(Reuse.Singleton);
        container.Register<INameplateRelationAlphaService, NameplateRelationAlphaService>(Reuse.Singleton);
    }

    /// <summary>The plate widget is engine-constructed (no DI) and lives in TAOM.Client, so it reads its settings
    /// through <see cref="NameplateRelationWidgetContext"/>, captured once here, in the end block of <c>IoC.Configure</c>.</summary>
    public static void InitializeWidgetStatics(IContainer container)
    {
        NameplateRelationWidgetContext.Initialize(container.Resolve<INameplateRelationSettingsProvider>());
    }
}
