using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Core.Persistence;
using TAOM.Features.HiddenHosts;

namespace TAOM.Tests.Features.HiddenHosts;

/// <summary>
/// #846 phase 3: the save text of the memory. A save is foreign input after a mod downgrade, an edit or a truncation, so
/// every field has an invalid case, the header is a version tag, and the escaping round trips every reserved character.
/// The size rules are checked against the engine's per-entry limit through <see cref="SyncDataChunker"/>.
/// </summary>
[TestClass]
public class HiddenHostsMemoryCodecTests
{
    private static MemoryRow Row(int count = 150, int known = 2, int stamp = 1000, int fade = -1, int xpStage = 0, int xpDay = -1) =>
        new() { Count = count, Known = known, StampHour = stamp, FadeHour = fade, XpStage = xpStage, XpDay = xpDay };

    private static string Payload(params string[] rows) => "HH1\n" + string.Join("\n", rows);

    private static HiddenHostsMemory DecodeInto(string payload, out MemoryDecodeReport report)
    {
        var memory = new HiddenHostsMemory();
        report = HiddenHostsMemoryCodec.Decode(payload, memory);
        return memory;
    }

    // ---- Encode

    [TestMethod]
    public void Encode_EmptyMemory_IsTheHeaderAlone() =>
        Assert.AreEqual("HH1", HiddenHostsMemoryCodec.Encode(new HiddenHostsMemory()));

    [TestMethod]
    public void Encode_WritesOneLinePerRow_PartiesBeforeSettlements()
    {
        var memory = new HiddenHostsMemory();
        memory.Put(MemoryKind.Settlement, "town_1", Row(300, 3, 50, 56, 0, -1));
        memory.Put(MemoryKind.Party, "lord_1", Row(150, 2, 1000, -1, 2, 41));

        var text = HiddenHostsMemoryCodec.Encode(memory);

        Assert.AreEqual("HH1\np|lord_1|150|2|1000|-1|2|41\ns|town_1|300|3|50|56|0|-1", text);
    }

    [TestMethod]
    public void Encode_SkipsARowThatWouldNotDecode()
    {
        var memory = new HiddenHostsMemory();
        memory.Put(MemoryKind.Party, "bad_known", Row(count: 5, known: 4));
        memory.Put(MemoryKind.Party, "bad_count", Row(count: -1, known: 0));
        memory.Put(MemoryKind.Party, "bad_stamp", Row(stamp: -2));
        memory.Put(MemoryKind.Party, "good", Row());

        var text = HiddenHostsMemoryCodec.Encode(memory);

        Assert.AreEqual("HH1\np|good|150|2|1000|-1|0|-1", text);
    }

    // ---- Round trip

    [TestMethod]
    public void RoundTrip_RestoresEveryField_OfBothTables()
    {
        var memory = new HiddenHostsMemory();
        memory.Put(MemoryKind.Party, "lord_1", Row(150, 2, 1000, 1006, 3, 41));
        memory.Put(MemoryKind.Party, "lord_2", Row(0, 0, 0, -1, 0, -1));
        memory.Put(MemoryKind.Settlement, "town_1", Row(1_000_000, 7, 2_000_000, 2_000_010, 1, 0));

        var back = DecodeInto(HiddenHostsMemoryCodec.Encode(memory), out var report);

        Assert.AreEqual(3, report.Loaded);
        Assert.AreEqual(0, report.Invalid);
        Assert.IsFalse(report.UnknownFormat);
        Assert.IsFalse(report.CapReached);
        foreach (var kind in new[] { MemoryKind.Party, MemoryKind.Settlement })
            foreach (var pair in memory.Rows(kind))
            {
                Assert.IsTrue(back.TryGet(kind, pair.Key, out var row), pair.Key);
                Assert.AreEqual(pair.Value.Count, row.Count);
                Assert.AreEqual(pair.Value.Known, row.Known);
                Assert.AreEqual(pair.Value.StampHour, row.StampHour);
                Assert.AreEqual(pair.Value.FadeHour, row.FadeHour);
                Assert.AreEqual(pair.Value.XpStage, row.XpStage);
                Assert.AreEqual(pair.Value.XpDay, row.XpDay);
            }
    }

    // ---- Escaping

