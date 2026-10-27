using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.InnRounds.Hooks;

namespace TAOM.Features.InnRounds;

/// <summary>
/// Inn Rounds (docs/features/inn-rounds.md) as a feature module: a service graph and one dialog behavior, no patch and no
/// model. The behavior is built with <c>new</c> for every campaign, because it owns the day's round count and saves it (a
/// container singleton is constructed once per process), so the module owns save data and fails closed.
/// </summary>
internal sealed class InnRoundsModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new InnRoundsDialogBehavior(r.Resolve<InnRoundsService>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "InnRounds";

    /// <summary>True: the day and the count of the rounds stood are saved in SyncData.</summary>
    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator) =>
        InnRoundsIoC.RegisterInnRoundsFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
