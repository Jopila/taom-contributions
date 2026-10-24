using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Features.Enlistment.Content;

/// <summary>The end of the term: offer to sign on again or leave with honor (#836).</summary>
public sealed class ContractRenewalService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _content;
    private readonly IEnlistmentContentConfigProvider _config;
    private readonly IEnlistmentConfigProvider _core;
    private readonly IServiceRewardService _rewards;
    private readonly IDischargeService _discharge;
    private readonly ICoopSessionProvider _coop;
    private readonly ICommanderLordAdapter _commander;
    private readonly IEncounterAdapter _encounter;
    private readonly IInquiryAdapter _inquiry;
    private readonly IModLogger _logger;

    /// <summary>
    /// The end day an offer was already made for. In memory only: the engine can run the daily tick twice
    /// in one frame (MBCampaignEvent.CheckUpdate loops while the trigger time is past), and a SAVED stamp
    /// could lose the offer when Save And Exit hides the inquiry without an answer. Set before showing,
    /// cleared on a refused Leave, and reset with the other session singletons.
    /// </summary>
    private double? _offeredForEndDay;

    public ContractRenewalService(
        IEnlistmentStore store,
        IEnlistmentContentStore content,
        IEnlistmentContentConfigProvider config,
        IEnlistmentConfigProvider core,
        IServiceRewardService rewards,
        IDischargeService discharge,
        ICoopSessionProvider coop,
        ICommanderLordAdapter commander,
        IEncounterAdapter encounter,
        IInquiryAdapter inquiry,
        IModLogger logger)
    {
        _store = store;
        _content = content;
        _config = config;
        _core = core;
        _rewards = rewards;
        _discharge = discharge;
        _coop = coop;
        _commander = commander;
        _encounter = encounter;
        _inquiry = inquiry;
        _logger = logger;
    }

    /// <summary>
    /// Host only. Offers when due, once per term, and returns true when the inquiry was shown. The cheap
    /// gate runs first and reads no adapter, so most daily ticks stop at once.
    /// </summary>
    public bool OfferIfDue(double nowDays)
    {
        if (!_coop.IsAuthority)
            return false;
        var record = _store.Record;
        if (string.IsNullOrEmpty(record.CommanderHeroId)
            || !ContractRenewalPolicy.IsDue(record.State, nowDays, record.ContractEndDay, _offeredForEndDay))
        {
            return false;
        }

        // The offer needs a commander who can still hold one, and nothing live for the modal to land on.
        var snapshot = _commander.GetSnapshot(record.CommanderHeroId);
        if (snapshot == null || !snapshot.Exists || !snapshot.IsAlive || snapshot.IsPrisoner)
            return false;
        var own = _encounter.GetOwnership(snapshot.PartyId);
        if (snapshot.PartyIsInMapEvent || own.PlayerInMapEvent || own.IsBattleEncounter || own.ReadFailed || own.ConversationInProgress)
            return false;

        var config = _config.GetConfig();
        var wage = WagePolicy.DailyWageForRank((int)_content.Record.Rank, config.Progression.DailyWageByRank);
        var bonus = (int)System.Math.Min((long)config.RenewalBonusWageDays * wage, int.MaxValue);

        // Guard BEFORE showing, so a second tick in the same frame cannot offer twice.
        _offeredForEndDay = record.ContractEndDay;
        _logger?.LogInfo($"[Enlistment] the term has ended (day {record.ContractEndDay}); offering renewal, bonus {bonus}");

        _inquiry.ShowTwoOptionInquiry(
            "taom_enlist_term_title", "Your term is served",
            "taom_enlist_term_body",
            "{COMMANDER} offers you up to {BONUS} denars and renewed trust to sign on for another term, or you may leave the service with honor.",
            // Option B is the one the engine runs on Escape and the gamepad B button, so the safe choice
            // (stay in service) is B and leaving is A.
            "taom_enlist_term_leave", "Leave with honor",
            "taom_enlist_term_renew", "Sign on again",
            Leave,
            () => Renew(nowDays, bonus, config.RenewalTrust),
            bodyVariables: new Dictionary<string, string>
            {
                { "COMMANDER", snapshot.Name ?? "" },
                { "BONUS", bonus.ToString() },
            },
            prioritize: true);
        return true;
    }

    /// <summary>Forget the once-per-term guard (a new campaign session).</summary>
    public void ResetForNewSession() => _offeredForEndDay = null;

    /// <summary>
    /// Dev console (<c>taom.set_term_end</c>): end the current term today so the next daily tick makes
    /// the offer. False when not the host or not enlisted.
    /// </summary>
    public bool ForceTermEnd(double nowDays)
    {
        if (!_coop.IsAuthority || !_store.Record.IsEnlisted)
            return false;
        _store.Record.ContractEndDay = nowDays;
        _offeredForEndDay = null;
        return true;
    }

    /// <summary>The choice lands on a later frame, so the gates are re-checked rather than assumed.</summary>
    private bool StillServing() => _coop.IsAuthority && _store.Record.IsEnlisted;

    private void Renew(double nowDays, int bonus, int trust)
    {
        if (!StillServing())
            return;
        // A gift from the commander's purse above the wage floor (minted when wages are); never owed.
        var paid = _rewards.PayBonusFromCommander(bonus, "contract-renewal");
        _rewards.Grant(new RewardSpec { Trust = trust }, "contract-renewal");
        if (paid > 0)
            _inquiry.ShowMessage("taom_enlist_term_bonus_paid", "You receive {GOLD} denars for signing on again.", "GOLD", paid.ToString());

        // The oath's own expression (EnlistmentService.CompleteOath): the term restarts from today.
        _store.Record.ContractEndDay = nowDays + _core.GetConfig().ContractDays;
        _logger?.LogInfo($"[Enlistment] signed on again; the term now ends day {_store.Record.ContractEndDay}");
    }

    private void Leave()
    {
        if (!StillServing())
            return;
        // A battle can start in the same frame after the offer shows; leaving then would discharge him mid-fight.
        if (_store.Record.State != EnlistmentState.EnlistedAttached)
        {
            _offeredForEndDay = null;
            _logger?.LogInfo("[Enlistment] leave refused: the soldier is not attached any more; the offer returns on the next daily tick");
            return;
        }
        _discharge.Execute(DischargeReason.ContractNotRenewed);
    }
}
