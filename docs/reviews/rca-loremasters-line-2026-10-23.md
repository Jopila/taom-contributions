# RCA: Loremaster's Line deep review (#877, 2026-10-23)

## Top-line

The first build of Loremaster's Line (a slowest-learning marker on the character screen's skill bar) went
through a tier 1 review: wave 1 (data flow, engine compatibility, XML) and, because all three found the same
HIGH, wave 2 (standards, efficiency, completeness, design). The HIGH: the arrow that carried the "Skill Limit"
tooltip sat wholly above the skill bar, outside every parent's hit rectangle, so the tooltip could never open.
Every finding below was confirmed against the v1.5.5 decompile and fixed in the same change. The design lens
also replaced TAOM's custom positioning widget with the engine's `FillBarHorizontalWidget`, which deleted the
widget, the copied bar maths and every per-frame TAOM code path. Codex was not run: paid dispatch needs the
maintainer's word.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The arrow (Top, `PositionYOffset -30`) lay above `PerkSelectionBarWidget` and its container. `EventManager.CollectEnableWidgetsAt` (EventManager.cs:880-901) descends only into a child whose own `AreaRect` holds the pointer, and a `HintWidget` relays only its parent's hover, so the tooltip never opened. | Hover path not read (repeat) | The plan copied vanilla's indicator markup but not its place: vanilla's indicator is a sibling of the bar, bottom-aligned inside the container's rectangle. Nobody walked the hit test, and the wiring tests checked bindings, not geometry. The same class shipped in Reeve's Ledger (a hint on a disabled button, `lessons/localization-ui.md`). | Hint moved onto the band (enabled, `DoNotPassEventsToChildren`, inside the bar); structural pins in `LoremastersLineWiringTests`. Lesson in `lessons/localization-ui.md`; one rule line in `.claude/rules/gui-ui.md` (follow-up commit). |
| 2 | MEDIUM | A wiring test called `GetCustomAttributes` on the mixin's `[DataSourceProperty]` members; in the reference assemblies that constructor is `throw null`, so the no-game unit step would fail. | Engine code in an untagged test (repeat) | `.claude/rules/tests.md` names this exact case, but the build ran only the game-loaded suite and the brief did not name the no-game step. | Reads `CustomAttributes` metadata by name; the RefAsm unit filter for the feature passed (124). Lesson in `lessons/testing-qa.md`. |
| 3 | MEDIUM | The mixin was 212 lines with its decisions inline (ADR-002). | Entry point size | The plan put the visibility gate, the percent choice and both tooltip builders in the mixin; the newest UI siblings already gate their files under 150 lines, but the brief did not name that gate. | Decisions moved to the calculator (tested), tooltips and the hero-item access to two client files; `EveryFile_StaysUnder150Lines` added. |
| 4 | LOW | The extra learning-limit line named "the red marker" even while the marker was hidden. | Text out of step with state | The line was gated on the switch, not on the marker. | Added only while `HasLoremastersLine`; source pin. |
| 5 | LOW | `Find` accepted a rising curve (its docstring said it would not). | Contract drift | The check used `Math.Abs`. | `!(full - floor > Tolerance)`; red test first. |
| 6 | LOW | Nothing pinned `LoremastersLineModule`; deleting its `FeatureModules` line left the suite green. | Wiring unpinned | 49 of 53 modules carry the pin; the brief did not list it. | The four ReevesLedger-shaped module tests. |
| 7 | LOW | Both sprites are redeclared by `TAOMSpriteData.xml` in `ui_taom` (the re-skin that draws), and TAOM's triangle points up, away from the bar. | Sprite source assumed | `.claude/rules/gui-ui.md` says to read `TAOMSpriteData.xml` before writing `Sprite=`; the plan read only SandBox's sheet. | The sprite test reads TAOM's sheet (SpritePart and GenericSprite); the arrow is flipped. Lesson in `lessons/localization-ui.md`. |
| 8 | LOW | The band's red at about 35% alpha over the fill sprite would read dark brown. | Visual guess | The tint was chosen without the zone it copies. | Vanilla's green-zone shape: the same sprite at full alpha (`#FF3333FF`). In-game check owed. |
| 9 | LOW | "Past it, this skill cannot improve further" overstated: `HeroDeveloper.ChangeSkillLevel` raises a skill without the focus factor. | Wording | The line restated the rate, not the skill. | "Past it, practice no longer improves this skill", edited in all 13 strings files (the seeds are English copies). |
| 10 | LOW | Wrong comments: a hero switch builds new skill view models (it rebinds; all are built at open), the switch is read per refresh (once per skill view model), `XDocument.Load` drops comments (it keeps them), the `GroupOrder` comment, and the client-internals list in `Main/TAOM.csproj`. | Comment accuracy | Written from the plan, not from the engine code. | All corrected. |
| 11 | LOW | The log latches were per instance on a mixin that has one instance per skill per hero, built twice at open. | Log volume | Copied from single-instance map-bar mixins. | Static latches with a comment; source pin. |
| 12 | LOW | `CannotImprove` had no NaN test; the widget's catch could throw from `IoC.Resolve`. | Gate coverage | Normal review catch. | Renamed `CannotLearn` with a NaN test; the widget is gone. |
| 13 | Design | A custom widget positioned the band and arrow every frame from a copy of the bar maths, where the engine's `FillBarHorizontalWidget` already does that placement (vanilla `DeclareWarDecisionPanel.xml:129-136`). | Reuse rung 1 missed | The plan searched for a seam on the bar, not for an engine widget that sizes a child from two numbers. | Engine widget; a 143-line widget, `BarFraction` and its test deleted. Lesson in `lessons/localization-ui.md`. |

## Root-cause pattern

Findings 1, 7 and 13 share one shape: UI behaviour was assumed from the markup's shape instead of read from the
engine code that runs it (the hit test, the sprite-sheet merge, the engine's own widgets). Each was one decompile
or one sheet read away. Findings 2, 3 and 6 share a second shape: a gate the newest siblings carry (the no-game
step, the 150-line test, the module pin) was not in the build brief, so the build did not meet it.

## Why each lens saw what it saw

- **Data flow (5), engine compatibility (2) and XML (7)** each found finding 1 independently by walking the hit
  test. Lens 7 also found 7 and 8; lens 2 found 9 and the comment errors in 10; lens 5 found 4 and 5.
- **Completeness (4)** and **standards (1)** found 2, 3, 6, 11 and 12 from the house rules and the newest siblings.
- **Efficiency (3)** measured the real model calls on the game's runtime (about 1 us per refresh, about 3 ms of
  probes and 8.5 ms of UIExtenderEx plumbing once at screen open for a 20-hero clan) and rejected every
  optimisation as not worth its code.
- **Design (6)** chose the hover host and found 13.

## Feedback memories to codify

None: the lessons and the rule line carry the systemic part.
