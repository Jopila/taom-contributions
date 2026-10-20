using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;

namespace TAOM.Features.LordFaceVariety;

/// <summary>
/// Lord Face Variety (#952, docs/features/lord-face-variety.md): one pass when a new campaign is created gives
/// campaign heroes that share a face their own. No patch and no save data of its own.
/// </summary>
internal sealed class LordFaceVarietyModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new LordFaceVarietyBehavior(
            r.Resolve<LordFaceVarietyService>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "LordFaceVariety";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IHeroFaceAdapter, HeroFaceAdapter>(Reuse.Singleton);
        registrator.Register<LordFaceVarietyService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
