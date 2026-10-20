using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.HardQuestions.Domain;
using TAOM.Features.InitialChildGeneration;

namespace TAOM.Features.HardQuestions;

/// <summary>
/// The two hard questions asked of a captive lord (docs/features/hard-questions.md): his lands, by bribe or by
/// force, and stripping him. The dialog asks the questions here and nothing else: eligibility, price, the bribe,
/// and the queued deed that waits for the conversation to end (a wound, a death or a change of kit must not
/// happen under the lord's own conversation). Conditions and the bribe read only the cheap
/// <see cref="CaptiveSnapshot"/>; the people around the lord are read once per deed.
/// </summary>
public sealed class InterrogationService
{
    private readonly HardQuestionsState _state;
    private readonly IHardQuestionsConfigProvider _config;
    private readonly IHardQuestionsSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;
    private readonly ICaptiveLordAdapter _captives;
    private readonly IArmouryPlayerAdapter _player;
    private readonly IHeroSkillXpAdapter _skills;
    private readonly IHardQuestionsActionAdapter _actions;
    private readonly IMapRevealAdapter _reveal;
    private readonly IRandomSource _random;
    private readonly HardQuestionsPenaltyApplier _penalties;
    private readonly StripService _strip;
    private readonly IModLogger _logger;

    private Action? _pending;

    public InterrogationService(HardQuestionsState state, IHardQuestionsConfigProvider config,
        IHardQuestionsSettingsProvider settings, ICoopSessionProvider coop, ICaptiveLordAdapter captives,
        IArmouryPlayerAdapter player, IHeroSkillXpAdapter skills, IHardQuestionsActionAdapter actions,
        IMapRevealAdapter reveal, IRandomSource random, HardQuestionsPenaltyApplier penalties, StripService strip,
        IModLogger logger)
    {
        _state = state;
        _config = config;
        _settings = settings;
        _coop = coop;
        _captives = captives;
        _player = player;
        _skills = skills;
        _actions = actions;
        _reveal = reveal;
        _random = random;
        _penalties = penalties;
        _strip = strip;
        _logger = logger;
    }

    public void ResetPending() => _pending = null;

    public bool CanAsk(HardQuestionTopic topic, string? partnerId) => Askable(topic, partnerId) != null;

    public bool CanStrip(string? partnerId) =>
        !string.IsNullOrEmpty(partnerId) && !_state.IsStripped(partnerId!) && Eligible(partnerId) != null;

    /// <summary>The bribe, in gold: the lord's ransom times the topic's multiplier, halved for a Calculating lord.</summary>
    public int Quote(HardQuestionTopic topic, string? partnerId)
    {
        var snapshot = Eligible(partnerId);
        return snapshot == null ? 0 : Price(topic, snapshot);
    }

    public bool CanAfford(HardQuestionTopic topic, string? partnerId)
    {
        var price = Quote(topic, partnerId);
        return price > 0 && _player.Gold >= price;
    }

    public BribeReplyKind ReplyKind(string? partnerId)
    {
        var snapshot = Eligible(partnerId);
        if (snapshot == null)
            return BribeReplyKind.Plain;
        return snapshot.CalculatingLevel > 0 ? BribeReplyKind.Calculating
            : snapshot.ValorLevel > 0 ? BribeReplyKind.Valor
            : BribeReplyKind.Plain;
    }

    /// <summary>Pays the bribe to the lord, then reveals the lands. False (nothing done) when the player cannot pay.</summary>
    public bool PayBribe(HardQuestionTopic topic, string? partnerId)
    {
        var snapshot = Askable(topic, partnerId);
        if (snapshot == null || !_actions.PayGold(snapshot.Lord.Id, Price(topic, snapshot)))
            return false;

        _state.MarkAsked(topic, snapshot.Lord.Id);
        var config = _config.GetConfig();
        _skills.AddSkillXp(_player.HeroId, "Charm", topic == HardQuestionTopic.Clan ? config.CharmXpClan : config.CharmXpKingdom);
        RevealLands(topic, snapshot.Lord, snapshot.ClanId);
        return true;
    }

    public void QueueForce(HardQuestionTopic topic, string? partnerId)
    {
        if (Askable(topic, partnerId) == null)
            return;
        var heroId = partnerId!;
        _state.MarkAsked(topic, heroId);
        _pending = () =>
        {
            if (Force(topic, heroId))
                _actions.CommitOpenPartyScreen();
        };
    }

    public void QueueStrip(string? partnerId)
    {
        if (!CanStrip(partnerId))
            return;
        var heroId = partnerId!;
        _state.MarkStripped(heroId);
        _pending = () =>
        {
            if (_strip.Strip(heroId))
                _actions.CommitOpenPartyScreen();
        };
    }

