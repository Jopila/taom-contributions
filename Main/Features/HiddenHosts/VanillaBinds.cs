namespace TAOM.Features.HiddenHosts;

/// <summary>
/// The three nameplate bind fields as vanilla wrote them, kept while a mask is laid over them for one call of
/// <c>PartyNameplateVM.RefreshBinding</c> (#846). The prefix lays the mask and hands this back as Harmony's
/// <c>__state</c>; the finalizer restores the originals, so no mask outlives the call that copies it to the bound
/// properties. Vanilla decides its own writes from int caches, never from the field text, and every property setter
/// returns early on an equal value, so putting the originals back is all the bookkeeping there is. Public because it
/// appears in the public prefix signature.
/// </summary>
public readonly struct VanillaBinds
{
    private readonly string? _count;
    private readonly string? _extraInfo;
    private readonly string? _name;

    private VanillaBinds(string? count, string? extraInfo, string? name)
    {
        IsSet = true;
        _count = count;
        _extraInfo = extraInfo;
        _name = name;
    }

    /// <summary>True when a mask was laid and the originals are held.</summary>
    public bool IsSet { get; }

    /// <summary>
    /// Lays the mask for <paramref name="intel"/> over the three fields and returns their originals, or returns an unset
    /// value and writes nothing when the plate is not masked. A masked plate shows the masked count and no wounded or
    /// prisoner text; the name changes only when the plate hides it (<see cref="NameplateIntel.HidesName"/>: at the horizon, and
    /// not named by a scout), to <paramref name="horizonName"/> (kept when that is null).
    /// </summary>
    internal static VanillaBinds LayMask(in NameplateIntel intel, string? horizonName,
        ref string count, ref string extraInfo, ref string name)
    {
        if (!intel.Masked) return default;

        var saved = new VanillaBinds(count, extraInfo, name);
        count = intel.CountText!;
        extraInfo = string.Empty;
        if (intel.HidesName && horizonName != null) name = horizonName;
        return saved;
    }

    /// <summary>Puts the originals back, nulls included. An unset value restores nothing.</summary>
    public void Restore(ref string count, ref string extraInfo, ref string name)
    {
        if (!IsSet) return;
        count = _count!;
        extraInfo = _extraInfo!;
        name = _name!;
    }
}
