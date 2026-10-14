using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Features.EyeOfSauron;

/// <summary>
/// The Eye of Sauron hunt as a feature module (#765 M8, docs/features/eye-of-sauron.md). Its services register in
/// <c>IoC.Configure</c> (OnSubModuleLoad), long before <c>SubModule.RegisterCulturalFeatModels</c> resolves
/// <see cref="IEyeOfSauronHook"/> for the speed and size models at campaign start. The behavior is built with
/// <c>new</c> inside the declaration's factory, which the runner calls once per campaign start, so its constructor
/// resets the singleton service for every campaign; it owns save data, so a fault fails closed.
/// </summary>
internal sealed class EyeOfSauronModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new EyeOfSauronBehavior(r.Resolve<EyeOfSauronService>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "EyeOfSauron";

    public override bool OwnsSaveData => true;

    public override void RegisterServices(IRegistrator registrator) => EyeOfSauronIoC.RegisterEyeOfSauronFeature(registrator);

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