    [DataTestMethod]
    [DataRow("lord_1", "lord_1")]
    [DataRow("a|b", "a%7Cb")]
    [DataRow("a%b", "a%25b")]
    [DataRow("a\nb", "a%0Ab")]
    [DataRow("a\rb", "a%0Db")]
    [DataRow("%|\n\r", "%25%7C%0A%0D")]
    [DataRow("%7C", "%257C")]
    public void EscapeId_EscapesExactlyTheFourReservedCharacters(string id, string escaped)
    {
        Assert.AreEqual(escaped, HiddenHostsMemoryCodec.EscapeId(id));
        Assert.IsTrue(HiddenHostsMemoryCodec.TryUnescapeId(escaped, out var back));
        Assert.AreEqual(id, back);
    }

    [TestMethod]
    public void RoundTrip_AnIdHoldingEveryReservedCharacter_AndNonAscii()
    {
        var memory = new HiddenHostsMemory();
        var id = "a|b%c\nd\re%7Cfé漢😀";
        memory.Put(MemoryKind.Party, id, Row());

        var back = DecodeInto(HiddenHostsMemoryCodec.Encode(memory), out var report);

        Assert.AreEqual(1, report.Loaded);
        Assert.IsTrue(back.TryGet(MemoryKind.Party, id, out _));
    }

    [DataTestMethod]
    [DataRow("a%")]
    [DataRow("a%2")]
    [DataRow("a%2G")]
    [DataRow("a%41")]
    [DataRow("a%7c")]
    [DataRow("%%25")]
    public void TryUnescapeId_AnyOtherPercentSequence_IsInvalid(string text) =>
        Assert.IsFalse(HiddenHostsMemoryCodec.TryUnescapeId(text, out _));

    // ---- The header

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Decode_EmptyPayload_IsAnEmptyStoreWithNoWarning(string? payload)
    {
        var memory = DecodeInto(payload!, out var report);

        Assert.AreEqual(0, memory.Count(MemoryKind.Party));
        Assert.AreEqual(0, report.Loaded);
        Assert.AreEqual(0, report.Invalid);
        Assert.IsFalse(report.UnknownFormat);
    }

    [DataTestMethod]
    [DataRow("HH2\np|a|1|1|1|-1|0|-1")]
    [DataRow("hh1\np|a|1|1|1|-1|0|-1")]
    [DataRow("p|a|1|1|1|-1|0|-1")]
    [DataRow("garbage")]
    [DataRow("\nHH1\np|a|1|1|1|-1|0|-1")]
    [DataRow("HH1 \np|a|1|1|1|-1|0|-1")]
    [DataRow("{\"json\":true}")]
    public void Decode_MissingOrForeignHeader_LoadsNothing_AndSaysTheFormatIsUnknown(string payload)
    {
        var memory = DecodeInto(payload, out var report);

        Assert.AreEqual(0, memory.Count(MemoryKind.Party));
        Assert.AreEqual(0, report.Loaded);
        Assert.IsTrue(report.UnknownFormat);
    }

    [TestMethod]
    public void Decode_HeaderAlone_IsEmptyAndFine()
    {
        var memory = DecodeInto("HH1", out var report);

        Assert.AreEqual(0, memory.Count(MemoryKind.Party));
        Assert.IsFalse(report.UnknownFormat);
        Assert.AreEqual(0, report.Invalid);
    }

    [TestMethod]
    public void Decode_BlankLines_AreIgnored()
    {
        var memory = DecodeInto("HH1\n\np|a|10|1|5|-1|0|-1\n\n", out var report);

        Assert.AreEqual(1, memory.Count(MemoryKind.Party));
        Assert.AreEqual(0, report.Invalid);
    }

    // ---- Invalid rows

