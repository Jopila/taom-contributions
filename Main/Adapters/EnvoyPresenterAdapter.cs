using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Adapters;

/// <summary>
/// The tribute envoy's popup and lines (#870 slice 2). The popup is the engine's multi-selection inquiry
/// (<c>MBInformationManager.ShowMultiSelectionInquiry</c> with <c>MultiSelectionInquiryData</c>, TaleWorlds.Core
/// MultiSelectionInquiryData.cs) with exactly one option to pick and no cancel button (<c>isExitShown</c> false, so the
/// view hides it: MultiSelectionQueryPopUpVM.cs:158), and it pauses the game while open. The choice reaches the service
/// through <c>AffirmativeAction</c>, which the view runs before it closes the popup, so a popup shown from inside it queues
/// behind the closing one (GauntletQueryManager.CreateMultiSelectionQuery). A popup that closes without an answer calls
/// nothing; the service learns of it when <see cref="MapPopupGate.IsMapFree"/>, the one test every TAOM map popup asks and
/// false while any inquiry is open, turns true again. Each message is one whole sentence, so a language can inflect it, and
/// the host's name is its own keyed string from the data.
/// </summary>
public sealed class EnvoyPresenterAdapter : IEnvoyPresenterAdapter
{
    public bool IsMapFree() => MapPopupGate.IsMapFree();

    public void ShowOffer(EnvoyOffer offer, Action<EnvoyChoice> onChosen)
    {
        var host = new TextObject(offer.KeyedHostName);
        var elements = new List<InquiryElement>
        {
            PayElement(offer, host),
        };
        if (offer.CanHaggle)
        {
            elements.Add(new InquiryElement(EnvoyChoice.Haggle, new TextObject("{=taom_fat_envoy_haggle}Haggle over the price").ToString(), null,
                true, new TextObject("{=taom_fat_envoy_haggle_hint}One try, and your Charm decides it. If you win, the price falls by a quarter.").ToString()));
        }
        elements.Add(new InquiryElement(EnvoyChoice.Delay, new TextObject("{=taom_fat_envoy_delay}Ask for time to decide").ToString(), null,
            true, new TextObject("{=taom_fat_envoy_delay_hint}The envoy returns in {DAYS} {?DAYS>1}days{?}day{\\?} at the same price. Nothing is spared until you pay.")
                .SetTextVariable("DAYS", offer.DelayDays).ToString()));
        elements.Add(new InquiryElement(EnvoyChoice.Refuse, new TextObject("{=taom_fat_envoy_refuse}Send the envoy away").ToString(), null,
            true, new TextObject("{=taom_fat_envoy_refuse_hint}The host raids your villages as it raids any others.").ToString()));
        elements.Add(new InquiryElement(EnvoyChoice.Seize, new TextObject("{=taom_fat_envoy_seize}Seize the envoy").ToString(), null,
            true, new TextObject("{=taom_fat_envoy_seize_hint}The next host of its side rides against your kingdom first, and every tribute after this one costs more.").ToString()));

        var title = new TextObject("{=taom_fat_envoy_title}{HOST} Demands Tribute").SetTextVariable("HOST", host);
        var body = new TextObject("{=taom_fat_envoy_body}An envoy rides up under a flag of truce. \"{HOST} marches against your kingdom, and every village of your clan ({VILLAGES} in all) lies in its path. Pay {PRICE} denars and its torches will pass them by. Refuse, and we shall see what is left of them.\"");
        body.SetTextVariable("HOST", host);
        body.SetTextVariable("VILLAGES", offer.Villages);
        body.SetTextVariable("PRICE", offer.Price);

        MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(
            titleText: title.ToString(),
            descriptionText: body.ToString(),
            inquiryElements: elements,
            isExitShown: false,
            minSelectableOptionCount: 1,
            maxSelectableOptionCount: 1,
            affirmativeText: new TextObject("{=taom_fat_envoy_answer}Answer the envoy").ToString(),
            negativeText: string.Empty,
            affirmativeAction: chosen =>
            {
                // An answer that names no option closes the popup like a hidden one: the next hour asks again.
                if (chosen != null && chosen.Count > 0 && chosen[0].Identifier is EnvoyChoice choice)
                    onChosen(choice);
            },
            negativeAction: _ => { }),
            pauseGameActiveState: true,
            prioritize: false);
    }

    /// <summary>The pay option is shown even when the player is short, disabled with their purse as the hint.</summary>
    private static InquiryElement PayElement(EnvoyOffer offer, TextObject host)
    {
        string title = new TextObject("{=taom_fat_envoy_pay}Pay {PRICE} denars").SetTextVariable("PRICE", offer.Price).ToString();
        string hint = offer.CanAfford
            ? new TextObject("{=taom_fat_envoy_pay_hint}{HOST} spares your clan's villages and does not attack your party.").SetTextVariable("HOST", host).ToString()
            : new TextObject("{=taom_fat_envoy_pay_poor}You have only {GOLD} denars.").SetTextVariable("GOLD", offer.Gold).ToString();
        return new InquiryElement(EnvoyChoice.Pay, title, null, offer.CanAfford, hint);
    }

    public void ShowOutcome(EnvoyOutcome outcome, string keyedHostName, int price, int days)
    {
        var text = new TextObject(Sentence(outcome));
        text.SetTextVariable("HOST", new TextObject(keyedHostName));
        text.SetTextVariable("PRICE", price);
        text.SetTextVariable("DAYS", days);
        InformationManager.DisplayMessage(new InformationMessage(text.ToString()));
    }

    private static string Sentence(EnvoyOutcome outcome)
    {
        switch (outcome)
        {
            case EnvoyOutcome.Paid:
                return "{=taom_fat_envoy_paid}You pay {PRICE} denars. {HOST} will pass your clan's villages by.";
            case EnvoyOutcome.CannotAfford:
                return "{=taom_fat_envoy_poor}You cannot raise {PRICE} denars, and the envoy waits for your answer.";
            case EnvoyOutcome.HaggleWon:
                return "{=taom_fat_envoy_haggle_won}The envoy bows to your words: the price falls to {PRICE} denars.";
            case EnvoyOutcome.HaggleLost:
                return "{=taom_fat_envoy_haggle_lost}The envoy will not be moved: the price stays at {PRICE} denars.";
            case EnvoyOutcome.Delayed:
                return "{=taom_fat_envoy_delayed}{HOST} will send its envoy back in {DAYS} {?DAYS>1}days{?}day{\\?}. Your villages are not safe until you pay.";
            case EnvoyOutcome.Refused:
                return "{=taom_fat_envoy_refused}{HOST} hears your answer: its envoy rides away without a coin.";
            default:
                return "{=taom_fat_envoy_seized}You seize the envoy. {HOST} will not forget it: the next host of its side rides against your kingdom first.";
        }
    }
}
