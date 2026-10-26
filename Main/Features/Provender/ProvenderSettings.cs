namespace TAOM.Features.Provender;

/// <summary>
/// The "Provender-master" MCM group after clamping. The large pay is never below the small one, in gold or in morale, so the
/// larger tier cannot be the worse buy. <see cref="WageDaysKept"/> is the days of party wages the food order never spends. The
/// remote-sale cut runs from <see cref="SaleMaxFeePercent"/> down to <see cref="SaleMinFeePercent"/> (never above it) with
/// the officer's Trade, at the <see cref="SaleTownCount"/> nearest eligible towns.
/// </summary>
public sealed record ProvenderSettings(
    bool Enabled,
    int LowFoodDays,
    int KeepPerType,
    int SmallPayGold,
    int SmallPayMorale,
    int LargePayGold,
    int LargePayMorale,
    int WageDaysKept,
    int SaleMaxFeePercent = ProvenderSettingsProvider.DefaultSaleMaxFeePercent,
    int SaleMinFeePercent = ProvenderSettingsProvider.DefaultSaleMinFeePercent,
    int SaleTownCount = ProvenderSettingsProvider.DefaultSaleTownCount);
