using DryIoc;
using TAOM.Composition;

namespace TAOM.Features.KingsMuster;

/// <summary>
/// The King's Muster (#874, docs/features/kings-muster.md): an AI kingdom ruler calls hosts for a fraction of the influence.
/// Services only. The model slot is already filled by <c>TaomArmyManagementModel</c> (CulturalFeats), which resolves
/// <see cref="KingsMusterService"/> once in its constructor, so this module declares no model, no patch and no behavior, saves
/// nothing, and <c>IoC.cs</c> and <c>SubModule.cs</c> are untouched. Parking this module turns nothing off (a parked module
/// still registers its services, <c>TaomFeatureModule</c>); the off switches are the MCM setting and the factor 1 in
/// <c>army_targeting.json</c>. Removing the feature means deleting the model's service field, its resolve, the cost call and
/// the <c>FeatureModules</c> line.
/// </summary>
internal sealed class KingsMusterModule : TaomFeatureModule
{
    public override string Id => "KingsMuster";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IKingsMusterSettingsProvider, KingsMusterSettingsProvider>(Reuse.Singleton);
        registrator.Register<KingsMusterService>(Reuse.Singleton);
    }
}
