using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.TokensOfEsteem.Models;
using TAOM.Features.Relations;

namespace TAOM.Features.TokensOfEsteem;

/// <summary>
/// Tokens of Esteem (docs/features/tokens-of-esteem.md, #863): every rule of a gift, over raw facts the adapters hand in.
/// <list type="bullet">
/// <item><b>Gate.</b> No line for a disabled feature (nothing is read then), a prisoner, a hero who is neither a companion in
/// the party nor an adult lord outside the player's clan, or a lord at war with the player. Then, in order: the cooldown (the line shows, greyed), a relation below -50 (the lord refuses and
/// the relation drops by 1, charged once by <see cref="Ask"/>), a Free lord against an Evil player (refuses, no penalty).</item>
/// <item><b>Gain.</b> A companion's base comes from the item's value (<see cref="CompanionBase"/>), a purse's from its gold
/// (<see cref="PurseGain"/>), a lord's from the catalog. The best matching taste adds its bonus. Above relation 0 the sum
/// scales by (90 - relation) / 90 and rounds; the result is capped (<see cref="ComputeGain"/>).</item>
/// <item><b>Payment.</b> One item from the unmodified stack, or a companion's gold purse; the gold leaves the player. Then the
/// cooldown starts, keyed on the hero whose relation the gift moves (<see cref="GiftRecipientFacts.RelationOwnerId"/>).</item>
/// </list>
/// The service keeps no state: the cooldown ledger belongs to the campaign's flow, which passes it in on every call, so a new
/// campaign or a load cannot inherit another's days.
/// </summary>
public sealed class GiftService
{
    /// <summary>A lord whose relation with the player is below this refuses every gift.</summary>
    internal const int HostileBelow = -50;

    /// <summary>The relation a refusing lord takes away.</summary>
    internal const int HostilePenalty = -1;

    /// <summary>The relation at which scaling reaches 0: a gift adds (this minus relation) / this of its worth.</summary>
    internal const int ScaleCeiling = 90;

    private static readonly GiftGate Hidden = new(GiftGateKind.Hidden);

    private static readonly GiftGate Open = new(GiftGateKind.Open);

    private readonly IGiftHeroAdapter _heroes;
    private readonly IGiftItemAdapter _items;
    private readonly IPartyItemRosterAdapter _roster;
    private readonly IGiftCatalogProvider _catalog;
    private readonly IGiftSettingsProvider _settings;
    private readonly IAlignmentService _alignment;
    private readonly IRelationGrantService _relations;
    private readonly IModLogger _logger;

    public GiftService(IGiftHeroAdapter heroes, IGiftItemAdapter items, IPartyItemRosterAdapter roster,
        IGiftCatalogProvider catalog, IGiftSettingsProvider settings, IAlignmentService alignment,
        IRelationGrantService relations, IModLogger logger)
    {
        _heroes = heroes;
        _items = items;
        _roster = roster;
        _catalog = catalog;
        _settings = settings;
        _alignment = alignment;
        _relations = relations;
        _logger = logger;
    }

    /// <summary>
    /// A companion's base gain from one item's value in gold: 1 under 100, 2 from 100, 3 from 300, 4 from 600 and 5 from 1000.
    /// </summary>
    public static int CompanionBase(int itemValue)
    {
        if (itemValue >= 1000) return 5;
        if (itemValue >= 600) return 4;
        if (itemValue >= 300) return 3;
        if (itemValue >= 100) return 2;
        return 1;
    }

    /// <summary>One point of purse gain per this much gold.</summary>
    public const int PurseGoldPerPoint = 500;

    /// <summary>The most a purse is worth, whatever it holds.</summary>
    public const int PurseMaxPoints = 10;

    /// <summary>
    /// A gold purse's base gain: one point per <see cref="PurseGoldPerPoint"/> gold, at most <see cref="PurseMaxPoints"/>. Its own
    /// band, so gold is not priced like an item (an item worth 1000 gold is 5; a purse of 1000 gold is 2).
    /// </summary>
    public static int PurseGain(int gold) => Math.Max(0, Math.Min(PurseMaxPoints, gold / PurseGoldPerPoint));

