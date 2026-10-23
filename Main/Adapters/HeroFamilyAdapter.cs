using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="IHeroFamilyAdapter"/> (Lines of Descent, #905). Heroes are found by string id with
/// <c>Hero.Find</c>, which goes through <c>Campaign.Current</c>, so every call needs a running campaign. Nothing is cached:
/// the family lists change when a hero is born, married or divorced. The engine's lists are returned raw (the setters append
/// without a duplicate check); the service cleans them. Listed means the hero has an encyclopedia page: the
/// same four tests as <c>DefaultEncyclopediaHeroPage.IsValidEncyclopediaItem</c>.
/// </summary>
public sealed class HeroFamilyAdapter : IHeroFamilyAdapter
{
    private static Hero? Find(string heroId) => Campaign.Current == null ? null : Hero.Find(heroId);

    private static readonly IReadOnlyList<string> None = new string[0];

    public bool Exists(string heroId) => Find(heroId) != null;

    public string? FatherId(string heroId) => Find(heroId)?.Father?.StringId;

    public string? MotherId(string heroId) => Find(heroId)?.Mother?.StringId;

    public string? SpouseId(string heroId) => Find(heroId)?.Spouse?.StringId;

    public IReadOnlyList<string> ExSpouseIds(string heroId)
        => Find(heroId)?.ExSpouses.Where(h => h != null).Select(h => h.StringId).ToList() ?? None;

    public IReadOnlyList<string> ChildIds(string heroId)
        => Find(heroId)?.Children.Where(h => h != null).Select(h => h.StringId).ToList() ?? None;

    public IReadOnlyList<string> SiblingIds(string heroId)
    {
        var hero = Find(heroId);
        if (hero == null)
            return None;
        var result = new List<string>();
        foreach (var parent in new[] { hero.Father, hero.Mother })
            if (parent != null)
                foreach (var child in parent.Children)
                    if (child != null && child != hero && !result.Contains(child.StringId))
                        result.Add(child.StringId);
        return result;
    }

    public bool IsFemale(string heroId) => Find(heroId)?.IsFemale ?? false;

    public double BirthDay(string heroId) => Find(heroId)?.BirthDay.ToDays ?? 0d;

    public bool IsListed(string heroId)
    {
        var hero = Find(heroId);
        if (hero == null || hero.IsTemplate || !hero.IsReady)
            return false;
        if (hero.MapFaction?.IsBanditFaction == true)
            return false;
        return hero.CharacterObject?.HiddenInEncyclopedia != true && !hero.HiddenInEncyclopedia;
    }

    public string? ClanId(string heroId) => Find(heroId)?.Clan?.StringId;

    public bool IsClanLeader(string heroId) => Find(heroId)?.IsClanLeader ?? false;

    public string? KingdomId(string heroId) => Find(heroId)?.Clan?.Kingdom?.StringId;

    public bool IsKingdomLeader(string heroId) => Find(heroId)?.IsKingdomLeader ?? false;

    public bool InRulingClan(string heroId)
    {
        var clan = Find(heroId)?.Clan;
        return clan?.Kingdom != null && clan.Kingdom.RulingClan == clan;
    }

    public bool InMinorFaction(string heroId) => Find(heroId)?.Clan?.IsMinorFaction ?? false;
}
