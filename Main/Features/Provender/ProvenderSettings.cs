namespace TAOM.Features.Provender;

/// <summary>
/// The "Provender-master" MCM group after clamping. The large pay is never below the small one, in gold or in morale, so the
/// larger tier cannot be the worse buy. <see cref="WageDaysKept"/> is the days of party wages the food order never spends.
/// </summary>
public sealed record ProvenderSettings(
    bool Enabled,
    int LowFoodDays,
    int KeepPerType,
    int SmallPayGold,
    int SmallPayMorale,
    int LargePayGold,
    int LargePayMorale,
    int WageDaysKept);
