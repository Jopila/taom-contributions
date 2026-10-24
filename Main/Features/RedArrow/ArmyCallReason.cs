namespace TAOM.Features.RedArrow;

/// <summary>
/// Why a party cannot be called into the player's army, or <see cref="None"/> when it can. The members after
/// <see cref="None"/> are declared in the order vanilla's <c>DefaultArmyManagementCalculationModel.CheckPartyEligibility</c>
/// tests them (v1.5.5, branches 2 to 12). <c>RedArrowServiceTests</c> pins the order of this enum and
/// <c>RedArrowBindingTests</c> pins the engine method's IL, so a reordered or reworded engine check fails a test. The first
/// branch, the player's siege, is what the feature lifts. Each member maps to one of vanilla's own texts in <c>RedArrowTexts</c>.
/// </summary>
public enum ArmyCallReason
{
    None = 0,
    NoParty,
    RulersParty,
    InAnotherArmy,
    InThisArmy,
    Fighting,
    TooSmall,
    Disbanding,
    NoShips,
    AtSea,
    LostAtSea,
    TooFar,
}
