using System;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.RedArrow;

/// <summary>
/// The engine boundary of the Red Arrow (#873, ADR-007), shared by the two view-model patches: it reads the snapshots
/// through the adapters, asks <see cref="RedArrowService"/>, and turns the verdict back into vanilla's own texts. The
/// army-management model is not involved: vanilla's check refuses during a siege, and the eligibility postfix redoes the
/// whole question here. Every method falls back to vanilla's answer when the feature does not apply, or when a read or the
/// service throws (the first fault is logged, the rest are counted silently): vanilla's answer during a siege is the block,
/// so a fault leaves the screen as the base game has it. Main thread only.
/// </summary>
public sealed class RedArrowArmyBoundary
{
    private const string Tag = "[RedArrow]";

    private readonly RedArrowService _service;
    private readonly IPlayerSiegeStateAdapter _player;
    private readonly IArmyCallCandidateAdapter _candidates;
    private readonly IModLogger _logger;
    private int _faults;

    public RedArrowArmyBoundary(RedArrowService service, IPlayerSiegeStateAdapter player, IArmyCallCandidateAdapter candidates, IModLogger logger)
    {
        _service = service;
        _player = player;
        _candidates = candidates;
        _logger = logger;
    }

    /// <summary>The faults caught so far (tests).</summary>
    internal int Faults => _faults;

    /// <summary>
    /// For <c>ArmyManagementItemVM.UpdateEligibility</c>'s postfix: false when the feature does not apply (or a fault), so
    /// vanilla's result stands. Otherwise the party check on the candidate's own facts, in vanilla's order and with vanilla's
    /// texts, then the map-action check with the siege blocks lifted for the player's own siege, as vanilla's method runs
    /// them. <paramref name="reason"/> is the empty text for an eligible party, as in vanilla.
    /// </summary>
    public bool TryRecheck(MobileParty party, out bool eligible, out TextObject reason)
    {
        eligible = false;
        reason = TextObject.GetEmpty();
        try
        {
            var player = _player.ReadSiege();
            if (!_service.Applies(player))
                return false;

            var partyVerdict = _service.CheckParty(player, _candidates.Read(party));
            if (!partyVerdict.HasValue)
                return false;
            if (partyVerdict.Value != ArmyCallReason.None)
            {
                reason = RedArrowTexts.For(partyVerdict.Value)!;
                return true;
            }

            var map = _service.CheckMapAction(player, _player.ReadMapAction());
            if (!map.HasValue)
                return false;

            eligible = map.Value == MapActionReason.None;
            reason = RedArrowTexts.For(map.Value);
            return true;
        }
        catch (Exception ex)
        {
            eligible = false;
            reason = TextObject.GetEmpty();
            ReportFault(nameof(TryRecheck), ex);
            return false;
        }
    }

    /// <summary>For the view-model constructor's postfix: null keeps vanilla's value, else the new <c>IsTransferDisabled</c>.</summary>
    public bool? TransferDisabled(bool isMainHero)
    {
        try
        {
            return _service.TransferDisabled(_player.ReadSiege(), isMainHero);
        }
        catch (Exception ex)
        {
            ReportFault(nameof(TransferDisabled), ex);
            return null;
        }
    }

    private void ReportFault(string where, Exception ex)
    {
        if (_faults++ == 0)
            _logger.LogError($"{Tag} {where} failed, vanilla's answer stands (later faults are not logged): {ex}");
    }
}
