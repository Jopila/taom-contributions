using TaleWorlds.CampaignSystem;
using TAOM.Features.SwornAnew.Models;

namespace TAOM.Adapters;

/// <summary>
/// The heroes, kingdoms and open screens of Sworn Anew, as plain facts (ADR-007). Every value is raw: a prisoner, a clan under
/// mercenary service, a blood feud are read here, and <c>SwornAnewService</c> decides what each one means. Each read answers null
/// when the campaign, the hero or the kingdom cannot be found, or the engine faults (logged by the adapter).
/// </summary>
public interface ISwornAnewAdapter
{
    /// <summary>The facts about a hero who might join the kingdom (the arguments of <c>DefectionModel.CanHeroDefectToFaction</c>).</summary>
    DefectionFacts? Read(Hero? hero, Kingdom? kingdom);

    /// <summary>The facts about the current conversation partner and the player's own kingdom, null when either is missing.</summary>
    DefectionFacts? ReadConversationPartner();

    /// <summary>
    /// Whether a clan that just changed kingdom is the conversation partner's own: the partner leads it and it joined the player's
    /// kingdom. False without a campaign, a conversation or a partner.
    /// </summary>
    bool IsConversationPartnerJoiningPlayersKingdom(Clan? clan, Kingdom? newKingdom);

    /// <summary>
    /// Queues, once, the commit of the party screen still open under the conversation, to run when the conversation ends (the
    /// conversation manager's <c>ConversationEndOneShot</c>). A conversation started from the party screen leaves the screen's
    /// pre-conversation snapshot alive, and closing it with Escape refills the prison roster from that snapshot, putting a released
    /// lord back in the prison. The queued step never throws; a screen that is not the main party's own is left alone
    /// (<c>PartyScreenCommit</c>). Does nothing without a campaign or a conversation manager.
    /// </summary>
    void CommitOpenPartyScreenWhenConversationEnds();
}
