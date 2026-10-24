using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.CharacterDeveloper;
using TAOM.Core.Logging;

namespace TAOM.Features.LoremastersLine.UI;

/// <summary>
/// How the Loremaster's Line (#877) reaches the engine's skill data: the hero item is private to <c>SkillVM</c> and no
/// public route reaches it, so it is read by a field reference bound once. A miss logs one warning and
/// <see cref="IsBound"/> stays false, which leaves the screen vanilla. Split from the mixin to keep it under 150 lines (ADR-002).
/// </summary>
internal static class LoremastersLineSkillAccess
{
    private static readonly AccessTools.FieldRef<SkillVM, CharacterDeveloperHeroItemVM>? HeroItemRef = BindHeroItem();

    public static bool IsBound => HeroItemRef != null;

    public static CharacterDeveloperHeroItemVM? HeroItem(SkillVM? vm) => vm == null || HeroItemRef == null ? null : HeroItemRef(vm);

    // The game model's learning rate for this skill as a function of skill level, under the preview attributes and focus.
    // Null when the model or the hero item is missing.
    public static Func<int, float>? RateFunction(SkillVM? vm)
    {
        var model = Campaign.Current?.Models?.CharacterDevelopmentModel;
        var heroItem = HeroItem(vm);
        if (vm == null || model == null || heroItem == null) return null;

        var attributes = heroItem.CharacterAttributes;
        var focus = vm.CurrentFocusLevel;
        var skill = vm.Skill;
        return level => model.CalculateLearningRate(attributes, focus, level, skill).ResultNumber;
    }

    private static AccessTools.FieldRef<SkillVM, CharacterDeveloperHeroItemVM>? BindHeroItem()
    {
        try
        {
            return AccessTools.FieldRefAccess<SkillVM, CharacterDeveloperHeroItemVM>("_heroItem");
        }
        catch (Exception ex)
        {
            // A field this game build lacks: leave the screen vanilla. This is a static initializer, so the log itself must
            // not throw: a failure here would make every later use of the type throw TypeInitializationException.
            try
            {
                IoC.Resolve<IModLogger>()?.LogWarning($"[LoremastersLine] SkillVM._heroItem did not bind, so the line is off. {ex.GetType().Name}: {ex.Message}");
            }
            catch (Exception)
            {
                // No logger to tell; the line stays off.
            }

            return null;
        }
    }
}
