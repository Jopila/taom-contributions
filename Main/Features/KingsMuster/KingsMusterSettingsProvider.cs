namespace TAOM.Features.KingsMuster;

/// <summary>
/// The King's Muster switch. The call cost runs on the AI's hourly think, once per army candidate (vanilla's
/// <c>CanLordCreateArmy</c> prices each one), so the MCM settings object is taken ONCE through one lazy accessor, as the
/// sibling providers do (<c>CampaignHotPathSettingsProvidersTests</c>): the constructor runs when the army-management model is
/// built in <c>OnGameStart</c> and touches the accessor; a non-null instance only is kept, so a read before MCM is up cannot
/// pin the defaults. MCM edits its one registered <see cref="TaomSettings"/> in place, so a change applies at once.
/// </summary>
public sealed class KingsMusterSettingsProvider : IKingsMusterSettingsProvider
{
    private TaomSettings? _settings;

    public KingsMusterSettingsProvider()
    {
        _ = Settings;
    }

    internal KingsMusterSettingsProvider(TaomSettings settings) => _settings = settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool DiscountEnabled => DiscountFrom(Settings);

    /// <summary>The discount switch, or on when MCM has not built its settings yet.</summary>
    public static bool DiscountFrom(TaomSettings? settings) => settings?.EnableKingsMusterDiscount ?? true;
}
