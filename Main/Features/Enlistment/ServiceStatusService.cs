using TAOM.Adapters;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Hooks;

using TAOM.Features.Enlistment.Presentation;
namespace TAOM.Features.Enlistment;

/// <summary>
/// Builds the wait menu's live status and re-pushes the text only when it actually changed.
///
/// THE REAL ROOT CAUSE OF THE FROZEN TEXT, verified against installed v1.4.7 rather than assumed:
/// it was never a caching problem. <c>GameMenuVM.IsMenuTextChanged</c> compares
/// <c>_menuTextAttributes.Count</c> (an <c>int</c>) against <c>_menuText?.Attributes?.Count</c> (an
/// <c>int?</c>); our menu TextObject is built by <c>new TextObject(text)</c> with no attributes, so
/// that comparison is <c>0 != null</c> — always TRUE under C#'s lifted equality. The menu therefore
/// re-renders EVERY frame and <c>.ToString()</c> re-resolves the global variable each time. The
/// sentence looked frozen because <c>RefreshWaitText</c> ran only on menu init and its one token
/// was the commander's name, which does not change all term. Nothing new was ever computed.
///
/// So the fix is cadence plus varying content — NOT the <c>MenuContext.GameMenu.GetText()</c>
/// plumbing an earlier design called for. That change would have been real code aimed at a bug this
/// menu does not have.
///
/// Since #1019 the board itself is rendered to a plain string once per push, so the per-frame re-render
/// resolves one variable instead of the whole nested board. The price is that the pushed text no longer
/// follows a language switch by itself: <see cref="RefreshIfChanged"/> also writes again when the writer
/// reports the board is in a language other than the active one.
/// </summary>
public interface IServiceStatusService
{
    /// <summary>Current status, or null when not enlisted.</summary>
    ServiceStatusModel Build();

    /// <summary>
    /// Rebuild and push the menu text if anything changed. Cheap on the unchanged path — one
    /// snapshot read, one behaviour text render (a hero lookup and a few TextObjects), one party wage
    /// read (the roster's wage sum), the oath rule's reads (the opposing-faction lookup, only while the
    /// commander is in a non-raid battle), a value comparison and a language check; no TextObject is built for the board itself.
    /// </summary>
    bool RefreshIfChanged();

    /// <summary>Drop the cached model so the next refresh always pushes (menu init, discharge).</summary>
    void Invalidate();
}

public class ServiceStatusService : IServiceStatusService
{
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentContentStore _content;
    private readonly ICommanderLordAdapter _commander;
    private readonly IServiceStatusTextWriter _writer;
    private readonly IPromotionService _promotion;
    private readonly IEnlistmentWagePreview _wagePreview;
    private readonly Equipment.IEquipmentIssueLedger _ledger;
    private readonly IPlayerPartyAdapter _playerParty;
    private readonly Content.IEnlistmentContentConfigProvider _config;
    private readonly IEncounterAdapter _encounter;
    private readonly IServiceDiplomacyService _diplomacy;

    private ServiceStatusModel _last;

    public ServiceStatusService(
        IEnlistmentStore store,
        IEnlistmentContentStore content,
        ICommanderLordAdapter commander,
        IServiceStatusTextWriter writer,
        IPromotionService promotion,
        IEnlistmentWagePreview wagePreview,
        Equipment.IEquipmentIssueLedger ledger,
        IPlayerPartyAdapter playerParty,
        Content.IEnlistmentContentConfigProvider config,
        IEncounterAdapter encounter,
        IServiceDiplomacyService diplomacy)
    {
        _store = store;
        _content = content;
        _commander = commander;
        _writer = writer;
        _promotion = promotion;
        _wagePreview = wagePreview;
        _ledger = ledger;
        _playerParty = playerParty;
        _config = config;
        _encounter = encounter;
        _diplomacy = diplomacy;
    }

