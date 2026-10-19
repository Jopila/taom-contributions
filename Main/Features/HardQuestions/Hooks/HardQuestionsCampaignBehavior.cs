using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.HardQuestions.Hooks;

/// <summary>
/// Thin campaign boundary for Hard Questions (ADR-002): saves and restores <see cref="HardQuestionsState"/>
/// and, when a lord's captivity ends, gives a stripped lord a fresh kit and forgets his used topics. Built
/// fresh for every campaign by the feature module and resets the state in its constructor, which runs before
/// the load's <c>SyncData</c> (csharp-architecture.md, session-reset rule).
/// </summary>
public sealed class HardQuestionsCampaignBehavior : CampaignBehaviorBase
{
    internal const string SaveKey = "_taom_hardQuestions";

    private readonly HardQuestionsState _state;
    private readonly StripService _strip;
    private readonly ICoopSessionProvider _coop;
    private readonly IModLogger _logger;

    public HardQuestionsCampaignBehavior(HardQuestionsState state, StripService strip, ICoopSessionProvider coop,
        IModLogger logger)
    {
        _state = state;
        _strip = strip;
        _coop = coop;
        _logger = logger;
        _state.Reset();
    }

    public override void RegisterEvents()
    {
        CampaignEvents.HeroPrisonerReleased.AddNonSerializedListener(this, OnHeroPrisonerReleased);
        CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
    }

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var data = _state.Encode();
            dataStore.SyncData(SaveKey, ref data);
            return;
        }
        // A key miss leaves the ref unchanged, so load into a fresh dictionary: a save made before this
        // feature existed then decodes to an empty state.
        var loaded = new Dictionary<string, string>();
        dataStore.SyncData(SaveKey, ref loaded);
        var skipped = _state.Decode(loaded);
        if (skipped > 0)
            _logger.LogWarning($"[HardQuestions] {skipped} damaged save entr(ies) were dropped while loading.");
    }

    private void OnHeroPrisonerReleased(Hero? hero, PartyBase? party, IFaction? captorFaction, EndCaptivityDetail detail, bool showNotification)
    {
        if (hero != null)
            ReleaseHero(hero.StringId);
    }

    // A lord who dies in captivity is never released (EndCaptivityAction returns at Death before raising the
    // event), so his record is dropped here. Fires for every death in the world: no lookups, one removal.
    private void OnHeroKilled(Hero? victim, Hero? killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
    {
        if (victim != null)
            HeroDied(victim.StringId);
    }

    internal void HeroDied(string heroId)
    {
        if (!_coop.ShouldDeferToHost)
            _strip.OnDied(heroId);
    }

    internal void ReleaseHero(string heroId)
    {
        if (!_coop.ShouldDeferToHost)
            _strip.OnReleased(heroId);
    }
}
