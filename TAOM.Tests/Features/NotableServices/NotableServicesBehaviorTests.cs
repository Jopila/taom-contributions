using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.NotableServices.Domain;
using TAOM.Features.NotableServices.Hooks;

namespace TAOM.Tests.Features.NotableServices;

/// <summary>
/// The dialog behavior is a thin boundary over the service. It derives from the engine's campaign behavior base, so
/// constructing it needs the engine assemblies (the same category as the Lord Courtesies behavior tests). The two conditions
/// that set a priced line's text variable are not run here: the wiring tests pin that the behavior sets the variables the lines
/// read, and <see cref="NotableServicesTextsTests"/> renders the texts they carry.
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class NotableServicesBehaviorTests
{
    private NotableServicesFixture _f = null!;
    private NotableServicesDialogBehavior _behavior = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new NotableServicesFixture();
        _behavior = new NotableServicesDialogBehavior(_f.Service, _f.Logger);
    }

    [TestMethod]
    public void TheGiftCondition_DelegatesToTheService()
    {
        Assert.IsTrue(_behavior.CanGift());

        _f.Speaker.AtWarWithPlayer = true;

        Assert.IsFalse(_behavior.CanGift());
    }

    [TestMethod]
    public void TheGiftConsequence_QueuesTheScreenThroughTheService()
    {
        _behavior.Gift();

        Assert.IsNotNull(_f.Queued);
    }

    [TestMethod]
    public void TheIntroduceConsequence_StoresTheAnswerForTheReplyLines()
    {
        _behavior.Introduce();

        Assert.IsTrue(_behavior.Introduced());

        _f.Adapter.PlayerGold.Returns(0);
        _behavior.Introduce();

        Assert.IsFalse(_behavior.Introduced(), "a failed request clears the last answer");
    }

    [TestMethod]
    public void TheRevealConsequence_StoresTheOutcomeForTheReplyLines()
    {
        _f.Speaker.AtWarWithPlayer = true;

        _behavior.Reveal();

        Assert.IsTrue(_behavior.Answered(RevealOutcome.AtWar));
        Assert.IsFalse(_behavior.Answered(RevealOutcome.Revealed));
    }

    [TestMethod]
    public void TheClickableConditions_GreyTheLinesWhenShort_WithAReason()
    {
        _f.Adapter.PlayerGold.Returns(1);

        Assert.IsFalse(_behavior.IntroduceClickable(out var introduceWhy));
        Assert.IsFalse(_behavior.RevealClickable(out var revealWhy));
        Assert.IsFalse(string.IsNullOrEmpty(introduceWhy.ToString()));
        Assert.IsFalse(string.IsNullOrEmpty(revealWhy.ToString()));
    }

    [TestMethod]
    public void TheClickableConditions_AreOpenWhenThePlayerCanPay()
    {
        Assert.IsTrue(_behavior.IntroduceClickable(out _));
        Assert.IsTrue(_behavior.RevealClickable(out _));
    }
}
