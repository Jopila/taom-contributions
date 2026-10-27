using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TAOM.Composition;
using TAOM.Features.UnlookedForAid.Models;

namespace TAOM.Features.UnlookedForAid;

/// <summary>
/// Help Unlooked-For (#872, docs/features/unlooked-for-aid.md): a lord strong enough stops fleeing a siege. One model on the
/// campaign <c>MobilePartyAIModel</c> slot, its service, config and MCM switch. No Harmony patch, nothing saved. Overlap
/// check, 2026-10-23: no other TAOM code fills the <c>MobilePartyAIModel</c> slot. The model is built in the factory below
/// on the main thread in <c>OnGameStart</c>, which resolves the service and with it captures the MCM settings reference
/// and loads the config, so the AI worker thread that later calls the model touches neither. The factory also resolves
/// Fire and Tribute's <c>IRaidHostOwnSideQuery</c> (registered by that module, parked or not), whose own-side rule the
/// model carries.
/// </summary>
internal sealed class UnlookedForAidModule : TaomFeatureModule
{
    private static readonly GameModelDecl[] Models =
    {
        GameModelDecl.Of<MobilePartyAIModel, TaomMobilePartyAIModel>(ModelTarget.Campaign,
            r => new TaomMobilePartyAIModel(r.Resolve<UnlookedForAidService>(), r.Resolve<FireAndTribute.IRaidHostOwnSideQuery>())),
    };

    public override string Id => "UnlookedForAid";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IUnlookedForAidConfigProvider, UnlookedForAidConfigProvider>(Reuse.Singleton);
        registrator.Register<IUnlookedForAidSettingsProvider, UnlookedForAidSettingsProvider>(Reuse.Singleton);
        registrator.Register<UnlookedForAidService>(Reuse.Singleton);
    }

    public override IReadOnlyList<GameModelDecl> GameModels => Models;
}
