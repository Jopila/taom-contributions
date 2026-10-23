using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;

namespace TAOM.Tests.Features.LinesOfDescent;

/// <summary>
/// A hand-built family graph behind <see cref="IHeroFamilyAdapter"/>. Heroes are listed and in a clan by default;
/// the parent links also append to the parent's child list, as the engine's setters do.
/// </summary>
internal sealed class FakeFamilyGraph : IHeroFamilyAdapter
{
    internal sealed class Person
    {
        public string Id = "";
        public string? Father, Mother, Spouse, Clan = "c", Kingdom;
        public bool Female, Listed = true, ClanLeader, KingdomLeader, Ruling, Minor;
        public double Birth;
        public List<string> Ex = new List<string>();
        public List<string> Kids = new List<string>();
    }

    internal readonly Dictionary<string, Person> People = new Dictionary<string, Person>();

    internal Person Add(string id, bool female = false, double birth = 0, string? father = null, string? mother = null)
    {
        var p = new Person { Id = id, Female = female, Birth = birth };
        People[id] = p;
        if (father != null) SetFather(id, father);
        if (mother != null) SetMother(id, mother);
        return p;
    }

    internal void SetFather(string child, string father)
    {
        People[child].Father = father;
        People[father].Kids.Add(child);
    }

    internal void SetMother(string child, string mother)
    {
        People[child].Mother = mother;
        People[mother].Kids.Add(child);
    }

    internal void Marry(string a, string b)
    {
        People[a].Spouse = b;
        People[b].Spouse = a;
    }

    /// <summary>The engine's divorce: the spouse is appended to the ex list of both, and the current spouse is cleared.</summary>
    internal void Divorce(string a, string b)
    {
        People[a].Spouse = null;
        People[b].Spouse = null;
        People[a].Ex.Add(b);
        People[b].Ex.Add(a);
    }

    private Person? Get(string id) => People.TryGetValue(id, out var p) ? p : null;

    public bool Exists(string heroId) => People.ContainsKey(heroId);

    public string? FatherId(string heroId) => Get(heroId)?.Father;

    public string? MotherId(string heroId) => Get(heroId)?.Mother;

    public string? SpouseId(string heroId) => Get(heroId)?.Spouse;

    public IReadOnlyList<string> ExSpouseIds(string heroId) => Get(heroId)?.Ex ?? new List<string>();

    public IReadOnlyList<string> ChildIds(string heroId) => Get(heroId)?.Kids ?? new List<string>();

    public IReadOnlyList<string> SiblingIds(string heroId)
    {
        var p = Get(heroId);
        if (p == null) return new List<string>();
        var all = new List<string>();
        foreach (var parent in new[] { p.Father, p.Mother })
            if (parent != null && People.ContainsKey(parent))
                all.AddRange(People[parent].Kids);
        return all.Where(k => k != heroId).Distinct().ToList();
    }

    public bool IsFemale(string heroId) => Get(heroId)?.Female ?? false;

    public double BirthDay(string heroId) => Get(heroId)?.Birth ?? 0;

    public bool IsListed(string heroId) => Get(heroId)?.Listed ?? false;

    public string? ClanId(string heroId) => Get(heroId)?.Clan;

    public bool IsClanLeader(string heroId) => Get(heroId)?.ClanLeader ?? false;

    public string? KingdomId(string heroId) => Get(heroId)?.Kingdom;

    public bool IsKingdomLeader(string heroId) => Get(heroId)?.KingdomLeader ?? false;

    public bool InRulingClan(string heroId) => Get(heroId)?.Ruling ?? false;

    public bool InMinorFaction(string heroId) => Get(heroId)?.Minor ?? false;
}
