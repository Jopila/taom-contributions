using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.NotableServices.Hooks;

namespace TAOM.Features.NotableServices;

/// <summary>
/// Notable Services (docs/features/notable-services.md) as a feature module: a service graph and one dialog behavior, no
/// patch, no model and no save data. The behavior is built with <c>new</c> for every campaign, because it holds the last
/// answers its reply lines read and a container singleton is constructed once per process. The engine saves the militia,
/// the hearth, the prosperity, the met flags and the tracker marks; the feature writes no record of its own, so
/// <see cref="OwnsSaveData"/> is false.
/// </summary>
internal sealed class NotableServicesModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new NotableServicesDialogBehavior(r.Resolve<NotableServicesService>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "NotableServices";

    /// <summary>False: the engine saves everything the feature changes.</summary>
    public override bool OwnsSaveData => false;

    public override void RegisterServices(IRegistrator registrator) =>
        NotableServicesIoC.RegisterNotableServicesFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
