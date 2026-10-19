using TAOM.Features.Enlistment;
using TAOM.Features.InitialChildGeneration;

namespace TAOM.Features.BondsOfBattle;

/// <summary>
/// The relation a barter overpay buys, with the cap taken from the Bonds of Battle settings. The caller reads the hero's
/// relation and the Tribute perk at the engine boundary and hands plain numbers. Null means "let vanilla decide": the
/// feature is off, or the player is enlisted.
/// </summary>
public sealed class BarterOverpayService
{
    private readonly IBondsOfBattleSettingsProvider _settings;
    private readonly IEnlistmentStateQuery _enlistment;
    private readonly IRandomSource _random;

    public BarterOverpayService(IBondsOfBattleSettingsProvider settings, IEnlistmentStateQuery enlistment, IRandomSource random)
    {
        _settings = settings;
        _enlistment = enlistment;
        _random = random;
    }

    public int? CalculateRelationIncrease(int relation, float overpayAmount, float tributeBonus)
    {
        var settings = _settings.Get();
        if (!settings.Enabled || _enlistment.IsEnlisted)
            return null;

        return OverpayRelationCalculator.Calculate(relation, overpayAmount, settings.OverpayRelationCap,
            tributeBonus, () => (float)_random.NextDouble());
    }
}
