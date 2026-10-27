using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.Messengers;
using TAOM.Features.NotableServices.Domain;
using TAOM.Features.Relations;

namespace TAOM.Features.NotableServices;

/// <summary>
/// Every rule of Notable Services (docs/features/notable-services.md): the three lines a notable of the player's settlement
/// offers. Give men to the settlement (troops raise its militia, prisoners its hearth or prosperity, and the notable thinks
/// better of the player), pay to be introduced to the other notables, and pay to have the nearest holds marked on the map.
/// It works on snapshots and ids and reaches the engine only through <see cref="INotableServicesAdapter"/>; the rules of the
/// holds line are <see cref="HoldsRevealService"/>'s, which the tavern keeper's line shares. The conditions (<see cref="OffersGift"/>, <see cref="QuoteIntroduce"/>,
/// <see cref="OffersReveal"/>, <see cref="CanAffordReveal"/>) never write; <see cref="QueueGift"/>, <see cref="Introduce"/> and
/// <see cref="Reveal"/> are the only members that do. The service keeps no campaign state.
/// </summary>
public sealed class NotableServicesService
{
    private const string Tag = "[NotableServices]";
    private const string RoguerySkillId = "Roguery";

    private readonly INotableServicesAdapter _adapter;
    private readonly HoldsRevealService _holds;
    private readonly IAlignmentService _alignment;
    private readonly IRelationGrantService _grants;
    private readonly IHeroSkillXpAdapter _skills;
    private readonly INotableServicesSettingsProvider _settings;
    private readonly INotableServicesConfigProvider _config;
    private readonly IMessengerConversationState _messenger;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    public NotableServicesService(INotableServicesAdapter adapter, HoldsRevealService holds, IAlignmentService alignment,
        IRelationGrantService grants, IHeroSkillXpAdapter skills, INotableServicesSettingsProvider settings, INotableServicesConfigProvider config,
        IMessengerConversationState messenger, ICoopSessionProvider coop, IDedicatedServerProvider server, IModLogger logger)
    {
        _adapter = adapter;
        _holds = holds;
        _alignment = alignment;
        _grants = grants;
        _skills = skills;
        _settings = settings;
        _config = config;
        _messenger = messenger;
        _coop = coop;
        _server = server;
        _logger = logger;
    }

