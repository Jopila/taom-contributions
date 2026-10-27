namespace TAOM.Features.InnRounds;

/// <summary>The live (MCM over default) switch and the two scales for Inn Rounds.</summary>
public interface IInnRoundsSettingsProvider
{
    bool Enabled { get; }

    /// <summary>Multiplies the morale a party round gives; 1 when the setting is not a finite number in [0, 3].</summary>
    float MoraleScale { get; }

    /// <summary>Multiplies the renown a house round gives; 1 when the setting is not a finite number in [0, 3].</summary>
    float RenownScale { get; }
}
