using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.LongMemory.Hooks;

/// <summary>
/// Thin boundary for the relation Long Memory takes from four campaign events (docs/features/long-memory.md, ADR-002).
/// <c>RaidCompletedEvent</c>, <c>ForceSuppliesCompletedEvent</c> and <c>ForceVolunteersCompletedEvent</c> carry the map event's
/// component: the adapter reads the village facts from it and the service decides. A forced taking whose villagers give way fires
/// no map event, so <c>GameMenuOptionSelectedEvent</c> (raised after the option's consequence) hands the service the selected
/// option's id, and the service reads the village only for the two pinned no-resist ids. No Harmony: every route is an event.
/// Each body is inside a try, because an <c>MbEvent</c> invokes its listeners with no try and a fault here would reach the engine's
/// raid or menu code. Saves nothing, and the service holds no per-campaign state; the module still builds this behavior with
/// <c>new</c> for every campaign, as the campaign registers the events on the instance.
/// </summary>
public sealed class LongMemoryBehavior : CampaignBehaviorBase
{
    private readonly LongMemoryService _service;
    private readonly ILongMemoryVillageAdapter _village;
    private readonly IModLogger _logger;

    public LongMemoryBehavior(LongMemoryService service, ILongMemoryVillageAdapter village, IModLogger logger)
    {
        _service = service;
        _village = village;
        _logger = logger;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.RaidCompletedEvent.AddNonSerializedListener(this, OnRaidCompleted);
        CampaignEvents.ForceSuppliesCompletedEvent.AddNonSerializedListener(this, OnForceSuppliesCompleted);
        CampaignEvents.ForceVolunteersCompletedEvent.AddNonSerializedListener(this, OnForceVolunteersCompleted);
        CampaignEvents.GameMenuOptionSelectedEvent.AddNonSerializedListener(this, OnGameMenuOptionSelected);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    // Internal for TAOM.Tests (InternalsVisibleTo): the events cannot be raised outside a campaign.
    internal void OnRaidCompleted(BattleSideEnum side, RaidEventComponent raid) => HandleRaid(raid);

    internal void OnForceSuppliesCompleted(BattleSideEnum side, ForceSuppliesEventComponent component) =>
        HandleCoercion(side == BattleSideEnum.Attacker, component);

    internal void OnForceVolunteersCompleted(BattleSideEnum side, ForceVolunteersEventComponent component) =>
        HandleCoercion(side == BattleSideEnum.Attacker, component);

    internal void OnGameMenuOptionSelected(GameMenu menu, GameMenuOption option) => HandleMenuOption(option?.IdString);

    internal void HandleRaid(MapEventComponent? raid)
    {
        try
        {
            _service.OnRaidCompleted(() => _village.ReadMapEvent(raid));
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LongMemory] raid handling failed: {ex.Message}");
        }
    }

    internal void HandleCoercion(bool attackerWon, MapEventComponent? component)
    {
        // The service reads the event only for an attacker win: a village that beat the forcing party costs the player nothing.
        try
        {
            _service.OnCoercionCompleted(attackerWon, () => _village.ReadMapEvent(component));
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LongMemory] forced taking handling failed: {ex.Message}");
        }
    }

    internal void HandleMenuOption(string? optionId)
    {
        try
        {
            _service.OnMenuOptionSelected(optionId, _village.ReadCurrentVillage);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[LongMemory] menu option handling failed: {ex.Message}");
        }
    }
}
