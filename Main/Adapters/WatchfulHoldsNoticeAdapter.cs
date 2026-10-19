using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// The harassment notice and horn. The message is one whole sentence (no fragment slotted into a frame) so each
/// language can inflect it. The horn is a vanilla 2D alert: <c>GetEventIdFromString</c> returns a negative id for an
/// event that is not registered, and a failure of any kind is logged and swallowed, never thrown into the campaign tick.
/// </summary>
public sealed class WatchfulHoldsNoticeAdapter : IWatchfulHoldsNoticeAdapter
{
    private const string HornEvent = "event:/alerts/horns/attack";

    private readonly IModLogger _logger;

    public WatchfulHoldsNoticeAdapter(IModLogger logger) => _logger = logger;

    public void ShowHarried(string fortId, int killed, int wounded, int food)
    {
        var text = new TextObject("{=taom_watchful_holds_harried}The garrison of {FORT} harries your march, slowing it: {KILLED} killed, {WOUNDED} wounded and {FOOD} food lost.");
        text.SetTextVariable("FORT", Settlement.Find(fortId)?.Name ?? new TextObject(fortId));
        text.SetTextVariable("KILLED", killed);
        text.SetTextVariable("WOUNDED", wounded);
        text.SetTextVariable("FOOD", food);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Red));
    }

    public void PlayHorn()
    {
        try
        {
            int id = SoundEvent.GetEventIdFromString(HornEvent);
            if (id < 0)
            {
                _logger.LogWarning($"[WatchfulHolds] horn '{HornEvent}' is not a registered sound event");
                return;
            }
            SoundEvent.PlaySound2D(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[WatchfulHolds] horn '{HornEvent}' failed: {ex.Message}");
        }
    }
}
