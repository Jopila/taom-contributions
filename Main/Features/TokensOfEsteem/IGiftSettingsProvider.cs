namespace TAOM.Features.TokensOfEsteem;

/// <summary>The "Tokens of Esteem" MCM group, clamped. Read live at each conversation step that needs it.</summary>
public interface IGiftSettingsProvider
{
    GiftSettings Get();
}
