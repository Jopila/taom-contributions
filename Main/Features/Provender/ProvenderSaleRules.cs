using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Core.Validation;
using TAOM.Features.Provender.Models;

namespace TAOM.Features.Provender;

/// <summary>
/// The arithmetic of remote selling (docs/features/provender.md "Remote selling"): the Provender-master's cut, the price a
/// town pays after it, which towns are offered and how a closed sale moves the town's gold. Pure functions over plain values.
/// </summary>
public static class ProvenderSaleRules
{
    /// <summary>The extra cut for each town further down the list.</summary>
    public const int RankFeeStep = 5;

    /// <summary>A town holding less gold than this is not offered: it could pay for almost nothing.</summary>
    public const int MinTownGold = 5000;

    /// <summary>The Trade skill at which the cut reaches its minimum.</summary>
    public const int MaxTradeForFee = 250;

    /// <summary>The cut never reaches 100 percent, so a sale always pays something.</summary>
    public const int MaxFeePercent = 95;

    /// <summary>
    /// A distance at or past this is the engine's "unreachable" placeholder, not a road (1e30 from the native path search,
    /// 1e8 in DefaultMapDistanceModel, float.MaxValue in DistanceHelper); the longest real distance on TAOM_Map is about
    /// 2,200.
    /// </summary>
    public const float UnreachableDistance = 1e7f;

    /// <summary>
    /// The cut at a town: <paramref name="maxFee"/> falling to <paramref name="minFee"/> as Trade rises to 250, plus
    /// <see cref="RankFeeStep"/> for each rank, kept between 0 and <see cref="MaxFeePercent"/>.
    /// </summary>
    public static int FeePercent(int trade, int maxFee, int minFee, int rank)
    {
        var skill = Math.Min(Math.Max(trade, 0), MaxTradeForFee);
        long fee = maxFee - (long)(maxFee - minFee) * skill / MaxTradeForFee + (long)RankFeeStep * Math.Max(rank, 0);
        return (int)Math.Min(Math.Max(fee, 0L), MaxFeePercent);
    }

    /// <summary>The price a town pays after the cut, never below 1 for an item that has a price.</summary>
    public static int FeePrice(int price, int feePercent)
    {
        if (price <= 0)
            return price;
        var fee = Math.Min(Math.Max(feePercent, 0), MaxFeePercent);
        return (int)Math.Max(1L, (long)price * (100 - fee) / 100);
    }

    /// <summary>
    /// The towns to offer: the eligible ones the engine could measure a road to (a finite distance above 0 and below
    /// <see cref="UnreachableDistance"/>), nearest first (ties by id), at most <paramref name="count"/>. The filter runs before
    /// the count, so an ineligible or unreachable town never takes a slot.
    /// </summary>
    public static List<SaleTownFacts> Rank(IEnumerable<SaleTownFacts> towns, Func<SaleTownFacts, bool> eligible, int count)
    {
        if (count <= 0)
            return new List<SaleTownFacts>();
        return towns
            .Where(t => FiniteFloatValidator.IsFinite(t.Distance) && t.Distance > 0f && t.Distance < UnreachableDistance && eligible(t))
            .OrderBy(t => t.Distance)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// How a closed sale changes the town's gold. Nothing when the engine already settled it (inside a settlement it calls the
    /// screen's gold listener); otherwise the town pays what the player gained, capped by its purse. The screen can only sell
    /// (a buy-back takes back a sale at its own price), so a gain of 0 or less changes nothing: it can only be a gold change
    /// from elsewhere, which the town must not absorb.
    /// </summary>
    public static int TownGoldChange(bool engineSettled, int playerGain, int townGold)
    {
        if (engineSettled || playerGain <= 0)
            return 0;
        return -Math.Min(playerGain, Math.Max(townGold, 0));
    }
}
