using System;
using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem.ViewModelCollection.Party;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.PrisonerBanners.UI;

/// <summary>Reads a captive hero's clan banner, kingdom banner and clan leader flag once, as the party screen row is built (#902).</summary>
[ViewModelMixin]
internal sealed class PrisonerBannerMixin : BaseViewModelMixin<PartyCharacterVM>
{
    // One line per exception type, shared across rows: a screen builds dozens of rows and a fault repeats on each.
    private static readonly HashSet<string> FailuresLogged = new HashSet<string>(StringComparer.Ordinal);

    private bool _hasClanBanner;
    private bool _hasKingdomBanner;
    private bool _isClanLeader;
    private BannerImageIdentifierVM? _clanBanner;
    private BannerImageIdentifierVM? _kingdomBanner;

    public PrisonerBannerMixin(PartyCharacterVM viewModel) : base(viewModel)
    {
        try
        {
            var settings = IoC.Resolve<PrisonerBannersSettingsProvider>();
            var hero = viewModel.Character?.HeroObject;
            var clan = hero?.Clan;
            var kingdom = clan?.Kingdom;
            var view = PrisonerBannerRules.Decide(
                enabled: settings.Enabled,
                isPrisoner: viewModel.IsPrisoner,
                hasClan: clan != null,
                hasKingdom: kingdom != null,
                isClanLeader: hero?.IsClanLeader == true);

            var clanBanner = view.ShowClanBanner ? clan!.ClanOriginalBanner : null;
            var kingdomBanner = view.ShowKingdomBanner ? kingdom!.Banner : null;
            PrisonerClanBanner = clanBanner == null ? null : new BannerImageIdentifierVM(clanBanner, nineGrid: true);
            HasPrisonerClanBanner = clanBanner != null;
            PrisonerKingdomBanner = kingdomBanner == null ? null : new BannerImageIdentifierVM(kingdomBanner, nineGrid: true);
            HasPrisonerKingdomBanner = kingdomBanner != null;
            IsPrisonerClanLeader = view.ShowLeaderIcon;
        }
        catch (Exception ex)
        {
            HasPrisonerClanBanner = false;
            HasPrisonerKingdomBanner = false;
            IsPrisonerClanLeader = false;
            PrisonerClanBanner = null;
            PrisonerKingdomBanner = null;
            LogOnce(ex);
        }
    }

    [DataSourceProperty]
    public bool HasPrisonerClanBanner
    {
        get => _hasClanBanner;
        set => SetField(ref _hasClanBanner, value, nameof(HasPrisonerClanBanner));
    }

    [DataSourceProperty]
    public BannerImageIdentifierVM? PrisonerClanBanner
    {
        get => _clanBanner;
        set => SetField(ref _clanBanner, value, nameof(PrisonerClanBanner));
    }

    [DataSourceProperty]
    public bool HasPrisonerKingdomBanner
    {
        get => _hasKingdomBanner;
        set => SetField(ref _hasKingdomBanner, value, nameof(HasPrisonerKingdomBanner));
    }

    [DataSourceProperty]
    public BannerImageIdentifierVM? PrisonerKingdomBanner
    {
        get => _kingdomBanner;
        set => SetField(ref _kingdomBanner, value, nameof(PrisonerKingdomBanner));
    }

    [DataSourceProperty]
    public bool IsPrisonerClanLeader
    {
        get => _isClanLeader;
        set => SetField(ref _isClanLeader, value, nameof(IsPrisonerClanLeader));
    }

    private static void LogOnce(Exception ex)
    {
        if (!FailuresLogged.Add(ex.GetType().FullName ?? "(unknown)"))
            return;
        try
        {
            IoC.Resolve<IModLogger>().LogError($"[PrisonerBanners] party row build threw, so that row shows no banners. Root cause: {ex}");
        }
        catch
        {
            // The logger itself is unavailable: nothing more to do while the screen builds.
        }
    }
}
