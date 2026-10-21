using System;
using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.NamedCompanions.Domain;

namespace TAOM.Features.NamedCompanions.Oaths;

/// <summary>
/// What a named companion asks before joining the party (#904): flags first, then gold, then items.
/// Pure over adapters. The hire itself is vanilla's call order, run by the adapter.
/// </summary>
public sealed class HireTermsService
{
    private readonly INamedCompanionConfigProvider _config;
    private readonly IOathFlagStore _flags;
    private readonly INamedCompanionAdapter _adapter;
    private readonly IPartyItemRosterAdapter _roster;
    private readonly ICareerPurseAdapter _purse;
    private readonly IModLogger _logger;

    public HireTermsService(
        INamedCompanionConfigProvider config,
        IOathFlagStore flags,
        INamedCompanionAdapter adapter,
        IPartyItemRosterAdapter roster,
        ICareerPurseAdapter purse,
        IModLogger logger)
    {
        _config = config;
        _flags = flags;
        _adapter = adapter;
        _roster = roster;
        _purse = purse;
        _logger = logger;
    }

    /// <summary>The terms of an enabled companion, or null when there are none.</summary>
    private HireTermsDefinition GetTerms(string characterId)
    {
        var companions = _config.GetCompanions();
        if (companions == null || characterId == null) return null;
        foreach (var companion in companions)
            if (companion != null && companion.Enabled && companion.CharacterId == characterId)
                return companion.HireTerms;
        return null;
    }

    public HireEvaluation Evaluate(string characterId)
    {
        var terms = GetTerms(characterId);
        if (terms == null) return HireEvaluation.NoTerms;

        var result = new HireEvaluation { Terms = terms, Price = terms.Gold ?? _adapter.GetVanillaHiringPrice(characterId) };

        if (terms.RequiresFlags != null)
            foreach (var flag in terms.RequiresFlags)
                if (!_flags.Has(flag))
                {
                    result.Status = HireStatus.Locked;
                    return result;
                }

        var gold = _purse.Gold;
        if (gold < result.Price)
            result.Missing.Add(new MissingTerm { Kind = MissingTermKind.Gold, Count = result.Price - gold });

        if (terms.Items != null)
            foreach (var item in terms.Items)
            {
                var have = _roster.GetItemCount(item.ItemId);
                if (have < item.Count)
                    result.Missing.Add(new MissingTerm { Kind = MissingTermKind.Item, ItemId = item.ItemId, Count = item.Count - have });
            }

        result.Status = result.Missing.Count == 0 ? HireStatus.Met : HireStatus.Missing;
        return result;
    }

    /// <summary>
    /// Takes the items, then hires (which takes the gold). If the hire fails or throws, the items taken are
    /// given back. Refused on a co-op client: the roster and the clan are save-backed.
    /// </summary>
    public bool TryPayAndHire(string characterId, bool isCoopClient)
    {
        if (!CoopSessionPolicy.MayWriteSaveBackedState(isCoopClient)) return false;

        var evaluation = Evaluate(characterId);
        if (evaluation.Status != HireStatus.Met) return false;

        var taken = new List<KeyValuePair<string, int>>();
        var terms = evaluation.Terms;
        if (terms.Items != null)
            foreach (var item in terms.Items)
            {
                var removed = _roster.RemoveItem(item.ItemId, item.Count);
                if (removed > 0) taken.Add(new KeyValuePair<string, int>(item.ItemId, removed));
                if (removed < item.Count)
                {
                    _logger.LogWarning($"[Oaths] hire of {characterId}: only {removed} of {item.Count} {item.ItemId} could be taken; refunded");
                    Refund(taken);
                    return false;
                }
            }

        bool hired;
        try
        {
            hired = _adapter.HireIntoPlayerParty(characterId, evaluation.Price);
        }
        catch (Exception ex)
        {
            // The hire is gold, then AddCompanionAction, then AddHeroToPartyAction: a throw in the last step leaves
            // the companion in the party with the gold paid, so the items stay taken.
            hired = _adapter.IsRecruitedOrInParty(characterId);
            _logger.LogError($"[Oaths] hire of {characterId} threw, "
                + (hired ? "the companion had already joined, items kept: " : "items refunded: ") + ex);
        }

        if (!hired) Refund(taken);
        return hired;
    }

    private void Refund(List<KeyValuePair<string, int>> taken)
    {
        foreach (var pair in taken)
            _roster.AddItem(pair.Key, pair.Value);
    }
}
