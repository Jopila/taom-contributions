using System;
using TAOM.Core.Validation;

namespace TAOM.Features.BondsOfBattle;

/// <summary>
/// The relation a barter overpay buys, over plain numbers. It is the engine's loop (v1.5.5
/// <c>DefaultBarterModel.CalculateOverpayRelationIncreaseCosts</c>) with the cap of 3 points taken as an argument: each
/// point from the current relation <c>r</c> costs <c>1000 + 100 r^2</c>, whole points are paid while the overpay covers
/// them, the last point is a chance of (what is left / its cost), and the Charm Tribute perk multiplies the total.
/// At a cap of 3 it matches the engine for the same rolls (<c>OverpayRelationCalculatorTests</c>).
/// </summary>
public static class OverpayRelationCalculator
{
    internal const int MinRelation = -100;
    internal const int MaxRelation = 100;

    /// <param name="startRelation">The player's relation with the hero; clamped to -100..100.</param>
    /// <param name="overpayAmount">The gold offered beyond fair value. Not above zero (or NaN) buys nothing.</param>
    /// <param name="cap">The most points one barter can add.</param>
    /// <param name="tributeBonus">The Tribute perk's bonus, or 0. A non-finite or non-positive value is ignored.</param>
    /// <param name="randomFloat">A roll in [0, 1) for the last, partly paid point.</param>
    public static int Calculate(int startRelation, float overpayAmount, int cap, float tributeBonus, Func<float> randomFloat)
    {
        if (!(overpayAmount > 0f) || cap <= 0)
            return 0;

        var start = Math.Max(MinRelation, Math.Min(MaxRelation, startRelation));
        var end = (int)Math.Min((long)MaxRelation, (long)start + cap);
        var points = 0f;
        for (var i = start; i < end; i++)
        {
            var cost = 1000 + 100 * (i * i);
            if (overpayAmount >= cost)
            {
                overpayAmount -= cost;
                points += 1f;
                continue;
            }

            if (randomFloat() <= overpayAmount / cost)
                points += 1f;
            break;
        }

        if (FiniteFloatValidator.IsFinite(tributeBonus) && tributeBonus > 0f)
            points *= 1f + tributeBonus;
        return (int)Math.Ceiling(points);
    }
}
