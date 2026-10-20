using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.TokensOfEsteem.Hooks;

/// <summary>
/// Thin dialog boundary for Tokens of Esteem (ADR-002, docs/features/tokens-of-esteem.md): at the session start it binds the
/// lines (<see cref="GiftDialogLines"/>) to this campaign's <see cref="GiftConversationFlow"/>, which holds every rule's
/// hand-off and the cooldown ledger, and it saves that ledger in <c>SyncData</c>. The module builds this with <c>new</c> for
/// every campaign, so a new campaign starts with an empty ledger and a load restores its own over it.
/// </summary>
public sealed class GiftBehavior : CampaignBehaviorBase
{
    internal const string SaveKey = "_taom_gifts";

    private readonly GiftConversationFlow _flow;
    private readonly IModLogger _logger;

    public GiftBehavior(GiftService service, IGiftConversationAdapter conversation, ICoopSessionProvider coop, IModLogger logger)
    {
        _flow = new GiftConversationFlow(service, conversation, coop, logger);
        _logger = logger;
    }

    internal GiftCooldownLedger Ledger => _flow.Ledger;

    public override void RegisterEvents() =>
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);

    public override void SyncData(IDataStore dataStore)
    {
        if (dataStore.IsSaving)
        {
            var data = Ledger.Encode();
            dataStore.SyncData(SaveKey, ref data);
            return;
        }
        // A key miss leaves the ref unchanged, so load into a fresh dictionary: a save made before this feature decodes empty.
        var loaded = new Dictionary<string, string>();
        dataStore.SyncData(SaveKey, ref loaded);
        var skipped = Ledger.Decode(loaded);
        if (skipped > 0)
            _logger.LogWarning($"[Gifts] {skipped} damaged save entr(ies) were dropped while loading.");
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        GiftDialogLines.Register(starter, _flow);
    }
}
