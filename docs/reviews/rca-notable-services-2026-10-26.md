# RCA: Notable Services (#899, deep-review, 2026-10-26)

## Top line

`/deep-review` ran seven lenses in two waves, then a convergence pass, on the new feature that lets a notable take
donated men, sell introductions and point out nearby holds. It confirmed one HIGH, five MEDIUM and a set of LOW findings
(listed below), all fixed before the commit, plus several behaviour-preserving simplifications. The HIGH was the half of the
feature that no player could reach: the prisoner gift ran in a screen mode that hides every prisoner.

The pattern behind the main findings: the builder copied a working sibling (Lord Courtesies' troop gift) and its
screen mode, and the unit tests drove the service directly, so nothing exercised what the engine's screen shows or what
the relation grant actually lands.

## Findings

| # | Sev | Finding | Why missed | Fix |
|---|---|---|---|---|
| 1 | HIGH | The gift screen used `PartyScreenMode.TroopsManage`, which sets `ArePrisonersRelevantOnCurrentMode` false (`PartyVM.cs:2059`) and hides both prisoner panels; only the D hotkey still moved them, unseen. Hearth, prosperity, Roguery XP and the alignment filter were unreachable. | The mode was copied from a troops-only gift, and the service tests call `Settle` directly. | `PartyScreenMode.Shared`, which differs from TroopsManage only in that one flag; a wiring test pins the mode. |
| 2 | MED | A gift made while a party screen was open underneath (party screen, talk to a companion, walk to a notable) left that screen's snapshot stale; Escape on it restored the given men and kept the credits. | The plan checked the gift screen itself, not the stack under it. | The gift line hides, and the opener refuses, while any `PartyState` is on the stack. |
| 3 | MED | The relation notice showed the asked gain; the diplomacy model adds Charm and caps at 100, so the landed change differs. | The fake `Grant` returned the ask. | The notice shows what `Grant` returns, through vanilla's localized relation text. |
| 4 | MED | A one-implementation service interface with no fake. | Copied from the brief's file list. | Deleted; the concrete service is registered. |
| 5 | MED | The settings switch was counted in the co-op fingerprint, though every write sits behind the single-player gate. | The sibling rule (Watch-Silver, Tidings) was not checked. | `PlayerLocal`. |
| 6 | MED | The first party-screen read was specified with a catch that returned false, which fails open. | My brief asked for "throw-safe"; the read cannot throw. | Expression-bodied, no try. |
| L | LOW | The speaker rule in the adapter, unused constructors, a hard-coded skill name, the gift line shown with no men to give, two names rendered in every condition, duplicated config limits, a map walk over every settlement, an indirect decision-4 expression, mixed rounding, wrong comments (the transfer delegate, the map bar, the payment contract), missing tests (priced line text, the behaviour decl, the origin skip, no map walk in a condition), doc drift and no backlog entry for the keys. | Hygiene and comments written before the engine trace. | Fixed; the keys are listed on #763. |

## Not done, on purpose

- A forced second commit (a companion removed while the screen is open, not verified as reachable) can take back men
  already credited. Recorded as a known limit in the feature doc; a guard for a speculative case is Mike's call.
- A repeated reveal keeps marking the next three holds, and every tracked nameplate draws on each map frame. The cost is
  unmeasured; the doc names it and the in-game check owes a frame-time A/B.

## Why each lens caught what it did

- **Data flow (5)** and **Engine (2)** both found the hidden prisoner panels: one traced the gift to the settlement
  writes, the other read every reader of the screen mode. Lens 5 also found the stale screen underneath.
- **XML (7)** found the asked-versus-landed notice and the hard-coded skill name.
- **Standards (1)** found the one-implementation interface and the rule in the adapter.
- **Efficiency (3)** found the names rendered per condition and the reveal's nameplate cost.
- **Completeness (4)** found the backlog gap and the untested wiring.
- **Design (6)** moved the notice to vanilla's text and removed the duplicated limits.

## Lessons

- `lessons/adapters-taleworlds-api.md`, "A party screen's mode decides what it lists: TroopsManage hides every prisoner".
- `lessons/adapters-taleworlds-api.md`, "Show the relation change the grant returns, not the change you asked for".
- The stale-screen finding is covered by the #1001 lesson ("A conversation can open from the ransom screen"), whose
  Prevent line names the gift case.
