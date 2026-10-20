using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.Relations;
using TAOM.Features.TokensOfEsteem;
using TAOM.Features.TokensOfEsteem.Models;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// The shared setup for the Tokens of Esteem service tests. The alignment service is the REAL one, built from the shipped
/// <c>execution/alignment.json</c>, because a fake that resolved every side the same way hid a Neutral bug in Hard Questions
/// (lessons/testing-qa.md, 2026-10-19). The facts are raw and mutable: a test changes the fixture field, and the adapters
/// answer with the new value on the next read. Every relation the facts hold stays inside the engine's -100 to 100. The
/// cooldown ledger is the real one, keyed on the recipient's relation owner, as in the game.
/// </summary>
internal sealed class GiftFixture
{
    public const string HeroId = "hero_under_test";

    public IGiftHeroAdapter Heroes { get; } = Substitute.For<IGiftHeroAdapter>();

    public IGiftItemAdapter Items { get; } = Substitute.For<IGiftItemAdapter>();

    public IPartyItemRosterAdapter Roster { get; } = Substitute.For<IPartyItemRosterAdapter>();

    public IGiftCatalogProvider CatalogProvider { get; } = Substitute.For<IGiftCatalogProvider>();

    public IGiftSettingsProvider SettingsProvider { get; } = Substitute.For<IGiftSettingsProvider>();

    public IRelationGrantService Relations { get; } = Substitute.For<IRelationGrantService>();

    public IModLogger Logger { get; } = Substitute.For<IModLogger>();

    public IAlignmentService Alignment { get; } = RealAlignment();

    public GiftCooldownLedger Ledger { get; } = new();

    public GiftSettings Settings { get; set; } = new(true, 7, 15, true, 500, 2000, 5000);

    public GiftPlayerFacts? Player { get; set; } = new("player_hero", "empire_w", "gondor", 20000, 100);

    public GiftRecipientFacts? Recipient { get; set; } = Lord();

    public List<HeldGood> Held { get; } = new();

    public GiftCatalog Catalog { get; set; } = DefaultCatalog();

    public GiftService Sut { get; }

    public GiftFixture()
    {
        SettingsProvider.Get().Returns(_ => Settings);
        CatalogProvider.Get().Returns(_ => Catalog);
        Heroes.ReadPlayer().Returns(_ => Player);
        Heroes.ReadRecipient(HeroId).Returns(_ => Recipient);
        Heroes.PayGoldToHero(HeroId, Arg.Any<int>()).Returns(true);
        Items.ReadHeldGoods(Arg.Any<IReadOnlyCollection<string>>()).Returns(_ => Held.ToArray());
        Roster.RemoveItem(Arg.Any<string>(), Arg.Any<int>()).Returns(call => call.ArgAt<int>(1));
        Relations.Grant(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()).Returns(call => call.ArgAt<int>(1));
        Sut = new GiftService(Heroes, Items, Roster, CatalogProvider, SettingsProvider, Alignment, Relations, Logger);
    }

    /// <summary>A lord in peace: not a child, prisoner or clan member; his own relation owner unless told otherwise.</summary>
    public static GiftRecipientFacts Lord(int relation = 0, string race = "human", string culture = "gondor", string kingdom = "empire_w",
        string? owner = null) =>
        new(HeroId, owner ?? HeroId, true, false, false, false, false, false, false, relation, race, culture, kingdom);

    /// <summary>A companion travelling in the player's party.</summary>
    public static GiftRecipientFacts Companion(int relation = 0, string race = "human", string culture = "gondor") =>
        new(HeroId, HeroId, false, false, false, false, true, true, false, relation, race, culture, "");

    public static HeldGood Good(string itemId, string category, int value, int count = 1, bool modified = false) =>
        new(itemId, itemId + " name", category, value, count, modified);

    /// <summary>
    /// The catalog these tests run on (not the shipped file): jewelry is worth 8 to a lord and dwarves add 4; wine is 5 and
    /// elves add 3 (or the Rohirrim culture, <c>vlandia</c>, add 2); two overlapping groups on horse pin "highest, no stacking".
    /// </summary>
    public static GiftCatalog DefaultCatalog() => new(new[]
    {
        new GiftCatalogEntry("jewelry", 8, new[] { new TasteGroup(new[] { "dwarf" }, new string[0], 4) }),
        new GiftCatalogEntry("wine", 5, new[]
        {
            new TasteGroup(new[] { "elf" }, new string[0], 3),
            new TasteGroup(new string[0], new[] { "vlandia" }, 2),
        }),
        new GiftCatalogEntry("horse", 4, new[]
        {
            new TasteGroup(new string[0], new[] { "vlandia" }, 3),
            new TasteGroup(new[] { "human" }, new string[0], 6),
        }),
        new GiftCatalogEntry("salt", 1, new TasteGroup[0]),
        new GiftCatalogEntry("noble_horse", 10, new[] { new TasteGroup(new string[0], new[] { "vlandia" }, 10) }),
    });

    public static IAlignmentService RealAlignment()
    {
        var path = RepoPaths.RepoPath("Main", "_Module", "ModuleData", "execution", "alignment.json");
        var sides = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path))!;
        var config = Substitute.For<IAlignmentConfigProvider>();
        config.LoadAlignments().Returns(sides);
        return new AlignmentService(config, Substitute.For<IModLogger>());
    }
}
