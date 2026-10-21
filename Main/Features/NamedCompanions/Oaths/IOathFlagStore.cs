namespace TAOM.Features.NamedCompanions.Oaths;

/// <summary>
/// Named oath flags, saved with the campaign. This is the public API other features (quests) call to
/// grant a flag that a companion's hire terms or dialog topics can require.
/// </summary>
public interface IOathFlagStore
{
    /// <summary>True when the flag has been set. The name is lower-cased; a bad name is never set.</summary>
    bool Has(string flag);

    /// <summary>
    /// Sets the flag. Returns false when refused: the name is not <c>^[a-z0-9_.]{1,64}$</c> after
    /// lower-casing, or this peer is a co-op client. Setting a flag twice returns true.
    /// </summary>
    bool Set(string flag);
}
