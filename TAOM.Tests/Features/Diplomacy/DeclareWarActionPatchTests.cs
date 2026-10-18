using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Features.Diplomacy.Hooks;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// Which DeclareWarAction reasons count as the player's own hostile act, so the phase vetoes let the
/// war start. The crime-rating case matters most: with the war allowed, ChangeCrimeRatingAction
/// latches once instead of re-applying a relation loss every day.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class DeclareWarActionPatchTests
{
    [DataTestMethod]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByPlayerHostility, true)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByCrimeRatingChange, true)]
    [DataRow(DeclareWarAction.DeclareWarDetail.Default, false)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByKingdomDecision, false)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByRebellion, false)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByKingdomCreation, true)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByClaimOnThrone, false)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByCallToWarAgreement, false)]
    [DataRow(DeclareWarAction.DeclareWarDetail.CausedByQuest, false)]
    public void IsCausedByPlayer_Detail_MatchesTheReason(DeclareWarAction.DeclareWarDetail detail, bool expected)
    {
        Assert.AreEqual(expected, DeclareWarAction_ApplyInternal_Patch.IsCausedByPlayer(detail));
    }
}
