# RCA: Help Unlooked-For deep review, #872 (2026-10-23)

**Summary.** Help Unlooked-For overrides `MobilePartyAIModel.ShouldConsiderAvoiding` so that an AI lord strong enough for a siege camp stops fleeing it. Six `/deep-review` lenses in two waves read the uncommitted tree before its first commit. They found no CRITICAL or HIGH defect, two MEDIUM and a set of LOW findings, each re-read against the code and the v1.5.5 decompile. All were fixed before the first commit. The maintainer added one rule during the review: AI lords keep vanilla behaviour at a siege the player leads.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | The feature doc's "Limit" paragraph cited the wrong vanilla lines and overstated the exemption: an army whose leader defends the settlement skips its besiegers (`DefaultMobilePartyAIModel` 187-193); any party on a defend order, a lone lord included, ignores only the settlement's last attacker (`MobilePartyAi` 488). The first rewrite in this review then said a lone lord is not exempt, which line 488 contradicts; the convergence pass caught it. | Engine claim from the plan | The plan's summary of the engine was copied into the doc without a second read of the lines it cited. | Rewritten from the source lines. Lesson below. |
| 2 | MEDIUM | The fact reader ran for every avoid question and walked the lord's army before the service read the switch or vanilla's answer. The question runs for every party pair the AI weighs, on the AI worker. | Hot-path ordering | The override was written as "vanilla answer plus facts, then decide", which evaluates both sides. | `base(...) && Decide(Read(...))`; strengths read only for a lord facing a siege camp; struct checks before the switch. Lesson below. |
| 3 | LOW | No rule for a siege the player leads: AI lords near the player's own siege would stop fleeing it. | Scope | The plan reasoned about AI sieges only. | Maintainer's decision: a `PlayerLedSiege` fact keeps vanilla, for both the camp and a party inside the settlement. |
| 4 | LOW | The reader caught only `InvalidOperationException`; a torn read on the worker can also throw `NullReferenceException`, and unread strengths were 0 (which reads as "stop fleeing") instead of NaN. | Worker-thread reads | The catch matched the one exception a changing collection throws. | Both exceptions caught; NaN keeps vanilla's answer. |
| 5 | LOW | The doc said nothing in a siege attacks a passing lord; the hourly engage path can send a lone besieger after one. The co-op and model remarks said the model runs only on the worker; `CheckPartyNeedsUpdate` also calls it on the main thread. | Overstated engine claim | Same as 1. | Reworded from the source. |
| 6 | LOW | An interface with one implementation and no fake; a private copy of `FiniteFloatValidator.IsFinite`; an always-true parameter after the short-circuit; a main-party check for a party the engine never sends here (`DefaultMobilePartyAIModel` 94); an unused `ObjectCreationHandling.Replace`. | Simplicity | Written before the design pass. | Deleted. |
| 7 | LOW | Tests: nothing pinned "only `ShouldConsiderAvoiding` is overridden", the slot scan covered only `FeatureModules.All`, the model factory was never called, the reader had no behaviour test. The API snapshot was not regenerated. | Completeness | The sibling's test set was not used as the checklist. | Reflection test, source-tree scan, factory test, a reader test for the no-siege case (the siege paths need a running campaign and stay in the in-game check); snapshot regenerated. |

## Root-cause pattern

Findings 1 and 5 share a cause: engine behaviour summarised in the plan went into the doc and comments without a second read of the cited lines. Findings 1, 4 and 5 were all claims about where and when the engine calls the code.

## Lessons

- `docs/reviews/lessons/gamemodels-services.md`: an override that only narrows vanilla's answer short-circuits on `base` before it reads anything.
- The engine-claim pattern (findings 1 and 5) is already a standing rule (AGENTS.md "Research first"); no new rule.