    /// <summary>Runs the queued deed after the conversation has ended.</summary>
    public void CompletePending()
    {
        var pending = _pending;
        _pending = null;
        pending?.Invoke();
    }

    /// <summary>
    /// The lord when the topic can still be asked of him: unused, and for the kingdom topic he has a kingdom. The
    /// cheap state check comes before the engine probe.
    /// </summary>
    private CaptiveSnapshot? Askable(HardQuestionTopic topic, string? partnerId)
    {
        if (string.IsNullOrEmpty(partnerId) || _state.IsAsked(topic, partnerId!))
            return null;
        var snapshot = Eligible(partnerId);
        if (snapshot == null)
            return null;
        return topic == HardQuestionTopic.Kingdom && string.IsNullOrEmpty(snapshot.Lord.KingdomId) ? null : snapshot;
    }

    private CaptiveSnapshot? Eligible(string? partnerId)
    {
        if (string.IsNullOrEmpty(partnerId) || !_settings.IsEnabled || _coop.ShouldDeferToHost
            || _captives.IsPrisonerDecisionContext)
            return null;
        var snapshot = _captives.Snapshot(partnerId!);
        return snapshot != null && snapshot.CaptorIsPlayerClan && !snapshot.IsInPlayerClan ? snapshot : null;
    }

    private int Price(HardQuestionTopic topic, CaptiveSnapshot snapshot)
    {
        var config = _config.GetConfig();
        var multiplier = topic == HardQuestionTopic.Clan ? config.BribeMultiplierClan : config.BribeMultiplierKingdom;
        double price = Math.Max(0, snapshot.RansomValue) * (double)multiplier;
        if (snapshot.CalculatingLevel > 0)
            price *= config.CalculatingBribeFactor;
        return (int)Math.Min(Math.Max(1d, Math.Round(price)), int.MaxValue / 2);
    }

    private bool Force(HardQuestionTopic topic, string heroId)
    {
        var circle = _captives.Circle(heroId);
        var snapshot = circle == null ? null : _captives.Snapshot(heroId);
        if (circle == null || snapshot == null)
        {
            _logger.LogWarning($"[HardQuestions] {heroId} is no longer a captive; the forced question was dropped.");
            return false;
        }

        var config = _config.GetConfig();
        var clan = topic == HardQuestionTopic.Clan;
        var dies = !clan && snapshot.IsWounded && _random.NextDouble() < config.DeathChance;
        var wounded = !snapshot.IsWounded && _random.NextDouble() < config.WoundChance;

        var hits = new List<KeyValuePair<LordRef, float>>
        {
            new(circle.Victim, clan ? config.ForceLordRelationClan : config.ForceLordRelationKingdom),
        };
        if (circle.ClanLeader != null)
            hits.Add(new(circle.ClanLeader, clan ? config.ForceClanLeaderRelationClan : config.ForceClanLeaderRelationKingdom));
        foreach (var kin in circle.Family)
            hits.Add(new(kin, clan ? config.ForceFamilyRelationClan : config.ForceFamilyRelationKingdom));
        if (!clan && circle.KingdomLeader != null)
            hits.Add(new(circle.KingdomLeader, config.ForceKingRelationKingdom));
        _penalties.ApplyRelations(circle.Victim, hits);

        _penalties.ApplyTraitXp(circle.Victim, "Mercy", clan ? config.ForceMercyXpClan : config.ForceMercyXpKingdom);
        _penalties.ApplyTraitXp(circle.Victim, "Honor", clan ? config.ForceHonorXpClan : config.ForceHonorXpKingdom);
        _skills.AddSkillXp(_player.HeroId, "Roguery", clan ? config.RogueryXpClan : config.RogueryXpKingdom);
        _penalties.ApplyCompanionLosses(circle.Companions, false, config.CompanionMercyMaxLoss);

        // The relation changes above go first: they read the victim's clan, which a death can remove. The
        // extra Honor cost follows only a death that happened (the engine may veto it).
        if (dies && _actions.KillByWounds(heroId))
        {
            _penalties.ApplyTraitXp(circle.Victim, "Honor", config.DeathExtraHonorXp);
            return true;
        }
        RevealLands(topic, circle.Victim, snapshot.ClanId);
        if (wounded)
            _actions.Wound(heroId);
        return true;
    }

    private void RevealLands(HardQuestionTopic topic, LordRef lord, string? clanId)
    {
        if (topic == HardQuestionTopic.Kingdom)
        {
            if (!string.IsNullOrEmpty(lord.KingdomId))
                _reveal.RevealKingdom(lord.KingdomId!);
        }
        else if (!string.IsNullOrEmpty(clanId))
            _reveal.RevealClan(clanId!);
    }
}
