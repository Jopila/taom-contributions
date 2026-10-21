using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.NamedCompanions.Oaths;

/// <summary>
/// Sends a <c>stay_at_home</c> companion back to their home settlement (#904): <c>home_settlement</c> when set, else the spawn one. It moves a hero, so every state where a
/// move would corrupt the entity is skipped first (Entity State Matrix). Hero.HeroState is one field, so
/// "not Active" already covers a prisoner, a fugitive, a dead or a missing hero (the adapter only finds living
/// heroes):
/// <list type="table">
/// <item><term>not Active, or missing, dead, prisoner, fugitive</term><description>skipped</description></item>
/// <item><term>recruited (CompanionOf set), in a party or a captor party</term><description>skipped</description></item>
/// <item><term>promoted to lord (own clan, CompanionOf null)</term><description>skipped</description></item>
/// <item><term>already home</term><description>skipped</description></item>
/// <item><term>the player is in the hero's current settlement or in the home one</term><description>skipped</description></item>
/// <item><term>anything else</term><description>moved home</description></item>
/// </list>
/// Each companion is handled in its own try, so one failing read or move skips only that companion.
/// </summary>
public sealed class HomeAnchorService
{
    private readonly INamedCompanionConfigProvider _config;
    private readonly INamedCompanionAdapter _adapter;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    public HomeAnchorService(
        INamedCompanionConfigProvider config,
        INamedCompanionAdapter adapter,
        ICoopSessionProvider coop,
        IModLogger logger)
    {
        _config = config;
        _adapter = adapter;
        _coop = coop;
        _logger = logger;
    }

    /// <summary>Returns the number of companions moved.</summary>
    public int AnchorAll()
    {
        if (!_coop.IsAuthority) return 0;

        var companions = _config.GetCompanions();
        if (companions == null) return 0;

        var moved = 0;
        foreach (var companion in companions)
        {
            if (companion == null || !companion.StayAtHome || !companion.Enabled) continue;
            var id = companion.CharacterId;
            var home = string.IsNullOrEmpty(companion.HomeSettlement) ? companion.SpawnSettlement : companion.HomeSettlement;
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(home)) continue;

            try
            {
                var current = _adapter.GetCurrentSettlementId(id);
                if (current == home) continue;
                if (!_adapter.IsHeroActive(id) || _adapter.IsRecruitedOrInParty(id)) continue;
                if (current != null && _adapter.IsPlayerInSettlement(current)) continue;
                if (_adapter.IsPlayerInSettlement(home)) continue;

                _adapter.ReturnHome(id, home);
                moved++;
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Oaths] could not return {id} to {home}: {ex}");
            }
        }
        return moved;
    }
}