    [DataTestMethod]
    [DataRow("p|a|x|1|5|-1|0|-1", "count not numeric")]
    [DataRow("p|a|+10|1|5|-1|0|-1", "count with a sign")]
    [DataRow("p|a|-10|1|5|-1|0|-1", "negative count")]
    [DataRow("p|a| 10|1|5|-1|0|-1", "count with a space")]
    [DataRow("p|a|10|1|5|-1|0|-2", "xpDay below -1")]
    [DataRow("p|a|10|1|5|-2|0|-1", "fadeHour below -1")]
    [DataRow("p|a|10|1|-1|-1|0|-1", "negative stamp")]
    [DataRow("p|a|10|-1|5|-1|0|-1", "negative known")]
    [DataRow("p|a|10|3|5|-1|0|-1", "known past the digits")]
    [DataRow("p|a|10|1|5|-1|4|-1", "xp stage above 3")]
    [DataRow("p|a|10|1|5|-1|-1|-1", "negative xp stage")]
    [DataRow("p|a|1000001|1|5|-1|0|-1", "count past the cap")]
    [DataRow("p|a|99999999999|1|5|-1|0|-1", "count past int")]
    [DataRow("q|a|10|1|5|-1|0|-1", "unknown kind")]
    [DataRow("P|a|10|1|5|-1|0|-1", "kind in capitals")]
    [DataRow("p||10|1|5|-1|0|-1", "empty id")]
    [DataRow("p|a%2|10|1|5|-1|0|-1", "bad escape")]
    [DataRow("p|a|10|1|5|-1|0", "seven fields")]
    [DataRow("p|a|10|1|5|-1|0|-1|9", "nine fields")]
    [DataRow("p|a|10|1|5||0|-1", "empty field")]
    public void Decode_AnInvalidRow_IsSkipped_AndCounted(string row, string why)
    {
        var memory = DecodeInto(Payload(row, "p|good|10|1|5|-1|0|-1"), out var report);

        Assert.AreEqual(1, report.Loaded, why);
        Assert.AreEqual(1, report.Invalid, why);
        Assert.IsTrue(memory.TryGet(MemoryKind.Party, "good", out _), why);
        Assert.AreEqual(1, memory.Count(MemoryKind.Party), why);
    }

    [TestMethod]
    public void Decode_AnIdOverTheLengthLimit_IsInvalid_AndTheLimitItselfIsFine()
    {
        string atLimit = new string('a', HiddenHostsMemoryCodec.MaxIdLength);
        string over = new string('b', HiddenHostsMemoryCodec.MaxIdLength + 1);

        var memory = DecodeInto(Payload($"p|{atLimit}|10|1|5|-1|0|-1", $"p|{over}|10|1|5|-1|0|-1"), out var report);

        Assert.AreEqual(1, report.Loaded);
        Assert.AreEqual(1, report.Invalid);
        Assert.IsTrue(memory.TryGet(MemoryKind.Party, atLimit, out _));
    }

    [TestMethod]
    public void Decode_TheLimits_AreAccepted()
    {
        var memory = DecodeInto(Payload("p|a|1000000|7|2147483647|2147483647|3|2147483647", "p|b|0|1|0|-1|0|-1"), out var report);

        Assert.AreEqual(2, report.Loaded);
        Assert.AreEqual(0, report.Invalid);
        Assert.AreEqual(2147483647, Row2(memory, "a").StampHour);
        Assert.AreEqual(1, Row2(memory, "b").Known, "a zero count has one digit");
    }

    private static MemoryRow Row2(HiddenHostsMemory memory, string id)
    {
        Assert.IsTrue(memory.TryGet(MemoryKind.Party, id, out var row));
        return row;
    }

    [TestMethod]
    public void Decode_ADuplicateId_KeepsTheLast_AndSameIdInTheOtherTableIsSeparate()
    {
        var memory = DecodeInto(Payload("p|a|10|1|5|-1|0|-1", "p|a|20|2|6|-1|0|-1", "s|a|30|2|7|-1|0|-1"), out var report);

        Assert.AreEqual(20, Row2(memory, "a").Count);
        Assert.IsTrue(memory.TryGet(MemoryKind.Settlement, "a", out var town));
        Assert.AreEqual(30, town.Count);
        Assert.AreEqual(0, report.Invalid);
    }

    [TestMethod]
    public void Decode_StopsAtTheCap_WithOneWarning()
    {
        var rows = Enumerable.Range(0, HiddenHostsMemory.MaxParties + 50).Select(i => $"p|id{i}|10|1|5|-1|0|-1").ToArray();

        var memory = DecodeInto(Payload(rows), out var report);

        Assert.AreEqual(HiddenHostsMemory.MaxParties, memory.Count(MemoryKind.Party));
        Assert.AreEqual(HiddenHostsMemory.MaxParties, report.Loaded);
        Assert.IsTrue(report.CapReached);
    }

