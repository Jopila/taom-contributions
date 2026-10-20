namespace TAOM.Features.LordCourtesies;

/// <summary>Live (MCM over default) switches for the two courtesies.</summary>
public interface ILordCourtesiesSettingsProvider
{
    bool TroopGiftsEnabled { get; }

    bool DomainRevealEnabled { get; }
}
