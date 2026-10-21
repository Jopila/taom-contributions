using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.NamedCompanions.Oaths.Hooks;

/// <summary>
/// The hire half of Oaths of Service (#904). Vanilla's hire is one player line into <c>companion_hire</c>
/// answered by an NPC line at priority 100 (LordConversationsCampaignBehavior). Two NPC lines at 105, one per
/// state, pre-empt it for a companion with terms. Wanderer Allegiance's refusal lines sit at 110 and the
/// conversation manager takes the first true NPC line in priority order, so a refusal always answers before a
/// 105 line is tried: that gate is kept by priority alone, with no check here (pinned by OathsOfServiceWiringTests).
/// A companion without terms falls through to vanilla untouched.
/// </summary>
internal sealed class OathHireDialog
{
    private const int NpcPriority = 105;

    private readonly HireTermsService _hire;
    private readonly INamedCompanionAdapter _adapter;
    private readonly ICoopSessionProvider _coop;
    private readonly Func<string, string> _itemName;
    private readonly IModLogger _logger;
    private bool _loggedEmptyTerms;

    public OathHireDialog(HireTermsService hire, INamedCompanionAdapter adapter, ICoopSessionProvider coop,
        Func<string, string> itemName, IModLogger logger)
    {
        _hire = hire;
        _adapter = adapter;
        _coop = coop;
        _itemName = itemName;
        _logger = logger;
    }

    public void Register(CampaignGameStarter starter)
    {
        starter.AddDialogLine("taom_oath_locked", "companion_hire", "lord_pretalk", "{=!}{TAOM_OATH_LOCKED}",
            LockedCondition, null, NpcPriority);
        starter.AddDialogLine("taom_oath_terms", "companion_hire", "taom_oath_hire_response",
            "{=taom_oath_terms}I do not take the road for nothing. My price is {TAOM_OATH_TERMS}. Will you meet it?",
            TermsCondition, null, NpcPriority);
        starter.AddPlayerLine("taom_oath_full", "taom_oath_hire_response", "lord_pretalk",
            "{=afdN8ZU7}Thinking again, I already have more companions than I can manage.",
            () => _adapter.IsCompanionLimitReached(), null);
        starter.AddPlayerLine("taom_oath_accept", "taom_oath_hire_response", "hero_leave",
            "{=taom_oath_accept}It is agreed. Take it, and ride with me.",
            () => !_adapter.IsCompanionLimitReached(), Pay, 100, CanAccept);
        starter.AddPlayerLine("taom_oath_decline", "taom_oath_hire_response", "lord_pretalk",
            "{=taom_oath_decline}Not yet.", () => true, null, 1);
    }

    private HireEvaluation Evaluate()
    {
        try
        {
            var hero = Hero.OneToOneConversationHero;
            return hero == null ? HireEvaluation.NoTerms : _hire.Evaluate(hero.StringId);
        }
        catch (Exception ex)
        {
            _logger.LogError($"[Oaths] could not read the hire state, deferring to vanilla hiring: {ex}");
            return HireEvaluation.NoTerms;
        }
    }

    private bool LockedCondition()
    {
        var evaluation = Evaluate();
        if (evaluation.Status != HireStatus.Locked) return false;

        var lockedId = evaluation.Terms?.LockedStringId;
        var line = !string.IsNullOrEmpty(lockedId) && GameTexts.TryGetText(lockedId, out var text)
            ? text
            : new TextObject("{=taom_oath_locked_generic}Not yet. Prove yourself first, then ask me again.");
        MBTextManager.SetTextVariable("TAOM_OATH_LOCKED", line);
        return true;
    }

    private bool TermsCondition()
    {
        var evaluation = Evaluate();
        if (evaluation.Status is not (HireStatus.Missing or HireStatus.Met)) return false;

        var text = OathTextRenderer.Terms(evaluation.Terms, evaluation.Price, _itemName);
        if (text == null)
        {
            // Nothing to list (a free hire with no items): an empty price line would be a broken sentence, so vanilla answers.
            if (!_loggedEmptyTerms)
                _logger.LogWarning($"[Oaths] hire terms of {Hero.OneToOneConversationHero?.StringId} list no price and no items; vanilla hiring applies");
            _loggedEmptyTerms = true;
            return false;
        }
        MBTextManager.SetTextVariable("TAOM_OATH_TERMS", text);
        return true;
    }

    private bool CanAccept(out TextObject explanation)
    {
        explanation = TextObject.GetEmpty();
        var id = Hero.OneToOneConversationHero.StringId;
        if (!CoopSessionPolicy.MayWriteSaveBackedState(_coop.IsCoopClient))
        {
            explanation = new TextObject("{=taom_oath_coop_hint}Only the host can seal an oath in a shared campaign.");
            return false;
        }
        var evaluation = _hire.Evaluate(id);
        if (evaluation.Status == HireStatus.Met) return true;
        explanation = new TextObject("{=taom_oath_missing_hint}You still need: {TAOM_OATH_MISSING}");
        explanation.SetTextVariable("TAOM_OATH_MISSING", OathTextRenderer.Missing(evaluation.Missing, _itemName));
        return false;
    }

    private void Pay()
    {
        try { _hire.TryPayAndHire(Hero.OneToOneConversationHero.StringId, _coop.IsCoopClient); }
        catch (Exception ex) { _logger.LogError($"[Oaths] hire consequence failed: {ex}"); }
    }
}
