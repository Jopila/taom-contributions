namespace TAOM.Features.LordCourtesies;

/// <summary>
/// The MCM toggles over a default of on. <c>TaomSettings.Instance</c> is null early in startup and when MCM
/// fails to load, so each read falls back to on.
/// </summary>
public sealed class LordCourtesiesSettingsProvider : ILordCourtesiesSettingsProvider
{
    public bool TroopGiftsEnabled => TaomSettings.Instance?.EnableTroopGifts ?? true;

    public bool DomainRevealEnabled => TaomSettings.Instance?.EnableDomainReveal ?? true;
}
