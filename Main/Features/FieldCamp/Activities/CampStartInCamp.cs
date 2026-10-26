using System;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.FieldCamp.Domain;

namespace TAOM.Features.FieldCamp.Activities;

/// <summary>
/// Starting a new campaign already in camp (#860 part C). The engine raises <c>CampaignEvents.OnCharacterCreationIsOverEvent</c> from
/// <c>CharacterCreationState.FinalizeCharacterCreationState</c>, after the map state is pushed and the main party exists, and
/// raises it ten times in a row (once for each index 0 to 9), so arming is idempotent. The camp is pitched on the first map tick
/// after that, which lands the player on the map. One attempt: a blocked start shows one message with the reason and gives up. A
/// load raises no such event, so a loaded save never arms. Built fresh for every campaign start by the behavior's module, so the
/// state is per campaign. Single-player only.
/// </summary>
internal sealed class CampStartInCamp
{
    private enum State { Idle, Armed, Done }

    private readonly ICampService _camps;
    private readonly ICampSettingsProvider _settings;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _dedicated;
    private readonly IModLogger _logger;
    private readonly Action<CampBlockReason> _onGaveUp;
    private State _state;

    /// <param name="onGaveUp">Told the reason when the camp could not be pitched, once; the hook side turns it into the player's message.</param>
    public CampStartInCamp(ICampService camps, ICampSettingsProvider settings, ICoopSessionProvider coop,
        IDedicatedServerProvider dedicated, IModLogger logger, Action<CampBlockReason> onGaveUp)
    {
        _camps = camps;
        _settings = settings;
        _coop = coop;
        _dedicated = dedicated;
        _logger = logger;
        _onGaveUp = onGaveUp;
    }

    /// <summary>Arms the start when the setting is on. Safe to call ten times; a hook that has already tried stays done.</summary>
    public void OnCharacterCreationIsOver(int index)
    {
        if (_state != State.Idle || !_settings.CampStartInCamp) return;
        _state = State.Armed;
    }

    // TickEvent runs every frame: an unarmed hook costs one comparison.
    public void OnMapTick(float dt = 0f)
    {
        if (_state != State.Armed) return;
        _state = State.Done;
        try
        {
            Pitch();
        }
        catch (Exception ex)
        {
            _logger.LogError($"[FieldCamp] start in camp failed and gave up: {ex}");
        }
    }

    private void Pitch()
    {
        if (!CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _dedicated)) return;

        // Only the town and castle distance is waived: every default start stands near its town gate, so the rule would block them all.
        var reason = _camps.CanEstablish(CampType.Field, ignoreTownDistance: true);
        if (reason == CampBlockReason.FeatureDisabled) return;
        if (reason == CampBlockReason.None && _camps.Establish(CampType.Field, ignoreTownDistance: true))
        {
            _logger.LogInfo("[FieldCamp] start in camp: established a Field camp on the first map tick, armed by CampaignEvents.OnCharacterCreationIsOverEvent");
            return;
        }

        // Blocked, or Establish refused after the check: one message, no retry.
        _logger.LogInfo($"[FieldCamp] start in camp: gave up ({reason}), armed by CampaignEvents.OnCharacterCreationIsOverEvent");
        _onGaveUp(reason);
    }
}
