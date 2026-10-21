using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.NamedCompanions.Domain;
using TAOM.Features.NamedCompanions.Oaths;

namespace TAOM.Tests.Features.NamedCompanions.Oaths;

[TestClass]
public class OathConfigValidatorTests
{
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private static NamedCompanionDefinition Companion(HireTermsDefinition terms = null, params TopicDefinition[] topics) => new()
    {
        CharacterId = "c1",
        HireTerms = terms,
        Topics = topics.ToList(),
    };

    private static TopicDefinition Topic(string id = "t1") => new() { Id = id, PromptStringId = "p", ReplyStringId = "r" };

    private static HireTermsDefinition Terms(int? gold = 100) => new() { Gold = gold };

    private OathValidationResult Run(params NamedCompanionDefinition[] companions) =>
        OathConfigValidator.Validate(companions, _logger);

    [TestMethod]
    public void ValidConfig_HasNoReversionsAndNoWarning()
    {
        var c = Companion(Terms(250), Topic());

        var result = Run(c);

        Assert.AreEqual(0, result.Reversions);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(1_000_001)]
    public void HireGold_OutOfRange_RevertsToVanillaPrice(int gold)
    {
        var c = Companion(new HireTermsDefinition { Gold = gold, RequiresFlags = new List<string> { "keep" } });

        var result = Run(c);

        Assert.IsNull(c.HireTerms.Gold);
        Assert.AreEqual(1, result.Reversions);
    }

    [TestMethod]
    public void HireGold_ZeroWithAnItem_IsKept()
    {
        var c = Companion(new HireTermsDefinition { Gold = 0, Items = new List<ItemCostDefinition> { new() { ItemId = "a", Count = 1 } } });

        var result = Run(c);

        Assert.AreEqual(0, c.HireTerms.Gold);
        Assert.AreEqual(0, result.Reversions);
    }

