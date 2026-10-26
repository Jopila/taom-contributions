using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.KeepWardens.Models;

namespace TAOM.Features.KeepWardens;

/// <summary>
/// Keep Wardens (#888, extended by #889, #991 and #992; docs/features/keep-wardens.md): castles with a built Guard House field a
/// patrol party. Vanilla's <c>PatrolPartiesCampaignBehavior</c> already queues, spawns and refills patrols for any settlement the
/// campaign <c>SettlementPatrolModel</c> allows, so this module adds no patrol logic of its own: three campaign models (patrol,
/// security, building score), two hooks that <c>TaomPartySizeModel</c> resolves (<c>ICastlePatrolSizeHook</c> for the size limit,
/// <c>IPatrolRosterHook</c> for the spawn roster), one small campaign behavior (<see cref="CastleGuardHouseUpgradeBehavior"/>, the
/// castle twin of vanilla's roster version bump on a Guard House upgrade), the config and two MCM switches
/// (<c>EnableCastlePatrols</c>, <c>EnableGuardHouseBuildPriority</c>). No patch, no save data.
/// Parking this module removes the three models and the behavior but not the two hooks in <c>TaomPartySizeModel</c>, and with
/// castle patrols alive that reopens the null-template crash on the hourly refill. Park it together with: the <c>_castlePatrolSize</c> and
/// <c>_patrolRoster</c> lines (and the <c>FindAppropriateInitialRosterForMobileParty</c> override) in <c>TaomPartySizeModel</c>,
/// the two switches in <c>TaomSettings</c> and their four loc keys, and the <c>CastlePatrolSettingsProvider</c> reads of them.
/// </summary>
internal sealed class KeepWardensModule : TaomFeatureModule
{
    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<SettlementPatrolModel, TaomSettlementPatrolModel>(ModelTarget.Campaign,
            r => new TaomSettlementPatrolModel(r.Resolve<CastlePatrolService>(), r.Resolve<ICastlePatrolAdapter>())),
        GameModelDecl.Of<SettlementSecurityModel, TaomSettlementSecurityModel>(ModelTarget.Campaign,
            r => new TaomSettlementSecurityModel(r.Resolve<CastlePatrolService>(), r.Resolve<ICastlePatrolAdapter>())),
        GameModelDecl.Of<BuildingScoreCalculationModel, TaomBuildingScoreModel>(ModelTarget.Campaign,
            r => new TaomBuildingScoreModel(r.Resolve<CastlePatrolService>(), r.Resolve<IGuardHouseAdapter>())),
    };

    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new CastleGuardHouseUpgradeBehavior(r.Resolve<IModLogger>())),
    };

    public override string Id => "KeepWardens";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ICastlePatrolConfigProvider, CastlePatrolConfigProvider>(Reuse.Singleton);
        registrator.Register<ICastlePatrolSettingsProvider, CastlePatrolSettingsProvider>(Reuse.Singleton);
        registrator.Register<ICastlePatrolAdapter, CastlePatrolAdapter>(Reuse.Singleton);
        registrator.Register<IGuardHouseAdapter, GuardHouseAdapter>(Reuse.Singleton);
        registrator.Register<CastlePatrolService>(Reuse.Singleton);
        registrator.Register<ICastlePatrolSizeHook, CastlePatrolSizeHook>(Reuse.Singleton);
        registrator.Register<PatrolRosterService>(Reuse.Singleton);
        registrator.Register<IPatrolRosterHook, PatrolRosterHook>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;

    public override IReadOnlyList<GameModelDecl> GameModels => Models;
}
