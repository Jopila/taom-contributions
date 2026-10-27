using TAOM.Core.Validation;

namespace TAOM.Features.InnRounds;

/// <summary>
/// The MCM switch and the two scales over their defaults (on, 1, 1). The settings object is taken once and only a non-null
/// instance is kept, so a read before MCM is up cannot pin the default (CampaignHotPathSettingsProvidersTests); MCM edits its one
/// <see cref="TaomSettings"/> in place, so a change applies at the next conversation. A scale that is not a finite number in
/// [0, 3] reads as 1, never as a value that would zero, flip or blow up a reward.
/// </summary>
public sealed class InnRoundsSettingsProvider : IInnRoundsSettingsProvider
{
    internal const bool DefaultEnabled = true;
    internal const float DefaultScale = 1f;
    internal const float MaxScale = 3f;

    private TaomSettings? _settings;

    public InnRoundsSettingsProvider()
    {
    }

    internal InnRoundsSettingsProvider(TaomSettings settings) => _settings = settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled => EnabledFrom(Settings);

    public float MoraleScale => MoraleScaleFrom(Settings);

    public float RenownScale => RenownScaleFrom(Settings);

    /// <summary>The switch, or on when MCM has not built its settings yet.</summary>
    public static bool EnabledFrom(TaomSettings? settings) => settings?.EnableInnRounds ?? DefaultEnabled;

    public static float MoraleScaleFrom(TaomSettings? settings) => Scale(settings?.InnRoundsMoraleScale);

    public static float RenownScaleFrom(TaomSettings? settings) => Scale(settings?.InnRoundsRenownScale);

    private static float Scale(float? value) =>
        value is { } v && FiniteFloatValidator.IsFiniteInRange(v, 0f, MaxScale) ? v : DefaultScale;
}
