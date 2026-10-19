using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.VoiceAccents;

namespace TAOM.Tests.Features.VoiceAccents;

/// <summary>The decision matrix behind TaomVoiceOverModel (docs/features/voice-accents.md).</summary>
[TestClass]
public class VoiceAccentServiceTests
{
    private const string Vanilla = "vanilla.ogg";
    private const int Human = 0;
    private const int Nazghul = 1;
    private const int CaveTroll = 2;
    private const int Saruman = 3;
    private const int Orc = 4;

    private IModLogger _logger = null!;
    private VoiceAccentService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        var cultures = new Dictionary<string, string>
        {
            ["gondor"] = "imperial",
            ["mordor"] = "silent",
            ["isengard"] = "silent",
            ["wild_trolls"] = "silent",
            ["umbar"] = "aserai",
            ["dunland_raiders"] = "looters",
        };
        var races = new Dictionary<string, string>
        {
            ["nazghul"] = "silent",
            ["cave_troll"] = "silent",
            ["saruman"] = "imperial",
            ["orc"] = "khuzait",
        };
        var names = new Dictionary<int, string>
        {
            [Human] = "human", [Nazghul] = "nazghul", [CaveTroll] = "Cave_Troll", [Saruman] = "saruman", [Orc] = "orc",
        };
        var provider = Substitute.For<IVoiceAccentsConfigProvider>();
        provider.GetMap().Returns(new VoiceAccentMap(cultures, races));
        var raceManager = Substitute.For<IRaceManager>();
        raceManager.IsValidRaceId(Arg.Any<int>()).Returns(ci => names.ContainsKey(ci.Arg<int>()));
        raceManager.GetRaceNameFromId(Arg.Any<int>()).Returns(ci => names.TryGetValue(ci.Arg<int>(), out var n) ? n : "human");
        _logger = Substitute.For<IModLogger>();
        _sut = new VoiceAccentService(provider, raceManager, _logger);
    }

    // True when SoundPath short-circuits to "" without asking the vanilla model.
    private bool Silent(string? culture, int race)
        => _sut.SoundPath(culture, race, () => Vanilla) == string.Empty;

    [TestMethod]
    public void SoundPath_SilentRaceWithAccentCulture_Silent()
        => Assert.IsTrue(Silent("gondor", Nazghul));

    [TestMethod]
    public void SoundPath_SilentRaceNameIsCaseInsensitive_Silent()
        => Assert.IsTrue(Silent("gondor", CaveTroll));

    [TestMethod]
    public void SoundPath_SilentCulture_Silent()
        => Assert.IsTrue(Silent("wild_trolls", Human));

    [TestMethod]
    public void SoundPath_AccentRaceWithSilentCulture_NotSilent()
        => Assert.IsFalse(Silent("isengard", Saruman));

    [TestMethod]
    public void SoundPath_AccentRaceWithUnmappedTaomCulture_NotSilentAndNotLogged()
    {
        Assert.IsFalse(Silent("new_culture", Saruman));

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SoundPath_AccentCultureAndOrdinaryRace_NotSilent()
        => Assert.IsFalse(Silent("gondor", Human));

    [TestMethod]
    public void SoundPath_UnmappedVanillaId_NotSilentAndNotLogged()
    {
        Assert.IsFalse(Silent("vlandia", Human));
        Assert.IsFalse(Silent("looters", -1));

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SoundPath_UnmappedTaomId_SilentAndLoggedOnce()
    {
        Assert.IsTrue(Silent("new_culture", Human));
        Assert.IsTrue(Silent("NEW_CULTURE", Human));

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("new_culture")));
    }

    [TestMethod]
    public void SoundPath_NullOrEmptyCulture_NotSilentAndNotLogged()
    {
        Assert.IsFalse(Silent(null, Human));
        Assert.IsFalse(Silent("", -1));

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void SoundPath_NegativeRaceId_CultureRowDecides()
    {
        Assert.IsTrue(Silent("mordor", -1));
        Assert.IsFalse(Silent("gondor", -1));
    }

    [TestMethod]
    public void SoundPath_OutOfRangeRaceId_CultureRowDecidesNotTheHumanFallback()
    {
        Assert.IsTrue(Silent("mordor", 99));
        Assert.IsFalse(Silent("gondor", 99));
    }

    [TestMethod]
    public void SoundPath_SilentRace_NeverCallsVanilla()
    {
        var called = false;

        var path = _sut.SoundPath("gondor", Nazghul, () => { called = true; return Vanilla; });

        Assert.AreEqual(string.Empty, path);
        Assert.IsFalse(called);
    }

    [TestMethod]
    public void SoundPath_NotSilent_ReturnsVanillaResult()
        => Assert.AreEqual(Vanilla, _sut.SoundPath("gondor", Human, () => Vanilla));

    [TestMethod]
    public void ResolveAccent_Imperial_SplitsByRegister()
    {
        Assert.AreEqual("imperial_high", _sut.ResolveAccent("gondor", true));
        Assert.AreEqual("imperial_low", _sut.ResolveAccent("gondor", false));
    }

    [TestMethod]
    public void ResolveAccent_PlainAccent_ReturnedAsIs()
        => Assert.AreEqual("aserai", _sut.ResolveAccent("umbar", true));

    [TestMethod]
    public void ResolveAccent_SilentCultureRow_PassesThroughAsTheSilentClass()
        => Assert.AreEqual("silent", _sut.ResolveAccent("wild_trolls", false));

    [TestMethod]
    public void ResolveAccent_UnmappedOrNullCulture_NullSoBaseDecides()
    {
        Assert.IsNull(_sut.ResolveAccent("vlandia", false));
        Assert.IsNull(_sut.ResolveAccent("new_culture", false));
        Assert.IsNull(_sut.ResolveAccent(null, false));
    }

    [TestMethod]
    public void ResolveAccent_RaceAccentRowBeatsCultureRowInsideSoundPath()
    {
        string? inside = null;

        _sut.SoundPath("isengard", Saruman, () => { inside = _sut.ResolveAccent("isengard", true); return Vanilla; });

        Assert.AreEqual("imperial_high", inside);
    }

    [TestMethod]
    public void ResolveAccent_OutsideSoundPath_UsesCultureRow()
        => Assert.AreEqual("looters", _sut.ResolveAccent("dunland_raiders", true));

    [TestMethod]
    public void ResolveAccent_RaceWithoutRowInsideSoundPath_UsesCultureRow()
    {
        string? inside = null;

        _sut.SoundPath("dunland_raiders", Human, () => { inside = _sut.ResolveAccent("dunland_raiders", true); return Vanilla; });

        Assert.AreEqual("looters", inside);
    }

    [TestMethod]
    public void SoundPath_Nested_RestoresTheOuterRace()
    {
        string? afterInner = null;

        _sut.SoundPath("gondor", Saruman, () =>
        {
            _sut.SoundPath("gondor", Orc, () => Vanilla);
            afterInner = _sut.ResolveAccent("gondor", false);
            return Vanilla;
        });

        Assert.AreEqual("imperial_low", afterInner);
    }

    [TestMethod]
    public void SoundPath_DelegateThrows_ClearsTheRace()
    {
        Assert.ThrowsException<InvalidOperationException>(
            () => _sut.SoundPath("dunland_raiders", Saruman, () => throw new InvalidOperationException("boom")));

        Assert.AreEqual("looters", _sut.ResolveAccent("dunland_raiders", true));
    }
}