    /// <summary>
    /// The relation a gift is worth: the base plus the taste bonus; above relation 0, times (90 - relation) / 90
    /// rounded half away from zero (so a 1-point gift still works at 45 and stops at 46) and 0 from relation 90; then capped.
    /// Never negative. Done in <c>long</c> so a hand-fed extreme cannot overflow.
    /// </summary>
    public static int ComputeGain(int baseGain, int tasteBonus, int relation, int cap)
    {
        if (baseGain <= 0 || cap <= 0)
            return 0;

        long worth = (long)baseGain + Math.Max(0, tasteBonus);
        if (relation > 0)
        {
            if (relation >= ScaleCeiling)
                return 0;
            worth = (long)Math.Round(worth * (double)(ScaleCeiling - relation) / ScaleCeiling, MidpointRounding.AwayFromZero);
        }
        return (int)Math.Max(0, Math.Min(worth, cap));
    }

    /// <summary>What the gift line does for this hero now. Reads facts; changes nothing.</summary>
    public GiftGate Gate(string heroId, GiftEntry entry, GiftCooldownLedger ledger)
    {
        var settings = _settings.Get();
        if (!settings.Enabled)
            return Hidden;

        var player = _heroes.ReadPlayer();
        var recipient = _heroes.ReadRecipient(heroId);
        return player == null || recipient == null ? Hidden : Evaluate(settings, player, recipient, entry, ledger);
    }

    /// <summary>
    /// The gate, for the moment the player chooses the line. A lord below -50 takes <see cref="HostilePenalty"/> relation, once
    /// per call; nothing else is charged.
    /// </summary>
    public GiftGate Ask(string heroId, GiftEntry entry, GiftCooldownLedger ledger)
    {
        var gate = Gate(heroId, entry, ledger);
        if (gate.Kind == GiftGateKind.RefusesHostile)
        {
            var landed = _relations.Grant(heroId, HostilePenalty, "gift refused: relation below " + HostileBelow);
            if (landed != 0)
                _heroes.ShowRelationChange(heroId, landed);
        }
        return gate;
    }

    /// <summary>
    /// The list the player picks from: the catalog goods in the unmodified stacks (a modified stack is skipped), and for a
    /// companion the gold purses
    /// (ascending, one line per size). Empty for an unknown hero. Changes nothing.
    /// </summary>
    public IReadOnlyList<GiftOption> Options(string heroId, GiftEntry entry)
    {
        var settings = _settings.Get();
        var player = _heroes.ReadPlayer();
        var recipient = _heroes.ReadRecipient(heroId);
        return player == null || recipient == null ? Array.Empty<GiftOption>() : Build(settings, player, recipient, entry);
    }

    /// <summary>
    /// Gives one gift. The facts and the list are read again, so a stale option, a changed relation or a closed gate cannot
    /// slip through. Once the item or the gold has left, the cooldown starts on the ledger, keyed on the relation owner.
    /// </summary>
    public GiftOutcome Give(string heroId, GiftEntry entry, GiftOption option, GiftCooldownLedger ledger)
    {
        var settings = _settings.Get();
        if (!settings.Enabled)
            return GiftOutcome.Failed;

        var player = _heroes.ReadPlayer();
        var recipient = _heroes.ReadRecipient(heroId);
        if (option == null || player == null || recipient == null
            || Evaluate(settings, player, recipient, entry, ledger).Kind != GiftGateKind.Open)
            return GiftOutcome.Failed;

        var fresh = Build(settings, player, recipient, entry).FirstOrDefault(o => Same(o, option));
        if (fresh == null || fresh.Block != GiftBlock.None)
            return GiftOutcome.Failed;

        if (fresh.Kind == GiftOptionKind.Good)
        {
            if (_roster.RemoveItem(fresh.ItemId, 1) < 1)
                return GiftOutcome.Failed;
            // A later troop change on a party screen still open under this conversation, then Cancel, would hand the item back.
            _items.CommitOpenPartyScreen();
        }
        else if (!_heroes.PayGoldToHero(heroId, fresh.Amount))
        {
            return GiftOutcome.Failed;
        }

        ledger.Stamp(CooldownKey(recipient), player.Today);
        var what = fresh.Kind == GiftOptionKind.Good ? fresh.ItemId : fresh.Amount + " gold";
        var landed = _relations.Grant(heroId, fresh.Gain, "gift of " + what);
        if (landed != 0)
            _heroes.ShowRelationChange(heroId, landed);
        _logger.LogInfo($"[Gifts] {heroId} took {what}: asked {fresh.Gain}, landed {landed}, suited={fresh.Suited}.");
        return new GiftOutcome(GiftResult.Given, fresh.Suited, landed);
    }

