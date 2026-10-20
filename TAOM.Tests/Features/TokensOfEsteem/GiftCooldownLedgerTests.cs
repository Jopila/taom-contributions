using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TokensOfEsteem;

namespace TAOM.Tests.Features.TokensOfEsteem;

/// <summary>
/// The saved map of hero id to the day of the last gift. Saved as a flat string dictionary (the HardQuestionsState shape),
/// and a damaged entry is refused and counted, never applied.
/// </summary>
[TestClass]
public class GiftCooldownLedgerTests
{
    [TestMethod]
    public void LastGiftDay_NeverStamped_IsNull()
    {
        Assert.IsNull(new GiftCooldownLedger().LastGiftDay("lord_1"));
    }

    [TestMethod]
    public void Stamp_RemembersTheDayPerHero_AndALaterGiftReplacesIt()
    {
        var ledger = new GiftCooldownLedger();

        ledger.Stamp("lord_1", 40);
        ledger.Stamp("lord_2", 55);
        ledger.Stamp("lord_1", 90);

        Assert.AreEqual(90, ledger.LastGiftDay("lord_1"));
        Assert.AreEqual(55, ledger.LastGiftDay("lord_2"));
        Assert.AreEqual(2, ledger.Count);
    }

    [DataTestMethod]
    [DataRow("", 5)]
    [DataRow(null, 5)]
    [DataRow("lord_1", -1)]
    public void Stamp_BlankIdOrNegativeDay_IsIgnored(string? heroId, int day)
    {
        var ledger = new GiftCooldownLedger();

        ledger.Stamp(heroId!, day);

        Assert.AreEqual(0, ledger.Count);
    }

    [TestMethod]
    public void EncodeThenDecode_RestoresEveryStamp()
    {
        var first = new GiftCooldownLedger();
        first.Stamp("lord_1", 40);
        first.Stamp("companion_9", 0);
        var second = new GiftCooldownLedger();

        var skipped = second.Decode(first.Encode());

        Assert.AreEqual(0, skipped);
        Assert.AreEqual(40, second.LastGiftDay("lord_1"));
        Assert.AreEqual(0, second.LastGiftDay("companion_9"));
        Assert.AreEqual(2, second.Count);
    }

    [TestMethod]
    public void Decode_ReplacesWhateverTheLedgerHeld()
    {
        var ledger = new GiftCooldownLedger();
        ledger.Stamp("old_campaign_lord", 77);

        ledger.Decode(new Dictionary<string, string> { ["v"] = "1", ["last|lord_1"] = "12" });

        Assert.IsNull(ledger.LastGiftDay("old_campaign_lord"));
        Assert.AreEqual(12, ledger.LastGiftDay("lord_1"));
    }

    [TestMethod]
    public void Decode_Null_LeavesAnEmptyLedger()
    {
        var ledger = new GiftCooldownLedger();
        ledger.Stamp("lord_1", 3);

        Assert.AreEqual(0, ledger.Decode(null));

        Assert.AreEqual(0, ledger.Count);
    }

    [TestMethod]
    public void Decode_DamagedEntries_AreRefusedAndCounted()
    {
        var ledger = new GiftCooldownLedger();
        var data = new Dictionary<string, string>
        {
            ["v"] = "1",
            ["last|good"] = "10",
            ["last|negative"] = "-3",
            ["last|text"] = "soon",
            ["last|"] = "4",
            ["mystery|x"] = "1",
            ["last|huge"] = "99999999999",
        };

        var skipped = ledger.Decode(data);

        Assert.AreEqual(5, skipped);
        Assert.AreEqual(1, ledger.Count);
        Assert.AreEqual(10, ledger.LastGiftDay("good"));
    }

    [TestMethod]
    public void Encode_CarriesAVersionKey()
    {
        Assert.AreEqual("1", new GiftCooldownLedger().Encode()["v"]);
    }
}
