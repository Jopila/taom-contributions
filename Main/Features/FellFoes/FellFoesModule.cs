using System.Collections.Generic;
using DryIoc;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FellFoes.Hooks;

namespace TAOM.Features.FellFoes;

/// <summary>
/// Fell Foes (#893, docs/features/fell-foes.md): three small tweaks to how a battle ends. A routing soldier
/// cornered by a few foes turns and fights, idle enemy archers fix on the player, and the last few enemy
/// stragglers of a big field battle are sent off so the vanilla rout victory can fire. One mission behavior
/// per mission holds all the state; there is no Harmony patch and no saved data.
/// </summary>
internal sealed class FellFoesModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((mission, r) =>
        {
            var coop = r.Resolve<ICoopSessionProvider>();
            var logger = r.Resolve<IModLogger>();
            return new FellFoesMissionLogic(
                new FellFoesService(
                    r.Resolve<IFellFoesAdapter>(),
                    r.Resolve<IFellFoesSettingsProvider>(),
                    logger,
                    () => MBRandom.RandomFloat,
                    () => FellFoesMissionGate.IsEligible(mission, coop)),
                logger);
        }),
    };

    public override string Id => "FellFoes";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IFellFoesSettingsProvider, FellFoesSettingsProvider>(Reuse.Singleton);
        registrator.Register<IFellFoesAdapter, FellFoesAdapter>(Reuse.Singleton);
    }

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
