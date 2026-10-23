using System;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Localization;

namespace TAOM.Features.LinesOfDescent.UI;

/// <summary>
/// Every line of player text Lines of Descent shows (#905), as inline <c>{=taom_lod_*}default</c> literals so the harvester
/// registers them, and the renderer that turns a <see cref="RelationPhrase"/> into one <see cref="TextObject"/>. A phrase is a
/// chain of links ("husband of Z", "daughter of W"); each link names its own target through a character tag, and the chain
/// nests <c>{FIRST}, {REST}</c> so a translation can reorder the pieces. The relation word is lower case in the table and the
/// first one of a phrase is upper-cased here, as the vanilla relation text is. Names reach the text through
/// <see cref="BindHero"/>, which sets the hero's character properties (name and gender); a test swaps it for a fake.
/// </summary>
internal static class FamilyTreeTexts
{
    /// <summary>Sets the <c>{TAG.NAME}</c> properties of the hero with this string id on a text. Replaced by tests.</summary>
    internal static Action<TextObject, string, string> BindHero = DefaultBind;

    internal static void DefaultBind(TextObject text, string tag, string heroId)
    {
        var character = Campaign.Current == null ? null : Hero.Find(heroId)?.CharacterObject;
        if (character != null)
            StringHelpers.SetCharacterProperties(tag, character, text);
    }

    internal static TextObject Button() => new("{=taom_lod_button}Family Tree");

    internal static TextObject Title(string rootId)
    {
        var text = new TextObject("{=taom_lod_title}The Line of {ROOT.NAME}");
        BindHero(text, "ROOT", rootId);
        return text;
    }

    internal static TextObject Kin(string heroId)
    {
        var text = new TextObject("{=taom_lod_kin}Kin of {HERO.NAME}");
        BindHero(text, "HERO", heroId);
        return text;
    }

    internal static TextObject More(int count)
        => new TextObject("{=taom_lod_more}{COUNT} more not shown").SetTextVariable("COUNT", count);

    internal static TextObject Repeat() => new("{=taom_lod_repeat}See above");

    /// <summary>The phrase as one text: "Husband of Z, daughter of W and V", or "Kin of X" when there is no short path.</summary>
    internal static TextObject Render(RelationPhrase phrase)
    {
        if (phrase.IsKin || phrase.Steps.Count == 0)
            return Kin(phrase.KinOfId ?? "");
        TextObject? rest = null;
        for (var i = phrase.Steps.Count - 1; i >= 0; i--)
        {
            var link = Link(phrase.Steps[i], upperCase: i == 0);
            if (rest == null)
            {
                rest = link;
                continue;
            }

            rest = new TextObject("{=taom_lod_chain}{FIRST}, {REST}").SetTextVariable("FIRST", link).SetTextVariable("REST", rest);
        }

        return rest!;
    }

    private static TextObject Link(RelationStep step, bool upperCase)
    {
        var word = Word(step.Kind);
        if (upperCase && word.Length > 0)
            word = char.ToUpperInvariant(word[0]) + word.Substring(1);
        if (step.IsTwoParent)
        {
            var two = new TextObject("{=taom_lod_link_parents}{RELATION} of {FATHER.NAME} and {MOTHER.NAME}").SetTextVariable("RELATION", word);
            BindHero(two, "FATHER", step.FatherId!);
            BindHero(two, "MOTHER", step.MotherId!);
            return two;
        }

        var one = new TextObject("{=taom_lod_link}{RELATION} of {HERO.NAME}").SetTextVariable("RELATION", word);
        BindHero(one, "HERO", step.TargetId);
        return one;
    }

    internal static string Word(RelationKind kind)
    {
        switch (kind)
        {
            case RelationKind.Father: return new TextObject("{=taom_lod_rel_father}father").ToString();
            case RelationKind.Mother: return new TextObject("{=taom_lod_rel_mother}mother").ToString();
            case RelationKind.Son: return new TextObject("{=taom_lod_rel_son}son").ToString();
            case RelationKind.Daughter: return new TextObject("{=taom_lod_rel_daughter}daughter").ToString();
            case RelationKind.Husband: return new TextObject("{=taom_lod_rel_husband}husband").ToString();
            case RelationKind.Wife: return new TextObject("{=taom_lod_rel_wife}wife").ToString();
            case RelationKind.Brother: return new TextObject("{=taom_lod_rel_brother}brother").ToString();
            case RelationKind.Sister: return new TextObject("{=taom_lod_rel_sister}sister").ToString();
            case RelationKind.ExHusband: return new TextObject("{=taom_lod_rel_ex_husband}former husband").ToString();
            default: return new TextObject("{=taom_lod_rel_ex_wife}former wife").ToString();
        }
    }
}
