using System;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// The raiding hosts' messages (#870). Each is one whole sentence (no fragment slotted into a frame), so a language can
/// inflect it; the host's name is its own keyed string from the data. The horn is a vanilla 2D alert:
/// <c>GetEventIdFromString</c> returns a negative id for an event that is not registered, and a failure of any kind is
/// logged and swallowed, never thrown into the campaign tick.
/// </summary>
public sealed class FireAndTributeNoticeAdapter : IFireAndTributeNoticeAdapter
{
    private const string HornEvent = "event:/alerts/horns/attack";

    private readonly IModLogger _logger;

    public FireAndTributeNoticeAdapter(IModLogger logger) => _logger = logger;

    public void ShowSpawned(string keyedHostName, string? nearestTownId)
    {
        var text = new TextObject("{=taom_fat_spawned}Riders bring word: {HOST} has taken the field near {TOWN}, and villages will burn.");
        text.SetTextVariable("HOST", new TextObject(keyedHostName));
        text.SetTextVariable("TOWN", (nearestTownId != null ? Settlement.Find(nearestTownId)?.Name : null) ?? new TextObject("{=taom_fat_wilds}the wilds"));
        InformationManager.DisplayMessage(new InformationMessage(text.ToString(), Colors.Red));
        PlayHorn();
    }

    public void ShowGoingHome(string keyedHostName) =>
        Show("{=taom_fat_going_home}{HOST} has had its fill and turns for home.", keyedHostName);

    public void ShowWentHome(string keyedHostName) =>
        Show("{=taom_fat_went_home}{HOST} has returned home and scattered.", keyedHostName);

    public void ShowBroken(string keyedHostName) =>
        Show("{=taom_fat_broken}{HOST} is broken. Its survivors scatter.", keyedHostName);

    private static void Show(string keyedSentence, string keyedHostName)
    {
        var text = new TextObject(keyedSentence);
        text.SetTextVariable("HOST", new TextObject(keyedHostName));
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }

    private void PlayHorn()
    {
        try
        {
            int id = SoundEvent.GetEventIdFromString(HornEvent);
            if (id < 0)
            {
                _logger.LogWarning($"[FireAndTribute] horn '{HornEvent}' is not a registered sound event");
                return;
            }
            SoundEvent.PlaySound2D(id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[FireAndTribute] horn '{HornEvent}' failed: {ex.Message}");
        }
    }
}