    /// <summary>
    /// The relation a gift earns: <c>min(maxGainPerGift, men x gainPerMan / max(1, 1 + relation / 10))</c>, rounded half away
    /// from zero. A friend is harder to impress, a stranger or an enemy is not; the divisor never drops below 1. A result
    /// under 1 is 0, which the caller never sends.
    /// </summary>
    public static int Gain(int men, float relation, NotableServicesConfig config)
    {
        if (men <= 0)
            return 0;
        var divisor = Math.Max(1f, 1f + relation / 10f);
        var raw = Math.Min(config.MaxGainPerGift, men * config.GainPerMan / divisor);
        return (int)Math.Round(raw, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Writes the player's gold and a settlement's scores, so it runs in single-player only: no live co-op session, no co-op
    /// mod TAOM cannot probe, no dedicated server, and a peer that may write the world. Read live on every use
    /// (ICoopSessionProvider's contract). The same predicate as Settlement Services.
    /// </summary>
    private bool MayWriteWorld => CoopSessionPolicy.IsSinglePlayerAuthority(_coop, _server);

    /// <summary>The gift line is shown: a notable of the player's settlement, not at war, with men to give and no party screen open.</summary>
    public bool OffersGift() => Giftable(Speaker(_adapter.ConversationPartnerId)) != null;

    /// <summary>
    /// The speaker when the gift may be offered or opened. No party screen may be open anywhere on the stack: the gift writes the
    /// live rosters, so an older screen's saved copy would go stale and its Escape would restore the given men while the credits
    /// stay. The player must hold a regular troop or prisoner to give.
    /// </summary>
    private NotableSnapshot? Giftable(NotableSnapshot? speaker) =>
        speaker == null || speaker.AtWarWithPlayer || speaker.Kind == SettlementKind.Other
            || _adapter.PartyScreenIsOpen || !_adapter.PlayerHasMenToGive()
            ? null
            : speaker;

    /// <summary>The notable's colleagues the player has not met: the adapter's list less the speaker himself.</summary>
    private List<string> UnmetColleagues(NotableSnapshot speaker) =>
        _adapter.UnmetNotables(speaker.SettlementId).Where(id => id != speaker.HeroId).ToList();

    public IntroduceQuote? QuoteIntroduce()
    {
        var speaker = Speaker(_adapter.ConversationPartnerId);
        if (speaker == null)
            return null;
        var count = UnmetColleagues(speaker).Count;
        if (count == 0)
            return null;
        var price = count * Whole(_config.GetConfig().IntroducePrice);
        return new IntroduceQuote(count, price, _adapter.PlayerGold >= price);
    }

    public bool OffersReveal() => Speaker(_adapter.ConversationPartnerId) != null;

    public int RevealPrice => _holds.Price;

    public bool CanAffordReveal() => _adapter.PlayerGold >= RevealPrice;

    public void QueueGift()
    {
        var speaker = Giftable(Speaker(_adapter.ConversationPartnerId));
        if (speaker == null)
            return;
        var heroId = speaker.HeroId;
        _adapter.RunAfterConversation(() => OpenGift(heroId));
    }

    public bool Introduce()
    {
        var speaker = Speaker(_adapter.ConversationPartnerId);
        if (speaker == null)
            return false;

        try
        {
            var unmet = UnmetColleagues(speaker);
            if (unmet.Count == 0)
                return false;
            var price = unmet.Count * Whole(_config.GetConfig().IntroducePrice);
            if (_adapter.PlayerGold < price)
                return false;
            if (price > 0 && !_adapter.PayNotable(speaker.HeroId, price))
                return false;

            _adapter.MarkMet(unmet);
            _adapter.ShowIntroducedNotice(unmet.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} The introduction by '{speaker.HeroId}' failed: {ex.Message}");
            return false;
        }
    }

    public RevealOutcome Reveal()
    {
        var speaker = Speaker(_adapter.ConversationPartnerId);
        if (speaker == null)
            return RevealOutcome.Unavailable;

        var ask = new RevealAsk(speaker.SettlementId, speaker.AtWarWithPlayer, speaker.Relation);
        return _holds.Reveal(ask, price => _adapter.PlayerGold >= price && _adapter.PayNotable(speaker.HeroId, price));
    }

    private void OpenGift(string heroId)
    {
        var speaker = Giftable(Speaker(heroId));
        if (speaker == null)
            return;

        var settled = GiftTally.Empty;
        var settlement = speaker.SettlementId;
        _adapter.OpenGiftScreen(settlement, MenCap(),
            transfer => CanTransfer(speaker, transfer),
            tally => tally.Men <= MenCap(),
            tally =>
            {
                var counted = Capped(tally);
                var delta = new GiftTally(Math.Max(0, counted.Troops - settled.Troops), Math.Max(0, counted.Prisoners - settled.Prisoners));
                settled = counted;
                Settle(heroId, delta);
            });
    }

    private bool CanTransfer(NotableSnapshot speaker, GiftTransfer transfer)
    {
        if (transfer.IsHero)
            return false;
        if (!transfer.FromPlayer)
            return true;
        if (transfer.MenGiven >= MenCap())
            return false;
        if (!transfer.IsPrisoner)
            return true;
        if (speaker.Kind == SettlementKind.Castle || speaker.Kind == SettlementKind.Other)
            return false;
        return !RefusesPrisoner(_alignment.ResolveSide(transfer.Side), _alignment.ResolveSide(speaker.SettlementSide));
    }

    // Decision 4 (Mike, 2026-10-24): a Free settlement refuses an Evil prisoner; an Evil settlement takes a Free one, and Neutral
    // on either end takes all.
    internal static bool RefusesPrisoner(FactionSide prisoner, FactionSide settlement) =>
        settlement == FactionSide.Free && prisoner == FactionSide.Evil;

    // Pays what arrived since the last commit; a fault is logged and never keeps the screen from closing.
    private void Settle(string heroId, GiftTally delta)
    {
        if (delta.Men <= 0)
            return;
        try
        {
            var speaker = _adapter.Snapshot(heroId);
            if (speaker == null)
                return;
            var config = _config.GetConfig();

            if (delta.Troops > 0)
                _adapter.AddMilitia(speaker.SettlementId, delta.Troops * config.MilitiaPerTroop);
            if (delta.Prisoners > 0 && speaker.Kind == SettlementKind.Village)
                _adapter.AddHearth(speaker.SettlementId, delta.Prisoners * config.HearthPerPrisoner);
            if (delta.Prisoners > 0 && speaker.Kind == SettlementKind.Town)
                _adapter.AddProsperity(speaker.SettlementId, delta.Prisoners * config.ProsperityPerPrisoner);
            _adapter.ShowGiftNotice(speaker.SettlementId, delta.Men);

            // The grant returns the change that landed (the diplomacy model adds Charm and the scale caps at 100), so the notice
            // shows that, never the ask.
            var gain = Gain(delta.Men, speaker.Relation, config);
            var landed = gain >= 1 ? _grants.Grant(heroId, gain, "Notable Services gift") : 0;
            if (landed != 0)
                _adapter.ShowRelationNotice(heroId, landed);

            var xp = delta.Prisoners * config.RogueryXpPerPrisoner;
            var playerId = _adapter.PlayerHeroId;
            if (xp >= 1f && !string.IsNullOrEmpty(playerId))
            {
                // The engine scales the ask (the XP multiplier and the learning rate), so the notice shows what landed.
                var earned = Whole(_skills.AddSkillXpLanded(playerId!, RoguerySkillId, xp));
                if (earned > 0)
                    _adapter.ShowRogueryNotice(earned);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Settling the gift to '{heroId}' failed: {ex.Message}");
        }
    }

    /// <summary>Troops first, then prisoners, so that no more than <c>maxMenPerGift</c> men are counted.</summary>
    private GiftTally Capped(GiftTally tally)
    {
        var cap = MenCap();
        var troops = Math.Min(tally.Troops, cap);
        return new GiftTally(troops, Math.Min(tally.Prisoners, cap - troops));
    }

    private int MenCap() => Whole(_config.GetConfig().MaxMenPerGift);

    private static int Whole(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    /// <summary>The notable when every gate is open: the switch, single-player, no messenger conversation, a notable who is not a preacher and stands in the player's settlement.</summary>
    private NotableSnapshot? Speaker(string? heroId)
    {
        if (string.IsNullOrEmpty(heroId) || !_settings.Enabled || !MayWriteWorld || _messenger.IsOpen)
            return null;
        var snapshot = _adapter.Snapshot(heroId!);
        return snapshot is { IsNotable: true, IsPreacher: false, InPlayersSettlement: true } && snapshot.SettlementId.Length > 0
            ? snapshot
            : null;
    }
}
