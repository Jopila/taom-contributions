using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.GoodDeeds.Models;
using TAOM.Features.Relations;

namespace TAOM.Features.GoodDeeds;

/// <summary>
/// Good Deeds (docs/features/good-deeds.md, #864): the relation the player earns for releasing a captured lord, donating a hero
/// prisoner to a settlement and winning a town's tournament. Pure over the raw facts the adapter reads; every rule is decided
/// here, so the three entry points (a campaign behavior with a release and a tournament listener, and a game model) stay thin.
/// <para>
/// Each grant is scaled by the side of the other party against the player's (<see cref="IAlignmentService.ResolveSide"/>, taken
/// from one entity's faction id and culture), rounded half away from zero, and dropped when it scales to 0. Grants are
/// de-duplicated per relation owner, the strongest once, because the engine lands a change on the owner (the clan leader unless
/// personal relations are on) and two grants to kin would otherwise stack on one person. They go through
/// <see cref="IRelationGrantService"/> and are counted by what landed. A donation is the exception: vanilla applies the value
/// the model returns, so <see cref="DonationRelation"/> only answers a number.
/// </para>
/// The service keeps no state, so it needs no per-campaign reset. A co-op peer that defers to the host, and a dedicated server,
/// earn nothing and see vanilla's own donation value.
/// </summary>
public sealed class GoodDeedsService
{
    private const string Tag = "[GoodDeeds]";

    private readonly IGoodDeedsSettingsProvider _settings;
    private readonly IGoodDeedsHeroAdapter _heroes;
    private readonly IGoodDeedsNoticeAdapter _notice;
    private readonly IAlignmentService _alignment;
    private readonly IRelationGrantService _grants;
    private readonly ICoopSessionProvider _coop;
    private readonly IDedicatedServerProvider _server;
    private readonly IModLogger _logger;

    public GoodDeedsService(IGoodDeedsSettingsProvider settings, IGoodDeedsHeroAdapter heroes, IGoodDeedsNoticeAdapter notice,
        IAlignmentService alignment, IRelationGrantService grants, ICoopSessionProvider coop, IDedicatedServerProvider server,
        IModLogger logger)
    {
        _settings = settings;
        _heroes = heroes;
        _notice = notice;
        _alignment = alignment;
        _grants = grants;
        _coop = coop;
        _server = server;
        _logger = logger;
    }

    /// <summary>
    /// A prisoner was released. Only a release by choice counts, from one of three sources (<see cref="ReleaseSource"/>: the
    /// player's own party in conversation, the player's own party screen, or a dungeon of a settlement the player's clan owns),
    /// and only a lord: +5 with the lord, +2 with the lord's living spouse and +2 with the lord's clan leader (the settings
    /// amounts), each scaled by the lord's side against the player's. Returns the number of relation owners whose relation changed.
    /// </summary>
    public int OnPrisonerReleased(string? prisonerId, bool releasedByChoice, ReleaseSource source)
    {
        var fromThePlayer = source.IsMainParty || source.IsPlayerPartyScreen || source.IsPlayerClanSettlement;
        if (string.IsNullOrWhiteSpace(prisonerId) || !releasedByChoice || !fromThePlayer || Suppressed)
            return 0;
        var settings = _settings.Get();
        if (!settings.Enabled || !settings.ReleaseEnabled)
            return 0;
        var facts = _heroes.ReadRelease(prisonerId!);
        if (facts == null || !facts.IsLord)
            return 0;

        var multiplier = settings.Release.For(PairOf(facts.Player, facts.LordSide));
        var candidates = new List<Candidate>();
        AddCandidate(candidates, facts.Lord, settings.ReleaseLordAmount, multiplier);
        if (facts.Spouse != null)
            AddCandidate(candidates, facts.Spouse, settings.ReleaseSpouseAmount, multiplier);
        if (facts.ClanLeader != null)
            AddCandidate(candidates, facts.ClanLeader, settings.ReleaseLeaderAmount, multiplier);
        return Apply(candidates, "release");
    }

