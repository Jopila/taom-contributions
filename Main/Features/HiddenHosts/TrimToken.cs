namespace TAOM.Features.HiddenHosts;

/// <summary>
/// Harmony's <c>__state</c> for the troop-rows trim (#846 phase 2): where this call's rows began. A default value (a
/// stripped prefix, or an exact subject) trims nothing; a bare index of 0 could not tell those apart from a real start.
/// Public because it appears in public prefix and postfix signatures.
/// </summary>
public readonly struct TrimToken
{
    internal TrimToken(int start)
    {
        IsSet = true;
        Start = start;
    }

    /// <summary>True when the prefix recorded a start.</summary>
    public bool IsSet { get; }

    /// <summary>The row count before the call added its rows.</summary>
    public int Start { get; }
}
