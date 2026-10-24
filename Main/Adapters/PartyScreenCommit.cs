using System;
using Helpers;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// Commits the party screen left open under a conversation, so a roster, inventory or hero-state write made in the conversation
/// survives leaving the screen with Escape (<c>PartyScreenData.ResetUsing</c>; adapters.md, "Relations and party-screen writes").
/// The commit protects the item roster as well as the prisoners (a gift, the strip, a defection's release). Only a screen whose
/// right owner is the main party is committed. The ransom screen binds the live item roster but cloned troop rosters, and its done
/// handler sells the broker's side again, so that one case (ransom mode with prisoners on the left) is skipped; every other mode
/// is idempotent. Never throws: the callers run inside engine events and conversation callbacks that have no try of their own.
/// </summary>
internal static class PartyScreenCommit
{
    internal static void CommitIfMainParty(IModLogger logger, string tag)
    {
        try
        {
            var state = GameStateManager.Current?.LastOrDefault<PartyState>();
            var logic = state?.PartyScreenLogic;
            if (logic == null || logic.RightOwnerParty != PartyBase.MainParty)
                return;
            if (state!.PartyScreenMode == PartyScreenHelper.PartyScreenMode.Ransom && logic.PrisonerRosters[(int)PartyScreenLogic.PartyRosterSide.Left].TotalManCount > 0)
                return;
            logic.DoneLogic(isForced: true);
        }
        catch (Exception ex)
        {
            logger.LogError($"[{tag}] Committing the open party screen failed: {ex.Message}");
        }
    }
}
