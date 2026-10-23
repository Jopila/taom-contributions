using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.ServerSettings;
using TAOM.Tests.Features.LoadTimeStamps;

namespace TAOM.Tests.Features.ServerSettings;

/// <summary>One test per validation rule ("Config Providers MUST Validate"): each parseable-but-invalid value reverts.</summary>
[TestClass]
public class ServerSettingsReaderTests
{
    private ServerSettingsSet _set = null!;

    private TaomSettings Settings => _set.Get<TaomSettings>()!;

    [TestInitialize]
    public void Setup() => _set = new ServerSettingsSet();

    [TestCleanup]
    public void Cleanup() => ServerSettingsSource.ResetForTests();

    private ServerSettingsReport Read(string json) => ServerSettingsReader.Read(json, _set);

    private static string Joined(ServerSettingsReport r) => string.Join("\n", r.Lines.Select(l => l.Level + " " + l.Text));

    // Bool (ShowAllEncyclopediaCharacters, default true)

    [TestMethod]
    public void Bool_GivenANumber_Reverts()
    {
        var r = Read("{ \"ShowAllEncyclopediaCharacters\": 0 }");

        Assert.IsTrue(Settings.ShowAllEncyclopediaCharacters);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Bool_GivenAString_Reverts()
    {
        var r = Read("{ \"ShowAllEncyclopediaCharacters\": \"false\" }");

        Assert.IsTrue(Settings.ShowAllEncyclopediaCharacters);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Bool_GivenABool_IsApplied()
    {
        var r = Read("{ \"ShowAllEncyclopediaCharacters\": false }");

        Assert.IsFalse(Settings.ShowAllEncyclopediaCharacters);
        Assert.AreEqual(1, r.Applied);
    }

    // Int (CastleNotablesPerCastle, 1..5, default 3)

    [TestMethod]
    public void Int_GivenAFraction_Reverts()
    {
        var r = Read("{ \"CastleNotablesPerCastle\": 2.5 }");

        Assert.AreEqual(3, Settings.CastleNotablesPerCastle);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Int_AboveMax_Reverts()
    {
        var r = Read("{ \"CastleNotablesPerCastle\": 6 }");

        Assert.AreEqual(3, Settings.CastleNotablesPerCastle);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Int_BelowMin_Reverts()
    {
        var r = Read("{ \"CastleNotablesPerCastle\": 0 }");

        Assert.AreEqual(3, Settings.CastleNotablesPerCastle);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Int_BeyondInt32_Reverts()
    {
        var r = Read("{ \"CastleNotablesPerCastle\": 99999999999999999999 }");

        Assert.AreEqual(3, Settings.CastleNotablesPerCastle);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Int_InRange_IsApplied()
    {
        var r = Read("{ \"CastleNotablesPerCastle\": 5 }");

        Assert.AreEqual(5, Settings.CastleNotablesPerCastle);
        Assert.AreEqual(1, r.Applied);
    }

    // Float (AiLordPartySizeFactor, 1..20, default 1)

    [TestMethod]
    public void Float_NaNLiteral_Reverts()
    {
        var r = Read("{ \"AiLordPartySizeFactor\": NaN }");

        Assert.AreEqual(1.0f, Settings.AiLordPartySizeFactor);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Float_Infinity_Reverts()
    {
        var r = Read("{ \"AiLordPartySizeFactor\": Infinity }");

        Assert.AreEqual(1.0f, Settings.AiLordPartySizeFactor);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Float_OverflowingSingle_Reverts()
    {
        var r = Read("{ \"AiLordPartySizeFactor\": 1e300 }");

        Assert.AreEqual(1.0f, Settings.AiLordPartySizeFactor);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Float_OutOfRange_Reverts()
    {
        var r = Read("{ \"AiLordPartySizeFactor\": 20.5 }");

        Assert.AreEqual(1.0f, Settings.AiLordPartySizeFactor);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Float_IntegerToken_IsAccepted()
    {
        var r = Read("{ \"AiLordPartySizeFactor\": 4 }");

        Assert.AreEqual(4.0f, Settings.AiLordPartySizeFactor);
        Assert.AreEqual(1, r.Applied);
    }

    // Text (MixedFormationsCycleHotkey, default "L")

    [TestMethod]
    public void Text_OverLengthBound_Reverts()
    {
        var r = Read("{ \"MixedFormationsCycleHotkey\": \"" + new string('x', 257) + "\" }");

        Assert.AreEqual("L", Settings.MixedFormationsCycleHotkey);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Text_AtLengthBound_IsApplied()
    {
        var r = Read("{ \"MixedFormationsCycleHotkey\": \"" + new string('x', 256) + "\" }");

        Assert.AreEqual(256, Settings.MixedFormationsCycleHotkey.Length);
        Assert.AreEqual(1, r.Applied);
    }

    [TestMethod]
    public void Text_Null_Reverts()
    {
        var r = Read("{ \"MixedFormationsCycleHotkey\": null }");

        Assert.AreEqual("L", Settings.MixedFormationsCycleHotkey);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Text_Number_Reverts()
    {
        var r = Read("{ \"MixedFormationsCycleHotkey\": 7 }");

        Assert.AreEqual("L", Settings.MixedFormationsCycleHotkey);
        Assert.AreEqual(1, r.Reverted);
    }

    // Dropdown (AiPlayerClanPartyScaling, options Never / Taken-over lords only / Always)

    [TestMethod]
    public void Dropdown_UnknownOption_Reverts()
    {
        var before = Settings.AiPlayerClanPartyScaling.SelectedIndex;
        var r = Read("{ \"AiPlayerClanPartyScaling\": \"Sometimes\" }");

        Assert.AreEqual(before, Settings.AiPlayerClanPartyScaling.SelectedIndex);
        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Dropdown_KnownOption_IsSelected()
    {
        var r = Read("{ \"AiPlayerClanPartyScaling\": \"Always\" }");

        Assert.AreEqual(2, Settings.AiPlayerClanPartyScaling.SelectedIndex);
        Assert.AreEqual(1, r.Applied);
    }

    [TestMethod]
    public void Dropdown_WrongCaseOption_Reverts()
    {
        var r = Read("{ \"AiPlayerClanPartyScaling\": \"always\" }");

        Assert.AreEqual(1, r.Reverted);
    }

    [TestMethod]
    public void Dropdown_Index_Reverts()
    {
        var before = Settings.AiPlayerClanPartyScaling.SelectedIndex;
        var r = Read("{ \"AiPlayerClanPartyScaling\": 2 }");

        Assert.AreEqual(before, Settings.AiPlayerClanPartyScaling.SelectedIndex);
        Assert.AreEqual(1, r.Reverted);
    }

    // Button, unknown keys, summaries

    [TestMethod]
    public void Button_InFile_WarnsAndIsIgnored()
    {
        var r = Read("{ \"RebuildDistanceCacheAction\": true }");

        Assert.AreEqual(0, r.Applied);
        Assert.IsTrue(r.Lines.Any(l => l.Level == ServerSettingsLevel.Warning && l.Text.Contains("RebuildDistanceCacheAction")));
    }

    [TestMethod]
    public void Parked_InFile_WarnsAndIsIgnored()
    {
        var r = Read("{ \"EnableShaderPrecompileScenePasses\": true }");

        Assert.IsFalse(Settings.EnableShaderPrecompileScenePasses);
        Assert.AreEqual(0, r.Applied + r.Reverted + r.Unknown);
        Assert.IsTrue(r.Lines.Any(l => l.Level == ServerSettingsLevel.Warning && l.Text.Contains("EnableShaderPrecompileScenePasses")));
    }

    [TestMethod]
    public void UnknownKey_Warns()
    {
        var r = Read("{ \"NoSuchSetting\": 1 }");

        Assert.AreEqual(1, r.Unknown);
        Assert.IsTrue(r.Lines.Any(l => l.Level == ServerSettingsLevel.Warning && l.Text.Contains("NoSuchSetting")));
    }

    [TestMethod]
    public void WrongCaseKey_IsUnknown()
    {
        var r = Read("{ \"showAllEncyclopediaCharacters\": false }");

        Assert.AreEqual(1, r.Unknown);
        Assert.IsTrue(Settings.ShowAllEncyclopediaCharacters);
    }

    [TestMethod]
    public void AnyRevert_EmitsOneSummaryWithCount()
    {
        var r = Read("{ \"CastleNotablesPerCastle\": 99, \"AiLordPartySizeFactor\": 99 }");

        var summaries = r.Lines.Where(l => l.Level == ServerSettingsLevel.Warning && l.Text.Contains("2 value(s) reverted")).ToList();
        Assert.AreEqual(1, summaries.Count, Joined(r));
    }

    [TestMethod]
    public void NoRevert_EmitsNoSummary()
    {
        var r = Read("{ \"CastleNotablesPerCastle\": 4 }");

        Assert.IsFalse(r.Lines.Any(l => l.Text.Contains("reverted")), Joined(r));
    }

    // Structure

    [TestMethod]
    public void MalformedJson_AllDefaultsWithError()
    {
        var r = Read("{ \"CastleNotablesPerCastle\": ");

        Assert.AreEqual(ServerFileState.Malformed, r.State);
        Assert.AreEqual(3, Settings.CastleNotablesPerCastle);
        Assert.AreEqual(1, r.Lines.Count(l => l.Level == ServerSettingsLevel.Error));
    }

    [TestMethod]
    public void TopLevelArray_IsMalformed()
    {
        var r = Read("[1, 2]");

        Assert.AreEqual(ServerFileState.Malformed, r.State);
        Assert.AreEqual(1, r.Lines.Count(l => l.Level == ServerSettingsLevel.Error));
    }

    [TestMethod]
    public void DuplicateKey_IsMalformed()
    {
        var r = Read("{ \"CastleNotablesPerCastle\": 4, \"CastleNotablesPerCastle\": 2 }");

        Assert.AreEqual(ServerFileState.Malformed, r.State);
        Assert.AreEqual(3, Settings.CastleNotablesPerCastle);
    }

    [TestMethod]
    public void Comments_AreIgnored()
    {
        var r = Read("// header\n{ /* why */ \"CastleNotablesPerCastle\": 4 // trailing\n }");

        Assert.AreEqual(4, Settings.CastleNotablesPerCastle);
        Assert.AreEqual(ServerFileState.Loaded, r.State);
    }

    [TestMethod]
    public void EmptyFile_IsZeroKeys()
    {
        foreach (var text in new[] { "", "   \r\n\t" })
        {
            var r = ServerSettingsReader.Read(text, new ServerSettingsSet());

            Assert.AreEqual(ServerFileState.Empty, r.State);
            Assert.AreEqual(0, r.Applied);
            Assert.AreEqual(0, r.Lines.Count(l => l.Level == ServerSettingsLevel.Error));
        }
    }

    [TestMethod]
    public void ValidKey_IsApplied_OthersStayDefault()
    {
        var other = new ServerSettingsSet().Get<TaomSettings>()!;
        var r = Read("{ \"CastleNotablesPerCastle\": 5 }");

        Assert.AreEqual(5, Settings.CastleNotablesPerCastle);
        Assert.AreEqual(other.AiLordPartySizeFactor, Settings.AiLordPartySizeFactor);
        Assert.AreEqual(other.ShowAllEncyclopediaCharacters, Settings.ShowAllEncyclopediaCharacters);
        Assert.AreEqual(1, r.Applied);
    }

    [TestMethod]
    public void AppliedLine_NamesValueAndDefault()
    {
        var r = Read("{ \"CastleNotablesPerCastle\": 5 }");

        Assert.IsTrue(r.Lines.Any(l => l.Level == ServerSettingsLevel.Info
            && l.Text == "set CastleNotablesPerCastle = 5 (server default 3)"), Joined(r));
    }

    [TestMethod]
    public void WriteTo_SendsLinesToTheLogger_AtTheirLevels()
    {
        var r = Read("{ \"CastleNotablesPerCastle\": 5, \"NoSuchSetting\": 1 }");
        var log = new RecordingLogger();

        r.WriteTo(log);

        Assert.IsTrue(log.Lines.Any(l => l.StartsWith("INFO ") && l.Contains("[ServerSettings]") && l.Contains("set CastleNotablesPerCastle")));
        Assert.IsTrue(log.Lines.Any(l => l.StartsWith("WARN ") && l.Contains("NoSuchSetting")));
    }
}
