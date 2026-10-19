using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Composition;
using TAOM.Features.PosternGate.Models;
using TAOM.Features.TaomBuildings;

namespace TAOM.Features.PosternGate;

/// <summary>
/// Postern Gate (#855, docs/features/postern-gate.md): a military building in every town and castle that cuts the troops
/// lost breaking into or out of a siege there. The building itself comes from the shared pipeline in
/// <see cref="TaomBuildingsModule"/>, whose catalog collects every registered <see cref="TaomBuildingDefinition"/> when it
/// is first resolved, at campaign start. This module adds the definition, the loss-cut model (the campaign
/// <c>TroopSacrificeModel</c> slot), its config and its MCM switch. The switch turns off the cut only; the building
/// stays registered and seeded, so turning it off never strips a save.
/// </summary>
internal sealed class PosternGateModule : TaomFeatureModule
{
    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<TroopSacrificeModel, TaomTroopSacrificeModel>(ModelTarget.Campaign,
            r => new TaomTroopSacrificeModel(r.Resolve<PosternGateService>())),
    };

    public override string Id => "PosternGate";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IPosternGateConfigProvider, PosternGateConfigProvider>(Reuse.Singleton);
        registrator.Register<IPosternGateSettingsProvider, PosternGateSettingsProvider>(Reuse.Singleton);
        registrator.RegisterDelegate<IPosternGateConfigProvider, IPosternGateSettingsProvider, TaomBuildingDefinition>(
            (config, settings) => PosternGateDefinition.Create(config.Costs, config.Reductions, settings), Reuse.Singleton);
        registrator.Register<PosternGateService>(Reuse.Singleton);
    }

    public override IReadOnlyList<GameModelDecl> GameModels => Models;
}
