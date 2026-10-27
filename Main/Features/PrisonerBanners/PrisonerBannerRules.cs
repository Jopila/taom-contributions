namespace TAOM.Features.PrisonerBanners;

/// <summary>What a party screen row shows for a captive: the clan banner, the kingdom banner and the clan leader's noble icon.</summary>
public readonly struct PrisonerBannerView
{
    public PrisonerBannerView(bool showClanBanner, bool showKingdomBanner, bool showLeaderIcon)
    {
        ShowClanBanner = showClanBanner;
        ShowKingdomBanner = showKingdomBanner;
        ShowLeaderIcon = showLeaderIcon;
    }

    public bool ShowClanBanner { get; }

    public bool ShowKingdomBanner { get; }

    public bool ShowLeaderIcon { get; }
}

/// <summary>Decides which prisoner banners a party screen row shows (#902).</summary>
public static class PrisonerBannerRules
{
    private static readonly PrisonerBannerView Nothing = new PrisonerBannerView(false, false, false);

    public static PrisonerBannerView Decide(bool enabled, bool isPrisoner, bool hasClan, bool hasKingdom, bool isClanLeader)
        => enabled && isPrisoner && hasClan
            ? new PrisonerBannerView(true, hasKingdom, isClanLeader)
            : Nothing;
}
