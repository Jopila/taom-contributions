using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.KeepWardens.Models;

namespace TAOM.Features.KeepWardens;

/// <summary>
/// Keep Wardens (#888, docs/features/keep-wardens.md): castles with a built Guard House field a patrol party. Vanilla's
/// <c>PatrolPartiesCampaignBehavior</c> already queues, spawns and refills patrols for any settlement the campaign
/// <c>SettlementPatrolModel</c> allows, so this module is models only: the model slot, the size hook that
/// <c>TaomPartySizeModel</c> resolves, the config and the MCM switch. No behavior, no patch, no save data.
/// Parking this module removes the model but not the size hook in <c>TaomPartySizeModel</c>, and with castle patrols alive that
/// reopens the null-template crash on the hourly refill; parking it also means removing the hook line from <c>TaomPartySizeModel</c>.
/// </summary>
internal sealed class KeepWardensModule : TaomFeatureModule
{
    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<SettlementPatrolModel, TaomSettlementPatrolModel>(ModelTarget.Campaign,
            r => new TaomSettlementPatrolModel(r.Resolve<CastlePatrolService>(), r.Resolve<ICastlePatrolAdapter>())),
    };

    public override string Id => "KeepWardens";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ICastlePatrolConfigProvider, CastlePatrolConfigProvider>(Reuse.Singleton);
        registrator.Register<ICastlePatrolSettingsProvider, CastlePatrolSettingsProvider>(Reuse.Singleton);
        registrator.Register<ICastlePatrolAdapter, CastlePatrolAdapter>(Reuse.Singleton);
        registrator.Register<CastlePatrolService>(Reuse.Singleton);
        registrator.Register<ICastlePatrolSizeHook, CastlePatrolSizeHook>(Reuse.Singleton);
    }

    public override IReadOnlyList<GameModelDecl> GameModels => Models;
}
