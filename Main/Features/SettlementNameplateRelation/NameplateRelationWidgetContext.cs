namespace TAOM.Features.SettlementNameplateRelation;

/// <summary>
/// What the settlement plate widget needs from TAOM's services (#591, ADR-013). Gauntlet builds the widget by reflection
/// with only a UIContext, and the widget lives in TAOM.Client, so core cannot set a static on it; this is set once by
/// NameplateRelationIoC.InitializeWidgetStatics at the end of IoC.Configure. Null means the widget uses its defaults.
/// </summary>
public static class NameplateRelationWidgetContext
{
    public static INameplateRelationSettingsProvider? Settings { get; private set; }

    public static void Initialize(INameplateRelationSettingsProvider? settings) => Settings = settings;
}
