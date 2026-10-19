namespace TAOM.Features.HardQuestions.Domain;

/// <summary>
/// The cheap facts about a captive lord, taken at the engine boundary and safe to read in a dialog condition.
/// The people around him are in <see cref="CaptiveCircle"/>, built only when a deed runs.
/// </summary>
public sealed class CaptiveSnapshot
{
    public CaptiveSnapshot(LordRef lord)
    {
        Lord = lord;
    }

    public LordRef Lord { get; }

    public string? ClanId { get; set; }

    public bool IsWounded { get; set; }

    /// <summary>True when a party or settlement of the player's clan holds him.</summary>
    public bool CaptorIsPlayerClan { get; set; }

    public bool IsInPlayerClan { get; set; }

    public int CalculatingLevel { get; set; }

    public int ValorLevel { get; set; }

    public int RansomValue { get; set; }
}
