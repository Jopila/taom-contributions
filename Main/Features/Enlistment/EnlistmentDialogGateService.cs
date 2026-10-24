using System;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.Enlistment;

public class EnlistmentDialogGateService : IEnlistmentDialogGateService
{
    private readonly IEnlistmentStore _store;
    private readonly ICommanderLordAdapter _commander;
    private readonly IPlayerContextAdapter _playerContext;
    private readonly IEnlistmentConfigProvider _config;
    private readonly IEnlistmentFeatureSettingsProvider _feature;
    private readonly IServiceHistoryService _history;
    private readonly IPlayerPartyAdapter _playerParty;
    private readonly IModLogger _logger;
    private bool _retinueFailureLogged;

    public EnlistmentDialogGateService(
        IEnlistmentStore store,
        ICommanderLordAdapter commander,
        IPlayerContextAdapter playerContext,
        IEnlistmentConfigProvider config,
        IEnlistmentFeatureSettingsProvider feature,
        IServiceHistoryService history,
        IPlayerPartyAdapter playerParty,
        IModLogger logger)
    {
        _store = store;
        _commander = commander;
        _playerContext = playerContext;
        _config = config;
        _feature = feature;
        _history = history;
        _playerParty = playerParty;
        _logger = logger;
    }

    public EnlistGateResult CanEnlistWith(string partnerHeroId)
    {
        // First gate: a disabled feature offers no enlistment at all.
        if (_feature?.IsEnabled == false)
            return EnlistGateResult.FeatureDisabled;

        if (_store.Record.IsEnlisted)
            return EnlistGateResult.AlreadyEnlisted;
        if (string.IsNullOrEmpty(partnerHeroId) || !_commander.IsLord(partnerHeroId))
            return EnlistGateResult.NotALord;
        if (_playerContext.IsUnderMercenaryService())
            return EnlistGateResult.UnderMercenaryContract;

        var snapshot = _commander.GetSnapshot(partnerHeroId);
        if (!snapshot.Exists || !snapshot.IsAlive || snapshot.IsPrisoner
            || !snapshot.HasParty || !snapshot.PartyIsActive)
        {
            return EnlistGateResult.CommanderUnavailable;
        }

        // Enlisting means fighting the commander's wars — never against your own crown.
        var playerKingdomId = _playerContext.GetPlayerKingdomId();
        if (!string.IsNullOrEmpty(playerKingdomId)
            && _commander.IsAtWarWithFaction(partnerHeroId, playerKingdomId))
        {
            return EnlistGateResult.AtWarWithYourKingdom;
        }

        // He deserted from this lord not long ago and the cooldown has not run out (#837).
        if (_history.IsInDesertionCooldown(partnerHeroId))
            return EnlistGateResult.DesertedFromHim;

        return EnlistGateResult.Ok;
    }

    public (int Cost, int Pay) OathRetinueCost(string partnerHeroId)
    {
        try
        {
            var cost = _playerParty.GetMainPartyDailyWage();
            var result = cost <= 0
                ? (0, 0)
                : (cost, _history.WageAtOath(partnerHeroId, _playerParty.GetMainHeroId(), _commander.GetSnapshot(partnerHeroId)));
            _retinueFailureLogged = false;
            return result;
        }
        catch (Exception ex)
        {
            if (!_retinueFailureLogged)
            {
                _retinueFailureLogged = true;
                _logger.LogError($"[Enlistment] OathRetinueCost('{partnerHeroId}') failed: {ex.Message}");
            }
            return (0, 0);
        }
    }

    public bool IsServingUnder(string partnerHeroId)
    {
        return _store.Record.IsEnlisted
            && !string.IsNullOrEmpty(partnerHeroId)
            && _store.Record.CommanderHeroId == partnerHeroId;
    }

    /// <summary>
    /// Classify a leave the COMMANDER GRANTS. Always honourable.
    ///
    /// This used to return Desertion whenever the player left before `ContractEndDay`, and the
    /// contract defaults to 365 days — so every realistic exit was desertion, forfeiting the
    /// player's arrears and calling them a deserter for asking their lord's leave and being
    /// given it. Reported in-game 2026-08-07 after two short services both ended "Desertion".
    ///
    /// Every path that reaches this method is the player asking and the commander agreeing, which is
    /// a release, not a betrayal. `DischargeReason.Desertion` is the reason for leaving before the
    /// minimum term against the commander's refusal; its two producers are the release conversation and
    /// the wait-menu confirmation, and it costs relation and Honor (#833).
    /// </summary>
    public Domain.DischargeReason ClassifyLeaveReason(double nowDays) =>
        Domain.DischargeReason.PlayerRequest;

    /// <summary>
    /// Every branch here is written to fall OPEN. The failure this gate can cause is a player
    /// locked in service with no exit — strictly worse than releasing someone a day early — so
    /// missing data, a poisoned config and a NaN clock all grant the release.
    /// </summary>
    public Domain.ReleaseRequest EvaluateReleaseRequest(double nowDays)
    {
        var record = _store.Record;
        if (!record.IsEnlisted)
            return Domain.ReleaseRequest.Granted;

        // Outranks the term: walking off mid-engagement is not a paperwork question. Deliberately
        // a refusal of the MOMENT, not of the request — the player is told to ask again after.
        if (record.State == Domain.EnlistmentState.EnlistedBattle)
            return Domain.ReleaseRequest.RefusedInBattle;

        // The term is WAIVED once the commander can no longer hold up his end. This is not a
        // difficulty choice; it is that the term cannot be SERVED. With no commander there are no
        // duties to take (DutyOrchestrationService gates on EnlistedAttached), no battles to join,
        // no trust to earn and nobody to earn it from — so refusing release leaves exactly one
        // interaction: stand still for N days or accept a desertion penalty for walking away from
        // a company that no longer exists.
        //
        // Deliberately ABOVE the minimum-service check, so it outranks an unserved contract rather
        // than being one more input to it.
        if (record.State == Domain.EnlistmentState.CommanderUnavailable)
            return Domain.ReleaseRequest.Granted;

        var minimum = _config?.GetConfig()?.MinimumServiceDays ?? 0.0;

        // Positive requirement, per the NaN-gate rule: we refuse only when we can PROVE a debt.
        // Written as `<= 0 || !finite -> granted` rather than `> 0 -> refuse` so every
        // non-finite value lands on the safe side without a separate guard.
        if (double.IsNaN(minimum) || double.IsInfinity(minimum) || minimum <= 0.0)
            return Domain.ReleaseRequest.Granted;

        // No recorded start day cannot prove a debt either.
        if (record.EnlistedAtDay == null)
            return Domain.ReleaseRequest.Granted;

        var served = nowDays - record.EnlistedAtDay.Value;

        // NaN served => `served < minimum` is false => granted. That is the intent, not an
        // accident: do not rewrite this as `if (served >= minimum) return Granted;`.
        if (!(served < minimum))
            return Domain.ReleaseRequest.Granted;

        // Clamped BEFORE the narrowing conversion. NaN/infinity are already rejected above, but
        // a merely huge finite config value overflows the cast and can surface as a negative
        // day count — the float->int trap the architecture rules call out separately from NaN.
        var owed = System.Math.Ceiling(minimum - served);
        if (owed >= int.MaxValue)
            return Domain.ReleaseRequest.TooSoon(int.MaxValue);

        return Domain.ReleaseRequest.TooSoon((int)owed);
    }
}
