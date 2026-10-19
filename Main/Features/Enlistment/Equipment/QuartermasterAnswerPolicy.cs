namespace TAOM.Features.Enlistment.Equipment;

/// <summary>
/// Maps an issuance outcome to the quartermaster's reply. <see cref="QuartermasterAnswer.HostOnly"/>
/// is not produced here: the behavior's authority branch sets it without calling the issuer. The
/// default arm is <see cref="QuartermasterAnswer.CannotLoad"/>, so a result added later still gets
/// a truthful "not just now" reply rather than a wrong one. At the top kit tier an already-drawn kit
/// answers <see cref="QuartermasterAnswer.AlreadyDrawnBest"/>, because there is no next stripe to
/// earn a better kit with.
/// </summary>
public static class QuartermasterAnswerPolicy
{
    public static QuartermasterAnswer For(EquipmentIssueResult result, EnlistmentKitTier tier) => result switch
    {
        EquipmentIssueResult.Issued               => QuartermasterAnswer.Issued,
        EquipmentIssueResult.AlreadyIssuedForRank => tier == EnlistmentKitTiers.Best
            ? QuartermasterAnswer.AlreadyDrawnBest
            : QuartermasterAnswer.AlreadyDrawn,
        EquipmentIssueResult.NoRosterFound        => QuartermasterAnswer.EmptyStores,
        EquipmentIssueResult.NoValidItems         => QuartermasterAnswer.EmptyStores,
        EquipmentIssueResult.PartyUnavailable     => QuartermasterAnswer.CannotLoad,
        _                                         => QuartermasterAnswer.CannotLoad,
    };
}
