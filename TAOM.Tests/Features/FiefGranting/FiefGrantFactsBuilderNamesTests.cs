using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FiefGranting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;

namespace TAOM.Tests.Features.FiefGranting;

/// <summary>
/// #866: the ballot's fief count and its hover list come from one enumeration, so they cannot drift. The ballot shows
/// <see cref="FiefGrantFactsBuilder.ClanFortificationNames"/> and the scoring reads
/// <see cref="FiefGrantFactsBuilder.CountClanFortifications"/>. Each test stages bare engine objects (uninitialized, private
/// fields set directly) so no campaign is needed.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FiefGrantFactsBuilderNamesTests
{
    private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static Settlement Fief(string name, string componentField, Type componentType, bool castle = false)
    {
        var settlement = (Settlement)FormatterServices.GetUninitializedObject(typeof(Settlement));
        Field(typeof(Settlement), "_name").SetValue(settlement, new TextObject("{=!}" + name));
        // Settlement.Name reads Party.CustomName first; a bare PartyBase has none, so the name falls through to _name.
        Field(typeof(Settlement), "<Party>k__BackingField").SetValue(settlement, FormatterServices.GetUninitializedObject(typeof(PartyBase)));
        var component = FormatterServices.GetUninitializedObject(componentType);
        if (castle)
            Field(componentType, "_isCastle").SetValue(component, true);
        Field(typeof(Settlement), componentField).SetValue(settlement, component);
        return settlement;
    }

    // A bare Town reports IsTown (its _isCastle is false); the Town field is what Settlement.IsTown and IsCastle read.
    internal static Settlement Town(string name) => Fief(name, "Town", typeof(Town));

    internal static Settlement Castle(string name) => Fief(name, "Town", typeof(Town), castle: true);

    internal static Settlement Village(string name) => Fief(name, "Village", typeof(Village));

    internal static Clan ClanHolding(params Settlement[] settlements)
    {
        var clan = (Clan)FormatterServices.GetUninitializedObject(typeof(Clan));
        var list = new TaleWorlds.Library.MBList<Settlement>();
        foreach (var settlement in settlements)
            list.Add(settlement);
        Field(typeof(Clan), "_settlementsCache").SetValue(clan, list);
        return clan;
    }

    private static FieldInfo Field(Type type, string name)
    {
        var field = type.GetField(name, Any);
        Assert.IsNotNull(field, type.Name + "." + name + " is gone; the staging needs a new field name.");
        return field!;
    }

    [TestMethod]
    public void Names_ListEveryFortificationExceptTheContestedOne_AndNoVillage()
    {
        var contested = Town("Pelargir");
        var clan = ClanHolding(Town("Dol Amroth"), Village("Edge"), contested, Castle("Minas Tirith"));

        var names = FiefGrantFactsBuilder.ClanFortificationNames(clan, contested);

        CollectionAssert.AreEquivalent(new[] { "Dol Amroth", "Minas Tirith" }, names.ToArray());
    }

    [TestMethod]
    public void Count_EqualsTheNamesList()
    {
        var contested = Town("Pelargir");
        var clan = ClanHolding(Town("A"), Village("V"), contested, Town("B"), Town("C"));

        Assert.AreEqual(
            FiefGrantFactsBuilder.ClanFortificationNames(clan, contested).Count,
            FiefGrantFactsBuilder.CountClanFortifications(clan, contested));
        Assert.AreEqual(3, FiefGrantFactsBuilder.CountClanFortifications(clan, contested));
    }

    // Build cannot run on bare objects (Clan.PlayerClan reads Campaign.Current), so the scoring input is pinned in source: Build
    // must take its fortification count from CountClanFortifications, the method the test above ties to the names list.
    [TestMethod]
    public void Build_TakesItsFortificationCount_FromCountClanFortifications()
    {
        var src = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/Features/FiefGranting/FiefGrantFactsBuilder.cs", stripComments: true);
        var build = src.Substring(src.IndexOf("public static FiefGrantCandidateFacts Build(", StringComparison.Ordinal));
        build = build.Substring(0, build.IndexOf("public static int CountClanFortifications", StringComparison.Ordinal));

        StringAssert.Contains(build, "CountClanFortifications(clan, contested)");
        Assert.IsFalse(build.Contains("CountFortifications("), "Build must not count through a second path");
    }

    [TestMethod]
    public void ANullClan_HasNoNames()
    {
        Assert.AreEqual(0, FiefGrantFactsBuilder.ClanFortificationNames(null!, null!).Count);
        Assert.AreEqual(0, FiefGrantFactsBuilder.CountClanFortifications(null!, null!));
    }
}
