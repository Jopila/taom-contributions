using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;
using TAOM.Features.NameGrammarTags;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IGrammarRendererAdapter"/>. Variables are bound as TextObjects, never strings,
/// because <c>ToString()</c> strips the grammar tag before the variable is set.
/// </summary>
public class GrammarRendererAdapter : IGrammarRendererAdapter
{
    public string ActiveLanguageId => MBTextManager.ActiveTextLanguage;

    public string ActiveTextProcessor
    {
        get
        {
            try
            {
                // The processor the active language's data builds; the engine builds the same one when it switches language.
                return LocalizedTextManager.CreateTextProcessorForLanguage(MBTextManager.ActiveTextLanguage)?.GetType().Name ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }
    }

    public IReadOnlyList<string> KingdomIds() =>
        Campaign.Current == null
            ? Array.Empty<string>()
            : Kingdom.All.Where(k => k != null && !k.IsEliminated).Select(k => k.StringId).ToList();

    public string KingdomLabel(string kingdomId) => FindKingdom(kingdomId).Name.ToString();

    public string CultureId(string kingdomId) => FindKingdom(kingdomId).Culture.StringId;

    public IReadOnlyList<string> ClanIds(string kingdomId) => FindKingdom(kingdomId).Clans.Select(c => c.StringId).ToList();

    public IReadOnlyList<string> AllClanIds() => Clan.All.Where(c => c != null).Select(c => c.StringId).ToList();

    public IReadOnlyList<string> FiefIds(string kingdomId) =>
        FindKingdom(kingdomId).Settlements.Where(s => s.IsTown || s.IsCastle).Select(s => s.StringId).ToList();

    public IReadOnlyList<string> AllFiefIds() => Settlement.All.Where(s => s.IsTown || s.IsCastle).Select(s => s.StringId).ToList();

    public IReadOnlyList<string> Render(IReadOnlyList<string> texts, IReadOnlyList<RenderBinding> bindings)
    {
        var rendered = texts.Select(t => new TextObject(t)).ToList();
        foreach (var binding in bindings)
        {
            var value = Bound(binding);
            foreach (var r in rendered)
                r.SetTextVariable(binding.Variable, value);
        }
        return rendered.Select(r => r.ToString()).ToList();
    }

    public string RenderCanary(string text, string variable, string taggedValue)
    {
        var rendered = new TextObject(text);
        rendered.SetTextVariable(variable, new TextObject(taggedValue));
        return rendered.ToString();
    }

    private static TextObject Bound(RenderBinding b)
    {
        switch (b.Kind)
        {
            case NameKind.Kingdom:
            {
                var kingdom = FindKingdom(b.ObjectId);
                return b.Form == NameForm.Link ? kingdom.EncyclopediaLinkWithName
                    : b.Form == NameForm.Informal ? kingdom.InformalName
                    : b.Form == NameForm.Formal ? kingdom.FormalName : kingdom.Name;
            }
            case NameKind.Clan:
            {
                var clan = Clan.All.First(c => c.StringId == b.ObjectId);
                return b.Form == NameForm.Link ? clan.EncyclopediaLinkWithName
                    : b.Form == NameForm.Informal ? clan.InformalName : clan.Name;
            }
            case NameKind.Settlement:
            {
                var settlement = Settlement.Find(b.ObjectId);
                return b.Form == NameForm.Link ? settlement.EncyclopediaLinkWithName : settlement.Name;
            }
            case NameKind.Culture:
            {
                var culture = MBObjectManager.Instance.GetObject<CultureObject>(b.ObjectId);
                return b.Form == NameForm.Informal
                    ? FactionHelper.GetInformalNameForFactionCulture(culture)
                    : FactionHelper.GetFormalNameForFactionCulture(culture);
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(b), b.Kind, "Only name variables are bound.");
        }
    }

    private static Kingdom FindKingdom(string id) => Kingdom.All.First(k => k.StringId == id);
}
