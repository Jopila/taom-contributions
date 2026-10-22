using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Library;
using TAOM.Core.Validation;
using TAOM.Features.Execution;
using TAOM.Features.FireAndTribute.Cheats;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FireAndTribute;

/// <summary>
/// #870: the console's pure argument parsers, validated before any lookup. <c>taom.spawn_raid_host</c> is tier C: the
/// bare command is a dry run and only the literal, ordinal <c>confirm</c> sends a host.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class FireAndTributeCheatsTests
{
    [DataTestMethod]
    [DataRow("free", FactionSide.Free)]
    [DataRow("EVIL", FactionSide.Evil)]
    [DataRow(" Neutral ", FactionSide.Neutral)]
    public void TryParseSpawn_TakesTheThreeSideNames_InAnyCase(string raw, FactionSide expected)
    {
        Assert.IsTrue(FireAndTributeCheats.TryParseSpawn(new List<string> { raw }, out var side, out bool confirmed, out _));
        Assert.AreEqual(expected, side);
        Assert.IsFalse(confirmed);
    }

    [DataTestMethod]
    [DataRow("good")]
    [DataRow("")]
    [DataRow("1")]
    [DataRow("0")]
    [DataRow("Free,Evil")]
    [DataRow("+1")]
    public void TryParseSpawn_RefusesAnythingButASideName_NeverANumberOrAList(string raw)
    {
        Assert.IsFalse(FireAndTributeCheats.TryParseSpawn(new List<string> { raw }, out _, out _, out string error), raw);
        StringAssert.Contains(error, "taom.spawn_raid_host");
    }

    [TestMethod]
    public void TryParseSpawn_ANullSideArgument_IsRefused()
    {
        Assert.IsFalse(FireAndTributeCheats.TryParseSpawn(new List<string> { null! }, out _, out _, out _));
    }

    [TestMethod]
    public void TheCheatsKeepNoPrivateSideParser_TheySharedEnumNamesTryParse()
    {
        var parsers = typeof(FireAndTributeCheats).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(m => m.Name.StartsWith("TryParseSide", System.StringComparison.Ordinal))
            .ToList();

        Assert.AreEqual(0, parsers.Count);
        var tryParse = typeof(FireAndTributeCheats).GetMethod(nameof(FireAndTributeCheats.TryParseSpawn), BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)!;
        var calls = IlCallScanner.ExtractCalledMethods(tryParse, tryParse.GetMethodBody()!.GetILAsByteArray());
        Assert.IsTrue(calls.Any(m => m.DeclaringType == typeof(EnumNames) && m.Name == nameof(EnumNames.TryParse)));
    }

    [TestMethod]
    public void TryParseSpawn_ASideAlone_IsADryRun()
    {
        Assert.IsTrue(FireAndTributeCheats.TryParseSpawn(new List<string> { "evil" }, out var side, out bool confirmed, out _));
        Assert.AreEqual(FactionSide.Evil, side);
        Assert.IsFalse(confirmed);
    }

    [TestMethod]
    public void TryParseSpawn_TheLiteralConfirm_Sends()
    {
        Assert.IsTrue(FireAndTributeCheats.TryParseSpawn(new List<string> { "free", "confirm" }, out var side, out bool confirmed, out string error));
        Assert.AreEqual(FactionSide.Free, side);
        Assert.IsTrue(confirmed);
        Assert.AreEqual(string.Empty, error);
    }

    [DataTestMethod]
    [DataRow("evil Confirm")]
    [DataRow("evil CONFIRM")]
    [DataRow("evil yes")]
    [DataRow("evil confirm extra")]
    [DataRow("confirm")]
    [DataRow("good confirm")]
    [DataRow("")]
    public void TryParseSpawn_AnythingElse_IsRefusedWithTheUsage(string raw)
    {
        var args = raw.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).ToList();

        Assert.IsFalse(FireAndTributeCheats.TryParseSpawn(args, out _, out bool confirmed, out string error), raw);
        Assert.IsFalse(confirmed, raw);
        StringAssert.Contains(error, "taom.spawn_raid_host");
    }

    [TestMethod]
    public void TryParseSpawn_NullArguments_AreRefused()
    {
        Assert.IsFalse(FireAndTributeCheats.TryParseSpawn(null!, out _, out _, out _));
    }

    [TestMethod]
    public void TryParseSetEnvoyDue_NoArguments_IsADryRun()
    {
        Assert.IsTrue(FireAndTributeCheats.TryParseSetEnvoyDue(new List<string>(), out bool confirmed, out string error));
        Assert.IsFalse(confirmed);
        Assert.AreEqual(string.Empty, error);
    }

    [TestMethod]
    public void TryParseSetEnvoyDue_TheLiteralConfirm_Sends()
    {
        Assert.IsTrue(FireAndTributeCheats.TryParseSetEnvoyDue(new List<string> { "confirm" }, out bool confirmed, out _));
        Assert.IsTrue(confirmed);
    }

    [DataTestMethod]
    [DataRow("Confirm")]
    [DataRow("CONFIRM")]
    [DataRow("yes")]
    [DataRow("1")]
    [DataRow("confirm extra")]
    [DataRow("evil")]
    public void TryParseSetEnvoyDue_AnythingElse_IsRefusedWithTheUsage(string raw)
    {
        var args = raw.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).ToList();

        Assert.IsFalse(FireAndTributeCheats.TryParseSetEnvoyDue(args, out bool confirmed, out string error), raw);
        Assert.IsFalse(confirmed, raw);
        StringAssert.Contains(error, "taom.set_envoy_due");
    }

    [TestMethod]
    public void TryParseSetEnvoyDue_NullArguments_AreRefused()
    {
        Assert.IsFalse(FireAndTributeCheats.TryParseSetEnvoyDue(null!, out _, out _));
    }

    [TestMethod]
    public void TheCommandsAreNamedAndShapedForTheEngineBinding()
    {
        var commands = typeof(FireAndTributeCheats).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.GetCustomAttributes(typeof(CommandLineFunctionality.CommandLineArgumentFunction), false).Length > 0)
            .ToList();

        CollectionAssert.AreEquivalent(new[] { "PrintFireAndTribute", "SpawnRaidHost", "SetEnvoyDue" }, commands.Select(m => m.Name).ToArray());
        foreach (var method in commands)
        {
            Assert.AreEqual(typeof(string), method.ReturnType, method.Name);
            CollectionAssert.AreEqual(new[] { typeof(List<string>) }, method.GetParameters().Select(p => p.ParameterType).ToArray(), method.Name);
        }
    }
}
