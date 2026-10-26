using System;
using System.Collections.Generic;
using System.Linq;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Execution;
using TAOM.Features.Provender;
using TAOM.Features.Provender.Models;

namespace TAOM.Tests.Features.Provender;

/// <summary>
/// The doubles and the default scene for the Provender-master tests: a valid officer in the party, a town with two foods in
/// stock, plenty of gold, forty soldiers and a catalog of beer (the default), wine for elves and meat for orcs. A test changes
/// one fact and asserts what the service did. <see cref="World"/> prices every unit at the offer's price, so a buy of n units
/// costs n times the price unless a test says otherwise.
/// </summary>
internal sealed class ProvenderFixture
{
    public const string Officer = "lord_quartermaster";
    public const string Town = "town_1";

    public IProvenderWorldAdapter World { get; } = Substitute.For<IProvenderWorldAdapter>();
    public IProvenderSettingsProvider SettingsProvider { get; } = Substitute.For<IProvenderSettingsProvider>();
    public IRationCatalogProvider Rations { get; } = Substitute.For<IRationCatalogProvider>();
    public IHeroSkillXpAdapter Xp { get; } = Substitute.For<IHeroSkillXpAdapter>();
    public IPartyItemRosterAdapter Items { get; } = Substitute.For<IPartyItemRosterAdapter>();
    public ICoopSessionProvider Coop { get; } = Substitute.For<ICoopSessionProvider>();
    public IEnlistmentStateQuery Enlistment { get; } = Substitute.For<IEnlistmentStateQuery>();
    public IDedicatedServerProvider Server { get; } = Substitute.For<IDedicatedServerProvider>();
    public IProvenderSaleAdapter Sale { get; } = Substitute.For<IProvenderSaleAdapter>();
    public IAlignmentService Alignment { get; } = Substitute.For<IAlignmentService>();
    public IModLogger Logger { get; } = Substitute.For<IModLogger>();

    public ProvenderSettings Settings { get; set; } = new(true, 3, 2, 50, 5, 100, 10, 3);

    private readonly Dictionary<(string Settlement, string Item), int> _prices = new();
    private readonly Dictionary<(string Settlement, string Item), int> _stock = new();

    public ProvenderFixture()
    {
        SettingsProvider.Get().Returns(_ => Settings);
        Rations.Get().Returns(Catalog());
        Party(new PartyBuilder());
        Settlement(new SettlementBuilder());
        World.Buy(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()).Returns(call =>
        {
            var count = call.ArgAt<int>(2);
            // Never call the substitute inside its own callback: a test that re-stubs Buy would bind the wrong call.
            var price = _prices.TryGetValue((call.ArgAt<string>(0), call.ArgAt<string>(1)), out var p) ? p : 1;
            return new PurchaseResult(count, count * price);
        });
        // A flat price for each unit up to the stock, as the adapter answers for a village; empty for an item not on sale.
        World.UnitPrices(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()).Returns(call =>
        {
            var key = (call.ArgAt<string>(0), call.ArgAt<string>(1));
            if (!_prices.TryGetValue(key, out var p) || !_stock.TryGetValue(key, out var held))
                return new int[0];
            return Enumerable.Repeat(p, Math.Max(Math.Min(call.ArgAt<int>(2), held), 0)).ToArray();
        });
        World.Slaughter(Arg.Any<string>(), Arg.Any<int>()).Returns(call => call.ArgAt<int>(1));
        World.PayTroops(Arg.Any<int>()).Returns(call => call.ArgAt<int>(0));
        Items.GetItemCount(Arg.Any<string>()).Returns(0);
        Items.RemoveItem(Arg.Any<string>(), Arg.Any<int>()).Returns(call => call.ArgAt<int>(1));
    }

    public ProvenderService Service() => new(World, SettingsProvider, Rations, Xp, Items, Coop, Enlistment, Server, Logger);

    public ProvenderSaleService SaleService() => new(Sale, World, Service(), SettingsProvider, Alignment, Xp);

    public static RationCatalog Catalog() => new(new[]
    {
        new RationEntry("beer", "beer", 7, 15, true, new[] { "dwarf" }, new[] { "vlandia" }),
        new RationEntry("wine", "wine", 20, 20, false, new[] { "elf" }, new string[0]),
        new RationEntry("meat", "meat", 5, 15, false, new[] { "orc" }, new string[0]),
    });

    public void Party(PartyBuilder b)
    {
        World.ReadParty().Returns(b.Build());
        World.QuartermasterId.Returns(b.OfficerId);
    }

    public void Settlement(SettlementBuilder b, string id = Town)
    {
        var facts = b.Build();
        foreach (var offer in facts.Offers)
        {
            _prices[(id, offer.ItemId)] = offer.Price;
            _stock[(id, offer.ItemId)] = offer.Stock;
        }
        World.ReadSettlement(id).Returns(facts);
    }

    public void Offer(string item, int price, int stock, string settlement = Town)
    {
        _prices[(settlement, item)] = price;
        _stock[(settlement, item)] = stock;
    }

    public sealed class PartyBuilder
    {
        public bool HasParty = true;
        public string? OfficerId = Officer;
        public bool OfficerIsPlayer;
        public bool OfficerInParty = true;
        public int Eaters = 100;
        public int Gold = 5000;
        public int Troops = 40;
        public string? SettlementId = Town;
        public float EdibleFood = 50f;
        public int DailyWage;
        public float FoodPerDay = 5f;
        public Dictionary<string, int> HeldFood = new();
        public List<LivestockStack> Livestock = new();
        public Dictionary<string, int> RaceCounts = new() { ["human"] = 40 };
        public string? LeaderCulture = "gondor";
        public bool CampReady;
        public bool InEncounter;

        public ProvenderPartyFacts Build() => new()
        {
            HasParty = HasParty,
            OfficerId = OfficerId,
            OfficerIsPlayer = OfficerIsPlayer,
            OfficerInParty = OfficerInParty,
            Eaters = Eaters,
            Gold = Gold,
            Troops = Troops,
            SettlementId = SettlementId,
            EdibleFood = EdibleFood,
            DailyWage = DailyWage,
            FoodPerDay = FoodPerDay,
            HeldFood = HeldFood.ToDictionary(p => p.Key, p => p.Value),
            Livestock = Livestock.ToList(),
            RaceCounts = RaceCounts.ToDictionary(p => p.Key, p => p.Value),
            LeaderCulture = LeaderCulture,
            CampReady = CampReady,
            InEncounter = InEncounter,
        };
    }

    public sealed class SettlementBuilder
    {
        public bool Found = true;
        public bool IsTown = true;
        public bool IsVillage;
        public bool IsCastle;
        public bool IsDisturbed;
        public bool RefusesPlayer;
        public bool AtWarWithPlayer;
        public bool HasMarketTown = true;
        public List<FoodOffer> Offers = new() { new FoodOffer("grain", 10, 50), new FoodOffer("cheese", 20, 30) };

        public ProvenderSettlementFacts Build() => new()
        {
            Found = Found,
            IsTown = IsTown,
            IsVillage = IsVillage,
            IsCastle = IsCastle,
            IsDisturbed = IsDisturbed,
            RefusesPlayer = RefusesPlayer,
            AtWarWithPlayer = AtWarWithPlayer,
            HasMarketTown = HasMarketTown,
            Offers = Offers.ToList(),
        };
    }
}
