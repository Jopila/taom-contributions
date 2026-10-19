using System.Collections.Generic;
using DryIoc;
using TAOM.Composition;
using TAOM.Features.ShadowfaxScreens.Diagnostics;

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
        new("PatchXXX_ShadowfaxInventory", ApplyPhase.GameInit),
        new("PatchXXX_ShadowfaxParty", ApplyPhase.GameInit),
        new("PatchXXX_ShadowfaxQuests", ApplyPhase.GameInit),
    };

    public override string Id => "ShadowfaxScreens";

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IShadowfaxScreensSettingsProvider, ShadowfaxScreensSettingsProvider>(Reuse.Singleton);
        registrator.Register<ScreenOpenTimer>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver)
    {
        ScreenOpenTiming.Initialize(resolver.Resolve<ScreenOpenTimer>());
    }
}
