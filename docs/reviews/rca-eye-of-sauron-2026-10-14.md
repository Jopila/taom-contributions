# RCA: Eye of Sauron hunt (M8), deep review and Codex pass, #765 (2026-10-14)

**Summary.** The Eye of Sauron hunt (#765 M8, committed as `34b0e25a9`) sends a Ringwraith, or Glorfindel for an
Evil-side player, after a player whose deeds draw the Eye: it re-tasks or spawns the hunter, steps him closer out
of sight, raises his speed once he is in sight, and tops up his host. A deep review of 49 findings (E01 to E49;
workflow journal `wf_1cd286bb-3fa`) had verifiers confirm 16 (E01 to E15 and E41: 2 HIGH, 14 MEDIUM) and refute
none; the other 33 were LOW, INFO and KEEP-style proposals that nobody verified. The review fixes landed in
`ac0a3e0c0`. The maintainer then decided three findings on 2026-10-14 (E06, E09, E17; `26718988b`, with the
stand-off made real in `33582471a`) and kept three more as built (E08, E38, E48). Codex found 2 MEDIUM and
2 LOW the review had missed, all in the resume, cache and boundary seams the fixes and decisions had just
created (F1 to F4, fixed in `2d17ceba3`).

## Findings (deep review)

The highest severity was E01 and E02 (HIGH). The rows below are the confirmed findings; each was applied in
`ac0a3e0c0` unless the last column says otherwise.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| E01 | HIGH | A player-founded kingdom was never hunted: the engine gives it the runtime id `new_kingdom`, which `alignment.json` does not know. | Id assumed static | The data was read for the shipped kingdoms; a runtime-created kingdom was not imagined. | The player's own kingdom is sided by id, else by its culture (`ResolveSide`); the console shows the same side. |
| E02 | HIGH | `ReadSight` ran `FindParty` (a linear string-compare walk over 2,039 to 3,226 parties) on every campaign frame of a hunt. | Hot path | A per-frame read was written like an hourly one. | The locator grid around the player, within seeing range; the reading is a value type; IL pins for the frame read. |
| E03 | MEDIUM | The hourly tick re-issued the engage with no war, kingdom, switch or timeout gate; those ran only daily, so a peace left a forced encounter for a day. | Gate on one path | The gates were written for the daily tick. | The end gates also run hourly, before the re-engage. |
| E04 | MEDIUM | `taom.eye_start_hunt` was Tier B but mutated persisted state that the end command never reversed (governor removed, a party spawned, up to 1000 troops added). | Wrong console tier | The command was tiered by its name, not its writes. | Tier C: the bare command is a dry run, only the literal `confirm` starts a hunt. |
| E05 | MEDIUM | The hourly step's decisions (19 early returns) sat in the adapter, against ADR-007. | Layering | The step was written where the engine reads were. | The decisions moved into the service; the adapter returns raw state; one test per gate. |
| E06 | MEDIUM | Any map event ending with the hunter ended the hunt and started the 60-day cooldown, including the player's "Try to get away". | Design question | The end rule was "hunter in a battle". | Maintainer decision, see "Decisions". |
| E07 | MEDIUM | `StartWith` returned the "Waiting" verdict for the first busy wraith, which blocked every later one. | Early return | A loop returned instead of continuing. | A busy or unfit hunter is passed over for the next in order; the hunt waits only when none can ride. |
| E08 | MEDIUM | `IsSinglePlayer` probed BannerlordCoop only, so under a Bannerlord Together session the hunt ran. | Probe coverage | BannerlordTogether cannot be probed (`IsAuthority` fails open there). | Maintainer decision: kept, see "Decisions". |
| E09 | MEDIUM | The steps refuse a hunter inside 1.5 S, the speed floor applied only at d <= S, so in the band between neither acted and the host was slower than the player. | Gap between two thresholds | Two rules were tuned separately. | Maintainer decision, see "Decisions". |
| E10 | MEDIUM | The host was topped up once; vanilla's wage-limit desertion takes men daily. | One-shot fix for a daily drain | The top-up was thought of as setup. | A daily top-up, never in a battle, before the engage. |
| E11 | MEDIUM | The size model's lambda captured the party, allocating a closure on every `GetPartyMemberSizeLimit` call on every party. | Hot path | A delegate built per call. | Built once; an IL allocation test (`TaomPartySizeModelAllocationTests`). |
| E12 | MEDIUM | A failed spawn search ran 15 loops of up to 250 path queries each in one frame. | Unbounded work | The retry was local, the cost was native. | A refused spawn stops the day's search; tomorrow retries. |
| E13 | MEDIUM | Save tests used an `IDataStore` that never writes the ref and loaded into the same instance, so deleting a `SyncData` key line failed nothing. | Vacuous test | The fake was not engine-faithful. | Field-by-field round trips through an engine-faithful store. |
| E14 | MEDIUM | `TryStartHunt`'s three refusals (running, switch off, co-op) were never tested; every test went through `OnDailyTick`, which returns earlier. | Skip-guard exhaustion | Tests entered at the top, not at the method. | Console refusals and the dry run tested directly. |
| E15 | MEDIUM | `HuntAnnouncement.Over` covered 8 of 10 end reasons, so an Evil-side player hunted by Glorfindel was told "The Eye turns away". | Side-blind text | The text was written for the Free-side case. | The end line is side-neutral ("The hunt is over, for now."). |
| E41 | MEDIUM | Issue #765 did not describe M8 or the hunt; its milestone list stopped at M7. | Missing record | The issue predates M8. | Issue body to be updated (follow-up). |

**Not individually verified (the other 33), by disposition.**

- **Applied in `ac0a3e0c0`:** E18 (a hunt restored into a co-op session ends at launch, a co-op peer scores no
  deed), E19 (releasing the pin drops the engage order), E20 (top-up before the engage), E22 (the pin flag and
  its save key are gone), E23 (`SyncData` reads the saved record by field), E24 (the shared `SettingClamp`),
  E28 (the six snapshot types split into one file each), E29 (console names `print_eye_of_sauron`,
  `add_eye_score`, `start_eye_hunt`, `clear_eye_hunt`), E30 (a speed-floor test file), E32 and E33 (the unread
  food field and the per-context empty arrays are gone), E35 (every hourly step gate tested), E37 (the Dol
  Guldur id data test), E31 and E49 (the dead relocate loop and the banner comments are no longer in the tree).
- **Decided by the maintainer:** E17 (below).
- **Decisions, kept:** E38 and E48, below.
- **Left as notes** (no change traced in the fix commits): E16 (battle types other than siege score as
  Field), E21, E25 to E27, E34, E36, E39 and E40 (doc rows and engine line references).
- **Follow-up:** E41 to E47, below.

## Decisions (maintainer, 2026-10-14)

Taken on the orchestrator's recommendation.

- **E06, a flee keeps the hunt (changed, `26718988b`).** A battle ends the hunt only when the hunter took part
  and someone won it, or when the hunter's side was defeated. A player who gets away is picked up again within
  the hour; a fight the hunter wins elsewhere does not end it.
- **E09, speed floor from 1.5 S (changed, `26718988b`).** The in-sight floor applies from 1.5 times the
  player's seeing range inward, the same margin the steps keep, so a moving player cannot be shadowed between
  S and 1.5 S.
- **E17, stand-off in another lord's army (changed, `26718988b`, then `33582471a`).** No hunt starts while the
  player rides in another lord's army, and a running one holds off: the hunter drops his order and pin, loses
  the floor, and is pinned again within the hour after the player leaves. The hunt's days still count and no
  cooldown starts. The first version only paused the service; `33582471a` made the stand-off real in the
  engine order and the save.
- **E08 (kept).** Bannerlord Together loaded means no hunt, because TAOM cannot probe it.
- **E38 (kept).** The Dol Guldur kingdom rule: a hunter's host follows his live kingdom, so the Nine placed in
  `clan_dolguldur_1` by `heroes.xslt` (Khamul and the three `lord_1_48_*`) lead Dol Guldur troops.
- **E48 (kept).** `EyeOfSauronEnabled` defaults to on.

## Codex review (gpt-6-astra, on the fixes and decisions)

Raw output `docs/reviews/raw/codex-adversarial-eye-of-sauron-2026-10-14.md` (gitignored). 0 CRITICAL, 0 HIGH,
2 MEDIUM, 2 LOW, 0 false positives. Codex ran no build or `dotnet test` (a no-file-write constraint); it
compiled the service and its fixture in memory (227 service cases passed on the baseline) and applied eight
mutations: seven were caught and replacing `Volatile.Write` with a plain assignment survived, a test limitation
it did not count as a defect. Engine-hosted save/load, native mesh results and live co-op stay UNVERIFIED.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM | The resume branch excluded only a map event, so a hunter whose own AI led or joined an army while he stood off was claimed and marked as hunting while attached (vanilla attached members skip path movement and the speed model). | Resume skipped the start checks | The E17 stand-off added a second way to enter the hunt (resume) and tested the player side only. | The resume waits, unpinned, while the hunter is busy (army above all, battle, siege, town, another order) and retries hourly. |
| F2 | MEDIUM | A hunt start that added no troops, and any end, did not invalidate the cached `PartySizeLimit`, which the engine keys on `MemberRoster.VersionNo`; the code bumped the party speed version instead. | Wrong version counter | The end path invalidated speed, the only cache the author had in mind. | Every change to hunt membership (start, even adding none, end, refused start) calls `MemberRoster.UpdateVersion`. IL test. |
| F3 | LOW | A stand-off save loaded into co-op ended the hunt through a path that released a pin the hunt no longer owned. | Stale ownership read | The stand-off made "pinned by us" a state that can be false while a hunt exists. | Session launch reads the live pin before any end. |
| F4 | LOW | The locator search radius was exactly 1.5 S and the engine skips a party at exactly its radius (`>=`), while the service accepts `d <= 1.5 S`. | Broad phase narrower than filter | E09 moved the edge to 1.5 S; the adapter's radius was copied from it. | The search reaches 1% past 1.5 S; the exact filter stays in the service. IL test. |

Suspect dispositions: S1 (save corruption, duplicate parties, persistent pins) disputed as demonstrated
corruption, narrowed to F3; S2 (threads, snapshots, band edges, NaN) disputed for the speed hook, F4 confirmed;
S3 (relocation) disputed, native mesh results UNVERIFIED; S4 (size, top-up, wages, food) F2 confirmed,
arithmetic and ordering disputed; S5 (ends and pin ownership) E06 gates disputed, F1 and F3 confirmed for the
E17 and load transitions; S6 (war, alignment, co-op gates) disputed, live Together behaviour UNVERIFIED; S7
(tests) coverage gaps confirmed at the engine boundary and the combined lifecycle transitions.

## Root-cause patterns

**A new way into a state must reuse the old way's checks.** F1: the stand-off (E17) added a resume path, and the
start path's availability checks were not carried over. The same shape as a sibling tracker whose scan was
copied without its limbs.

**A cache is invalidated with the counter it is keyed on.** F2: the engine's size limit follows
`MemberRoster.VersionNo`; the code bumped another version and tested nothing that read the limit back.

**A broad-phase radius must exceed the exact filter's edge.** F4: the engine's locator is half-open, the
service's filter is closed. When a decision moves an edge (E09), grep every search that feeds it.

**Ownership of an engine flag can lapse while the feature exists.** F3, and E19: once a stand-off can release a
pin, "release what we pinned" must read the live pin, not a flag.

**A hot path written like an hourly one.** E02, E11, E12: a per-frame or per-call site did a linear walk, an
allocation or a native retry loop. IL pins and a locator grid fixed them; the lens that finds this is
efficiency, and it found all three.

**A decision that changes a rule changes its neighbours.** E06, E09 and E17 each produced a Codex finding
(F1, F3, F4) within the area the decision touched. Re-run the lenses on the decision commits.

## Follow-ups (outside this change; issues on Mike's word)

- E41: update issue #765's body for M8 (the Eye of Sauron hunt).
- E42: four private `IDataStore` fakes (WarOfTheRing, ArmourAcquisition, SpecialResources, CareerPersistence)
  drifted from the engine's save semantics; share the engine-faithful store.
- E43: both party models allocate a `CultureFeatAdapter` per call, cost UNVERIFIED.
- E44: a comment says the AI pin is not persisted; the engine saves it as `[SaveableProperty(11)]`.
- E45: the main checkout's uncommitted chronicle work bumps the same settings pins and the same
  `FeatureModules.cs` list; recount on merge.
- E46: the installed game is v1.5.5 while the pin and the suite are v1.5.4.
- E47: the four `taom_eye_hunt_*` keys need translations in 12 languages (`/localize`; paid, on Mike's word).
- Context, not this work: 9 data tests fail on trunk from `08b30bfc4`.

## Lessons recorded

`docs/reviews/lessons/campaign-mechanics.md`: a resume path must reuse the start path's availability checks.
`docs/reviews/lessons/adapters-taleworlds-api.md`: an engine cache keyed on a different version counter than the
one you bump; a broad-phase search radius must exceed the exact filter's edge.
