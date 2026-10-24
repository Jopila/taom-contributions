using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BondOfHire.Hooks;

namespace TAOM.Features.BondOfHire;

/// <summary>
/// Bond of Hire (docs/features/bond-of-hire.md, #867, with the Clans panel line of #868): a clan that signs on as a mercenary
/// stays a minimum number of days before the engine lets it leave a ruler in debt, be hired away or leave in the daily leave
/// consideration (three departures that read one date). One entry point, a campaign behavior with one listener on the engine's mercenary-service-started event, over one service and one MCM group. No Harmony
/// patch, no game model and nothing of TAOM's saved: the whole mechanism is the engine's own saved
/// <c>Clan.ShouldStayInKingdomUntil</c>. The co-op pieces come from the kernel and CoopInterop and are never registered here.
/// The panel line lives in TAOM.Client (<c>Client/Features/BondOfHire/UI</c>) and reads <see cref="ContractTermRules"/>.
/// </summary>
internal sealed class BondOfHireModule : TaomFeatureModule
{
    private static readonly CampaignBehaviorDecl[] Behaviors =
    {
        CampaignBehaviorDecl.Of(r => new BondOfHireBehavior(
            r.Resolve<BondOfHireService>(),
            r.Resolve<IModLogger>())),
    };

    public override string Id => "BondOfHire";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IBondOfHireSettingsProvider, BondOfHireSettingsProvider>(Reuse.Singleton);
        registrator.Register<BondOfHireService>(Reuse.Singleton);
    }

    public override IReadOnlyList<CampaignBehaviorDecl> CampaignBehaviors => Behaviors;
}
