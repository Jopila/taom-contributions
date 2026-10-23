namespace TAOM.Features.UnderTheEaves;

/// <summary>
/// MCM (<c>TaomSettings</c>) over the compiled default. The toggle is read on every spotting query, so the settings object is
/// taken once in the lazy accessor and read through (CampaignHotPathSettingsProvidersTests). With no MCM instance (a test host)
/// the answer is on, the property's own default; UnderTheEavesSettingsProviderTests pins the two together. On a dedicated server
/// <c>TaomSettings.Instance</c> is the file-backed instance, so the server uses the operator's value.
/// </summary>
public sealed class UnderTheEavesSettingsProvider : IUnderTheEavesSettings
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public UnderTheEavesSettingsProvider() { }

    internal UnderTheEavesSettingsProvider(TaomSettings settings) => _settings = settings;

    public bool Enabled => Settings?.ForestSightPenaltyEnabled ?? true;
}
