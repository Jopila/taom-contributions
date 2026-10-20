using TaleWorlds.CampaignSystem;
using TAOM.Features.LordCourtesies.Domain;

namespace TAOM.Features.LordCourtesies.Hooks;

/// <summary>
/// Thin dialog boundary for Lord Courtesies (ADR-002, docs/features/lord-courtesies.md): registers the lines
/// (<see cref="LordCourtesiesDialogLines"/>) and hands every condition and consequence to
/// <see cref="LordCourtesiesService"/>. Built fresh for every campaign by the feature module; it keeps only the
/// last domain answer, which the lord's reply lines read, and saves nothing.
/// </summary>
public sealed class LordCourtesiesDialogBehavior : CampaignBehaviorBase
{
    private readonly LordCourtesiesService _service;
    private DomainOutcome _outcome = DomainOutcome.Unavailable;

    public LordCourtesiesDialogBehavior(LordCourtesiesService service)
    {
        _service = service;
    }

    public override void RegisterEvents() =>
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore) { }

    private void OnSessionLaunched(CampaignGameStarter starter) => LordCourtesiesDialogLines.Register(starter, this);

    internal bool CanGift() => _service.CanGift();

    internal void Gift() => _service.QueueGift();

    internal bool CanAskDomain() => _service.CanAskDomain();

    internal void AskDomain() => _outcome = _service.AskDomain();

    internal bool Answered(DomainOutcome outcome) => _outcome == outcome;

    internal bool AnsweredWithRefusal() => LordCourtesiesService.RepliesWithRefusal(_outcome);
}
