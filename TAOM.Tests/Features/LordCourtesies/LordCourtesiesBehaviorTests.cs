using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.LordCourtesies.Domain;
using TAOM.Features.LordCourtesies.Hooks;

namespace TAOM.Tests.Features.LordCourtesies;

/// <summary>
/// The dialog behavior is a thin boundary over the service. It derives from the engine's campaign behavior base, so
/// constructing it needs the engine assemblies (the same category as the Hard Questions behavior tests).
/// </summary>
[TestCategory("RequiresGame")]
[TestClass]
public class LordCourtesiesBehaviorTests
{
    private LordCourtesiesFixture _f = null!;
    private LordCourtesiesDialogBehavior _behavior = null!;

    [TestInitialize]
    public void Setup()
    {
        _f = new LordCourtesiesFixture();
        _behavior = new LordCourtesiesDialogBehavior(_f.Service);
    }

    [TestMethod]
    public void Conditions_DelegateToTheService()
    {
        Assert.IsTrue(_behavior.CanGift());
        Assert.IsTrue(_behavior.CanAskDomain());

        _f.Lord.AtWarWithPlayer = true;

        Assert.IsFalse(_behavior.CanGift());
        Assert.IsFalse(_behavior.CanAskDomain());
    }

    [TestMethod]
    public void GiftConsequence_QueuesTheScreenThroughTheService()
    {
        _behavior.Gift();

        Assert.IsNotNull(_f.Queued);
    }

    [TestMethod]
    public void AskDomainConsequence_StoresTheServicesAnswerForTheReplyLines()
    {
        _f.Lord.Relation = 0;

        _behavior.AskDomain();

        Assert.IsTrue(_behavior.Answered(DomainOutcome.Refused));
        _f.Player.Received(1).ChangeRelationWith(LordCourtesiesFixture.LordId, -1);
    }

    [TestMethod]
    public void TheFourOutcomes_RouteToTheRightReply()
    {
        // Revealed
        _behavior.AskDomain();
        Assert.IsTrue(_behavior.Answered(DomainOutcome.Revealed));
        Assert.IsFalse(_behavior.AnsweredWithRefusal());

        // Landless
        _f.Lord.Holdings = 0;
        _f.Lord.Fortifications = 0;
        _behavior.AskDomain();
        Assert.IsTrue(_behavior.Answered(DomainOutcome.Landless));
        Assert.IsFalse(_behavior.AnsweredWithRefusal());

        // Refused
        _f.Lord.Relation = 0;
        _behavior.AskDomain();
        Assert.IsTrue(_behavior.Answered(DomainOutcome.Refused));
        Assert.IsTrue(_behavior.AnsweredWithRefusal());

        // Unavailable (no longer allowed): the refusal line is the fallback
        _f.Lord.AtWarWithPlayer = true;
        _behavior.AskDomain();
        Assert.IsTrue(_behavior.Answered(DomainOutcome.Unavailable));
        Assert.IsTrue(_behavior.AnsweredWithRefusal());
    }

    [TestMethod]
    public void ANewBehavior_StartsWithNoAnswer_SoItRepliesWithTheRefusalFallback()
    {
        Assert.IsTrue(_behavior.Answered(DomainOutcome.Unavailable));
        Assert.IsTrue(_behavior.AnsweredWithRefusal());
    }
}