    public ServiceStatusModel Build()
    {
        var record = _store.Record;
        if (!record.IsEnlisted)
            return null;

        var commander = _commander.GetSnapshot(record.CommanderHeroId) ?? CommanderSnapshot.Missing;
        var activity = ResolveActivity(commander);
        var content = _content.Record;

        // Peek(), never EvaluateAndApply(): a render path must not be able to promote anyone. The
        // two share one evaluator call, which is what keeps the numbers on screen identical to the
        // numbers that actually grant the rank.
        var promotion = _promotion?.Peek();

        // Show the ladder only while there is something to ask of the player. At the top rank there
        // is no next step, and when every requirement is already met the promotion lands on the next
        // daily tick — a "you still need…" line in either case would be a lie.
        var showLadder = promotion != null && !promotion.AtTopRank && promotion.MostBindingUnmetKey != null;

        return new ServiceStatusModel(
            commanderName: commander.Name,
            activity: activity,
            settlementName: commander.SettlementName,
            rank: content.Rank,
            assignment: content.Assignment,
            daysServed: content.DaysServed,
            trust: content.Trust,
            deferredWages: content.DeferredWages,
            activeDutyId: content.ActiveDutyId,
            serviceXp: content.ServiceXp,
            // What service pays today, so 0 while the column is lost: the board must not promise unpaid wages.
            dailyWage: _wagePreview.GetDailyWage(),
            nextRank: showLadder ? promotion.ToRank : (ServiceRank?)null,
            nextRequirementKey: showLadder ? promotion.MostBindingUnmetKey : null,
            nextRequirementTarget: showLadder ? promotion.MostBindingUnmetTarget : 0,
            kitWaits: Equipment.EnlistmentKitTiers.KitWaits(content.Rank, _ledger),
            // Left out in a settlement: the engine blanks this text there and the activity line already says where he is.
            columnText: activity != CommanderActivity.Unavailable && !commander.PartyIsInSettlement
                ? _commander.GetPartyBehaviorText(record.CommanderHeroId)
                : null,
            retinueWage: _playerParty.GetMainPartyDailyWage(),
            activeDutyTitle: ActiveDutyTitle(content.ActiveDutyId));
    }

    /// <summary>The active field duty's title string: its loaded row's own, or the composed key with generic English when the row is gone; null when no duty is active.</summary>
    private string ActiveDutyTitle(string activeDutyId)
        => string.IsNullOrEmpty(activeDutyId)
            ? null
            : Content.Domain.DutyText.Source(
                _config.GetDuties().FieldDuties.Find(d => d.Id == activeDutyId)?.Title,
                activeDutyId, Content.Domain.DutyText.TitleSuffix, "a task for the company");

    /// <summary>
    /// Order matters: a raid (#831) outranks a battle, a battle (or a sit-out of it) outranks a siege,
    /// and a siege outranks a settlement; an assault is all three, and "in battle" is the one to show.
    /// </summary>
    private CommanderActivity ResolveActivity(CommanderSnapshot commander)
    {
        if (!commander.IsFit)
            return CommanderActivity.Unavailable;
        if (commander.PartyIsRaiding)
            return CommanderActivity.Raiding;
        if (commander.PartyIsInMapEvent)
        {
            return OathSitOutRule.Decide(_encounter, _diplomacy, commander.PartyId, out _) != OathSitOutReason.None
                ? CommanderActivity.SittingOut
                : CommanderActivity.InBattle;
        }
        if (commander.PartyIsBesieging)
            return CommanderActivity.Besieging;
        if (commander.PartyIsInSettlement)
            return CommanderActivity.InSettlement;
        return CommanderActivity.Marching;
    }

    public bool RefreshIfChanged()
    {
        var current = Build();
        if (current == null)
            return false;
        if (current.Equals(_last) && !_writer.IsStale)
            return false;

        _last = current;
        _writer.Write(current);
        return true;
    }

    public void Invalidate() => _last = null;
}
