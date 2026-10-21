using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.MinorClanMigration.Hooks;

namespace TAOM.Features.MinorClanMigration;

/// <summary>
/// The minor-clan load migration (#957 part 2, docs/features/minor-factions.md): an old save's clans get the
/// cultures and leader templates the update ships. The behavior is built with <c>new</c> inside the declaration's
/// factory, once per campaign start. It persists nothing of its own (the engine saves the clan fields), so
/// <see cref="OwnsSaveData"/> is false and a fault never fails a campaign start.
/// </summary>
internal sealed class MinorClanMigrationModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new MinorClanMigrationBehavior(
            r.Resolve<MinorClanMigrationService>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "MinorClanMigration";

    public override bool OwnsSaveData => false;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IMinorClanMigrationAdapter, MinorClanMigrationAdapter>(Reuse.Singleton);
        registrator.Register<MinorClanMigrationService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
