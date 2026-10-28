using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.WardensTally.Hooks;

namespace TAOM.Features.WardensTally;

/// <summary>
/// Warden's Tally (docs/features/wardens-tally.md, #903) as a feature module: a menu-opened listener, a pure service and one MCM switch, with nothing saved.
/// The odds, the veto and the boost come from War Chronicle's registrations (<c>WarChronicleIoC</c>), which this module never repeats.
/// </summary>
internal sealed class WardensTallyModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new WardensTallyMenuBehavior(r.Resolve<WardensTallyMenuService>(), r.Resolve<IModLogger>())),
    };

    public override string Id => "WardensTally";

    public override bool OwnsSaveData => false;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<WardensTallySettingsProvider>(Reuse.Singleton);
        registrator.Register<WardensTallyService>(Reuse.Singleton);
        registrator.Register<WardensTallyMenuService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
