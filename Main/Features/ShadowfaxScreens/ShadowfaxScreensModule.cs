using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.ShadowfaxScreens.Clan;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Features.ShadowfaxScreens.Party;
using TAOM.Features.ShadowfaxScreens.Quests;

namespace TAOM.Features.ShadowfaxScreens;

/// <summary>
/// Shadowfax Screens, screen open speed (docs/features/shadowfax-screens.md): the shared settings provider and the
/// [ScreenOpen] timer, plus the four screen categories (clan, inventory, party, quest log), each applied at GameInit.
/// IStampClock is registered by LoadTimeStampsModule, which precedes this module in the list. Each screen's builder
/// appends its own block to RegisterServices and InitializeStatics.
/// </summary>
internal sealed class ShadowfaxScreensModule : TaomFeatureModule
{
    private static readonly PatchCategoryDecl[] Categories =
    {
        new("PatchXXX_ShadowfaxClan", ApplyPhase.GameInit),
        new(InventoryPatchCategory.Name, ApplyPhase.GameInit),
        new("PatchXXX_ShadowfaxParty", ApplyPhase.GameInit),
        new("PatchXXX_ShadowfaxQuests", ApplyPhase.GameInit),
    };

    public override string Id => "ShadowfaxScreens";

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IShadowfaxScreensSettingsProvider, ShadowfaxScreensSettingsProvider>(Reuse.Singleton);
        registrator.Register<ScreenOpenTimer>(Reuse.Singleton);

        // Clan fast open (Clan/): needs ICoopPresenceProvider (CoopInteropIoC) and IStampClock, both registered elsewhere.
        registrator.Register<ClanScreenFastPathService>(Reuse.Singleton);

        // Inventory fast open.
        registrator.Register<IInventoryOpenAdapter, InventoryOpenAdapter>(Reuse.Singleton);
        registrator.Register<InventoryOpenFastPathService>(Reuse.Singleton);

        // Party fast open.
        registrator.Register<PartyScreenFastPathService>(Reuse.Singleton);

        // Quest log fast open.
        registrator.Register<IQuestJournalAdapter, QuestJournalAdapter>(Reuse.Singleton);
        registrator.Register<QuestLogDeferService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver)
    {
        ScreenOpenTiming.Initialize(resolver.Resolve<ScreenOpenTimer>());
        ClanFastOpenCalls.Initialize(resolver.Resolve<ClanScreenFastPathService>());

        // Inventory fast open.
        ShadowfaxInventoryCalls.Initialize(resolver.Resolve<InventoryOpenFastPathService>(),
            resolver.Resolve<IInventoryOpenAdapter>(), resolver.Resolve<IModLogger>());

        // Party fast open.
        PartyFastOpenCalls.Initialize(resolver.Resolve<PartyScreenFastPathService>());

        // Quest log fast open.
        QuestLogDeferCalls.Initialize(resolver.Resolve<QuestLogDeferService>());
    }

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        // The Quest log's category has just applied: bind the adapter, which checks its four patches are attached.
        if (phase == ApplyPhase.GameInit)
            resolver.Resolve<QuestLogDeferService>().Install();
    }
}
