using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Features.Relations;
using TAOM.Features.Relations.Hooks;

namespace TAOM.Tests.Features.Relations;

/// <summary>
/// The gate of the notice patch. The heroes are passed as null: the adapter is a substitute and never reads them, and the
/// decision is the only thing under test. True means vanilla's handler runs.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class PersonalRelationNoticeTests
{
    private IPersonalRelationService _service = null!;
    private IPersonalRelationNoticeAdapter _notice = null!;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<IPersonalRelationService>();
        _service.IsActive.Returns(true);
        _notice = Substitute.For<IPersonalRelationNoticeAdapter>();
        _notice.ShowNamedNotice(Arg.Any<Hero>(), Arg.Any<Hero>(), Arg.Any<int>()).Returns(true);
    }

    private bool Run(int change = 3) =>
        DefaultNotificationsCampaignBehavior_OnRelationChanged_Patch.Run(_service, _notice, null!, null!, change);

    [TestMethod]
    public void Run_FeatureOn_ShowsTheNamedNotice_AndSkipsVanilla()
    {
        Assert.IsFalse(Run());

        _notice.Received(1).ShowNamedNotice(Arg.Any<Hero>(), Arg.Any<Hero>(), 3);
    }

    [TestMethod]
    public void Run_FeatureOff_RunsVanilla_AndShowsNothing()
    {
        _service.IsActive.Returns(false);

        Assert.IsTrue(Run());

        _notice.DidNotReceiveWithAnyArgs().ShowNamedNotice(Arg.Any<Hero>(), Arg.Any<Hero>(), Arg.Any<int>());
    }

    [TestMethod]
    public void Run_AChangeVanillaWouldNotShow_RunsVanilla()
    {
        _notice.ShowNamedNotice(Arg.Any<Hero>(), Arg.Any<Hero>(), Arg.Any<int>()).Returns(false);

        Assert.IsTrue(Run());
    }

    [TestMethod]
    public void Run_ANegativeChange_IsPassedThroughSigned()
    {
        Run(-4);

        _notice.Received(1).ShowNamedNotice(Arg.Any<Hero>(), Arg.Any<Hero>(), -4);
    }

    [TestMethod]
    public void Prefix_VanillasOwnGateIsClosed_RunsVanillaWithoutTouchingTheContainer()
    {
        // showNotification false or a zero change: the original does nothing, and TAOM.IoC is not built in a test host.
        Assert.IsTrue(DefaultNotificationsCampaignBehavior_OnRelationChanged_Patch.Prefix(null!, null!, 5, false));
        Assert.IsTrue(DefaultNotificationsCampaignBehavior_OnRelationChanged_Patch.Prefix(null!, null!, 0, true));
    }

    [TestMethod]
    public void Prefix_TheContainerCannotBeResolved_RunsVanilla()
    {
        // TAOM.IoC is not configured in a test host, so the resolve throws; vanilla's handler must still run.
        Assert.IsTrue(DefaultNotificationsCampaignBehavior_OnRelationChanged_Patch.Prefix(null!, null!, 5, true));
    }
}
