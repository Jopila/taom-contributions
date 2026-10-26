using System;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.BanditVoices;
using TAOM.Features.BanditVoices.Hooks;
using TAOM.Features.CoopInterop;
using TAOM.Features.InitialChildGeneration;
using TAOM.Features.Messengers;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.BanditVoices;

/// <summary>
/// The postfix body and its registration, with no campaign (#900): it does nothing when vanilla's condition said no, reaches the
/// service when it said yes, and a fault anywhere leaves vanilla's text and warns once. The SubModule.xml node is pinned
/// because a file registered for the wrong game types loads as "ERROR: Text with id" rows.
/// </summary>
[TestClass]
public class BanditVoicesPatchTests
{
    private IModLogger _logger = null!;
    private IBanditVoicesSettingsProvider _settings = null!;
    private IBanditEncounterTextAdapter _text = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IBanditVoicesSettingsProvider>();
        _settings.Enabled.Returns(true);
        _text = Substitute.For<IBanditEncounterTextAdapter>();
        var service = new BanditVoicesService(
            _settings, Substitute.For<IRandomSource>(), Substitute.For<IDedicatedServerProvider>(),
            Substitute.For<IMessengerConversationState>(), _text);
        BanditGreetingPatch.Initialize(service, _logger);
    }

    [TestMethod]
    public void Postfix_WhenVanillasConditionIsFalse_NeverReadsTheServiceOrTheAdapter()
    {
        BanditGreetingPatch.Postfix(false);

        Assert.AreEqual(0, _text.ReceivedCalls().Count());
        Assert.AreEqual(0, _settings.ReceivedCalls().Count());
    }

    [TestMethod]
    public void Postfix_WhenVanillasConditionIsTrue_ReadsTheService()
    {
        BanditGreetingPatch.Postfix(true);

        _ = _settings.Received().Enabled;
        _text.Received().IsFirstMeeting();
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void Postfix_WhenTheServiceThrows_SwallowsIt_AndWarnsOnceOverTwoCalls()
    {
        _settings.Enabled.Throws(new InvalidOperationException("boom"));

        BanditGreetingPatch.Postfix(true);
        BanditGreetingPatch.Postfix(true);

        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void Postfix_WhenTheAdapterThrows_SwallowsIt_AndWarnsOnceOverTwoCalls()
    {
        _text.IsFirstMeeting().Throws(new InvalidOperationException("boom"));

        BanditGreetingPatch.Postfix(true);
        BanditGreetingPatch.Postfix(true);

        _logger.ReceivedWithAnyArgs(1).LogWarning(default!);
    }

    [TestMethod]
    public void TheStringsFile_IsRegisteredAsGameText_ForCampaignAndCampaignStoryModeOnly()
    {
        var subModule = XDocument.Load(RepoPaths.RepoPath("Main", "_Module", "SubModule.xml"));
        var node = subModule.Descendants("XmlNode")
            .Single(n => (string?)n.Element("XmlName")?.Attribute("path") == "taom_bandit_voice_strings");

        Assert.AreEqual("GameText", (string?)node.Element("XmlName")?.Attribute("id"));
        var types = node.Element("IncludedGameTypes")!.Elements("GameType").Select(g => ((string?)g.Attribute("value") ?? "").Trim()).ToList();
        CollectionAssert.AreEquivalent(new[] { "Campaign", "CampaignStoryMode" }, types);
    }
}
