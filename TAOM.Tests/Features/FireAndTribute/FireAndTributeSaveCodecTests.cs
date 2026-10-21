using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute;

namespace TAOM.Tests.Features.FireAndTribute;

[TestClass]
public class FireAndTributeSaveCodecTests
{
    internal static FireAndTributeState Seed()
    {
        var state = new FireAndTributeState { WarSeenDay = 44 };
        state.CooldownUntil[FactionSide.Free] = 300;
        state.CooldownUntil[FactionSide.Evil] = 210;
        state.Hosts.Add(new RaidHostRecord
        {
            PartyId = "bandit_party_7", HostId = "gundabad", Side = FactionSide.Evil, TargetKingdomId = "empire_w",
            StartDay = 91, Raids = 3, Quota = 9, Returning = false,
        });
        state.Hosts.Add(new RaidHostRecord
        {
            PartyId = "bandit_party_9", HostId = "umbar", Side = FactionSide.Neutral, TargetKingdomId = "erebor",
            StartDay = 95, Raids = 11, Quota = 11, Returning = true,
        });
        return state;
    }

    private static string Root(string hosts = "[]", string extra = "") =>
        "{\"v\":1," + extra + "\"hosts\":" + hosts + "}";

    private const string GoodRow = "{\"p\":\"p1\",\"h\":\"gundabad\",\"s\":\"Evil\",\"t\":\"empire_w\",\"d\":5,\"r\":1,\"q\":6,\"ret\":false}";

    [TestMethod]
    public void RoundTrip_KeepsEveryField()
    {
        var parsed = FireAndTributeSaveCodec.Parse(FireAndTributeSaveCodec.Serialize(Seed()));

        Assert.AreEqual(0, parsed.Warnings.Count);
        Assert.AreEqual(44, parsed.WarSeenDay);
        Assert.AreEqual(300, parsed.CooldownUntil[FactionSide.Free]);
        Assert.AreEqual(210, parsed.CooldownUntil[FactionSide.Evil]);
        Assert.IsFalse(parsed.CooldownUntil.ContainsKey(FactionSide.Neutral));
        Assert.AreEqual(2, parsed.Hosts.Count);
        var a = parsed.Hosts[0];
        Assert.AreEqual("bandit_party_7", a.PartyId);
        Assert.AreEqual("gundabad", a.HostId);
        Assert.AreEqual(FactionSide.Evil, a.Side);
        Assert.AreEqual("empire_w", a.TargetKingdomId);
        Assert.AreEqual(91, a.StartDay);
        Assert.AreEqual(3, a.Raids);
        Assert.AreEqual(9, a.Quota);
        Assert.IsFalse(a.Returning);
        Assert.IsTrue(parsed.Hosts[1].Returning);
        Assert.AreEqual(FactionSide.Neutral, parsed.Hosts[1].Side);
    }

    [TestMethod]
    public void RoundTrip_AnEmptyRecord_IsEmpty()
    {
        var parsed = FireAndTributeSaveCodec.Parse(FireAndTributeSaveCodec.Serialize(new FireAndTributeState()));

        Assert.AreEqual(-1, parsed.WarSeenDay);
        Assert.AreEqual(0, parsed.Hosts.Count);
        Assert.AreEqual(0, parsed.CooldownUntil.Count);
        Assert.AreEqual(0, parsed.Warnings.Count);
    }

