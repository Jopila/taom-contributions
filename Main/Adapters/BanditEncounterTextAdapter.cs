using System;
using System.Collections;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// The bandit encounter's facts and its two conversation text variables (#900); vanilla's private <c>_interactedBandits</c> is
/// bound once as an <c>object</c> read as <see cref="IDictionary"/> (its value type is a private enum), and a failed binding logs once and reads as not a first meeting.
/// </summary>
public sealed class BanditEncounterTextAdapter : IBanditEncounterTextAdapter
{
    internal const string GreetingVariable = "BANDIT_NEUTRAL_GREETING";
    internal const string UltimatumVariable = "BANDIT_ULTIMATUM";

    private readonly AccessTools.FieldRef<BanditInteractionsCampaignBehavior, object>? _interacted;

    public BanditEncounterTextAdapter(IModLogger logger) => _interacted = BindInteractedBandits(logger);

    public string? EncounteredFactionId => PlayerEncounter.EncounteredMobileParty?.MapFaction?.StringId;

    public bool EncounteredAtSea => PlayerEncounter.EncounteredMobileParty?.IsCurrentlyAtSea ?? false;

    public bool IsFirstMeeting()
    {
        var accessor = _interacted;
        var behavior = Campaign.Current?.GetCampaignBehavior<BanditInteractionsCampaignBehavior>();
        if (accessor == null || behavior == null)
            return false;

        return FirstMeeting(accessor(behavior), MobileParty.ConversationParty);
    }

    public bool TrySetGreeting(string textId, string variation)
    {
        if (!GameTexts.TryGetText(textId, out var text, variation))
            return false;

        MBTextManager.SetTextVariable(GreetingVariable, text);
        return true;
    }

    public void SetUltimatum(string keyedText) => MBTextManager.SetTextVariable(UltimatumVariable, keyedText);

    /// <summary>Binds vanilla's private record of the parties the player has met; null (and one warning) when the field is gone.</summary>
    internal static AccessTools.FieldRef<BanditInteractionsCampaignBehavior, object>? BindInteractedBandits(IModLogger logger)
    {
        try
        {
            return AccessTools.FieldRefAccess<BanditInteractionsCampaignBehavior, object>("_interactedBandits");
        }
        catch (Exception ex)
        {
            logger.LogWarning("[BanditVoices] BanditInteractionsCampaignBehavior._interactedBandits did not bind, clan greetings are off: " + ex.Message);
            return null;
        }
    }

    /// <summary>True when <paramref name="party"/> is not a key of <paramref name="interacted"/>; false when either is missing.</summary>
    internal static bool FirstMeeting(object? interacted, object? party) =>
        interacted is IDictionary dictionary && party != null && !dictionary.Contains(party);
}