    /// <summary>
    /// A tournament finished. When the player won: +1 with the owner's clan leader (not when the player's clan owns the town) and
    /// +1 with every living notable in the town (the settings amount), each scaled by his own side against the player's. One
    /// green line is shown only when something landed. Returns the number of relation owners whose relation changed.
    /// </summary>
    public int OnTournamentFinished(bool playerWon, string? townSettlementId)
    {
        if (!playerWon || string.IsNullOrWhiteSpace(townSettlementId) || Suppressed)
            return 0;
        var settings = _settings.Get();
        if (!settings.Enabled || !settings.TournamentEnabled)
            return 0;
        var facts = _heroes.ReadTournament(townSettlementId!);
        if (facts == null)
            return 0;

        var candidates = new List<Candidate>();
        if (!facts.PlayerClanOwnsTown && facts.OwnerLeader != null)
            AddCandidate(candidates, facts.OwnerLeader, settings.TournamentAmount,
                settings.Tournament.For(PairOf(facts.Player, facts.OwnerSide)));
        foreach (var notable in facts.Notables)
            AddCandidate(candidates, notable.Hero, settings.TournamentAmount,
                settings.Tournament.For(PairOf(facts.Player, notable.Side)));

        var landed = Apply(candidates, "tournament");
        if (landed > 0)
            _notice.ShowTournamentHonor(facts.TownName);
        return landed;
    }

    /// <summary>
    /// The relation vanilla applies to the settlement owner's clan leader for a donated hero prisoner: vanilla's own gain plus
    /// the bonus (5), both scaled by the captive's side against the owner's, rounded half away from zero. Vanilla's value comes
    /// back untouched when the feature is off, an id is blank, the facts cannot be read, or this peer defers to the host.
    /// </summary>
    public float DonationRelation(float vanillaGain, string? heroId, string? settlementId)
    {
        if (string.IsNullOrWhiteSpace(heroId) || string.IsNullOrWhiteSpace(settlementId) || Suppressed)
            return vanillaGain;
        var settings = _settings.Get();
        if (!settings.Enabled || !settings.DonationEnabled)
            return vanillaGain;
        var facts = _heroes.ReadDonation(heroId!, settlementId!);
        if (facts == null)
            return vanillaGain;

        var multiplier = settings.Donation.For(PairOf(facts.Captive, facts.Owner));
        var relation = GoodDeedsRules.ScaleDonation(vanillaGain, settings.DonationAmount, multiplier);
        _logger.LogInfo($"{Tag} donation of {heroId} to {settlementId}: vanilla {vanillaGain:0.##}, bonus {settings.DonationAmount}, x{multiplier:0.##} = {relation:0}.");
        return relation;
    }

    // A dedicated server has no player to earn for, and a co-op peer that is not the authority leaves a shared-world change to
    // the host (CoopInterop: ShouldDeferToHost also covers co-op mods TAOM cannot probe).
    private bool Suppressed => _server.IsDedicatedServer || _coop.ShouldDeferToHost;

    private SidePairing PairOf(DeedSide player, DeedSide other) =>
        GoodDeedsRules.Pair(_alignment.ResolveSide(player.KingdomId, player.CultureId),
            _alignment.ResolveSide(other.KingdomId, other.CultureId));

    // A dead hero, the player himself, and a grant that scales to nothing earn nothing; the settings clamp keeps an amount at 0
    // or more, so Good Deeds never costs relation.
    private static void AddCandidate(List<Candidate> candidates, DeedHero hero, int amount, float multiplier)
    {
        if (!hero.IsAlive || hero.IsPlayer)
            return;
        var delta = GoodDeedsRules.Scale(amount, multiplier);
        if (delta <= 0)
            return;
        candidates.Add(new Candidate(hero.HeroId, string.IsNullOrEmpty(hero.RelationOwnerId) ? hero.HeroId : hero.RelationOwnerId, delta));
    }

    /// <summary>
    /// Keeps the strongest candidate per relation owner (the first listed on a tie), in the order the owners first appear, and
    /// grants each one through the seam. Returns how many landed (a non-zero change).
    /// </summary>
    private int Apply(List<Candidate> candidates, string deed)
    {
        // GroupBy keeps the groups in the order their key first appears, and OrderByDescending is stable, so the first listed
        // candidate wins a tie.
        var landed = 0;
        foreach (var group in candidates.GroupBy(c => c.OwnerId, StringComparer.Ordinal))
        {
            var grant = group.OrderByDescending(c => c.Delta).First();
            if (_grants.Grant(grant.HeroId, grant.Delta, "good deed: " + deed) != 0)
                landed++;
        }
        return landed;
    }

    private readonly record struct Candidate(string HeroId, string OwnerId, int Delta);
}
