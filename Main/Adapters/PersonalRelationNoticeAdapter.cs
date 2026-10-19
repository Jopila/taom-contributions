using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

public sealed class PersonalRelationNoticeAdapter : IPersonalRelationNoticeAdapter
{
    public bool ShowNamedNotice(Hero effectiveHero, Hero effectiveHeroGainedRelationWith, int relationChange)
    {
        // Vanilla's gate (DefaultNotificationsCampaignBehavior.OnRelationChanged, v1.5.5): a change that involves the player.
        // The campaign statics throw with no campaign (adapters.md), so the player is read the safe way.
        var player = CharacterObject.PlayerCharacter?.HeroObject;
        if (player == null || relationChange == 0 || effectiveHero == null || effectiveHeroGainedRelationWith == null
            || (effectiveHero != player && effectiveHeroGainedRelationWith != player))
            return false;

        // Vanilla's pick of the other hero, and its own "notable" text and sound: with personal relations on the hero named
        // is the one dealt with, clan member or not, so the "clan" variant (which names the clan) is never wanted here.
        var other = effectiveHero == player ? effectiveHeroGainedRelationWith : effectiveHero;
        var text = relationChange > 0
            ? GameTexts.FindText("str_your_relation_increased_with_notable")
            : GameTexts.FindText("str_your_relation_decreased_with_notable");
        StringHelpers.SetCharacterProperties("HERO", other.CharacterObject, text);
        text.SetTextVariable("VALUE", other.GetRelation(player));
        text.SetTextVariable("MAGNITUDE", MathF.Abs(relationChange));
        MBInformationManager.AddQuickInformation(text, 0, other.IsNotable ? other.CharacterObject : null, null,
            "event:/ui/notification/relation");
        return true;
    }
}
