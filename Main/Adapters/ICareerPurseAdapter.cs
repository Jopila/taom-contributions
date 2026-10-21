namespace TAOM.Adapters;

/// <summary>The player's purse, as the career system sees it: a retrain or a career switch costs gold.</summary>
public interface ICareerPurseAdapter
{
    int Gold { get; }

    /// <summary>
    /// Takes <paramref name="amount"/> gold from the player. False, with nothing taken, when the
    /// amount is negative or exceeds the purse; zero succeeds without a transfer.
    /// </summary>
    bool ChargeGold(int amount);

    /// <summary>
    /// Gives the player <paramref name="amount"/> gold from no one, with the engine's gold notification. False,
    /// with nothing given, when there is no main hero or the amount is not positive.
    /// </summary>
    bool GrantGold(int amount);
}
