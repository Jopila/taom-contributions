namespace TAOM.Features.ShadowfaxScreens.Party.Hooks;

/// <summary>The __state a Party Fast Open prefix hands its finalizer: whether this call opened the count window, and the phase clock start.</summary>
public struct PartyPhaseState
{
    public bool OpenedWindow;
    public long Start;
}
