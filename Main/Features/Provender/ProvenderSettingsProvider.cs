using System;
using TAOM.Core.Validation;

namespace TAOM.Features.Provender;

/// <inheritdoc cref="IProvenderSettingsProvider"/>
public sealed class ProvenderSettingsProvider : IProvenderSettingsProvider
{
    public const bool DefaultEnabled = true;
    public const int DefaultLowFoodDays = 3;
    public const int MinLowFoodDays = 1;
    public const int MaxLowFoodDays = 30;
    public const int DefaultKeepPerType = 2;
    public const int MinKeepPerType = 0;
    public const int MaxKeepPerType = 20;
    public const int DefaultSmallPayGold = 50;
    public const int DefaultLargePayGold = 100;
    public const int MinPayGold = 10;
    public const int MaxPayGold = 500;
    public const int DefaultSmallPayMorale = 5;
    public const int DefaultLargePayMorale = 10;
    public const int MinPayMorale = 1;
    public const int MaxPayMorale = 30;
    public const int DefaultWageDaysKept = 3;
    public const int MinWageDaysKept = 0;
    public const int MaxWageDaysKept = 30;
    public const int DefaultSaleMaxFeePercent = 30;
    public const int DefaultSaleMinFeePercent = 10;
    public const int MinSaleFeePercent = 0;
    public const int MaxSaleFeePercent = 90;
    public const int DefaultSaleTownCount = 3;
    public const int MinSaleTownCount = 1;
    public const int MaxSaleTownCount = 5;

    private TaomSettings? _settings;

    public ProvenderSettingsProvider()
    {
    }

    internal ProvenderSettingsProvider(TaomSettings settings) => _settings = settings;

    // Takes the settings reference once; a null (MCM not up yet) is not cached.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public ProvenderSettings Get() => From(Settings);

    internal static ProvenderSettings From(TaomSettings? s)
    {
        var smallGold = SettingClamp.Clamp(s?.ProvenderSmallPayGold, DefaultSmallPayGold, MinPayGold, MaxPayGold);
        var smallMorale = SettingClamp.Clamp(s?.ProvenderSmallPayMorale, DefaultSmallPayMorale, MinPayMorale, MaxPayMorale);
        var largeGold = SettingClamp.Clamp(s?.ProvenderLargePayGold, DefaultLargePayGold, MinPayGold, MaxPayGold);
        var largeMorale = SettingClamp.Clamp(s?.ProvenderLargePayMorale, DefaultLargePayMorale, MinPayMorale, MaxPayMorale);
        var saleMax = SettingClamp.Clamp(s?.ProvenderSaleMaxFeePercent, DefaultSaleMaxFeePercent, MinSaleFeePercent, MaxSaleFeePercent);
        return new ProvenderSettings(
            s?.ProvenderEnabled ?? DefaultEnabled,
            SettingClamp.Clamp(s?.ProvenderLowFoodDays, DefaultLowFoodDays, MinLowFoodDays, MaxLowFoodDays),
            SettingClamp.Clamp(s?.ProvenderKeepPerType, DefaultKeepPerType, MinKeepPerType, MaxKeepPerType),
            smallGold,
            smallMorale,
            Math.Max(largeGold, smallGold),
            Math.Max(largeMorale, smallMorale),
            SettingClamp.Clamp(s?.ProvenderWageDaysKept, DefaultWageDaysKept, MinWageDaysKept, MaxWageDaysKept),
            saleMax,
            Math.Min(SettingClamp.Clamp(s?.ProvenderSaleMinFeePercent, DefaultSaleMinFeePercent, MinSaleFeePercent, MaxSaleFeePercent), saleMax),
            SettingClamp.Clamp(s?.ProvenderSaleTownCount, DefaultSaleTownCount, MinSaleTownCount, MaxSaleTownCount));
    }
}