    [TestMethod]
    public void Serialize_IsStable_AndAFixedShape()
    {
        string a = FireAndTributeSaveCodec.Serialize(Seed());

        Assert.AreEqual(a, FireAndTributeSaveCodec.Serialize(Seed()));
        StringAssert.StartsWith(a, "{\"v\":1,\"seen\":44,");
        StringAssert.Contains(a, "\"cool\":{\"Free\":300,\"Evil\":210}");
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Parse_NullEmptyOrBlank_IsAnEmptyRecordWithoutAWarning(string? json)
    {
        var parsed = FireAndTributeSaveCodec.Parse(json);

        Assert.AreEqual(0, parsed.Hosts.Count);
        Assert.AreEqual(0, parsed.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("{ not json")]
    [DataRow("[1,2,3]")]
    [DataRow("42")]
    [DataRow("{\"hosts\":[]}")]
    [DataRow("{\"v\":2,\"hosts\":[]}")]
    [DataRow("{\"v\":\"1\",\"hosts\":[]}")]
    [DataRow("{\"v\":1.5,\"hosts\":[]}")]
    [DataRow("{\"v\":99999999999999999999999999,\"hosts\":[]}")]
    public void Parse_Garbage_ResetsEverythingWithAWarning_AndNeverThrows(string json)
    {
        var parsed = FireAndTributeSaveCodec.Parse(json);

        Assert.AreEqual(0, parsed.Hosts.Count);
        Assert.AreEqual(-1, parsed.WarSeenDay);
        Assert.AreEqual(1, parsed.Warnings.Count);
    }

    [DataTestMethod]
    [DataRow("\"seen\":\"x\",")]
    [DataRow("\"seen\":-5,")]
    [DataRow("\"seen\":1.5,")]
    [DataRow("\"seen\":99999999999,")]
    public void Parse_ABadSeenDay_ResetsThatFieldAlone(string extra)
    {
        var parsed = FireAndTributeSaveCodec.Parse(Root("[" + GoodRow + "]", extra));

        Assert.AreEqual(-1, parsed.WarSeenDay);
        Assert.AreEqual(1, parsed.Hosts.Count);
        Assert.AreEqual(1, parsed.Warnings.Count);
    }

    [TestMethod]
    public void Parse_CooldownRows_AreSkippedOneByOne()
    {
        var parsed = FireAndTributeSaveCodec.Parse(Root(extra: "\"cool\":{\"Free\":10,\"Good\":20,\"Evil\":-3,\"Neutral\":\"x\"},"));

        Assert.AreEqual(1, parsed.CooldownUntil.Count);
        Assert.AreEqual(10, parsed.CooldownUntil[FactionSide.Free]);
        Assert.AreEqual(3, parsed.Warnings.Count);
    }

    [TestMethod]
    public void Parse_ASideNameInAnyCase_IsASide_ButANumberOrAListIsNot()
    {
        var parsed = FireAndTributeSaveCodec.Parse(Root(extra: "\"cool\":{\"free\":10,\"1\":5,\"Evil,Free\":6,\" Neutral \":7},"));

        Assert.AreEqual(2, parsed.CooldownUntil.Count);
        Assert.AreEqual(10, parsed.CooldownUntil[FactionSide.Free]);
        Assert.AreEqual(7, parsed.CooldownUntil[FactionSide.Neutral]);
        Assert.AreEqual(2, parsed.Warnings.Count);
    }

    [TestMethod]
    public void Parse_AHostRowWithALowercaseSide_IsKept()
    {
        var parsed = FireAndTributeSaveCodec.Parse(Root("[" + GoodRow.Replace("\"s\":\"Evil\"", "\"s\":\"evil\"") + "]"));

        Assert.AreEqual(1, parsed.Hosts.Count);
        Assert.AreEqual(FactionSide.Evil, parsed.Hosts[0].Side);
        Assert.AreEqual(0, parsed.Warnings.Count);
    }

    [TestMethod]
    public void Parse_CooldownSectionNotAnObject_ResetsItAlone()
    {
        var parsed = FireAndTributeSaveCodec.Parse(Root("[" + GoodRow + "]", "\"cool\":[1],"));

        Assert.AreEqual(0, parsed.CooldownUntil.Count);
        Assert.AreEqual(1, parsed.Hosts.Count);
        Assert.AreEqual(1, parsed.Warnings.Count);
    }

    [TestMethod]
    public void Serialize_WritesNoGrudgeSection_AndAnOldOneIsIgnoredWithoutAWarning()
    {
        Assert.IsFalse(FireAndTributeSaveCodec.Serialize(Seed()).Contains("grudge"));

        var parsed = FireAndTributeSaveCodec.Parse(Root("[" + GoodRow + "]", "\"grudge\":{\"Evil\":\"empire_w\"},"));

        Assert.AreEqual(1, parsed.Hosts.Count);
        Assert.AreEqual(0, parsed.Warnings.Count);
    }

    [TestMethod]
    public void Parse_HostsSectionNotAnArray_ResetsItAlone()
    {
        var parsed = FireAndTributeSaveCodec.Parse("{\"v\":1,\"seen\":7,\"hosts\":{}}");

        Assert.AreEqual(0, parsed.Hosts.Count);
        Assert.AreEqual(7, parsed.WarSeenDay);
        Assert.AreEqual(1, parsed.Warnings.Count);
    }

    [TestMethod]
    public void Parse_AHostRowMissingTheOptionalRetFlag_IsNotReturning()
    {
        var parsed = FireAndTributeSaveCodec.Parse(Root("[" + GoodRow.Replace(",\"ret\":false", string.Empty) + "]"));

        Assert.AreEqual(1, parsed.Hosts.Count);
        Assert.IsFalse(parsed.Hosts[0].Returning);
    }

    [DataTestMethod]
    [DataRow("\"p\":\"p1\"", "\"p\":\"\"")]
    [DataRow("\"p\":\"p1\"", "\"p\":5")]
    [DataRow("\"p\":\"p1\"", "\"p\":\"bad id\"")]
    [DataRow("\"p\":\"p1\"", "\"p\":null")]
    [DataRow("\"h\":\"gundabad\"", "\"h\":\"\"")]
    [DataRow("\"t\":\"empire_w\"", "\"t\":\"\"")]
    [DataRow("\"t\":\"empire_w\"", "\"t\":3")]
    [DataRow("\"s\":\"Evil\"", "\"s\":\"1\"")]
    [DataRow("\"s\":\"Evil\"", "\"s\":2")]
    [DataRow("\"s\":\"Evil\"", "\"s\":\"Good\"")]
    [DataRow("\"d\":5", "\"d\":-1")]
    [DataRow("\"d\":5", "\"d\":\"5\"")]
    [DataRow("\"d\":5", "\"d\":5.5")]
    [DataRow("\"d\":5", "\"d\":99999999999")]
    [DataRow("\"r\":1", "\"r\":-1")]
    [DataRow("\"r\":1", "\"r\":100001")]
    [DataRow("\"r\":1", "\"r\":null")]
    [DataRow("\"q\":6", "\"q\":0")]
    [DataRow("\"q\":6", "\"q\":101")]
    [DataRow("\"q\":6", "\"q\":-2")]
    public void Parse_ABadHostRow_IsSkippedAlone(string original, string replacement)
    {
        string rows = "[" + GoodRow.Replace(original, replacement) + ",{\"p\":\"p2\",\"h\":\"umbar\",\"s\":\"Neutral\",\"t\":\"erebor\",\"d\":9,\"r\":0,\"q\":7}]";

        var parsed = FireAndTributeSaveCodec.Parse(Root(rows));

        Assert.AreEqual(1, parsed.Hosts.Count, "the good row survives");
        Assert.AreEqual("p2", parsed.Hosts[0].PartyId);
        Assert.AreEqual(1, parsed.Warnings.Count);
    }

    [TestMethod]
    public void Parse_AHostRowThatIsNotAnObject_IsSkipped()
    {
        var parsed = FireAndTributeSaveCodec.Parse(Root("[7, null, " + GoodRow + "]"));

        Assert.AreEqual(1, parsed.Hosts.Count);
        Assert.AreEqual(2, parsed.Warnings.Count);
    }

    [TestMethod]
    public void Parse_ADuplicatePartyId_IsSkipped()
    {
        var parsed = FireAndTributeSaveCodec.Parse(Root("[" + GoodRow + "," + GoodRow.Replace("Evil", "Free") + "]"));

        Assert.AreEqual(1, parsed.Hosts.Count);
        StringAssert.Contains(parsed.Warnings.Single(), "already listed");
    }

    [TestMethod]
    public void Parse_TwoHostsOfOneSide_KeepsTheFirst()
    {
        var parsed = FireAndTributeSaveCodec.Parse(Root("[" + GoodRow + "," + GoodRow.Replace("\"p1\"", "\"p2\"") + "]"));

        Assert.AreEqual(1, parsed.Hosts.Count);
        Assert.AreEqual("p1", parsed.Hosts[0].PartyId);
        StringAssert.Contains(parsed.Warnings.Single(), "already has a host");
    }

    [TestMethod]
    public void Parse_ADroppedHostRow_SaysItsPartyKeepsItsSavedPin()
    {
        var bad = FireAndTributeSaveCodec.Parse(Root("[" + GoodRow.Replace("\"d\":5", "\"d\":-1") + "]"));
        var twice = FireAndTributeSaveCodec.Parse(Root("[" + GoodRow + "," + GoodRow.Replace("\"p1\"", "\"p2\"") + "]"));
        var dup = FireAndTributeSaveCodec.Parse(Root("[" + GoodRow + "," + GoodRow.Replace("Evil", "Free") + "]"));

        foreach (var warning in new[] { bad.Warnings.Single(), twice.Warnings.Single(), dup.Warnings.Single() })
            StringAssert.Contains(warning, "keeps its saved pin");
    }

    [TestMethod]
    public void Parse_AnUnknownVersion_SaysItsHostPartiesKeepTheirSavedPin()
    {
        var parsed = FireAndTributeSaveCodec.Parse("{\"v\":2,\"hosts\":[]}");

        StringAssert.Contains(parsed.Warnings.Single(), "keeps its saved pin");
    }

    [TestMethod]
    public void Parse_DatesStayText_AnIdThatLooksLikeADateIsNotMangled()
    {
        var parsed = FireAndTributeSaveCodec.Parse(Root("[" + GoodRow.Replace("\"p1\"", "\"2026_10_20\"") + "]"));

        Assert.AreEqual("2026_10_20", parsed.Hosts.Single().PartyId);
    }
}
