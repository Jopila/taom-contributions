using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CoopInterop;
using TAOM.Features.HardQuestions.Domain;

namespace TAOM.Features.HardQuestions;

/// <summary>
/// Strips a captive lord: every piece he carries goes to the player's inventory except the named hero
/// pieces (<see cref="ArmourClass.Named"/>), he is left in rags, the deed costs Honor, renown and relations,
/// and on release he gets a fresh kit. A piece held in several of his sets is looted once.
/// </summary>
public sealed class StripService
{
    internal const string RagItemId = "torn_bandit_clothes";

    /// <summary>The body armour slot, <c>EquipmentIndex.Body</c> (pinned by a test).</summary>
    internal const int BodySlot = 6;

    internal const int MaxReproaches = 3;

    private readonly HardQuestionsState _state;
    private readonly IHardQuestionsConfigProvider _config;
    private readonly ICaptiveLordAdapter _captives;
    private readonly ILordEquipmentAdapter _equipment;
    private readonly IArmouryPlayerAdapter _player;
    private readonly IArmourGateService _gate;
    private readonly IHardQuestionsActionAdapter _actions;
    private readonly HardQuestionsPenaltyApplier _penalties;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    public StripService(HardQuestionsState state, IHardQuestionsConfigProvider config, ICaptiveLordAdapter captives,
        ILordEquipmentAdapter equipment, IArmouryPlayerAdapter player, IArmourGateService gate,
        IHardQuestionsActionAdapter actions, HardQuestionsPenaltyApplier penalties, IDedicatedServerProvider server,
        IModLogger logger)
    {
        _state = state;
        _config = config;
        _captives = captives;
        _equipment = equipment;
        _player = player;
        _gate = gate;
        _actions = actions;
        _penalties = penalties;
        _server = server;
        _logger = logger;
    }

    /// <summary>Strips the lord now; the caller has checked eligibility and marked him stripped. False when he is gone.</summary>
    public bool Strip(string heroId)
    {
        var circle = _captives.Circle(heroId);
        if (circle == null)
        {
            _logger.LogWarning($"[HardQuestions] {heroId} is no longer a captive; the strip was dropped.");
            return false;
        }

        TakeKit(heroId);
        PayTheCost(circle);
        return true;
    }

    /// <summary>A released lord gets a fresh kit if he was stripped; either way his topics reset.</summary>
    public void OnReleased(string heroId)
    {
        if (_state.IsStripped(heroId) && _captives.IsAlive(heroId) && !_equipment.GiveFreshKit(heroId, RagItemId))
            _logger.LogWarning($"[HardQuestions] {heroId} was released in rags and the engine gave no fresh kit.");
        _state.ClearHero(heroId);
    }

    /// <summary>A lord who died in captivity is never released, so his record is dropped here.</summary>
    public void OnDied(string heroId) => _state.ClearHero(heroId);

    private void TakeKit(string heroId)
    {
        var groups = _equipment.ReadPieces(heroId)
            .Where(piece => _gate.GetClass(piece.ItemId) != ArmourClass.Named)
            .GroupBy(piece => (piece.ItemId, piece.ModifierId));
        foreach (var group in groups)
        {
            // One sword worn in two sets is one sword (the most any single set holds), two identical stacks
            // in one set are two.
            var count = group.GroupBy(piece => piece.Set).Max(set => set.Count());
            if (!_player.AddPiece(group.Key.ItemId, group.Key.ModifierId, count))
                continue;
            foreach (var piece in group)
                _equipment.ClearSlot(heroId, piece.Set, piece.Slot);
        }

        foreach (var set in new[] { LordEquipmentSet.Battle, LordEquipmentSet.Civilian })
            if (_equipment.IsSlotEmpty(heroId, set, BodySlot))
                _equipment.SetItem(heroId, set, BodySlot, RagItemId);
    }

    private void PayTheCost(CaptiveCircle circle)
    {
        var config = _config.GetConfig();
        _penalties.ApplyTraitXp(circle.Victim, "Honor", config.StripHonorXp);
        _actions.LoseRenown((int)Math.Round(-config.StripRenownLoss));

        var hits = new List<KeyValuePair<LordRef, float>> { new(circle.Victim, config.StripLordRelation) };
        foreach (var lord in circle.ClanLords)
            hits.Add(new(lord, config.StripClanRelation));
        foreach (var lord in circle.KingdomLords)
            hits.Add(new(lord, config.StripKingdomRelation));
        foreach (var lord in circle.HonorPositiveLords)
            hits.Add(new(lord, config.StripHonorLordRelation));
        _penalties.ApplyRelations(circle.Victim, hits);

        var losers = _penalties.ApplyCompanionLosses(circle.Companions, true, config.CompanionHonorMaxLoss);
        if (_server.IsDedicatedServer)
            return;
        for (var i = 0; i < losers.Count && i < MaxReproaches; i++)
            _actions.ShowReproach(losers[i]);
    }
}