    [TestMethod]
    public void HireGold_ZeroAlone_RevertsToTheVanillaPrice()
    {
        var c = Companion(new HireTermsDefinition { Gold = 0, RequiresFlags = new List<string> { "keep" } });

        var result = Run(c);

        Assert.IsNull(c.HireTerms.Gold);
        Assert.AreEqual(1, result.Reversions);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("items only")));
    }

    [TestMethod]
    public void HireGold_ZeroAloneWithNothingElse_DropsTheBlock()
    {
        var c = Companion(Terms(0));

        Run(c);

        Assert.IsNull(c.HireTerms);
    }

    [TestMethod]
    public void Validate_TakesAnyEnumerable()
    {
        var c = Companion(Terms(250), Topic());

        var result = OathConfigValidator.Validate(new[] { c }.Where(x => x != null), _logger);

        Assert.AreEqual(0, result.Reversions);
    }

    [TestMethod]
    public void Item_EmptyId_Skipped()
    {
        var c = Companion(new HireTermsDefinition { Gold = 1, Items = new List<ItemCostDefinition> { new() { ItemId = "", Count = 1 } } });

        Run(c);

        Assert.AreEqual(0, c.HireTerms.Items.Count);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(1000)]
    public void Item_CountOutOfRange_Skipped(int count)
    {
        var c = Companion(new HireTermsDefinition { Gold = 1, Items = new List<ItemCostDefinition> { new() { ItemId = "a", Count = count } } });

        Run(c);

        Assert.AreEqual(0, c.HireTerms.Items.Count);
    }

    [TestMethod]
    public void Items_MoreThanSix_ExtrasDropped()
    {
        var items = Enumerable.Range(0, 8).Select(i => new ItemCostDefinition { ItemId = "i" + i, Count = 1 }).ToList();
        var c = Companion(new HireTermsDefinition { Gold = 1, Items = items });

        Run(c);

        Assert.AreEqual(6, c.HireTerms.Items.Count);
        Assert.AreEqual("i5", c.HireTerms.Items.Last().ItemId);
    }

    [TestMethod]
    public void Items_DuplicateId_RepeatSkipped()
    {
        var items = new List<ItemCostDefinition> { new() { ItemId = "a", Count = 1 }, new() { ItemId = "a", Count = 5 } };
        var c = Companion(new HireTermsDefinition { Gold = 1, Items = items });

        Run(c);

        Assert.AreEqual(1, c.HireTerms.Items.Count);
        Assert.AreEqual(1, c.HireTerms.Items[0].Count);
    }

    [TestMethod]
    public void TermsFlags_LowerCasedAndBadOnesSkipped()
    {
        var c = Companion(new HireTermsDefinition { Gold = 1, RequiresFlags = new List<string> { "Good.Flag_1", "bad flag", "" } });

        var result = Run(c);

        CollectionAssert.AreEqual(new[] { "good.flag_1" }, c.HireTerms.RequiresFlags);
        Assert.AreEqual(2, result.Reversions);
    }

    [TestMethod]
    public void TopicFlags_BadRequiresAndSetsSkipped()
    {
        var topic = Topic();
        topic.RequiresFlags = new List<string> { "ok", new string('a', 65) };
        topic.SetsFlags = new List<string> { "ok2", "no-dash" };
        var c = Companion(null, topic);

        Run(c);

        CollectionAssert.AreEqual(new[] { "ok" }, c.Topics[0].RequiresFlags);
        CollectionAssert.AreEqual(new[] { "ok2" }, c.Topics[0].SetsFlags);
    }

    [TestMethod]
    public void HireTerms_NoGoldNoItemsNoFlags_DroppedSoVanillaApplies()
    {
        var c = Companion(new HireTermsDefinition { LockedStringId = "x" });

        Run(c);

        Assert.IsNull(c.HireTerms);
    }

    [TestMethod]
    public void HireTerms_ItemsOnly_Kept()
    {
        var c = Companion(new HireTermsDefinition { Items = new List<ItemCostDefinition> { new() { ItemId = "a", Count = 1 } } });

        Run(c);

        Assert.IsNotNull(c.HireTerms);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("Has Space")]
    [DataRow("UPPER")]
    public void TopicId_BadPattern_Skipped(string id)
    {
        var c = Companion(null, Topic(id));

        Run(c);

        Assert.AreEqual(0, c.Topics.Count);
    }

    [TestMethod]
    public void TopicId_DuplicateAcrossCompanions_SecondSkipped()
    {
        var first = Companion(null, Topic("same"));
        var second = Companion(null, Topic("same"));

        Run(first, second);

        Assert.AreEqual(1, first.Topics.Count);
        Assert.AreEqual(0, second.Topics.Count);
    }

    [DataTestMethod]
    [DataRow(null, "r")]
    [DataRow("", "r")]
    [DataRow("p", null)]
    [DataRow("p", "")]
    public void Topic_MissingTextId_Skipped(string prompt, string reply)
    {
        var topic = Topic();
        topic.PromptStringId = prompt;
        topic.ReplyStringId = reply;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, c.Topics.Count);
    }

    [DataTestMethod]
    [DataRow("any", TopicWhen.Any)]
    [DataRow("Recruited", TopicWhen.Recruited)]
    [DataRow("UNRECRUITED", TopicWhen.Unrecruited)]
    [DataRow("", TopicWhen.Any)]
    public void When_KnownValuesParsed(string when, TopicWhen expected)
    {
        var topic = Topic();
        topic.When = when;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(expected, c.Topics[0].WhenKind);
    }

    [TestMethod]
    public void When_UnknownNonEmpty_SkipsTopic()
    {
        var topic = Topic();
        topic.When = "recuited";
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, c.Topics.Count);
    }

    [DataTestMethod]
    [DataRow(-101)]
    [DataRow(101)]
    public void MinRelation_OutOfRange_RevertsToMinus100(int value)
    {
        var topic = Topic();
        topic.MinRelation = value;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(-100, c.Topics[0].MinRelation);
    }

    [DataTestMethod]
    [DataRow(-5)]
    [DataRow(100_001)]
    public void GoldCost_OutOfRange_RevertsToZero(int value)
    {
        var topic = Topic();
        topic.GoldCost = value;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, c.Topics[0].GoldCost);
    }

    [DataTestMethod]
    [DataRow(-5)]
    [DataRow(10_001)]
    public void GoldReward_OutOfRange_RevertsToZero(int value)
    {
        var topic = Topic();
        topic.GoldReward = value;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, c.Topics[0].GoldReward);
    }

    [TestMethod]
    public void GoldCostAndReward_Both_SkipsTopic()
    {
        var topic = Topic();
        topic.GoldCost = 10;
        topic.GoldReward = 10;
        topic.Once = true;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, c.Topics.Count);
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(366)]
    public void CooldownDays_OutOfRange_RevertsToZero(int value)
    {
        var topic = Topic();
        topic.CooldownDays = value;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, c.Topics[0].CooldownDays);
    }

    [DataTestMethod]
    [DataRow(5, 0, 0)]
    [DataRow(0, 5, 0)]
    [DataRow(0, 0, 3)]
    public void StateChangingTopic_WithoutOnceOrCooldown_Skipped(int cost, int reward, int gain)
    {
        var topic = Topic();
        topic.GoldCost = cost;
        topic.GoldReward = reward;
        topic.RelationGain = gain;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, c.Topics.Count);
    }

    [TestMethod]
    public void StateChangingTopic_WithOnceOrCooldown_Kept()
    {
        var once = Topic("a");
        once.GoldReward = 5;
        once.Once = true;
        var cool = Topic("b");
        cool.RelationGain = 1;
        cool.CooldownDays = 3;
        var c = Companion(null, once, cool);

        Run(c);

        Assert.AreEqual(2, c.Topics.Count);
    }

    [DataTestMethod]
    [DataRow(-101)]
    [DataRow(101)]
    public void MaxRelation_OutOfRange_RevertsTo100(int value)
    {
        var topic = Topic();
        topic.MaxRelation = value;
        var c = Companion(null, topic);

        var result = Run(c);

        Assert.AreEqual(100, c.Topics[0].MaxRelation);
        Assert.AreEqual(1, result.Reversions);
    }

    [TestMethod]
    public void MaxRelation_DefaultIs100()
    {
        Assert.AreEqual(100, Topic().MaxRelation);
    }

    [TestMethod]
    public void MinRelationAboveMaxRelation_SkipsTheTopic()
    {
        var topic = Topic();
        topic.MinRelation = 30;
        topic.MaxRelation = 20;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, c.Topics.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("min_relation") && s.Contains("max_relation")));
    }

    [TestMethod]
    public void MinRelationEqualToMaxRelation_IsKept()
    {
        var topic = Topic();
        topic.MinRelation = 20;
        topic.MaxRelation = 20;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(1, c.Topics.Count);
    }

    [TestMethod]
    public void MaxRelationRevertedBelowMin_IsOrderedAfterTheRevert()
    {
        // 150 reverts to 100, so a min of 50 is then valid and the topic stays.
        var topic = Topic();
        topic.MinRelation = 50;
        topic.MaxRelation = 150;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(1, c.Topics.Count);
        Assert.AreEqual(100, c.Topics[0].MaxRelation);
    }

    [DataTestMethod]
    [DataRow(-11)]
    [DataRow(11)]
    public void RelationGain_BeyondTen_RevertsToZero(int value)
    {
        var topic = Topic();
        topic.RelationGain = value;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, c.Topics[0].RelationGain);
    }

    [TestMethod]
    public void RelationGain_MinusTen_IsKept()
    {
        var topic = Topic();
        topic.RelationGain = -10;
        topic.Once = true;
        var c = Companion(null, topic);

        var result = Run(c);

        Assert.AreEqual(-10, c.Topics[0].RelationGain);
        Assert.AreEqual(0, result.Reversions);
    }

    [TestMethod]
    public void NegativeRelationGain_WithoutOnceOrCooldown_SkipsTheTopic()
    {
        var topic = Topic();
        topic.RelationGain = -3;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, c.Topics.Count);
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(-5)]
    [DataRow(101)]
    public void RandomChance_OutOfRange_RevertsTo100(int value)
    {
        var topic = Topic();
        topic.RandomChance = value;
        var c = Companion(null, topic);

        var result = Run(c);

        Assert.AreEqual(100, c.Topics[0].RandomChance);
        Assert.AreEqual(1, result.Reversions);
    }

    [DataTestMethod]
    [DataRow(1)]
    [DataRow(50)]
    [DataRow(100)]
    public void RandomChance_InRange_IsKept(int value)
    {
        var topic = Topic();
        topic.RandomChance = value;
        var c = Companion(null, topic);

        var result = Run(c);

        Assert.AreEqual(value, c.Topics[0].RandomChance);
        Assert.AreEqual(0, result.Reversions);
    }

    [TestMethod]
    public void RandomChance_Alone_IsNotAStateChange()
    {
        var topic = Topic();
        topic.RandomChance = 20;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(1, c.Topics.Count, "a chance alone needs no once or cooldown");
    }

    [TestMethod]
    public void RandomChance_DefaultIs100()
    {
        Assert.AreEqual(100, Topic().RandomChance);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TopicItems_EmptyId_Skipped(bool give)
    {
        var topic = Topic();
        topic.Once = true;
        var list = new List<ItemCostDefinition> { new() { ItemId = " ", Count = 1 }, new() { ItemId = "ok", Count = 1 } };
        if (give) topic.GiveItems = list; else topic.TakeItems = list;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual("ok", (give ? c.Topics[0].GiveItems : c.Topics[0].TakeItems).Single().ItemId);
    }

    [DataTestMethod]
    [DataRow(true, 0)]
    [DataRow(true, 1000)]
    [DataRow(false, 0)]
    [DataRow(false, 1000)]
    public void TopicItems_CountOutOfRange_Skipped(bool give, int count)
    {
        var topic = Topic();
        topic.Once = true;
        var list = new List<ItemCostDefinition> { new() { ItemId = "a", Count = count } };
        if (give) topic.GiveItems = list; else topic.TakeItems = list;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, (give ? c.Topics[0].GiveItems : c.Topics[0].TakeItems).Count);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TopicItems_MoreThanSix_ExtrasDropped(bool give)
    {
        var topic = Topic();
        topic.Once = true;
        var list = Enumerable.Range(0, 8).Select(i => new ItemCostDefinition { ItemId = "i" + i, Count = 1 }).ToList();
        if (give) topic.GiveItems = list; else topic.TakeItems = list;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(6, (give ? c.Topics[0].GiveItems : c.Topics[0].TakeItems).Count);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TopicItems_DuplicateId_RepeatSkipped(bool give)
    {
        var topic = Topic();
        topic.Once = true;
        var list = new List<ItemCostDefinition> { new() { ItemId = "a", Count = 1 }, new() { ItemId = "a", Count = 5 } };
        if (give) topic.GiveItems = list; else topic.TakeItems = list;
        var c = Companion(null, topic);

        Run(c);

        var kept = give ? c.Topics[0].GiveItems : c.Topics[0].TakeItems;
        Assert.AreEqual(1, kept.Count);
        Assert.AreEqual(1, kept[0].Count);
    }

    [TestMethod]
    public void TopicItems_NullListsBecomeEmpty()
    {
        var c = Companion(null, Topic());

        Run(c);

        Assert.AreEqual(0, c.Topics[0].GiveItems.Count);
        Assert.AreEqual(0, c.Topics[0].TakeItems.Count);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void TopicItems_WithoutOnceOrCooldown_SkipsTheTopic(bool give)
    {
        var topic = Topic();
        var list = new List<ItemCostDefinition> { new() { ItemId = "a", Count = 1 } };
        if (give) topic.GiveItems = list; else topic.TakeItems = list;
        var c = Companion(null, topic);

        Run(c);

        Assert.AreEqual(0, c.Topics.Count);
    }

    [TestMethod]
    public void TopicItems_WithOnceOrCooldown_Kept()
    {
        var once = Topic("a");
        once.Once = true;
        once.GiveItems = new List<ItemCostDefinition> { new() { ItemId = "x", Count = 1 } };
        var cool = Topic("b");
        cool.CooldownDays = 2;
        cool.TakeItems = new List<ItemCostDefinition> { new() { ItemId = "y", Count = 1 } };
        var c = Companion(null, once, cool);

        Run(c);

        Assert.AreEqual(2, c.Topics.Count);
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ResolveIds_UnknownTopicItem_SkipsTheWholeTopicAndNamesTheId(bool give)
    {
        var bad = Topic("bad");
        var list = new List<ItemCostDefinition> { new() { ItemId = "real", Count = 1 }, new() { ItemId = "fake", Count = 1 } };
        if (give) bad.GiveItems = list; else bad.TakeItems = list;
        var good = Topic("good");
        var c = Companion(null, bad, good);

        var drops = OathConfigValidator.ResolveIds(new[] { c }, id => id == "real", _ => true, _ => true, _logger);

        Assert.AreEqual(1, drops);
        Assert.AreEqual("good", c.Topics.Single().Id);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("'fake'")));
    }

    [TestMethod]
    public void ResolveIds_UnknownHomeSettlement_IsClearedSoSpawnApplies()
    {
        var c = Companion(null);
        c.SpawnSettlement = "town_A";
        c.HomeSettlement = "town_nowhere";

        var drops = OathConfigValidator.ResolveIds(new[] { c }, _ => true, _ => true, id => id == "town_A", _logger);

        Assert.AreEqual(1, drops);
        Assert.IsNull(c.HomeSettlement);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("'town_nowhere'")));
    }

    [TestMethod]
    public void ResolveIds_KnownHomeSettlement_IsKept()
    {
        var c = Companion(null);
        c.HomeSettlement = "town_B";

        var drops = OathConfigValidator.ResolveIds(new[] { c }, _ => true, _ => true, id => id == "town_B", _logger);

        Assert.AreEqual(0, drops);
        Assert.AreEqual("town_B", c.HomeSettlement);
    }

    [TestMethod]
    public void ResolveIds_NoHomeSettlement_DoesNotAskTheSettlementCheck()
    {
        var c = Companion(null);

        var drops = OathConfigValidator.ResolveIds(new[] { c }, _ => true, _ => true, _ => false, _logger);

        Assert.AreEqual(0, drops);
    }

    [TestMethod]
    public void AnyReversion_LogsOneSummaryWarning()
    {
        var a = Topic("A");
        var b = Topic("B");
        var c = Companion(null, a, b);

        var result = Run(c);

        Assert.AreEqual(2, result.Reversions);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("2 invalid")));
    }

    [TestMethod]
    public void GoldZeroWithAnUnknownItem_SurvivesValidate_ThenResolveIdsDropsTheTerms()
    {
        var c = Companion(new HireTermsDefinition { Gold = 0, Items = new List<ItemCostDefinition> { new() { ItemId = "fake", Count = 1 } } });

        Run(c);
        Assert.AreEqual(0, c.HireTerms.Gold, "Validate keeps gold 0 because an item is configured");
        var drops = OathConfigValidator.ResolveIds(new[] { c }, _ => false, _ => true, _ => true, _logger);

        Assert.AreEqual(1, drops);
        Assert.IsNull(c.HireTerms, "a renamed item id must not leave a free hire");
    }

    [TestMethod]
    public void ResolveIds_UnknownItem_DropsTheWholeHireTermsAndNamesTheId()
    {
        var c = Companion(new HireTermsDefinition
        {
            Gold = 1,
            LockedStringId = "kept_or_not",
            Items = new List<ItemCostDefinition> { new() { ItemId = "real", Count = 1 }, new() { ItemId = "fake", Count = 1 } },
        });

        var drops = OathConfigValidator.ResolveIds(new[] { c }, id => id == "real", _ => true, _ => true, _logger);

        Assert.AreEqual(1, drops);
        Assert.IsNull(c.HireTerms, "a half-priced hire would be cheaper than configured, so vanilla hire applies");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("'fake'")));
    }

    [TestMethod]
    public void ResolveIds_UnknownTopicTextAndLockedLine_AreDropped()
    {
        var topicOk = Topic("ok");
        var topicBadPrompt = Topic("badp");
        topicBadPrompt.PromptStringId = "missing";
        var topicBadReply = Topic("badr");
        topicBadReply.ReplyStringId = "missing";
        var c = Companion(new HireTermsDefinition { Gold = 1, LockedStringId = "missing" }, topicOk, topicBadPrompt, topicBadReply);

        var drops = OathConfigValidator.ResolveIds(new[] { c }, _ => true, id => id != "missing", _ => true, _logger);

        Assert.AreEqual(3, drops);
        Assert.IsNull(c.HireTerms.LockedStringId);
        Assert.AreEqual("ok", c.Topics.Single().Id);
    }

    [TestMethod]
    public void ResolveIds_TakesAnyEnumerable()
    {
        var c = Companion(Terms(5), Topic());

        var drops = OathConfigValidator.ResolveIds(new[] { c }.Where(x => x != null), _ => true, _ => true, _ => true, _logger);

        Assert.AreEqual(0, drops);
    }

    [TestMethod]
    public void ResolveIds_AllResolve_DropsNothing()
    {
        var c = Companion(Terms(5), Topic());

        var drops = OathConfigValidator.ResolveIds(new[] { c }, _ => true, _ => true, _ => true, _logger);

        Assert.AreEqual(0, drops);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedConfig_HasNoReversions()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln"))) dir = dir.Parent;
        Assert.IsNotNull(dir, "TAOM.sln not found walking upward from cwd");
        var path = Path.Combine(dir!.FullName, "Main", "_Module", "ModuleData", "named_companions", "named_companion_config.json");
        Assert.IsTrue(File.Exists(path), path);

        var list = JsonConvert.DeserializeObject<List<NamedCompanionDefinition>>(File.ReadAllText(path))!;
        var result = OathConfigValidator.Validate(list, _logger);

        Assert.AreEqual(0, result.Reversions);
    }
}
