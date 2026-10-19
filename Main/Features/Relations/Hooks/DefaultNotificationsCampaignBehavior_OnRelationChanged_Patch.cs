using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SandBox.CampaignBehaviors;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.Relations.Hooks;

/// <summary>
/// Patch107, the relation notice that names the hero (docs/features/personal-relations.md). Vanilla's private
/// <c>DefaultNotificationsCampaignBehavior.OnRelationChanged</c> (v1.5.5, a <c>HeroRelationChanged</c> listener, seven
/// parameters) names the clan, not the hero, for any hero who has a clan other than the player's. With personal relations on a
/// change belongs to one lord, so the prefix shows vanilla's own "with notable" text, which names him, and skips the
/// handler. Only parameters 1 to 4 are bound, by name (<c>PersonalRelationNoticeBindingTests</c> pins the seven). Off, a
/// change that does not involve the player, or any fault: it returns true and vanilla runs untouched.
/// </summary>
[HarmonyPatch(typeof(DefaultNotificationsCampaignBehavior), "OnRelationChanged")]
[HarmonyPatchCategory(RelationsModule.PatchCategory)]
public static class DefaultNotificationsCampaignBehavior_OnRelationChanged_Patch
{
    private static IPersonalRelationService? _service;
    private static IPersonalRelationNoticeAdapter? _notice;
    private static IModLogger? _logger;

    [HarmonyPrefix]
    public static bool Prefix(Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange, bool showNotification)
    {
        // Vanilla's own first test: with it false the original does nothing, so there is nothing to replace.
        if (!showNotification || relationChange == 0)
            return true;

        try
        {
            return Resolve(effectiveHero, effectiveHeroGainedRelationWith, relationChange);
        }
        catch (Exception ex)
        {
            LogFault(ex);
            return true;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool Resolve(Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange) =>
        Run(_service ??= TAOM.IoC.Resolve<IPersonalRelationService>(), _notice ??= TAOM.IoC.Resolve<IPersonalRelationNoticeAdapter>(),
            effectiveHero, effectiveHeroGainedRelationWith, relationChange);

    /// <summary>
    /// The decision, with the collaborators handed in so it is testable. True means "run vanilla's handler": the feature is
    /// inactive, or vanilla would show nothing. A fault is the Prefix's to catch, which also returns true. False means the notice was shown here.
    /// </summary>
    internal static bool Run(IPersonalRelationService service, IPersonalRelationNoticeAdapter notice,
        Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange) =>
        !service.IsActive || !notice.ShowNamedNotice(effectiveHero, effectiveHeroGainedRelationWith, relationChange);

    private static void LogFault(Exception ex)
    {
        try
        {
            (_logger ??= TAOM.IoC.Resolve<IModLogger>()).LogError($"[PersonalRelations] Patch107 failed: {ex.Message}");
        }
        catch
        {
            // The log is gone too; vanilla's handler still runs.
        }
    }
}