    [TestMethod]
    public void Decode_IntoAStoreAlreadyFull_StopsAtOnce()
    {
        var memory = new HiddenHostsMemory();
        for (int i = 0; i < HiddenHostsMemory.MaxSettlements; i++) memory.Put(MemoryKind.Settlement, "s" + i, Row());

        var report = HiddenHostsMemoryCodec.Decode(Payload("s|extra|10|1|5|-1|0|-1", "p|party|10|1|5|-1|0|-1"), memory);

        Assert.IsTrue(report.CapReached);
        Assert.IsFalse(memory.TryGet(MemoryKind.Party, "party", out _), "decoding stopped at the cap");
    }

    // ---- Size against the engine's per-entry limit

    [TestMethod]
    public void Payload_OverOneChunk_RoundTripsThroughSplitAndJoin()
    {
        var memory = new HiddenHostsMemory();
        for (int i = 0; i < 600; i++) memory.Put(MemoryKind.Party, "party_with_a_long_id_" + i, Row(100 + i, 2, 1000 + i, 1006 + i, 1, 41));
        var text = HiddenHostsMemoryCodec.Encode(memory);
        Assert.IsTrue(text.Length > SyncDataChunker.MaxChunkChars, "the payload must span several chunks: " + text.Length);

        var chunks = SyncDataChunker.Split(text);
        var back = DecodeInto(SyncDataChunker.Join(chunks), out var report);

        Assert.IsTrue(chunks.Count > 1);
        Assert.AreEqual(600, report.Loaded);
        Assert.AreEqual(600, back.Count(MemoryKind.Party));
    }

    [TestMethod]
    public void Payload_OfLongNonAsciiIds_KeepsEveryChunkUnderTheEngineEntryLimit()
    {
        var memory = new HiddenHostsMemory();
        for (int i = 0; i < 300; i++)
            memory.Put(MemoryKind.Party, new string('漢', 250) + i, Row());

        var chunks = SyncDataChunker.Split(HiddenHostsMemoryCodec.Encode(memory));

        Assert.IsTrue(chunks.Count > 5);
        foreach (var chunk in chunks)
            Assert.IsTrue(SyncDataChunker.Utf8ByteLength(chunk) <= SyncDataChunker.EngineEntryByteLimit,
                "a chunk of " + SyncDataChunker.Utf8ByteLength(chunk) + " bytes would corrupt the save");
    }

    [TestMethod]
    public void Payload_TheWorstCaseTables_StayUnder400KiloBytes()
    {
        var memory = new HiddenHostsMemory();
        for (int i = 0; i < HiddenHostsMemory.MaxParties; i++)
            memory.Put(MemoryKind.Party, "TroopsParty_with_a_realistic_id_" + i, Row(1_000_000, 7, 2_184_576, 2_184_580, 3, 91_024));
        for (int i = 0; i < HiddenHostsMemory.MaxSettlements; i++)
            memory.Put(MemoryKind.Settlement, "town_EW6_" + i, Row(1_000_000, 7, 2_184_576, 2_184_580, 3, 91_024));

        var text = HiddenHostsMemoryCodec.Encode(memory);

        Assert.IsTrue(System.Text.Encoding.UTF8.GetByteCount(text) < 400 * 1024, text.Length + " chars");
    }

    // ---- IsValid

    [TestMethod]
    public void IsValid_ChecksEveryFieldRange()
    {
        Assert.IsTrue(HiddenHostsMemoryCodec.IsValid("a", Row()));
        Assert.IsFalse(HiddenHostsMemoryCodec.IsValid("", Row()));
        Assert.IsFalse(HiddenHostsMemoryCodec.IsValid(null!, Row()));
        Assert.IsFalse(HiddenHostsMemoryCodec.IsValid(new string('a', 257), Row()));
        Assert.IsFalse(HiddenHostsMemoryCodec.IsValid("a", Row(count: 1_000_001, known: 1)));
        Assert.IsFalse(HiddenHostsMemoryCodec.IsValid("a", Row(known: -1)));
        Assert.IsFalse(HiddenHostsMemoryCodec.IsValid("a", Row(known: 4)));
        Assert.IsFalse(HiddenHostsMemoryCodec.IsValid("a", Row(stamp: -1)));
        Assert.IsFalse(HiddenHostsMemoryCodec.IsValid("a", Row(fade: -2)));
        Assert.IsFalse(HiddenHostsMemoryCodec.IsValid("a", Row(xpStage: 4)));
        Assert.IsFalse(HiddenHostsMemoryCodec.IsValid("a", Row(xpDay: -2)));
    }
}