    // A relation owner's id; the hero's own when the adapter could not say.
    private static string CooldownKey(GiftRecipientFacts recipient) =>
        string.IsNullOrEmpty(recipient.RelationOwnerId) ? recipient.HeroId : recipient.RelationOwnerId;

    private static bool Same(GiftOption a, GiftOption b) =>
        a.Kind == b.Kind && (a.Kind == GiftOptionKind.Purse ? a.Amount == b.Amount : a.ItemId == b.ItemId);

    private GiftGate Evaluate(GiftSettings settings, GiftPlayerFacts player, GiftRecipientFacts recipient, GiftEntry entry, GiftCooldownLedger ledger)
    {
        // A companion keeps the companion branch even though he belongs to the player's clan; a lord of that clan is not a courtesy.
        var fits = entry == GiftEntry.Companion
            ? recipient.IsPlayerCompanion && recipient.IsInPlayerParty
            : recipient.IsLord && !recipient.IsChild && !recipient.IsInPlayerClan;
        if (!settings.Enabled || !fits || recipient.IsPrisoner || recipient.IsAtWarWithPlayer)
            return Hidden;

        if (ledger.LastGiftDay(CooldownKey(recipient)) is { } last)
        {
            var elapsed = (long)player.Today - last;
            // A stamp from the future (a damaged save) counts as over, so a bad entry cannot lock a hero out.
            if (elapsed >= 0 && elapsed < settings.CooldownDays)
                return new GiftGate(GiftGateKind.Cooldown, (int)(settings.CooldownDays - elapsed));
        }

        if (entry == GiftEntry.Lord)
        {
            if (recipient.Relation < HostileBelow)
                return new GiftGate(GiftGateKind.RefusesHostile);
            if (settings.AlignmentRefusalEnabled && RefusesByAlignment(player, recipient))
                return new GiftGate(GiftGateKind.RefusesAlignment);
        }
        return Open;
    }

    // Free lord, Evil player. AreEnemyAlignments is not the test: it counts Neutral as everyone's enemy, which would make
    // every Khand or Umbar lord and player refuse (the Hard Questions trap, lessons/testing-qa.md 2026-10-19).
    private bool RefusesByAlignment(GiftPlayerFacts player, GiftRecipientFacts recipient) =>
        _alignment.ResolveSide(recipient.Side) == FactionSide.Free
        && _alignment.ResolveSide(player.Side) == FactionSide.Evil;

    private IReadOnlyList<GiftOption> Build(GiftSettings settings, GiftPlayerFacts player, GiftRecipientFacts recipient, GiftEntry entry)
    {
        var options = new List<GiftOption>();
        var catalog = _catalog.Get();
        if (catalog.Count > 0)
        {
            foreach (var good in _items.ReadHeldGoods(catalog.CategoryIds))
            {
                // A modified copy is not the stack the roster adapter removes from: never list one.
                if (good.Count <= 0 || good.IsModified || !catalog.TryGet(good.CategoryId, out var listed))
                    continue;
                var baseGain = entry == GiftEntry.Companion ? CompanionBase(good.Value) : listed.LordBase;
                var taste = listed.TasteBonus(recipient.RaceName, recipient.CultureId);
                var gain = ComputeGain(baseGain, taste, recipient.Relation, settings.RelationCap);
                options.Add(new GiftOption(GiftOptionKind.Good, good.ItemId, good.Name, good.Count, 0, baseGain, taste, gain,
                    gain > 0 ? GiftBlock.None : GiftBlock.NoEffect));
            }
        }

        if (entry == GiftEntry.Companion)
        {
            foreach (var amount in new[] { settings.PurseSmall, settings.PurseMedium, settings.PurseLarge }.Distinct().OrderBy(a => a))
            {
                var baseGain = PurseGain(amount);
                var gain = ComputeGain(baseGain, 0, recipient.Relation, settings.RelationCap);
                var block = player.Gold < amount ? GiftBlock.CannotAfford : gain > 0 ? GiftBlock.None : GiftBlock.NoEffect;
                options.Add(new GiftOption(GiftOptionKind.Purse, string.Empty, string.Empty, 0, amount, baseGain, 0, gain, block));
            }
        }
        return options;
    }
}
