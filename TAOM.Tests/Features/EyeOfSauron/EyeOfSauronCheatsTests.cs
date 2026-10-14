using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EyeOfSauron.Cheats;
using TAOM.Features.Execution;

namespace TAOM.Tests.Features.EyeOfSauron;

/// <summary>
/// #765 M8: the console's pure argument parsers, validated before any lookup. <c>taom.start_eye_hunt</c> is tier C:
/// the bare command is a dry run and only the literal, ordinal <c>confirm</c> starts a hunt.
/// </summary>
[TestClass]
public class EyeOfSauronCheatsTests
{
    [TestMethod]
    public void TryParseConfirm_NoArgument_IsADryRun()
    {
        Assert.IsTrue(EyeOfSauronCheats.TryParseConfirm(new List<string>(), out bool confirmed));
        Assert.IsFalse(confirmed);
    }

    [TestMethod]
    public void TryParseConfirm_TheLiteralConfirm_Starts()
    {
        Assert.IsTrue(EyeOfSauronCheats.TryParseConfirm(new List<string> { "confirm" }, out bool confirmed));
        Assert.IsTrue(confirmed);
    }

    [DataTestMethod]
    [DataRow("Confirm")]
    [DataRow("CONFIRM")]
    [DataRow("yes")]
    [DataRow("confirm x")]
    public void TryParseConfirm_AnythingElse_IsRefused(string raw)
    {
        Assert.IsFalse(EyeOfSauronCheats.TryParseConfirm(new List<string>(raw.Split(' ')), out bool confirmed), raw);
        Assert.IsFalse(confirmed, raw);
    }

    [DataTestMethod]
    [DataRow("evil", FactionSide.Evil)]
    [DataRow("EVIL", FactionSide.Evil)]
    [DataRow(" free ", FactionSide.Free)]
    public void TryParseSide_AcceptsTheTwoSides(string raw, FactionSide expected)
    {
        Assert.IsTrue(EyeOfSauronCheats.TryParseSide(raw, out var side));
        Assert.AreEqual(expected, side);
    }

    [DataTestMethod]
    [DataRow("enemy")]
    [DataRow("")]
    [DataRow("neutral")]
    [DataRow(null)]
    public void TryParseSide_RefusesAnythingElse(string? raw)
    {
        Assert.IsFalse(EyeOfSauronCheats.TryParseSide(raw!, out _));
    }

    [DataTestMethod]
    [DataRow("1000", 1000f)]
    [DataRow("-1000", -1000f)]
    [DataRow("2.5", 2.5f)]
    public void TryParseScoreEdit_InBounds_Accepted(string amount, float expected)
    {
        Assert.IsTrue(EyeOfSauronCheats.TryParseScoreEdit(new List<string> { "evil", amount }, out var side, out float value, out _));
        Assert.AreEqual(FactionSide.Evil, side);
        Assert.AreEqual(expected, value);
    }

    [DataTestMethod]
    [DataRow("1000.5")]
    [DataRow("-1001")]
    [DataRow("NaN")]
    [DataRow("abc")]
    public void TryParseScoreEdit_OutOfBoundsOrNotAFiniteNumber_Refused(string amount)
    {
        Assert.IsFalse(EyeOfSauronCheats.TryParseScoreEdit(new List<string> { "free", amount }, out _, out _, out string error), amount);
        Assert.IsFalse(string.IsNullOrEmpty(error), amount);
    }

    [TestMethod]
    public void TryParseScoreEdit_WrongShape_Refused()
    {
        Assert.IsFalse(EyeOfSauronCheats.TryParseScoreEdit(new List<string> { "evil" }, out _, out _, out _));
        Assert.IsFalse(EyeOfSauronCheats.TryParseScoreEdit(new List<string> { "neutral", "5" }, out _, out _, out _));
        Assert.IsFalse(EyeOfSauronCheats.TryParseScoreEdit(new List<string> { "evil", "5", "6" }, out _, out _, out _));
    }
}
