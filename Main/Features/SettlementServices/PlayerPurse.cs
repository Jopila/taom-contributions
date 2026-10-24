using System;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.SettlementServices;

/// <summary>
/// The one way both services take gold. A charge succeeds only when the purse lost exactly the price and nothing threw. Anything
/// else fails and gives back what was taken: <c>GiveGoldAction</c> debits the purse before it dispatches the trade event, so a
/// throw can arrive after the gold is gone. The gold lost is measured on the purse itself, not read from the throw.
/// </summary>
internal static class PlayerPurse
{
    /// <param name="what">The caller's name for the log line, such as <c>Watch-Silver</c>.</param>
    internal static bool TryCharge(ISettlementServicesAdapter adapter, IModLogger logger, int price, string what)
    {
        var before = adapter.PlayerGold;
        int taken;
        Exception? fault = null;
        try
        {
            taken = adapter.ChargePlayer(price);
        }
        catch (Exception ex)
        {
            fault = ex;
            taken = before - adapter.PlayerGold;
        }

        if (fault == null && taken == price)
            return true;

        var cause = fault == null ? "" : $" Root cause: {fault}";
        logger.LogError($"[SettlementServices] {what} took {taken} of {price} gold, so the charge fails.{cause}");
        if (taken <= 0)
            return false;

        // The refund raises the same trade event after it credits, so a listener that threw on the debit can throw again.
        try
        {
            adapter.Refund(taken);
        }
        catch (Exception ex)
        {
            logger.LogError($"[SettlementServices] {what} could not give back {taken} gold. Root cause: {ex}");
        }

        return false;
    }
}
