# CHANGELOG: TAOM (Tales From the Age of Men)

> **Generated at each release; do not edit by hand.** `/release` runs
> `python tools/changelog_from_commits.py --version vX.Y.Z --write`, which adds one section per
> release from the subjects and bodies of every commit since the previous release tag. The commit
> body is the changelog entry, so write it for a reader of the release note. Between releases,
> `git log <last tag>..HEAD` is what changed.
>
> **Archive:** the hand-written entries from 2026-07-01 up to the switch to generation are in
> [`docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md`](docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md);
> older ones are in [`docs/changelog-archive/CHANGELOG-2026-H1.md`](docs/changelog-archive/CHANGELOG-2026-H1.md).

## v2.0.35 (2026-10-19)

Commits since v2.0.34: 193 (181 with the version label, 12 without).

### Features

#### feat(balance): v2.0.34 - cap evil party-size feats at 10%

`e166d8c8`

Every party-size bonus of any weight sat on the evil side, and a
200-day balance simulation had the free peoples falling from 47 to 41
men per 100 evil men. The evil cultures' party-size feats are now
capped at 10%:

- Goblin-town and Blue Craig: +40% to +10%
- Misty Mountain Orcs: +30% to +10%
- Mordor, Isengard, Gundabad and Dol Guldur: +20% to +5%

The men's realms keep their +5% and Gondor its +2.5%.

The feats also set garrison and patrol limits, so evil garrisons
shrink too: a Goblin-town town garrison goes from about 560 to about
440, a Mordor town from about 480 to about 420, before leadership and
buildings. On an orc roster heavy with weighted troops, the elite tax
can now cancel the +5% entirely; that is accepted.

Existing saves pick up the new limits on load. Evil AI lords over the
limit are trimmed on the first day; a goblin or orc player's own
party and garrisons lose a quarter of the excess each day.

The character-creation culture cards and the faction screen show the
new numbers in every language; Mordor's card had still said +10% from
before its last retune. A test now fails when a card's party-size
number differs from the feat.

Refs #818.

#### feat(balance): v2.0.34 - larger Arthedain hosts and new army targets

`039e30f4`

A 200-day balance simulation (ArmorySim, yotthani's standalone tool)
had Arthedain losing 66 % of its men, the most of any free realm. Its
lords spawned from a template capped at 83 men, against 200 for the
other realms of men. Arthedain lords now spawn from the same 200 cap,
so a new lord party starts with about 100 men instead of about 45.

Armies now pick their siege targets in a different order:

- Dunland goes for Rohan first, then Gondor. Arthedain comes last.
- Gundabad goes for Mirkwood and Erebor first, then Dale. Arthedain
  is now a target of opportunity.
- Dol Guldur goes for Lothlorien first, then Mirkwood and Dale, then
  Erebor.
- Rhun already went for Erebor first and then Dale, and is unchanged.

The Arthedain troop generator owns the template, so the new numbers
live there and a regeneration keeps them.

Refs #818.

#### feat(diplomacy): v2.0.34 - no wars before the War of the Ring

`2410aec5`

Campaigns now open in peace. The years before the War of the Ring are
a build-up: from day 0 to day 30 no kingdom is at war and the AI
cannot declare one. On day 30 Isengard and Dunland attack Rohan, and
that is the only war until the full War of the Ring begins on day 44.
Gundabad's war on Arthedain now starts on day 44 as well.

Every war declaration between two kingdoms follows the War of the
Ring phase. Permanent allies never fight. Realms on the same side
cannot fight while the war runs, but may after it ends. With the War
of the Ring switched off, the old rule applies.

- No start wars in the data. Dale against Rohan, Dunland against Rhun
  and Mordor against Harad were vanilla leftovers; Gondor against
  Khand, Shaghana and Abanissa and Arthedain against Gundabad are
  gone too. A negative relation value declares war at load whatever
  isAtWar says, so every row is now 0, and a test fails on any
  future war row.
- A war you cause yourself still happens before day 44: attacking a
  kingdom's party or caravan, a crime rating high enough for war,
  founding a kingdom with the wars your clan already has, and
  enlisting under a lord of another kingdom.
- The AI no longer proposes a war the rule would cancel, so clans
  stop losing influence on proposals that cannot pass.
- Call-to-war offers that can never lead to a war are cancelled
  before you see them, so declining one no longer costs relation.
- A daily check ends any war the phase forbids. An old save made
  before day 44 loses its start wars on the first day after loading;
  your own kingdom's wars are kept.
- The Advanced Start scenarios that open with a war (Two Faction War,
  Last Stand, Invasion, Alternative Calradia, Nord Invasion) are
  hidden while the War of the Ring is on.
- The kingdom screen explains a refused war: an unbreakable alliance,
  the same side of the war, the war not begun yet, or only the first
  wars of the age allowed.

The three new refusal texts are English in every language until the
next translation run.

Refs #818. Follow-ups: #819, #820.

#### feat(animation): v2.0.34 - per-soldier orc gait variety and limp gaits

`d13e9165`

Orc and goblin warriors no longer march in perfect lockstep. Each
soldier now gets its own step size when it spawns: its normal step
size times a random factor between a minimum (0.92 by default) and
1.0, fixed for that soldier. The spread builds up while a line keeps
walking; the engine snaps every soldier back to the same step phase
when it stands fully still, so it starts again after each halt. A new
MCM group, Animation Variety, has the switches: random step size per
soldier (on), the minimum factor, random gait set per soldier (on),
and a swing alternative probe (off, a diagnostic that only logs).
Riders, mounts and the player are left alone.

The same feature can also give each orc one of three walk and run
styles, but those styles are not in the game yet. They need a
Modding Kit import first (see below), so for now the gait set switch
finds nothing and changes nothing.

The new orc walk and run, reviewed by eye with Mike in Blender:
- The main orc is a small Mordor or Moria scuttler: a deep hunch with
  the head pushed forward and the eyes level, a crouch, feet on wide
  separate tracks with the toes turned out, arms held out from the
  body, and a limp: the left leg drags, the right leg takes the
  weight, and the right shoulder sits low and leads. The run is a
  forward-falling lope with the weak leg trailing.
- Variant B is a bigger, more upright brawler with the mildest limp.
- Variant C sits between the two, with the limp mirrored.
The orc idles take the main orc's posture. The spine bend of the main
orc (about 40 degrees) is over the 30 degree limit kept for blending
into attacks, at Mike's request, so an attack from a walk may show
the upper body straightening; that look is owed an in-game check.

Animation tooling (tools/blender): the restyle tool gained the limp
(strong and dragged leg, uneven 60/40 timing on a smooth time map,
rebuilt foot paths so planted feet move at ground speed), a run
override, mirrored variants, hand clearance against the thighs,
calves and torso (with a wider gap for the shield arm), and spec and
code fingerprints in its report. The validator now finds planted
frames on its own, judges every frame the report claims as planted,
and checks clearance.
A new viewer, show_gait_variants.py, plays variants side by side with
Armory weapons on the hands. The clip generator takes the stride and
footstep points from the restyle report (-RestyleReport) and refuses
a stale or failed report. The binder writes the variant action sets
and refuses to touch any set it does not own.

Live Armory: the 36 new FBX are copied into
AssetSources\Race Test\Mordor\Orc\animations, animations_b and
animations_c. Nothing is imported, so the game still plays the
2026-10-14 orc clips. Backup and rollback:
E:\LOTRAOMAssets\backups\orc_locomotion_20261017-224440. A reinstall
of the Armory removes the copies.

Still owed, in order: Mike reimports the 16 main orc masters (same
names) and imports B and C in the Kit; wire the masters; move the old
clip packages aside; generate the clips with -RestyleReport; save in
the Kit; check the cache entries; bind the variant sets, then run
--check --require-variants and the parity audit; refresh the Armory
snapshot; then a Custom Battle with mixed orc formations. Testable
today after a deploy: the step size switch, on and off.

Known limitation: in the hunched orcs the sword tip can touch the
floor (accepted by Mike). The swing foot of a crouched gait touches
down about two frames early; capping how far it drops is a follow-up.
The knee-limit pelvis lowering can step near full leg reach; also a
follow-up. Bows, crossbows, two-handed weapons and polearms without a
shield still use the vanilla walk and run.

No GitHub issue, by Mike's decision of 2026-10-17; the clip half is
tracked in #791. Reviewed by /deep-review (eight lenses), two Codex
passes and two convergence passes over three fix rounds; every
finding, fix and follow-up is in
docs/reviews/rca-animation-variety-2026-10-17.md.

#### feat(scenes): v2.0.34 - wire Kysaro's town, village and Rohan scenes

`4bd4f3e5`

Kysaro's scene drop (lotraom-assets db387f23f) is wired into the game.

Settlements, in TAOM_Map's settlements.xml: Pelargir and Serelond get
their own town scenes, Serelond its own arena, Amon Gelin and Lothgobel
their own village scenes, and Eastern Moria the eastern Moria scene.
Barad-dur, Durthang, Seregost, Thaurband and Naerband share the new
Mordor town scene for now; Minas Morgul keeps its own. Calembel waits
until its scene is finished.

Custom Battle gains Eastern Moria, Pelargir, Serelond and Mordor Town as
siege maps, Rohan Plains II as a field map, and Amon Gelin and
Lothgobel as village maps. Rohan Plains II also joins Rohan campaign
battles: 1 in 9 on cells 81 and 87, 1 in 10 on 88 and 89, 1 in 12 on
80. The 7 new names are seeded in English in all 12 languages; their
translation is still owed.

TAOM_Map is not versioned, so a new LiveInstall test pins the 11
settlement scenes and fails if a reinstall or copy-port reverts them.

Not yet checked in game: none of these scenes has been loaded.

Known limitation: the Mordor town scene has no player spawn point, so a
night walk, a second walk or a disguised entry in those five towns
puts the player outside the walls. Pelargir, Serelond and Lothgobel use
building prefabs the game does not load, so some houses and decorations
are missing. Both stay as delivered by decision; fixes are requested
from Kysaro.

Known limitation: no release channel carries these scenes or this
wiring yet. A release needs a TAOM_Map package that holds them.

Deep review: docs/reviews/rca-kysaro-scene-drop-2026-10-17.md

#### feat(map): v2.0.34 - Lithur gets its three villages

`d8c09626`

Lithur, Gundabad's new castle in the far north, now has villages of
its own: Lith-bosh (swine), Lith-mauz (cattle) and Lith-mîr (a silver
mine). Without them the castle had no village trade, no villager
parties and no rural notables. New campaigns only: an existing save
keeps the map it started with.

The rows live in the unversioned TAOM_Map install; this commit carries
the village table that writes them (add_map_villages.py), so a
reinstall can restore them and its --check fails if they drift from
the map scene. The names show in English in every language until the
next translation run.

#### feat(scenes): v2.0.34 - two new Gondor field-battle scenes

`0e0387e0`

Two new Gondor battle maps join campaign field battles and the Custom
Battle picker.

Ruined Outpost (taom_gondor_battle_001_forceatmo) is a dark scene of
cliffs, caves, river bridges and a ruined tower. It joins the Pelennor
and Anorien cells (85, 86, 90, 91, 92) and comes up in 1 battle of 6
there, 1 of 10 on cell 85.

Pelennor Fields (taom_gondor_pelennor_fields_forceatmo) shows Minas
Tirith and Osgiliath on its horizon, so it gets its own region on their
cell 85 only, at 1 in 10. A test pins it there.

Both names are registered in English and seeded into all 12 language
files; the translation is still owed. The generated header of
sp_battle_scenes.xml no longer carries a hand-kept scene count.

Not yet checked in game: neither scene has been loaded in Custom Battle
or a campaign battle. Ruined Outpost is town-sized (about 22,600
entities), so its load and frame cost are measured at that test.

Known limitation: no release gate checks that a channel's TAOM_Map
holds every taom_* scene TAOM names. On 2026-10-17 the patreon and
public channels lacked both new scenes and the Edoras siege scene, so a
TAOM release there needs a TAOM_Map package carrying them.

Deep review: docs/reviews/rca-gondor-field-scenes-2026-10-17.md

#### feat(troopweight): v2.0.34 - trace main-party troop removals

`36d4dda1`

A player lost about 60 troops from their party right after buying
elite units from the faction emissary. The heroes and the newly bought
troops stayed. No code path in TAOM removes a whole party at once, so
this release adds a temporary diagnostic to find out what did.

Every time regular (non-hero) troops leave the player's party, the
TAOM debug log now gets one [RosterLoss] line naming the troop, how
many left, the party's regular count and the chain of code that
removed them. Players see nothing in game, and nothing about the party
changes. If you lose troops without a reason, send us your
taom_debug log from that session.

For developers: two observe-only prefixes, Patch_MainRosterLossDiag,
on TroopRoster.AddToCountsAtIndex (the funnel for AddToCounts, Clear,
RemoveIf, RemoveTroop and Add) and TroopRoster.SetElementNumber (the
one public write that skips it). Lines are DEBUG and grouped by
tick=, Campaign.CurrentTickCount. The registry entry lists the benign
sources (battles, party-screen upgrades, transfers and Esc, desertion)
and the removal checklist.

Deep review: an earlier watchdog and burst warning were removed. The
watchdog could not see SetElementNumber, and the warning fired on
every party-screen Esc in the exact shape of the report. RCA:
docs/reviews/rca-main-roster-loss-diag-2026-10-17.md

#### feat(harness): v2.0.34 - upstream review; gates judge the right repo

`7e26d77f`

TAOM now tracks yotthani's bannerlord and MithrilForge repositories
between adoption reviews. tools/upstream_watch.py reports how far each
repository has moved since its last reviewed commit (status), writes
per-area briefs from a blobless clone (areas), and moves the reviewed
commit forward once a review record exists (advance). The session-start
hook prints an UPSTREAM AHEAD line with the commit count; the check
runs last, gets what is left of the hook's time budget (at most
5 seconds) and reuses a 20-hour cache, so a slow or offline GitHub
cannot hold a session for long. /upstream-review runs the whole cycle
with every gate kept. A daily unattended run is described in the skill
but is not set up.

The git gates now judge the repository a commit or an add really runs
in. They used to judge the main checkout even when the session worked
in a worktree, so another session's untracked skill there blocked every
worktree commit, and the ModuleData, doc-drift and version-label checks
read the wrong tree. One shared reader in .claude/hooks/_shellwords.py
(the version gate's parser, moved there) takes the directory from a cd
or git -C before the call, then the session's working directory, and
_pybin.sh resolves it to the work tree's top level, so a commit from a
subdirectory is judged as the whole repository. The two gates that run
the repository's own tools skip a commit in another repository.

The shared reader reads at most 256 KB of command text, so a very long
message cannot push a gate past its time limit. Open from the review
(docs/reviews/rca-upstream-review-i3-2026-10-16.md "Open after the
push"): a wrapper word before git (env, command, timeout) still hides
the call from two gates.

#### feat(harad): v2.0.34 - KEYforce's javelins, maces and bardings installed

`f6b5bc7f`

The Harad and Shaghana troops from d94ee1631 now find their new gear:
KEYforce's Shaghana javelins, Harad one-handed maces and the regular
cavalry bardings and saddles are installed in the live Armory, so none
of those slots spawns empty.

- Enlisting under an Aserai, Shaghana or Abanissa lord now issues his
  maces, spears, shields and Harad armour; Shaghana infantry get his
  javelins from soldier up, and archers keep the steppe bows a new
  soldier can draw. The ten older Aserai kits are replaced by 14 kits
  from the Harad tree, identical to Abanissa's.
- His javelins keep their one-handed melee mode: their pieces sit
  outside the starter-kit block that generate_starter_kit.py rewrites.
- Custom Battle shows Aserai as "Harwan" with only the Harad army in
  its troop slots (his d94ee1631 filter, now documented).
- English names for his 32 new items and pieces; the other languages
  show English until the translation run (#810).

Live Armory files this changes (unversioned; backups beside each as
*.bak-keyforce-harad-20261016-120114 and
weapon_descriptions.xslt.bak-harad-javalt-20261016-133303):
LOTRLOME_crafting_pieces.xml, harad/body_armors.xml and
crafting_templates.xslt (his files), harad/weapons.xml and
weapon_descriptions.xslt (three-way merged, TAOM's #805 edits kept),
Languages/loc_harad.xml and loc_LOTRLOME_crafting_pieces.xml.

Known limitation: six Harad troops sit outside their melee damage band
(Mace II at 49 DPS on the tier 5 infantry and archers, his cavalry
sword at 69 on the tier 3 horsemen); left as they are for now (#812).
Release channels need an Armory package with harad/weapons.xml and the
new bardings before this ships (docs/reference/release-process.md).

Refs #805, #810, #811

#### feat(skills): v2.0.34 - prep-tripo-prop skill for Tripo props and icons

`71cc4c6d`

Adds the /prep-tripo-prop skill: the phases that turn a Tripo AI FBX
into a Bannerlord prop or map icon (probe, Blender decimation and UV
prep, a scripted Substance Painter retexture, texture conversion, the
Modding Kit hand-off and the records).

Committed on its own so other commits can pass the hook that refuses a
commit while a skill file is untracked. The scripts and docs it names
(tools/blender, tools/painter, docs/kitbash/map-icons.md and the
pipeline doc edits) are still uncommitted work of the session that
wrote it.

#### feat(mission-perf): v2.0.34 - time the camera and PreDisplay per frame

`0273e2ea`

The hitch probe and the tick profiler now report a cameraMs column in
[TickProfile], [Hitch] and [TickSummary]. It times MissionScreen's
UpdateCamera, which runs the camera tick and the engine's native
PreDisplay (render preparation) inside Mission.OnTick. That time was
booked in missionTickMs (probe mode) or otherMs (full mode) and is now
taken out of that column, so a long frame at battle start can be split
between behaviours and render preparation. yotthani measured the native
PreDisplay at 1.1 to 3 ms a frame (MithrilForge docs/engine/perf.md).

- A void prefix and finalizer on MissionScreen's explicit
  IMissionSystemHandler.UpdateCamera, shielded by PatchShield. It is a
  Harmony category of its own, Patch98_HitchProbe_Camera, applied only
  after the probe. If it fails, the probe and the profiler stay on, the
  install line leaves the camera out, and a WARNING says cameraMs reads
  0.00 as unmeasured.
- The engine calls UpdateCamera only while the mission is Continuing,
  not on a dedicated server, and only when Mission.OnTick updates the
  camera: fast-forward sub-ticks carry no cameraMs.
- tools/perf_runs.py reads the column when present and still reads
  older logs. compare is unchanged: it reads no tick-phase column.
- Measured cost of the probe's managed brackets with the camera: 0.70
  to 0.75 us per simulated frame (four runs), about 0.01% of a 10 ms
  frame.
- The "Enable Hitch Probe" hint names the camera and render
  preparation.
- The heartbeat doc's pin-class line for #806 is in this commit.

Refs #809.

#### feat(harad): v2.0.34 - KEYforce's javelins, maces, bardings, Custom Battle cleanup

`d94ee163`

The Harad and Shaghana regular troops get KEYforce's new kit on top of the
tree rework (ladder bows unchanged):

- One-handed maces replace the native aserai swords on every infantry and
  archer troop: light mace at T1-T2, Mace I/II at T3-T5 (A/B per battle kit).
- Southron infantry carry his javelins: light at T3, Javelin I/II at T4-T5.
- Every cavalry tier carries his cavalry sword (the T3 Horseman was missing it).
- Cavalry ride his bardings per battle kit, light/med/heavy A+B for the
  Horseman, Scout and Cavalry, instead of the shared desert_cloth_harness.
- The six Custom Battle lords use the Harad spears, shields, mace, sword,
  javelin, longbow and heavy barding instead of native weapons.

Custom Battle only (CustomGame nodes, campaigns never load them):
- custom_battle_harad_troop_filter.xslt, after every NPCCharacters node, marks
  non-hero Aserai/Shaghana characters obsolete unless their id starts with
  harad_, so the Harwan and Shaghana slots stop offering Jawwal, Beni Zilal,
  militia, caravan guards, practice dummies and town NPCs (53 troops remain).
- custom_battle_cultures.xslt shows Culture.aserai as "Harwan" in the faction
  list; the campaign keeps "Haradrim".

This build needs the matching Armory (lotraom-assets 03320a65): the javelin
and mace crafting pieces, harad/weapons.xml and the bardings in
harad/body_armors.xml.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>

#### feat(map): v2.0.34 - Gundabad gains Carn Dûm and Lithur

`7245b0f3`

Gundabad gets a third town and a sixth castle in the far north, and an
Arthedain castle gets a new name. New campaigns only: an existing save
keeps the map it started with.

- Carn Dûm (town_G3), the old seat of Angmar, held by the Morzûg clan,
  with four villages: Dûm-bosh, Dûm-lug, Dûm-ghâsh (an iron mine) and
  Dûm-burzum. It starts as rich and as well built as Mount Gundabad.
- Lithur (castle_G6), the first fief of the Grishûk clan. Its three
  villages follow once they are placed on the map.
- Arthedain's castle_AN5 is now Hoarwell Watch, a post on the
  Mitheithel. The old name, Weather Hills, belongs to the broken
  country around Weathertop that the Rangers still walk.

The settlement rows live in the unversioned TAOM_Map install. This
commit carries the tables that write them (add_map_fortifications.py,
add_map_villages.py, now with a Gundabad style copied from Mount
Gundabad and Bagbûrz), so a reinstall can restore them, and their
--check fails if the rows drift from the map scene.

The new names show in English in every language until the next
translation run.

#### feat(kitbash): v2.0.34 - Gondor kit catalog and six buildings in one FBX

`434b8271`

The Gondor kitbash kit is now measured and documented, and six new
Gondor buildings are ready for the Modding Kit in one file:
Scenes/Gondor/buildings_taom/gondor_buildings_a.fbx (a town house, a
two storey townhouse, a watch tower, a barracks, a hall and a market
arcade). Each building has a full mesh, .lod3, .lod6 and a collision
body; all six were ray-probed (no see-through walls, closed gables and
roofs, floors at grade) and their triangle counts match their parts.

The Erebor kitbash tools take --kit erebor|gondor through one shared
kit table and one tested set of part rules
(tools/oneoff/kitbash_kits.py).
A Gondor piece is a part family: sub-objects with their own LODs and
one collision body, decals left out, a bare .lod kept as a base and a
Blender .NNN copy never taken over its plain name. Erebor output is
unchanged: its catalog, tables and prefab mining match the old tools
byte for byte.

Measured and written up in docs/kitbash/gondor/ and
docs/kitbash/gondor-buildings.md: 330 kit parts and 166 props, the
frame rule (the engine reads the kit turned 180 degrees about Z,
proved by prefab rebuilds and a raw FBX compare), 15 joins mined from
51,491 prefab placements and tested in Blender, and the Lond Cirion
placement rules re-checked.

Building the six found three traps, now handled and documented: trim
tops are mouldings, so each trim is lifted by its measured cover
(14.9 mm and 25 mm); buttresses are turned by their bounds, not a
vertex count; corner columns are 2 % wider so the 6 m trim faces do not
share their plane. New tools: blender_assemble_gondor_buildings.py,
blender_kit_frame_probe.py and blender_zfight_attribution.py, which
names the materials of every shared plane and tests whether it is
visible. The probe gains --name for a multi-building file and probes
a gable up to the ridge height the layout states.

Not yet checked in game: the six buildings await their Modding Kit
import. The old Lond Cirion barracks probably has its buttresses inside
its walls (same placement call); see the known-open list.

Deep review and RCA: docs/reviews/rca-gondor-kitbash-2026-10-15.md

#### feat(kitbash): v2.0.34 - Erebor barracks, deep-review fixes and RCA

`581144d3`

Erebor kitbash buildings (#797): a third merged building and the fixes
from a six-lens deep review of the Blender tools and their docs.

New building: erebor_barracks_01, a 24 x 12 m two-storey barracks with
a 12 m stone gable (two panel rows a side, ridge at z 9), two
pilastered doors and dormitory windows. Exported with LOD tiers and a
bo_ body, probed closed, and imported in the Modding Kit.

Assembler fixes:
- A failed export no longer deletes the live FBX. The export checks
  every piece, builds every tier and refuses on a triangle mismatch,
  a .NNN material or a missing collision body before any live write;
  it stages the FBX, copies the old live file to .bak-<mtime>, then
  copies the new one in.
- The old DONE.txt is cleared before arguments are parsed, so a
  rejected run cannot read as the previous verdict.
- The probe layout (eave, half sizes, chamfer, ridge_z) is derived
  from the placements instead of a hand-kept table.
- New --inspect and --frame-probe modes; --preview and --export run
  together; a bare .lod name is a base object; mirrored nodes flip
  their normals; spec helpers for columns, pilasters and courses.
- fbx_frame_compare.py flags pivots, offsets, geometric transforms
  and parent models it does not model, and decodes only vertices.

Docs: the buildings record gets one probe section per building and
the Kit status; design-patterns.md gets the 12 m gable rule and the
mirrored tower ramp (a negative prefab scale is a mirror, never a
yaw 180); meshes.md counts corrected from the catalog.

All three buildings were re-exported with the fixed assembler: same
placements, triangle counts and bounds, so the Kit packages stay
valid. Tests: tools/tests/test_erebor_assembler.py (21) and
test_erebor_kit_tools.py (12).

RCA: docs/reviews/rca-erebor-kitbash-2026-10-15.md. Findings in the
tools the Gondor kit work now shares (prefab transform reader, probe
verdict) are handed off there.

#### feat(harad): v2.0.34 - KEYforce's Harad weapons and names, tier-7 bows

`659d0fda`

The Harad and Shaghana regular troops now carry KEYforce's own spears,
sword, shields and arrows, in four battle kits per troop, and use his
[Harwan] and [Shaghana] names. His spears can be couched from
horseback. The Aserai careers start with his cavalry shield and sword
(and his light spear for cavalry and infantry), and the Shaghana and
Abanissa enlistment kits now come from the new troops; their archers
keep the steppe bows an enlistee can draw.

Harad archers get a tier-7 ladder bow for the Serpent and Red Serpent
marksmen, every Harad archer moves to its tier's bow, and twelve Bow
skills rise to match (the Serpent marksman goes from 125 to 260). No
Harad archer now out-shoots a higher tier.

This build needs the matching Armory: harad/weapons.xml, its crafting
pieces and ladder_harad_bow_t7. Without them 20 Harad troops, the six
Aserai careers and 19 enlistment kits spawn unarmed
(docs/reference/release-process.md has the channel check).

Known limitations: his three longbows are not sold and nothing hands
them out yet. Troops shared by Shaghana, Abanissa, Umbar and the
Haradrim Raiders read [Harwan]. New Harad names and 50 new item names
show in English in other languages until the translation run (#810).
His bow materials use three shaders no release ships yet (#811), and
8 melee-band warnings on his spears stay open (#812).

Refs #805, #803

#### feat(kitbash): v2.0.34 - Erebor great hall, house review fixes

`83b946e5`

The second Erebor building, erebor_great_hall_01, is exported and
passes the probe of the exported file (#797). It is a 21 x 12 m
two-storey hall with 3 m chamfered corners, a hipped slate roof that
rises to a flat deck at 7.5 m, and a door between double-height
pilasters: 247 kit pieces merged into one entity with 25,590 triangles
at full detail. Its floor tiles use their flat LOD as the base tier,
since the floor is seen only through the door and windows; the two
statues of the first draft were removed on review.

erebor_dwarf_house_01 was imported in the Modding Kit and approved after
two review fixes: the paving floor read as roof tiles in game, so the
floor is now the carved stone tile, and the corner columns now carry the
square capital at their foot as well as their head.

The assembler can now lower the base LOD of a single placement, and its
import note marks any material that has no material package in
TAOM_Map. The probe now checks every storey of a wall, the chamfered
corner walls (closed when plain, open when they carry a window) and a
chamfered footprint, so it covers buildings like the hall.

#### feat(kitbash): v2.0.34 - measured Erebor kit and merged buildings

`fe06128e`

The Erebor dwarven kitbash is now measured piece by piece, and whole
buildings can be assembled from it in Blender and exported as one FBX
each, so a building costs one prefab entity instead of one per piece
(#797).

The old catalog in docs/kitbash/erebor was written from thumbnails and
had 14 wrong claims: the castle wall module is 20 m, not 3 m; the castle
tower is square, not round; Roof_Top is a flat deck plate, not a ridge
cap. meshes.md now holds the measured size, pivot, slope, openings, LODs
and collision body of all 145 pieces. design-patterns.md holds the join
rules, taken from 25,332 placements in the authored TAOM_Map prefabs and
tested in Blender, and the frame rule: the engine reads each Erebor FBX
turned 180 degrees about Z from Blender's import, so a prefab offset
(x, y, z, yaw) is (-x, -y, z, yaw) in Blender.

The first building, erebor_dwarf_house_01, is a 9 x 6 m gable house of
63 kit pieces merged into one entity with two LOD tiers and a collision
body. Its floor sits on the terrain. It is exported to TAOM_Map's
AssetSources and passes a probe of the exported file (closed walls,
gables and roof, planned openings only, triangle counts as briefed). Its
first Modding Kit import is still owed.

New one-off tools: the kit catalog, the prefab join miner and rebuild
renderer, the join tester, the building assembler and its probe, and a
Blender-free FBX frame compare, with tests for the pure Python ones.
docs/kitbash/erebor/blender-build-prompt.md drives the work and
docs/kitbash/erebor-buildings.md records each building.

#### feat(war): v2.0.34 - chronicle_open is a dry run without confirm

`629dcd08`

taom.chronicle_open was rated Tier B, but an opened event saves its
row, applies its effects and pays its momentum award, and no command
reverts them: Tier C by the console's own definition. Decision D33,
the recommended action:

- taom.chronicle_open <id> now runs the same checks and prints what
  a confirmed open would do: the narrative occurs, the siege opens
  its window until day N, it resolves Fell at once because an
  attacker already holds the target, or it stays Pending and why.
  It adopts nothing, notes no phase day and changes no row.
- taom.chronicle_open <id> confirm acts as before. Any other second
  word is refused with the usage, before any lookup.
- The refusals are unchanged: a co-op client, the chronicle off in
  MCM, after the War of the Ring has ended, an unknown id and an
  event that is no longer Pending.

Tests first: seven service tests (a siege, a held target, a
narrative, a failed precondition and three refusals, all as dry
runs) and parser and output tests; with the dry run ignored, four of
the service tests fail. dev-console.md and war-chronicle.md rate the
command C, the in-game steps add confirm, and the test counts are
recounted.

Refs #765, #792.

#### feat(tools): v2.0.34 - WildMesh3D mounts onto horse_skeleton (#794)

`ba6eb1d4`

Five animals from the WildMesh3D "Realistic Animals" pack are on their
way in as horse-skeleton mounts with their own animation: a boar, a
warthog and a mountain goat for the Dwarves, a deer and a moose for the
Elves. Each is a second variant beside the war ram, the great elk and
the Animalia elk and moose; nothing is replaced. Stages 1 to 8 of the
quadruped-pack workflow are done: the meshes (LOD0 to LOD5), 1K
textures, 74 clip masters wired to horse_skeleton and 96 clips are in
the live Armory. The Armory data that makes them rideable comes next,
so no player can ride one yet.

The pack ships .blend files, so tools/blender/export_blend_pack.py
(committed in 776054c0c with the bone map and the first measurements)
replaces the Unreal export, and resamples the pack's 24 and 25 fps
clips to the 30 fps the clip generator counts. Every WildMesh neck is
far shorter than a horse's, so all five use a keep-neck profile; the
boar and warthog come out leggier than the source.

This commit finishes the tooling:
- gen_animalia_anim_clips.ps1 takes -Pack wildmesh beside the Animalia
  default, which still verifies all 54 of its clips.
- measure_animalia_clips.py compared a hoof touching down on frame 0
  with the closing frame, which repeats frame 0, so four walks and
  trots measured three plants for four hooves. It now looks at the
  frame before the wrap; the Animalia elk values are unchanged.
- The re-measured gaits of the boar, deer, mountain goat and warthog.

Several goat and warthog gaits in the pack do not loop, or curve
sideways under forward names, so the warthog canters and gallops on
the boar's clips and the goat gallops on the deer's. Docs: a feature
page, the workflow's stage notes and gotchas, the tools catalogue, the
feature map, the index and three lessons.

#### feat(careers): v2.0.34 - one choice rule, forks, retrain, tree versions

`b9a36f41`

Career choices now go through one rule. The career screen used to
restate its take check three times and had no refund check at all; a
single rule now decides both the click and how each diamond is drawn,
and a dimmed diamond says why in its tooltip ("Requires Level 10",
"You took another keystone in this tier.", "No free career points.").
That reason line is the only change players see on today's careers.

The rest is engine work for the career redesign (#769), inert until a
kingdom's careers are regenerated with the new attributes:

- Forks: a choice group with max_picks holds at most that many picks;
  the others dim with their own red rim. A fork pick can be refunded
  freely only while the screen is still open; after that it is locked
  in. Groups without max_picks keep today's rules, keystone limit
  included.
- Point schedule: point_levels on a career gives career points at those
  levels only ("Next point at level N" under Free Points). Careers
  without it keep one point per level.
- Retrain: refunds every choice except the career's first for
  1,000 gold plus 250 per level (retrain_gold_base and
  retrain_gold_per_level on <Careers>). It keeps the career, its
  unlocked tiers and its flags. Confirm is disabled when the player
  cannot pay. Leaving a career that holds fork picks costs the same;
  leaving one without them stays free.
- Tree versions: tree_version on a career refunds a hero's choices once
  when a save meets a newer tree, only when the new tree loaded, and
  tells the player only when something was refunded. Character
  creation and a career switch stamp the version, so a new player's
  picks are never refunded.

The group +/- and hover commands, bound by no screen since #388, are
gone with their three tests. ICareerSwitchService is gone: nothing
faked it (ADR-002). Two deliberate departures from the plan: there is
no separate choice service, because the screen is the only caller that
takes or refunds a choice; and a career switch costs gold only when the
old career holds fork picks, so old trees still switch free.

Review: /deep-review, seven lenses, 0 critical, 0 high, 2 medium (a
price that wrapped negative on absurd config and made a switch free;
a feature doc the change had made wrong), both fixed, plus a
convergence pass. RCA docs/reviews/rca-career-e4e7-2026-10-14.md.
Translation of the 15 new strings is owed (English until then).

Checks: dotnet 15,285 passed, 6 skipped, 2 failed (the pinned game
build, and language coverage with 32 older missing keys and none of
these). validate_moduledata.py reports 1,867 errors, all broken item
refs in the Harad, Umbar, Abanissa and Shaghana files after the live
Armory's Harad items were rewritten the same morning; none is in a
file this change touches.

Not tested in game. On today's careers: load a save (no refund message,
points unchanged); hover dimmed diamonds in each tier (the reason line
fits); Retrain and "Next point" stay hidden; a career switch shows no
cost. With a local test career (max_picks="1" on one group,
tree_version="2"): the red rim and the fork and locked-in reasons;
Retrain price, dimming, and Confirm greyed when short of gold; the
switch cost prompt, Cancel staying in the picker; the one-time refund
message on an old save.

Refs #769.

#### feat(war): v2.0.34 - hunt keeps going after a flee

`cbacbf1f`

Escaping the Eye of Sauron's hunter no longer ends the hunt. If you
get away from his host, he picks up your trail again within the hour.
Only a battle that someone wins against him, or a defeat of his side
by anyone, ends it; a fight he wins elsewhere does not.

Once he is within one and a half times your sight range he now rides
faster than you, so you can no longer keep him just out of view by
riding on.

While you ride in another lord's army no hunt begins, and a hunter
already on your trail holds back until you leave that army.

#### feat(war): v2.0.34 - chronicle phase anchors and lost sieges

`bad32ad0`

War Chronicle events tied to a War of the Ring phase now count from
the day the chronicle first saw that phase begin, when that is later
than the configured phase day. Switching the War of the Ring on late,
lowering a phase day, or turning on test mode mid-campaign no longer
fires every overdue event and siege on the same day: they arrive one
by one, as they would have on schedule. A campaign on schedule keeps
its usual days, and the day is kept in the save.

When a chronicle siege opens and one of its attackers already holds
the town, the siege now counts as fallen: its fell rewards and
momentum are paid, the usual fell message shows, and the events that
follow it (Minas Tirith after Osgiliath) still happen. A town held by
some other kingdom still skips the siege.

#### feat(war): v2.0.34 - chronicle events, army focus, momentum award

`84f5e63a`

#### feat(war): v2.0.34 - the Eye of Sauron hunt

`34b0e25a`

Adds the Eye of Sauron hunt (#765 M8): a Ringwraith, or Glorfindel for an
Evil-side player, hunts a player whose deeds draw the Eye. Single-player
only in v1.

#### feat(fiefs): v2.0.34 - a Fiefs button on the campaign map bar

`ca248d1b`

The campaign map bar now has a Fiefs button beside Kingdom (#789). A
click opens the fief hub, as the F6 key does. When the hub cannot
open, the button is greyed out and its tooltip says why: Fief
Management is off in the mod options, you own no fief, or the map
cannot open a menu now. The tooltip names the F6 key.

F6 and the button share one opener and one map check, so they cannot
behave differently. That check now also matches vanilla's own map
rules: it refuses while the escape menu is open, while you are a
prisoner or in an encounter, and in the first frame after a menu
opens. The field camp's Make camp button uses the same check.

The unused FiefManagementNavItemVM is deleted. Reviewed by a
seven-lens deep review: docs/reviews/rca-fief-nav-button-2026-10-09.md.
Built and tested on the installed v1.5.5; /engine-bump is still owed.
The two new strings need the translation run (#782).

#### feat(war): v2.0.34 - war ledger, timed war effects and rally

`d340a725`

Adds the first part of the War Chronicle (#765): a small catch-up layer
that keeps the AI war of the Free Peoples and the Dark Powers closer to
a stalemate, so the player decides the war.

- Timed war effects: a kingdom can get a temporary bonus to volunteer
  production (towns, villages and castles) or to its captured lords'
  chance to escape. Effects are small and clamped; the MCM setting
  "War Effect Strength" (0 to 2) scales them. The player's own clan
  never gains or loses from them.
- Rally: an AI-ruled kingdom at war that has lost 25% of its fortresses
  since the start gets more volunteers and faster escapes; at 50% lost,
  more of both. The bonus ends when it recovers past 15% and 40%. It
  can be turned off in the MCM group "War of the Ring/Rally".
- War ledger: one log line per kingdom per day records the balance of
  the war, and tools/analyze_war_ledger.py turns a log into a stalemate
  report or compares two runs.
- Console: taom.war_effects, taom.war_effect_add, taom.rally_status and
  taom.print_war_ledger.

The escape roll mirrors vanilla's daily escape check, including its
vetoes and perk factors. A save made before this change starts its
rally baselines on the first day after the load, with a warning in the
log. Under a co-op session the rally and escape rolls run on the host
only.

Reviewed by an eight-lens deep review with adversarial verification;
all confirmed findings are fixed.

Refs #765.

#### feat(companion-tactics): v2.0.34 - formation presets save and load

`1b9c2423`

Formation presets in the Order of Battle screen now work (#779).
Before, Save stored a name only and Load did nothing. Save now
records the type, the captain and the hero troops of each formation.
Load puts them back through the same steps that a player uses by
hand, and checks each step.

A formation type changes only when the game allows that by hand: a
formation that is the only one of its type, while the battle has
troops of that type, cannot change. A hero goes only into a
formation whose saved type is in place. In a siege, saved cavalry and
horse archer types become infantry and ranged types, as the game's
own layout memory does. Heroes or formations that are not in the
battle are skipped, and the message counts them. Only the general of
the battle can use presets, and only in a campaign.

A preset does not store troop shares or filters, and it cannot swap
two formations that are each the only one of their type (#787). The
feature stays off by default (MCM: "Enable Formation Presets") until
it is checked in game. Every new string is localized; the
translations are in #782.

Reviews: a seven-lens deep review, a convergence lens and two Codex
passes. Findings and fixes:
docs/reviews/rca-formation-presets-2026-10-09.md. Save and Load
follow the behaviour of yotthani's HoN FormationPresetManager; the
code is TAOM's own.

#### feat(stability): v2.0.34 - mission start, freeze and map guards

`ecaa67d2`

Four stability and performance fixes, adapted from yotthani's
VanillaTuning (MIT; the notice is in THIRD-PARTY-LICENSES.txt).

Mission start guard (#775). A battle whose start calls threw, from
any module, loaded again in every frame, for ever. Now the guard logs
a call that throws, with the type and module that own it. It shows
one message, cuts that call short and lets the mission start. MCM:
TAOM Crash Report, "Survive Mission Start Failures", on by default.

Skeleton buffer guard and watch (#776). The engine reserves room for
the skeletons that it draws in each frame. It does not check the
limit, and one reservation too many freezes the game for good. The
guard changes the engine code at the two reservation sites, so that
an overflow reuses the first slots. A figure can then show wrong
bones for one frame. The watch logs the peak fill of each mission. If
nothing guards the buffer and a mission reaches 90 percent, one
message tells the player to lower the battle size. MCM: TAOM Crash
Report, "Skeleton Buffer Guard" (needs a restart) and "Skeleton
Buffer Watch", both on by default.

Settlement nameplate cull (#777). The campaign map updated about a
thousand settlement nameplates in every frame, hidden ones included.
Now the map skips a nameplate that is hidden and cannot become
visible in that frame. This saves frame time on the map, most in fast
forward. MCM: TAOM Battle Load Diagnostics, Map Performance, "Cull
Hidden Settlement Nameplates", on by default. An A/B measurement is
owed before release.

Map view release (#778). In yotthani's measurement, each screen or
menu over the campaign map left four render targets of the map view
in GPU memory. They stayed until the next mission. Now every 20th
cover releases them when the map returns, and that return shows the
short loading screen. MCM: TAOM Battle Load Diagnostics, Map
Performance, "Release Map View Memory" (on) and "Map View Release
Interval" (1 to 1000).

The BattleLoad loading window now also closes when TAOM's own mission
setup throws. TAOM adds its closer in a finally around that setup.

Reviews: two deep-review rounds, a final-state review with a critic,
two convergence lenses and two Codex passes. Every finding is fixed
or recorded in docs/reviews/rca-yotthani-adoption-2026-10-08.md.
Follow-ups: #779 (formation presets) and #780 to #786. Owed: in-game
checks for #775 to #778, and translations of the two new messages
(#782).

#### feat(tools): v2.0.34 - kitbash tools for new dwarf beards and hair

`9bb78ae5`

Tooling for #759: five new dwarf beards and five new hair styles, made
by recombining the hair cards of the existing dwarf grooms. Nothing
changes in game with this commit. The new grooms reach players after a
Modding Kit import and an append to the Armory skins.xml.

- tools/blender/groom_kitbash.py: pure Python, stdlib only, so Blender
  can import it. Splits each groom into its loose hair cards, classes
  each card by where it roots on the male dwarf head (moustache, chin,
  cheek, sideburn and braid for beards; crown, back, side and braid for
  hair), and validates and resolves recipes. 106 unit tests.
- tools/blender/kitbash_grooms.py: Blender script with three modes.
  inventory classes every card of the 22 existing grooms and renders
  region views. preview renders recipe candidates on the head. build
  joins the chosen cards, writes one UV layer per card, keeps only the
  family's card and ring materials (the ring slot only when rings are
  used), keeps or drops the 101 face-morph channels, exports, and
  re-imports the file to check UVs, materials, weights and channels.
- tools/groom_contact_sheet.py: labelled contact sheets of the renders.
- tools/blender/specs/dwarf_grooms_kitbash.json: 19 candidates. The
  picks are beards b03, b04, b05, b06 and b08 and hairs h01, h04, h07,
  h08 and h09. The final map holds the pilot only: beard b04 twice,
  with and without channels, for an in-game A/B, and hair h07 as
  Dwarf_Hair_K.
- tools/oneoff/append_dwarf_grooms.py: appends beards and hairs to the
  four dwarf skins that carry grooms, and removes the temporary test
  beards. It only appends, because a face key stores the groom as a
  list position, and it refuses a removal that would shift a later
  entry. Dry run by default; refuses to write the live file while the
  game or the Kit runs. 44 unit tests.

The pilot FBX files are built and installed in the live Armory, which
git does not track: Race Test/Beards/SK_Dwarf_Beards_B.fbx and
Race Test/Hair/Dwarf_Hairs_ThirdBatch.fbx. Still owed: the Kit import
and save, the skins.xml append, the in-game A/B, then the other four
beards and four hairs.

Refs #759

#### feat(markets): v2.0.34 - Arthedain and Lindon sell their own gear

`685db7e4`

Arthedain and Lindon towns now stock their own people's gear instead
of only their donor culture's armour.

- Arthedain's markets sell mostly the Arnor armour and the Numenorean
  swords and poleaxes, about nine in ten draws. Gondor fills only
  what the Arnor kit lacks: spears, lances, bows, shields, harness and
  more swords.
- Lindon's markets sell Rivendell's silver and silver-gold armour,
  never the gold, the plain capes, leather and light pieces its troops
  wear, and every Rivendell weapon.
- The Arnor King's Crown is never sold, looted or given as a prize.
  An Arthedain hero earns it as the first choice on the lord's gear
  ladder's head rung, and King Arvegil II starts with it.

Kingdoms no longer list two policies the game does not have (Lord's
Prerogative and Religious Privilege, in all 15 TAOM kingdoms); the
game already ignored them, so nothing changes in play. Gondor's own
policy was misspelt the way vanilla misspells it, so Gondor started
without it: new campaigns now start Gondor with Land Grants for
Veterans (more veteran militia, 5 percent less village tax).
Existing saves keep their policies.

For modders: a culture's market config takes <Stock> rows (source
culture, armour, weapons or everything but armour, an id pattern, a
draw weight), and the lord's ladder takes <LadderPieces> to offer a
named piece on an armour rung. The validator fails a Stock row or
ladder piece that names nothing, and a test fails on any kingdom or
culture policy id the game does not define.

RCA: docs/reviews/rca-market-stock-and-policies-2026-10-08.md
Closes #755
Closes #756

#### feat(arthedain): v2.0.34 - playable kingdom of Arthedain (Arnor)

`b0246774`

Arthedain, the North-kingdom of Arnor, is a new playable realm: a
what-if in which Arvedui's line survived. King Arvegil II rules from
Fornost Erain over five small clans and twenty lords, with three towns,
seven castles and 24 villages in the north-west of the main map. It is
weak by design, few lords and poor towns, and at war with Gundabad
(Angmar) from the first day. At the War of the Ring's Full War every
evil realm turns on it, as on every free realm.

- A Dunedain troop tree of 23 troops in the Arnor armour: levies and
  Fornost infantry, Rangers of the North with their own ranger bows,
  the Citadel line up to the King's Guard of Fornost, and a short
  cavalry branch. Lords, rulers and heirs wear the Arnor noble kit, and
  the king wears the Arnor crown.
- Arthedain can be picked at character creation, with its own
  narrative, starting gear and three careers (Ranger of the North,
  Warden of Fornost, Knight of Arthedain), and on the faction-select
  map.
- It shares Gondor's survival bonus, charge damage, banner bearers,
  Citadel Guard rally and Castar costs, has its own Armourer's
  Commission, and shares Gondor's Deep Seam quest.
- Blue Craig gains a second town, Luinkrag, with three villages.

The 18 careers of the six cultures promoted in August (goblin, Misty
Mountains, Blue Craig, Lindon, Shaghana, Abanissa) now apply their
battle abilities; before, they showed the activation and did nothing.

A new campaign is needed to play Arthedain. Every new settlement sits
at the end of the map's settlement list, so a save from before this
change drops them and keeps all of its existing settlements.

Translations of the new text are queued in #754. Follow-ups: #755
(donor-culture market weapons) and #756 (two policy ids no kingdom can
use).

RCA: docs/reviews/rca-arthedain-2026-10-07.md
Refs #753

#### feat(shaders): v2.0.34 - show shader compile progress during rebuilds

`82645b53`

After a mod list change, a game update or a fresh install, the game
rebuilds about 1,340 shaders before the main menu. Every core is busy
and the loading symbol freezes for a minute or more, so the game looks
hung.

TAOM now shows a small window over the game while that runs: "Compiling
shaders: 412 of about 1336", with a line saying the game is not frozen.
It counts the shaders from the game's own log, remembers the last full
total for the next time, never takes the focus, and closes at the main
menu. While it is up the game runs at below-normal priority so the rest
of the computer stays usable; the priority is restored afterwards.

It stays off on a dedicated server and when yotthani's VanillaTuning is
loaded, which shows the same notice. The idea is from VanillaTuning;
the code is TAOM's own.

The three new texts are in English for now; the translations follow
with the next translator run.

Not-tested: in game; the check is owed (#752,
docs/features/shader-compile-notice.md).
Review: the Opus deep-review lenses are still out on the weekly model
limit; tests (16 new, full suite at its baseline) and a manual review.

Refs #752

#### feat(shaders): v2.0.34 - keep the shader cache across mod list changes

`372773d1`

Bannerlord throws away its compiled shaders whenever the list of active
mods changes, even by a mod with no shaders of its own, and a TAOM start
then spends about 40 seconds recompiling roughly 1,340 of them. The
check runs before any mod loads, so no mod can stop it from inside.

TAOM now ships yotthani's ShaderCacheKeeper (MIT), a small
xinput9_1_0.dll placed beside the game's executable through the
launcher. When the mods that bring their own shaders are unchanged, it
updates the mod list stored with the cache and the game keeps its
shaders. Changing TAOM, TAOM_Map or the Armory, or a game update, still
rebuilds them as before. Gamepad calls pass through to Windows.

Four problems in the original were fixed before it ships: the game could
not start on a PC where Windows is not on C:, an updated shader mod
could leave old shaders in use, a multiplayer start through the BLSE
launcher could throw away a good cache, and with very many shader mods a
change could go unnoticed. To switch it off, put an empty file named
ShaderCacheKeeper.off beside the DLL.

Source and tests are in Native/ShaderCacheKeeper (13 hermetic cases and
4 parse tests, 0 failed; the fixed cases failed against the original
first). The DLL is built and copied into each channel's bin folder at
release time, testing channel first.

Known limitation: the launcher never deletes this DLL, so retiring it
later needs a do-nothing build or a launcher removal step.
Not-tested: in game; the check is owed on the testing channel (#751).
Review: the Opus deep-review lenses hit the weekly model limit; a
manual pass found one test-fixture bug, fixed
(docs/reviews/rca-shader-cache-keeper-2026-10-06.md).

Refs #751

#### feat(arena): v2.0.34 - tournaments keep Free and Evil apart

`646846cb`

Tournaments no longer seat the other side. A player found Mordor orcs
competing at Minas Tirith: vanilla fills the sixteen slots from the
lords, wanderers and garrison in town and never looks at culture or
side.

Now a tournament of the Free Peoples seats no lord, wanderer or troop
of the Shadow, and a tournament of the Shadow seats none of the Free
Peoples. Each one is replaced by one of the town's own troops, so the
bracket stays full. A town's side is its owner's, so a captured town
follows its conqueror, and a town held by Khand, Umbar, Shaghana or
Abanissa bars nobody. Neutral lords and wanderers, the troops the town
recruits, you and your clan always compete. Dunland counts as the
Shadow. With fewer lords in the field the prize and the renown are
smaller. Off-screen tournaments follow the same rule.

Toggle: MCM, Tournaments, "Keep Enemy Sides Out" (on by default).

Refs #744 (in-game check owed). Review and RCA:
docs/reviews/rca-tournament-alignment-filter-2026-10-06.md

#### feat(siege): v2.0.34 - siege troop picker and troll siege role

`92839bc5`

Two features for siege wall battles, so trolls stop standing idle at
ladders they cannot climb (#734, #735).

Siege troop picker (#734). Before every wall battle you fight, as
attacker or defender, the vanilla Manage Troops screen opens, the same
one hideouts and the lord's hall use. Cave and hill trolls start
unticked; tick them to bring them. Troops you leave out stay in camp:
they do not spawn, take no losses, and a lost wall battle with men
still in camp continues the siege instead of ending in capture. The
picker covers your party, your whole army when you lead it, and your
own garrison when you defend your own fief. It never appears in
sally-outs, relief-force battles, the lord's hall, co-op or on a
dedicated server, and it is refused if its spawn-total patch did not
load. MCM: Battle Tactics / Siege Forces. Engine seams: Patch102 (a
prefix on PlayerSiege.StartSiegeMission and one on
DefaultBattleMissionAgentSpawnLogic.InitWithSinglePhase) and a new
TroopSupplierProbabilityModel override.

Troll siege role (#735). In every wall battle, for every side and every
army, AI-controlled trolls never use ladders, siege towers, rams, gate
levers or siege engines. They stand clear while a ram works, then smash
the outer gate and the inner gate, then hold the courtyard; defending
trolls hold the gate at ground level instead of climbing the walls.
Player orders are obeyed, except that Charge before the gates fall
sends trolls to the gate. A troll's melee blow on a gate is multiplied
by gateDamageMultiplier in ModuleData/siege/creature_siege_role.json,
a provisional 2.0 until it is measured in game. No Harmony patches:
the detachment cost and damage scaling overrides on the four shared
models, per-agent navmesh exclusion of ladder and tower faces (at most
6 ids per scene), and one mission behaviour. MCM: Battle Tactics /
Creature Siege Role.

The troll role is a behavioural port of The Old Realms' TOR_Core
(GPL-3.0, read at commit 5ccd0b91): TAOM reproduces the behaviour on
its own seams and copies no code, constants or identifiers. The
provenance register row is updated to behavioural-port.

Built against Bannerlord v1.5.4; every engine member used was checked
on the installed DLLs. The API snapshot rows were hand-edited rather
than regenerated. The orientation trap index merged its two Armory rows
into one to make room for the ladder-queue row. Deep review, fixes and
lessons: docs/reviews/rca-siege-forces-2026-10-05.md.

Known limitation: the in-game checks are still owed (troll climbing
spike, gate strike, defenders engaging, gate break timing to set the
multiplier, the campaign checklists in docs/features/siege-forces.md
and docs/features/creature-siege-role.md). With trolls unticked, the
vanilla "room for more soldiers" prompt appears on most assaults.

### Fixes

#### fix(tools): v2.0.34 - release packager refuses Custom Battle test troops

`a4b5d625`

The release packager now refuses to package a module whose ModuleData
defines a taom_test_ troop, in a dry run too, and names each file.
The /release pre-flight already blocked test troops in the repo, but
a channel package is made from the game install, where a deploy never
deletes a file: the v2.0.35 testing package of 2026-10-18 shipped the
five WildMesh test riders that way. A dry run against that package
now exits 2 and names ModuleData/test_riders/wildmesh.xml.

A name key alone ({=taom_test_x}) defines nothing and passes, as do
files outside ModuleData and excluded backup sidecars. The release
skill and release-process.md say the Phase 8 prune must remove such a
leftover before the dry run passes.

Review: docs/reviews/rca-wildmesh-rider-removal-2026-10-18.md
finding 1.

Tests: tools/tests/test_package_release.py, 147 passed (4 new).

#### fix(tools): v2.0.34 - battle-load triage spots a frozen shader count

`89374fc4`

The battle-load triage tool no longer calls a render-wait hang a
draining shader cache when the log went quiet long before the
watchdog fired. A player bundle from 2026-10-18 showed two samples
falling from 3128 to 3066, then nothing for about 290 seconds. The
game writes that line once per second, and the watchdog fires only
on a count unchanged for 60 seconds, so the count was frozen.

When the gap between the last WaitingForRender line and the watchdog
line is 30 seconds or more, the verdict now says the count is frozen,
that the log alone cannot separate a slow compile from a main-thread
wedge, and points at the compile_shader lines in the full rgl_log and
a hang dump. The timing summary and next steps say the same.

Tests: tools/tests/test_triage_battle_load.py, 158 passed.

#### fix(animation): v2.0.34 - orc gaits installed, strict variant gate

`a4f42f42`

The per-soldier orc gaits are now in the game. Mike imported the 36
orc animation masters into the Armory on 2026-10-18; the clips were
generated, saved by the Modding Kit and bound, so each orc soldier
walks and runs with one of three gaits (the main hunched limp, B or C)
at its own step size. A first Custom Battle split 125 orcs 38, 37 and
50 across the three gaits, with no missing clip, set or duplicate name
in the game log.

The swing alternative probe is on by default while the swing clips are
tested (Mike). It only logs: with no act_taom_ code loaded it writes
one line per mission. No saved value of the setting exists anywhere
(no deployed or released build had it off, and the only TAOM.json
predates it), so the rule that a changed default's tooltip mentions
saved values does not apply. Owed: rename the setting with the default
off before the next release or before any act_taom_ code reaches the
Armory.

The reinstall gate is stricter: bind_orc_goblin_locomotion.py --check
now fails when an orc gait variant has vanished with its clips, which
is what a reinstall leaves. --allow-no-clips covers an install before
the Kit import, and --require-variants is gone.

The docs record the install, the full rollback and restore steps, the
probe default, the DualWield limit Mike accepted (its few dual-wield
troops keep their own set) and the corrected anf_cyclic rule (not
required on locomotion-table clips). Review and lessons:
docs/reviews/rca-orc-gait-install-2026-10-18.md.

Live LOTRLOME_Armory changes (unversioned): ModuleData/action_sets.xml
(+55 lines, as_orc_warrior_b and _c; backup
action_sets.xml.bak-orc-goblin-bind-20261018-092156), 72 packages in
Assets/Race Test/Mordor/Orc/animations, animations_b and animations_c,
and 36 RuntimeDataCache entries. The 2026-10-14 clips are in
E:/LOTRAOMAssets/backups/orc_locomotion_20261017-224440.

Issue #817; refs #791.

#### fix(hooks): v2.0.34 - config override unlocks one session only

`88e80545`

config-protection.sh lets an edit to settings.json,
Directory.Build.props or an ADR through only when an override file
exists, created on the maintainer's explicit request. The hook built
the file name from CLAUDE_SESSION_ID, which the harness never sets, so
the name was always /tmp/claude-config-override-none, and that one
file unlocked protected edits for every session on the machine while
it existed. The hook now takes the session id from its input (the
session_id field every hook receives), then from
CLAUDE_CODE_SESSION_ID, so an override unlocks only the session that
made it. With no id there is no override at all.

#### fix(tools): v2.0.34 - map icon tooling review fixes and shared job file

`041808e5`

The Tripo-to-map-icon tooling first landed in 71fea53c8, whose message
lists files rather than what they do. In short: a Blender builder that
rebuilds the Carn Dum campaign map icon (town_G3) from clean solids,
because the Tripo model looked like melted wax at any triangle count;
an unwrap tool that gives each texture set its own unique UVs; and
scripts that drive Substance Painter 12.1.5 over its remote-scripting
port to bake, paint and export each set. The approved shape has no gate
horns or sigil, tower ribs that run into the ground and half as many
houses. That commit also carried other sessions' work: the Erebor
dress fix (DwarfDressWearerTests), KEYforce Harad javelin follow-ups
and a translate_with_claude.py change.

This commit applies the deep review of that tooling:

- Every Blender script that a caller polls now writes its completion
  file through tools/blender/blender_job.py. The old report goes first,
  "ok" is written only when the work returns, and any exception writes
  "error" with its trace. Before, a failed export or a stale file could
  read as success. The prep script clears its old report before it
  parses arguments.
- convert_tripo_prop_textures.py no longer defaults its output to the
  live TAOM_Map textures folder: --src and --dst are required, and an
  existing file is replaced only with --force. Without --stem it
  converts every texture set to t_<set>; before, it took the first set
  and silently dropped the other six.
- painter_new_project.py refuses to close a project with unsaved work,
  fails when the mesh load or the bake fails, and reads each set's
  resolution from the unwrap report. painter_run.py tells a timeout
  from an unreachable Painter. painter_export.py takes the exact preset
  name and only power-of-two sizes.
- unwrap_texture_sets.py breaks ties the same way on every run, flips
  normals under a mirrored transform, fails when inverted faces exceed
  0.1% of the area, and reports per part how much hidden area keeps
  full density.
- The builder (v11) starts each terrace riser and the outer wall where
  the geometry that hides them ends. That raises the town set from 9.7
  to 11.9 px/m and the walls set from 11.8 to 12.9.
- One recipe file, tools/painter/painter_stacks.py, replaces the three
  stack scripts. It refuses an unknown ARGS key, a set name that is not
  in the project and a run that would paint nothing.
  render_painter_preview.py (with --albedo for the unlit colour)
  replaces both preview scripts. The tiling route is
  retired. measure_tripo_layout.py measures a Tripo model's footprint
  and heights for a rebuild.

35 new tests cover the job file, the converter, painter_run and the
unwrap rules. The icon's texturing is not finished; it continues in the
next session.

#### fix(hooks): v2.0.34 - upstream check in its own hook; Codex fixes

`407570ec`

The upstream review check now runs as its own SessionStart hook,
.claude/hooks/upstream-watch.sh, registered in .claude/settings.json
with a 10-second limit of its own (the maintainer's request). It no
longer shares session-start.sh's limit with the game-version and
Armory drift checks, so a slow GitHub cannot cost those banners. The
two probes left in session-start.sh share one deadline counted from
the hook's first command, so that hook ends inside its limit even
when both probes are slow.

The Codex review of the previous push found gaps in the shared
repository reader that the git gates use, all fixed: a command that
commits in two repositories had only the last one checked (the
commit gates now deny it and ask for one repository per command, and
the broad-add confirm says its list is incomplete); a cd inside
parentheses leaked out when the closing parenthesis touched a
semicolon; and pushd moved the directory while popd was ignored.
Codex found no defect in the garrison and patrol fix or the busy-time
logging.

#### fix(troopweight): v2.0.34 - garrisons and patrols pay no size penalty

`c2449a30`

Garrisons deserted every day under the troop-weight penalty. The
penalty shrank a garrison's size limit by the weight of its elite
troops; vanilla desertion then removed a quarter of the excess each
day and the garrison's morale fell with it, while lords kept refilling
the garrison to its full, unweighted limit. Yotthani's campaign run
counted about 20,600 deserters in the free block over 150 days,
19,600 of them in the four elven realms (yotthani's reading of the
cause; TAOM confirmed the mechanism by reading the code).

Patrol parties had the same flaw in another form: nothing trims or
refills a patrol to its reduced limit, so an elite patrol stayed over
it for good and crawled under vanilla's over-limit speed penalty.

Garrisons and patrols now pay no size penalty, and the party screen
and the clan screen show their real counts against their real limits.
Lord parties and caravans are unchanged.

Not yet checked in game: an elven garrison over a month, and the
speed of an elite patrol.

#### fix(tools): v2.0.34 - safer port tools, artist name and merge helpers

`a9f2d7ed`

Shaghana lord parties now field Shaghana's own Red Serpent troops
instead of the Harwan Serpent line, as their clan heraldry intends
(27 stacks in nine party templates).

Tools for porting an artist's drop into the live Armory:
- register_armory_names.py finds item and piece names with no English
  row. --check fails on a new one (a baseline of 611 existing names
  can only shrink), --apply writes the rows. It is in the artist
  brief's pre-commit checks.
- merge_live_file.py three-way merges a live CRLF file with the
  artist's versions from the mirror, writes nothing on a conflict and
  refuses XML that does not parse. First slice of #813.
- generate_enlistment_rosters.py: --reseed rebuilds one culture's
  kits, and the Harad archer chains get their steppe bows from a table.

Fixes:
- generate_starter_kit.py refuses to rewrite or revert a marker block
  that holds another author's rows; --verify reports such a block.
- validate_moduledata.py: the melee ladder pass now reads the
  --moduledata and --game-modules it is given, and the armour class
  table check says "not verified" for another tree (#812, tool half).
- translate_with_claude.py: --max-entries 0 is a cap of zero (it used
  to mean no cap on paid calls); --sync-ids --seed-only seeds rows for
  free.
- generate_tavern_mercenaries.py is a dry run unless --apply.
- Three generators no longer name the Harad armour retired on
  2026-10-10 (#804).

Refs #804, #812, #813

#### fix(mission-perf): v2.0.34 - cameraMs review fixes in tools and docs

`c7612577`

Fixes from the second review of the cameraMs column (#809). The game
code is unchanged apart from comments.

- tools/perf_runs.py: a mission row whose windows mix logs with and
  without cameraMs no longer drops the measured camera time; the column
  shows when any window has it. No log TAOM writes produces such a row
  (one log is one build), so real reports are unchanged.
- The test helper for old-build lines now puts the camera time back in
  otherMs, as an old build wrote it, and a test checks that such a line
  sums to its wall clock.
- The engine calls PreDisplay() only while the mission is Continuing
  and its result is not decided, so after a battle's result cameraMs
  times the camera tick alone. Comments and docs now say so.
- The camera target is shielded by PatchShield only from a process's
  second game start (the probe installs after PatchShield's game-init
  pass); the PatchShield section has its row.
- If MissionScreen cannot be loaded, the camera class is skipped at load
  (an ERROR in the startup notice) and the install line still names the
  camera: documented.
- Cost: a same-session A/B on the desktop gave 0.646 to 0.767 us per
  simulated frame without the camera bracket and 0.715 to 0.836 us with
  it, so the bracket's own cost is inside run noise. The doc no longer
  sets the new figure beside one from another day.
- The full-mode [HitchDetail] sample's onTickMs includes the camera.
- The v1.5.5 PreDisplay address in the frame table names its build.
- Tests: one scan instead of two, one list per fact, two duplicate
  tests removed.

Refs #809.

#### fix(main-menu): v2.0.34 - main-menu setup never throws into the engine

`85c60896`

Bannerlord calls every module's OnBeforeInitialModuleScreenSetAsRoot
with no try, and calls the whole chain again on every tick while no
game state is active (v1.5.5 Module.cs). TAOM's crash containment
(Patch37) swallows a throw there, so one throw in TAOM's menu setup
would re-run every module's hook on every tick instead of failing once.

TAOM's main-menu customization now runs through LifecycleStep.Run. A
throw is caught and logged once as a [MainMenuStep] WARNING that names
the step, and the hook goes on without it. The step is not latched, so
a later call can still succeed. The shader-notice stop beside it only
sets a flag and stays a direct call.

SubModuleMainMenuHookTests pins this shape. LifecycleStepTests covers
the guard: success, a throw, one warning per step name, and a logger
that is null or throws.

Refs #807.

#### fix(race-abilities): v2.0.34 - cap ability radii at the 15 m grid limit

`b6f7ab36`

Race-ability radii (rally, kin bonus, fear on kill, fear aura) and the
KinWithin trigger range are now capped at 15 m, down from 30 m and
40 m. Past 15 m the engine's nearby-agent search stops using its
proximity grid and checks every agent on the field. No shipped value
changes: every shipped radius is 12 m or less. A custom value above
15 m is rejected with a warning: a radius reverts to 0, a kin bonus is
dropped, and a KinWithin trigger is skipped.

Enemy trigger ranges keep their 40 m cap, because five shipped
abilities sense past 15 m (Stand Fast 20 m, Forth Eorlingas 30 m, and
three ranged-weapon triggers at 30 to 40 m). Each such soldier pays
one full search a second until his ability fires; the cost is not yet
measured.

The race-ability and signature-strike docs now say that their fear
effects have no morale floor, so their routs spread to the formation.

Tests: four redundant 31 m rows were replaced by 15.5 m rows; a 15 m
edge test, a kin-bonus NaN row and a KinWithin row were added.

Research: native IMBMission.GetNearbyAgentsAux (15 m grid limit)

#### fix(dread): v2.0.34 - the dread aura weakens close troops, never routs

`efa32184`

The Aura of Dread of the Nazgul, the Witch-King and Sauron no longer
reaches across the battlefield. It now touches only the enemy troops in
contact with a wraith (5 m, full strength within 2 m; it was 12 m and
4 m), and it weakens them instead of routing them: their morale drains
down to 10 and stays there, and the fighting finishes them.

Why it spread: the aura could drain morale to 0 and rout troops by
itself, and every rout costs up to 10 allies morale. Vanilla takes
those allies from within 4 m first, then from anywhere in the routing
troop's formation, so once a line thinned, dread seemed to reach the
whole field.

The MCM "Dread Radius" setting is stored under a new name, so every
settings file starts on the new 5 m default and an old saved 12 is
ignored. The slider now stops at 15 m: past 15 m the engine's
proximity search checks every agent on the field on every pulse.

Known limitation: the Nazgul Scream, Sauron's Slam and race-ability
fear have no floor, so they can still rout troops the aura has
weakened, within 4 to 8 m of the source.

Tests: three floor-0 race tests (elf 93 s, dwarf 113 s, elf hero) were
deleted as redundant. Race resistance and floor 0 stay covered by other
tests, and the floor-10 time-to-floor table replaces them.

Deep review and RCA: docs/reviews/rca-dread-aura-local-2026-10-15.md

Not-tested: in game (a campaign battle with a wraith)
Research: AgentMoraleInteractionLogic, native GetNearbyAgentsAux
Save-compat: none; MCM key DreadAuraRadius is now DreadAuraCloseRadius

#### fix(data): v2.0.34 - Harad pools, kits and names after the tree rework

`44e9edd4`

Harad, Shaghana and Abanissa work again after the Harad troop tree was
replaced on 2026-10-10.

- Volunteers: Harad and Abanissa offer the Harad Levy, the Serpent
  Noble and, more rarely, the Pezarsan camel levy. Shaghana offers its
  own Southron Levy and Red Serpent Noble.
- War elephant and Mumakil riders are no longer volunteers. Buy them
  from the Elite Emissary at Korb Taskral for War Drums: 70 for an
  elephant rider, 2800 for a Mumakil rider. AI lords no longer field
  them.
- Upgrading a Harad troop never lowers a skill any more.
- Enlisting under a Shaghana or Abanissa lord issues Harad kits instead
  of Rohan militia gear. Harad archer kits carry steppe bows a new
  soldier can actually draw, and no rank's bow is weaker than the rank
  below it.
- Aserai careers start in the Harad tunic and carry the lance the Harad
  militia carry.
- The Harad and Shaghana armourer's commissions reward a heavy chest
  of that culture's own line.
- Shaghana and Abanissa taverns hire their own mercenaries.

Known limitation: a v2.0.34 save that holds one of the 25 Harad troops
replaced on 2026-10-10 is not supported (Mike's decision); start a new
campaign for Harad.

Known limitation: the new Harad troop names read in English in every
language until the next translation run.

Tooling: generate_enlistment_rosters.py --seed-missing works for the
first time (its marker comment held a double hyphen, which XML forbids),
generate_tavern_mercenaries.py can finish a run again, and the name
generator no longer registers vanilla keys from the obsolete-troop
overrides file.

Refs #798

#### fix(tools): v2.0.34 - WildMesh review convergence fixes

`e6b8fd49`

The convergence pass of the WildMesh3D mounts review (#794) found two
MED and five LOW defects in the first fix round; this fixes them.

The five new Armory item names (War Boar, Bristleback Boar, Iron Hills
Goat, Mirkwood Stag, Greenwood Moose) now have English rows, so
check_external_loc_coverage.py counted them as untranslated in all 12
languages and failed. The baseline records them: only the Armory counts
move, by five per language. Their translations are owed.

gen_animalia_anim_clips.ps1 exits 2, not 1, when a measure file cannot
be read, and its help text describes the SHORT check, the new BAD
MEASURE conditions and the per-pack checksum scope.
export_blend_pack.py refuses abbreviated options, so a stale verdict is
always cleared in the real --out folder. Two comments now say what the
code does: the dwarf mounts are four Monsters, not five, and the
WildMesh action sets also rebind the stands, idles and hit reactions.

The Animalia elk's walk-back plants were not re-measured into the
repo: the same run records that clip at 60 fps, so the new fps guard
refuses it until the Animalia follow-up fixes its frame rate.

RCA: docs/reviews/rca-wildmesh-mounts-2026-10-15.md

#### fix(tools): v2.0.34 - WildMesh mounts review fixes and clip regen

`eaba9588`

The deep review of the WildMesh3D mounts (#794) found two HIGH defects
and a set of smaller ones; this fixes them and regenerates the clips.

The live-Armory writer, tools/apply_wildmesh_armory.py, now works per
animal: each step inserts only the animals its file lacks, so the next
WildMesh animal is a row in its tables and one --apply. It refuses an
empty or self-closing Monsters file instead of writing Horse items that
name undefined Monsters (HIGH), refuses an animal partly present in a
file, reports a malformed file as PARSE FAILED, exits 2 on a missing
input, and backs up under a per-run stamp. It also writes the five item
names to the Armory's English loc file. Its LiveRecipeParityTests are
now the reinstall gate: they fail, not skip, whenever the Armory is
installed, and also check the sets, the registration, every bound clip,
the loc rows and the repo snapshot of the action sets (refreshed).

The Kit had compiled six WildMesh clip masters one frame short (the
deer RunSlow_F, GrazingOnSpotB_1, GrazingSteps and Idle_1, the goat
HoppingSlow_F, the warthog TrotFast_F_2), because Blender's FBX exporter
truncates frame times to ticks (HIGH). The horse-skeleton retarget now
exports inside tools/blender/fbx_exact_times.py, and the clip generator
reports a master shorter than its measured clip as SHORT. The 30 fps
resample in export_blend_pack.py now spreads its samples to the source's
closing pose (five gaits had a 2.6 to 7.7 degree loop seam), and the
measure no longer takes a toe scuff for a hoof plant. All 74 masters
were re-exported and re-imported, and 37 clips regenerated: 16 of 17
gaits now close within 0.84 degrees, like vanilla's.

The five Custom Battle test riders moved to
ModuleData/test_riders/wildmesh.xml, outside troops/, so no troops_*.xml
tool reads them and their 11 exemption lines are gone; their dead
upgrade edges and duplicate rosters are removed and their header now
says they show in the Custom Battle picker. The dwarf-mount allowlist is
DWARF_MOUNT_IDS, with tests for the WildMesh ids. The workflow doc,
ledger, feature doc and catalogues are updated.

Known limitation: the deer WalkMedium_F still closes 2.95 degrees off
on one bone; the five item names are English in the 12 other languages
until they are translated; nothing has been checked in game yet.

Committed with the ModuleData commit gate overridden at Mike's request:
the 7 Harad UPGRADE_SKILL_REGRESSION errors and 6 Harad career-kit
drifts it reports predate this change (08b30bfc4).

RCA: docs/reviews/rca-wildmesh-mounts-2026-10-15.md

#### fix(war): v2.0.34 - honest chronicle_open dry-run texts, tidy-ups

`74bdd5ef`

The taom.chronicle_open dry run for an event the chronicle has not yet
adopted no longer promises that 'confirm' will open it. It now says
there is nothing to preview, that adoption adds the event as Pending or
as Skipped when its day has passed or its anchor fails, and that only a
Pending event opens. A siege that would fall at once now says it would
apply its Fell outcome, if it defines one, instead of promising to pay
one. Tests pin both texts.

No behaviour change otherwise: an unused using goes from the party
item roster adapter, and an over-long comment line in the Eye of
Sauron service is wrapped.

Refs #792.

#### fix(adapters): v2.0.34 - three more main party reads via the campaign

`2c09a414`

Three more places read the player's party through the engine's
computed PartyBase.MainParty and then checked a copy of it for null:
stripping the player's gear into the inventory, the recruitment
screen's weighted party size, and the enlistment check for whether the
player's party is in a battle. That getter throws, rather than
returning null, while the campaign has no main party, so each check
could never do its job. They now read the stored main party through
the campaign; nothing changes while a main party exists.

The source scan that guards this now also catches the getter in its
own parentheses and a ?? fallback on it (Codex finding F1), and pins
the three converted files by name. It cannot see a check through a
local copy; the adapters rule and the lesson now say exactly that.
The Codex prompt for this review is committed alongside.

Refs #792.

#### fix(adapters): v2.0.34 - read the main party through the campaign

`e9854bbd`

PartyBase.MainParty returns null only without a campaign; with one
it reads Campaign.Current.MainParty.Party (v1.5.5
PartyBase.cs:209-219), so PartyBase.MainParty?. throws while the
stored main party is null. Seven such reads, in EncounterAdapter's
map event checks and PartyItemRosterAdapter's gear roster, now read
Campaign.Current?.MainParty?.Party.

With a main party the results are the same, so players see no
change. Without one, these calls give their no-party answer instead
of throwing into their catch and logging an error.

MainPartyNullCheckScanTests now also fails on PartyBase.MainParty?.;
run against the old adapters it failed naming both files. The
adapters rule and the lesson name the ?. case.

Refs #765, #792.

#### fix(war): v2.0.34 - hunt hint and console text match the hunt rules

`8fd7b2a6`

What the player sees:

- The Hunter Speed Multiplier hint in Mod Options said the floor
  applies once the hunter is within your sight. It now says within
  one and a half times your seeing range, which is where the code
  applies it (decision E09).
- The taom.start_eye_hunt usage text now says the start is refused
  while you ride in another lord's army, the dry run included
  (decision E17).

No logic changes. The rest are comments: the Eye of Sauron service
and hook summaries name the 1.5 S band and the E17 gate; the War
Chronicle summaries say the hunt is its own module, the save holds
the phase days and the daily gate, the focus registry names the
strength gate as the zero it keeps, and doc pointers name sections
that exist. Over-long comment lines are wrapped to each file's
width.

Refs #765, #792.

#### fix(tools): v2.0.34 - bind 42 orc and goblin codes, review fixes

`2952ccc5`

Orcs and goblins now play their restyled walk, run and idle clips on
every walk and run code that used the same vanilla clips: the running
idles and the thrown, dagger, hand-shield and polearm-with-shield
walks and runs (42 codes per race, was 18; #791). The live
LOTRLOME_Armory action_sets.xml was rebound with
tools/bind_orc_goblin_locomotion.py --apply (backup
action_sets.xml.bak-orc-goblin-bind-20261014-170848), and the repo
snapshot is a byte copy of it.

Deep review fixes for 776054c0c:
- The binder's --check now fails an Armory with no restyled clips,
  which is what a reinstall leaves. --apply refuses to drop a bound
  row unless --allow-shrink. It reads Native with ElementTree, checks
  every link of each derived set chain, and exits 2 on bad input.
- The armour index skips any item whose Type is not character armour,
  so the camel bardings filed in Harad's body_armors.xml stay out of
  the class table and the tier map.
- wire_starter_kit_rosters.py reads roster ids in one place, checks
  the override with the same vanilla rule as the culture file, and
  skips commented-out rosters.
- restyle_human_clip.py shares the leg IK and a JSON pose sampler
  with retarget_mannequin_to_human.py (about 5 s per variant, was
  31 s) and refuses unknown spec keys; both Blender tools clear their
  old .DONE marker. Parity held on all 32 clips and on one retarget
  clip.
- A test fails any TpacTool Save call in tools/*.ps1 that does not
  pass all three arguments.

Docs: the feature doc (Dependencies, how to restore the rows after a
reinstall), the tools README, the starter kit and armour docs, and
three lessons. RCA: docs/reviews/rca-orc-goblin-locomotion-2026-10-14.md

Still owed in game: the 24 new codes (#791), and the Harad starts
after ./build.ps1 (#795).

#### fix(war): v2.0.34 - skip the day and load marker on a broken clock

`5ba8d6c8`

A NaN, infinite or negative campaign clock used to reach the War
Chronicle as day 0. The snapshot adapter floored it to 0, so the daily
gate claimed day 0 on a fresh lane and the load marker wrote day=0,
which makes the ledger analyzer drop every retained day after day 0.

The snapshot adapter now reports validity: TryGetElapsedDay returns
false for no campaign or an unreadable engine clock. The daily tick
skips the day and logs one warning until a readable day returns, and
the load marker is not written (a warning instead). GetElapsedDay
keeps its 0 fallback for event line labels only.

The chronicle's own day path needed no change: its world adapter
passes the raw engine value through, and the finite check plus the
gate's range check already skip the day. Its test now covers NaN,
both infinities and negative days.

Tests go through the adapter boundary: a substituted adapter reporting
an invalid clock for the tick service, ledger and behavior, and the
production adapter's floor helper for each invalid value.

Refs #792.

#### fix(messengers): v2.0.34 - check the stored main party, not its getter

`91f36998`

IsPlayerAvailable null-checked PartyBase.MainParty. That getter returns
null without a campaign, but with one it reads
Campaign.Current.MainParty.Party, so it throws while the stored main
party is null: the check failed in the one case it was written for. It
now checks Campaign.Current?.MainParty, as ChroniclePresenter does. No
other behaviour changes.

A source scan test now fails if any production file null-checks
PartyBase.MainParty again.

Refs #792.

#### fix(war): v2.0.34 - war ledger marks each load, analyzer rewinds to it

`5479109b`

The war ledger analyzer kept the last line of a re-emitted day, but a
reload of an older save left the abandoned run's later days in the
report, and the day numbers alone cannot show a reload: the engine can
tick the save's own day again.

The ledger now writes a t=load line with the save's day on every game
load, on the co-op authority only. The analyzer drops that campaign's
lines, events and chronicle outcomes after that day and keeps the
save's own day from both runs. Logs from earlier builds carry no marker
and are not rewound, so a reload in such a log can keep an abandoned
run's days.

Refs #792.

#### fix(war): v2.0.34 - War Chronicle daily work runs once per day

`238f0033`

After a load the engine waits only the elapsed part of the day before
the next daily tick (Campaign.CreateCampaignEvents), so a save made
before midday on day S ticked day S a second time. The War Chronicle
then repeated that day's work: effect expiry, baselines, the rally, the
extra prisoner escape rolls, the ledger lines and the chronicle's daily
step.

Each of its two daily listeners now records the last campaign day it
ran and skips a repeat. The days are saved with the War Chronicle's
other state. A save from an earlier build holds none, so its first tick
runs as before, and a new campaign starts clean.

Refs #792.
Refs #793.

#### fix(war): v2.0.34 - null-safe main party check for the hunt popup

`5f80f95c`

The hunt announcement chooses a popup over a banner when the map is
free. That check read PartyBase.MainParty, a computed getter that
returns Campaign.Current.MainParty.Party (v1.5.5 PartyBase.cs:209-219),
so it throws instead of returning null while the campaign has no main
party. It now asks Campaign.Current?.MainParty, as ChroniclePresenter
does.

Refs #792.

#### fix(refuge): v2.0.34 - the engine saves the AI pin

`63630e8a`

E44: the load path's comment, the party component's summary and a test
message said SetDoNotMakeNewDecisions is not persisted, so refuges could
wander after a reload. The installed v1.5.5 engine saves it
(MobilePartyAi.DoNotMakeNewDecisions is [SaveableProperty(11)]). A
refuge is pinned at spawn and re-pinned on load; since the engine saves
the pin, the load pin is a belt for a failed founding pin or an adopted
orphan. Refuge never releases the pin, so nothing relied on the wrong
claim. Comments, test message and the feature doc corrected.

Refs #792.

#### fix(careers): v2.0.34 - tree migration covers every saved hero

`59c1803d`

The career tree-version migration ran at session launch for the main
hero only, while every saved hero keeps its career data. A co-op hero,
or a hero the player takes over later, is never the main hero at
launch, and nothing migrated it afterwards. After a tree update it
would keep its old picks, its fork picks would read as locked in, and
its player would pay to retrain what the migration owes for free.

MigrateAllTreeVersions now migrates every hero that holds career data
and returns only the player's refund count, so only the player is
told. Found by the Codex review of the career forks range.

Not tested in game: no shipped career has a tree_version yet.

Refs #769. RCA docs/reviews/rca-career-e4e7-2026-10-14.md.

#### fix(engine): v2.0.34 - v1.5.5 review fixes, dead mod guard removed

`4e408e0e`

Follow-up to the v1.5.5 engine bump after its deep review. Nothing
changes for players: a fresh build, the binding gate and the member
body diff were already clean, and they still are.

The BUTR reference assemblies now point at 1.5.5.124170-beta, which
BUTR published after the first commit (that commit said they stayed at
1.5.4). A build against them has 0 errors and the pin test is green.

TAOM.Dependencies' SubModuleConstructionGuard is deleted. It could never
catch anything: Bannerlord's base SubModule constructor is empty, a
mod's field initialisers run before it, and the guard installed only
after the engine had constructed every SubModule. Since 2026-05-27 it
logged 526 installs and no swallowed failure. On v1.5.5 the engine
itself catches a SubModule constructor failure, names the module in a
message box and closes the game, so players lose nothing.

The two new bump tools now fail instead of reporting a false clean. The
engine-reference checker exits 2 on a mistyped or empty folder, an
engine assembly it could not read, a non-.NET file or a same-build
baseline; it also checks type references, resolves generic types
instead of skipping them, and takes wildcards. The body diff requires
both builds, checks that they differ, fails on rows it could not
compare, clears old output and runs about four times faster. The first
v1.5.5 body diff reported 509 false changes because the installed Coop
module bundles its own v1.5.4 engine DLLs; the tool now reads engine
code only from vanilla modules. The handbook gate now reports a stale
manifest. Both tool test suites run under the project's unittest
runner.

Two TAOM.Dependencies engine bindings by name are now rows in the
reflection binding gate. Docs, the /engine-bump skill and the lessons
are corrected.

Write-up: docs/migration/v1.5.5-impact.md. Review and root cause:
docs/reviews/rca-engine-bump-v1.5.5-2026-10-14.md.

#### fix(war): v2.0.34 - Eye of Sauron Codex fixes

`2d17ceba`

The four findings of the Codex review of the hunt (#765).

- A hunter whose own AI led or joined an army while he stood off, or
  left him in a battle, siege, town or under another order, is no
  longer claimed on resume: the hunt waits, unpinned, and retries
  each hour.
- Every change to the hunt's membership (a start, even one adding no
  troops, an end, a refused start) refreshes the party's cached size
  limit through MemberRoster.UpdateVersion.
- Session launch reads the live pin before any end, so a stand-off
  save loaded into co-op releases no pin the hunt no longer owns.
- The per-frame locator search reaches 1% past 1.5 sights, so a
  hunter exactly on the band's edge is found.

#### fix(war): v2.0.34 - the hunter holds off while you ride in an army

`7ab91767`

When you join another lord's army, the Eye of Sauron's hunter now
stops chasing you at once. He drops his order to ride you down, goes
back to his own business and loses his extra speed. The hunt itself
is not over: its days still count, and no cooldown starts.

When you leave the army he takes up the chase again within the hour.
A save made while he holds off loads the same way.

#### fix(war): v2.0.34 - no chronicle payout after the war ends

`8f2c9e45`

The owner-change and siege-start callbacks skipped the War Ended check that launch and the daily step make, so a conquest between War Ended and the next daily step could resolve and pay an Active siege. The check now lives in the shared callback wrapper.

#### fix(war): v2.0.34 - chronicle review fixes

`7e845155`

Fixes from the deep review of the War Chronicle event chain (#765).

Siege windows now follow the map more closely. A window no longer
closes Held while an attacker is still besieging the target: the
close waits for the siege to end, for at most one more window, so a
town taken a few days late resolves Fell. Each daily step also checks
who owns the target, because a clan that defects or leaves with
rebellion takes its towns along without an owner-changed event. A
town lost to a third party now says it changed hands instead of
"no assault came". Days are whole campaign days, so a window no
longer slips a day when the daily tick lands after midnight.

Announcements can no longer stall: the queue relies on the engine's
open-inquiry check instead of a close callback that a hidden popup
never runs. Switching the chronicle or its announcements off drops
what is still queued. The map check runs before the co-op probe, and
the presenter no longer reads PartyBase.MainParty, which throws with
no main party.

war_chronicle.json is checked harder: unknown keys warn instead of
dropping a block silently, kinds, anchors and outcomes must be names
(no "+1" or comma lists), and a momentum value that is not an object
is dropped alone instead of disabling the whole file.

The save carries the chronicle rows as typed records through the
codec; a resolved row without its resolution day is skipped. The
army focus registry gets a session reset, the focus slider and the
momentum cap share one constant each with their consumers, and
taom.chronicle_status flags an overtime window and a focus that AI
Strategic Intelligence leaves inactive. The ledger analyzer keeps one
resolution per event even when a reload repeats a line.

Docs: the listener order, the defender focus waiting for an attack,
the strength gate, the cohesion-boost side effect, the phase-day
burst and the Test Mode lore order are written up; test counts are
recomputed.

#### fix(war): v2.0.34 - Eye of Sauron review fixes

`ac0a3e0c`

Applies the deep review of the Eye of Sauron hunt (#765 M8).

- A player-founded kingdom is now hunted: the player's own kingdom is
  sided by its id, else by the kingdom's own culture (ResolveSide), so a
  new_kingdom of Gondor culture draws one of the Nine. The console shows
  the same side.
- The per-frame sight read no longer walks every party on the map. It
  searches the locator grid around the player within his seeing range,
  and the reading is a value type. One SeeingRange call per frame stays:
  it is the search radius, and vanilla makes the same call each frame.
  The locator walk grows with sight, so it is not a flat cost.
- The end gates (peace, kingdomless, switch off, co-op, hunter down,
  party lost, timeout) also run at the hunter's hourly tick, before the
  re-engage, so a peace no longer brings a forced encounter for a day.
- Releasing the pin drops an engage order on the player and has the
  party rethink next hour; a pinned party never advanced its think
  counter, so it could keep chasing for six hours.
- A busy or unfit hunter is passed over for the next in order, as the
  approved plan says; the hunt waits only when none can ride. A refused
  spawn still stops the day's search, since the spawn ring depends only
  on the player; tomorrow's tick retries.
- The host is topped up daily (never in a battle), replacing the men
  vanilla's wage-limit desertion takes. It is topped up before the
  engage, since the engage can start the encounter at once.
- The hunt stands down while Bannerlord Together is loaded, a co-op mod
  TAOM cannot probe. A solo player with it enabled gets no hunt. A hunt
  restored into a co-op session ends at launch; a co-op peer scores no
  deed.
- The step decisions moved from the adapter into the service, with one
  test per gate, and the straight-line gate now runs before the path
  rebuild. The last step's outcome shows in the status command.
- A failed spawn search is no longer repeated 15 times in one frame.
- The console commands follow the naming rules: print_eye_of_sauron,
  add_eye_score, start_eye_hunt and clear_eye_hunt. start_eye_hunt is
  tier C: the bare command is a dry run, and only the literal confirm
  starts a hunt.
- The size model builds its weighted-count delegate once instead of
  allocating a closure on every party-size call.
- The end line is side-neutral ("The hunt is over, for now."), so
  Glorfindel's hunt no longer ends with the Eye turning away.
- Simplified: the pin flag and its save key are gone (the hunt always
  owns its pin), SyncData reads the saved record by field, the settings
  use the shared clamp, the pure arithmetic moved to HuntMath, and the
  boundary values have one file each.
- Tests: field-by-field save round trips through an engine-faithful
  store, the console refusals, the dry run, every step gate, and IL
  pins for the frame read and the size model.

#### fix(heroRace): v2.0.34 - dwarf portraits no longer sit too low

`6c26f003`

Dwarf faces in the small portraits (settlement notables, party and
battle screens) sat at the bottom edge of the frame, showing little
more than hair and eyes. The 2D portrait offset for dwarves was only
half of what the shorter body needs.

The dwarf entry in CharacterImagePatch.json goes from Vertical 0.15,
Zoom -0.1 to Vertical 0.3, Zoom -0.2. These are the values that were
tuned by eye in game and that the separate race portraits module
applied until it stopped framing portraits. The 3D preview offsets in
CharacterAvatarPatch.json are unchanged.

#### fix(war): v2.0.34 - stricter capital verdict, rally refresh

`2ee36e1f`

Fixes the three findings of the Codex review of the war ledger, timed
war effects and rally (#765):

- The war ledger analyzer no longer passes the "no capital lost before
  day 120" target on incomplete evidence: a capital already lost in
  its first line now fails it ("lost by day N"), and coverage is
  checked per kingdom, so one kingdom's complete log cannot hide
  another kingdom's missing days.
- The rally rebuilds a kingdom's effects on every daily refresh, so a
  rally effect from a save no longer survives when rally.json sets
  that effect to zero.

Refs #765.
RCA: docs/reviews/rca-war-chronicle-batch-a-2026-10-09.md

#### fix(combat): v2.0.34 - Custom Battle runs TAOM's combat mechanics

`b0154d86`

TAOM's Combat Mechanics rules now apply in Custom Battle too (#788).
Before, only the campaign ran them, so a Custom Battle used vanilla
rules for skill crush-through, monster and orc shield crush-through,
creature cleave and stagger immunity, the per-race stagger
threshold, weight-driven charge knockdown, shield penetration and
shield damage, Signature Strikes knockdown and knock-back, and the
MCM horse charge penetration slider.

One Custom Battle damage model now carries all of them beside the
creature, race ability and siege rules it already had. It sits on
vanilla's Custom Battle base, because the campaign model's base
crashes on a mounted hit outside a campaign. Career passives and the
Refuge reduction stay campaign only: they need heroes and parties. A
fault in the creature bandits feature can no longer remove the
model for the rest of a session.

New tests fail when the campaign model gains a rule that the Custom
Battle model lacks. Custom Battle results for the culture doctrine
A/B, the Signature Strikes smoke and the Race Abilities runs change,
so their baselines must be measured again.

Reviewed by a six-lens deep review; findings and fixes:
docs/reviews/rca-custom-battle-damage-2026-10-09.md. Built and
tested on the installed v1.5.5; /engine-bump is still owed.

#### fix(resources): v2.0.34 - hideout send-troops clear pays once (#790)

`bc59806f`

Clearing a bandit hideout with "send troops" paid the hideout's special
resource reward twice: the game reports the clear once from the
success menu and again when the fight is finalized. The reward is now
paid only for the finalized clear, so it is paid once, also when the
game is saved in the success menu and loaded.

Not tested in game: one send-troops clear should write one
[SpecRes] HIDEOUT: line.
Refs #790. Found by the Codex review recorded in
docs/reviews/rca-career-e1e3-2026-10-08.md

#### fix(resources): v2.0.34 - one party-screen handler per session (#771)

`c96a02a4`

Each save load in one game session left another party-screen handler
behind, so after a few loads opening the party screen started the
special resource session and the troop count diagnostic several times.
One handler now stays live per session. The lesson that recommended the
old pattern is corrected.

This commit also carries the special-resources.md updates for #767 and
#770.

Not tested in game: load a save three times, open the party screen
once, expect one session line and one diagnostic dump.
Refs #771. Review: docs/reviews/rca-career-e1e3-2026-10-08.md

#### fix(resources): v2.0.34 - raids stop paying the battle credit (#770)

`30cbcd46`

Raiding a village paid the battle reward on top of the raid reward,
and a raid where the militia fought first paid the raid reward twice.
Rohan and the elves, which earn nothing from raids by design, earned
from every raid.

A raid now pays the raid reward once, when the game marks the village
as looted, and only to the side that won. A battle inside a raid or an
extortion pays the battle reward only when a real party (a lord's or
another field party) was beaten, so defending your village from a
lord's raid still pays; beating only the militia pays nothing extra.
Tournament and hideout messages no longer claim an earning when nothing
was credited.

Not tested in game: a raid with a lord inside, defending a village,
a militia-only raid.
Refs #770. Review: docs/reviews/rca-career-e1e3-2026-10-08.md

#### fix(careers): v2.0.34 - party size picks apply at once, own party only

`c74ad5ae`

A career choice that adds party size now changes the party limit at
once, not after the next roster change (#768). The bonus now applies
only to the party the career hero leads: garrisons, caravans, militia,
villagers, patrols, supply caravans and refuges no longer receive it.
On an existing save a caravan or garrison filled to its old limit may
be over the new one, and vanilla desertion trims the excess over a few
days. The flat bonus is now added after every percentage factor, so the
AI lord party size setting no longer multiplies it.

Rohan's "Garrison Commander" choice now reads "Your company swells with
volunteers"; its 12 translations show the English until the next
translation run. The taom.career_perks console report now prints the
engine's cached party limit beside the model's value.

This commit also carries the career-system.md updates for #766 and
#767.

Not tested in game: the instant limit change and the console lines.
Refs #768. Review: docs/reviews/rca-career-e1e3-2026-10-08.md

#### fix(resources): v2.0.34 - career gain scales every earning (#767)

`01ca483e`

A career choice that raises special resource income only applied to
the daily town income, so a player without towns got nothing from it.
It now also scales the battle, raid, siege, prisoner, tournament and
hideout earnings, and the map bar tooltip shows the scaled per-event
amounts.

Elite troops whose daily upkeep a career choice reduced to zero no
longer desert when the balance runs out. The daily upkeep is now worked
out in one place for the tooltip, the daily tick and desertion, and an
unusable upkeep modifier or configured upkeep falls back safely instead
of wiping the day's income.

Not tested in game: the scaled earnings and the tooltip rows.
Refs #767. Review: docs/reviews/rca-career-e1e3-2026-10-08.md

#### fix(careers): v2.0.34 - link each career choice to its group (#766)

`259ae2cb`

Career choices never knew which group they belonged to: the loader read
the group from an attribute no choice carries. So the career screen's
tier lock, the one-keystone-per-tier rule and the save repair for
choices left over from another career never worked, and a level 1 hero
could take a tier 3 choice.

Each choice now takes its group from the group that contains it. The
tier lock and the keystone rule now apply; keystones a save already
holds stay taken. The repair pass can now remove a choice that provably
belongs to another career, and a message tells the player how many
choices were refunded. If two careers ever claim the same group, root
or choice, the choice is kept rather than deleted, and a test fails on
such data.

Known limitation: completing a full tier 3 group still opens every
keystone, and keystones taken that way stay after the group is refunded.
The career redesign (#769) retires this rule.

Not tested in game: the click refusals and the refund message.
Refs #766. Review: docs/reviews/rca-career-e1e3-2026-10-08.md

#### fix(wotr): v2.0.34 - declare the Full War wars for real

`d1b9fd91`

When the War of the Ring reaches the full war (day 44 by default), every
pair of kingdoms set as Hostile now really goes to war: each war is
declared, the war notice and the history entry appear, and each
kingdom's list of enemies is rebuilt at once (#772). Before, no
Hostile-pair war was ever declared. Peace stayed blocked, but the real
stances stayed at peace, so there were no notices, and AI armies could
miss the new enemies as siege or raid targets until the next load.

The cause: each declaration first asked whether the two kingdoms were
already at war, and that question went through TAOM's own diplomacy
model, which answers "at war" for every Hostile pair once the full war
starts. So every declaration was skipped. The declarations now check
the stored stance instead. The phase still changes first, so vanilla's
alliance logic does not call allies into wars that the same step is
about to declare.

Existing saves already in the full war are repaired when they load: on
the host, every Hostile-pair or scripted Phase 2 war that is still at
peace in its stored stance is declared once. Those wars get their
history entries and map notices, dated at the load day, but no chat
line. A pair that made peace while the full war allowed it is left
alone. Scripted war lists in war_of_the_ring.json now drop empty and
self pairs, and name an unknown kingdom id, with a warning.

Known behaviour, unchanged by this fix: when the full war declares its
wars, vanilla still asks each permanent ally that is not itself Hostile
with the enemy to join (for example Harad into Mordor's wars).

Reviewed by an eight-lens deep review with two adversarial verifiers
per finding, then by Codex; its findings are fixed or documented.

Refs #772.
RCA: docs/reviews/rca-wotr-phase2-wars-2026-10-08.md

#### fix(wotr): v2.0.34 - reset the War of the Ring per campaign

`2719e59e`

Starting a new campaign without restarting the game no longer carries
the previous campaign's War of the Ring into it. Before, a campaign
started after one that had reached the full war never declared the
Isengard war or the full war, and after a war that had ended it never
got a war at all (#764).

The War of the Ring state lives for the whole game process. Its
campaign behavior is now built fresh for every campaign and resets the
state in its constructor, before any campaign event can read it; a
loaded save then restores its own phase. An earlier version reset on
session launch, which was too late: the momentum behavior runs first
and would have started the momentum war on day 0 of the new campaign.

Known limitation: a save already written by an affected campaign keeps
the phase it saved.

Tests pin the reset, a save-then-load round trip and the per-campaign
construction in SubModule, including the shapes a cached instance would
take. Reviewed by an eight-lens deep review in two waves and by Codex.

Refs #764.
RCA: docs/reviews/rca-wotr-session-reset-2026-10-08.md

#### fix(wanderers): v2.0.34 - stop Calradian wanderers spawning in TAOM

`546d1181`

Calradian wanderers from the base game kept turning up in taverns, about
a quarter of every campaign's wanderers, telling Calradian stories in
Middle-earth. The game spawns every wanderer template any module
defines, and nothing removed the base game's 67. They are now taken out
of the pool, so new campaigns only spawn Middle-earth wanderers.

Dale and Khand had no wanderers of their own, only the Calradian ones,
so each gets ten: Bardings of Dale and Lake-town, and Variag warriors
of Khand, each with their own story, gear (one bow set for the
archers) and a place on the faction screen's Wanderer tab. Promoting a
Dale or Khand soldier to Refuge warden now also gives a man of his own
people again.

Known limitation: a save started before this change keeps the
Calradian wanderers it already has; they stay until recruited. Other
languages show the new stories in English until the translation run.

The rule now also comes from the lords.xslt generator, so regenerating
that file cannot undo it, and tests pin the retag, the load order it
relies on, and the Faction-tab lists. Follow-ups: #761 (no female
wanderers remain), #762 (21 older wanderers missing from the tab).

Refs #758.
RCA: docs/reviews/rca-vanilla-wanderers-2026-10-08.md

#### fix(wanderers): v2.0.34 - give 53 wanderers their own backstories

`9dd2f594`

Wanderers of the Goblin-town, Misty Mountain Orc, Blue Craig, Lindon
and Arthedain cultures showed "ERROR: Text with id backstory_c doesn't
exist!" instead of their introduction. Their templates were cloned
from other cultures without the dialogue rows the conversation reads.

All 53 now tell their own stories: goblins of the High Pass tunnels,
orcs of Mount Gram and Moria, the coastal raiders of Blue Craig, the
sea-elves of the Grey Havens, and the Dunedain holding Fornost against
Angmar. Each story fits the wanderer's traits and best skill. The
short line a later wanderer of the same kind gives is written about
their trade and people rather than repeating the first one's tale.
Other languages show the English text until the translation run.

WandererBackstoryCoverageTests now fails on any wanderer missing a
row, so a new culture cannot ship this gap again; #257 fixed the same
bug by hand in May with no test, and it came back three times. The
writing-system localization test now skips rows still equal to their
English, the translator's own "not yet translated" marker, so it
reports real translation damage again. The culture and kingdom recipes
name the strings file.

Refs #757.
RCA: docs/reviews/rca-wanderer-backstory-strings-2026-10-08.md

#### fix(battles): v2.0.34 - end AI battles stuck at N vs 0 (#748)

`a913629c`

An AI battle on the campaign map could freeze for good, for example at
8000 against 0. Lords of the same faction kept joining it, the whole
faction stood still, and the player could join neither side. A player
traced the cause and sent a workaround module, Stuck Battle Guard; this
fix is written from their diagnosis.

The engine runs a battle round only while both sides have healthy
troops, and picks a winner only inside a round. A side that drops to 0
healthy troops between rounds therefore never loses. A party destroyed
while still attached also makes the battle refuse every joiner.

Once per in-game hour TAOM now checks every AI battle at least 12
in-game hours old:

- A destroyed party still attached is removed, as the engine does when
  it destroys a party itself (a quest's party is left alone).
- A side with no healthy troops loses: the other side wins, with the
  usual prisoners and loot. When both sides have none, the defenders
  hold.
- The player's own battle, an enlisted player's commander's battle and
  raids are never touched.

The check stands down in co-op sessions for now, because Coop decides
battles that hold a remote player's party. The console command
taom.print_stuck_battles lists every running battle with what the check
would do.

Known limitation: while the player is enlisted, a stuck battle holding
their commander is not resolved.

Deep review: docs/reviews/rca-stuck-battle-guard-2026-10-06.md.
Follow-up: #750 (alignment desertion removes troops from parties that
are in a battle, the one known TAOM trigger).

#### fix(balance): v2.0.34 - Lindon troops weigh and survive like Rivendell's

`0e75ec50`

Lindon's troops take party space and its soldiers survive battle the
way Rivendell's do again. Both were lost when Lindon became its own
culture in August (cc1713eb3): its new troop tree and culture id never
reached the two tables keyed on Rivendell's. A player reported it.

- Lindon's 22 elite troops now take the same party space as their
  Rivendell twins: the 15 at level 41 and above count as three troops
  each, the 7 from level 26 to 36 as two. Before, every Lindon troop
  counted as one, so a party could hold three times as many Mithlond
  Marchwardens as Imladris ones. Recruits, infantry, militia and
  mercenaries stay at one, as Rivendell's do.
- Parties and garrisons of the Lindon kingdom get Rivendell's survival
  bonus: a soldier struck down in battle dies 40 percent less often.

Existing saves pick both up on load. A Lindon AI party already holding
many elites trims down to its limit on its next daily upgrade check,
A player's party is never trimmed: one full of Lindon elites shows
over its limit and moves slower until it has room again.

A new test fails if any Lindon troop stops weighing the same as its
Rivendell twin.

RCA: docs/reviews/rca-lindon-balance-tables-2026-10-06.md
Refs #749

#### fix(lords): v2.0.34 - generated lords wear their culture's own kits

`db805427`

Lords the game creates during a campaign now dress like the lords of
their culture and race. Before, a Dunland lord who came of age (one
player met "Ulric") wore vanilla Empire armour, and Rohan, Harad,
Easterling, Dale and Khand lords were just as likely to turn up in
Calradian gear. The code and data shipped in bb68eeca3; this commit
carries the review's last fixes and is the release note for both.

- A lord who comes of age, a companion you make a lord, the lords of
  a new companion clan, rebel leaders and a ruler stepping down now
  get a kit that at least two of their culture's lords of the same
  race and sex are defined with. Mordor's men get the Black
  Numenorean kits and its uruks the uruk kits; one lord's unique gear
  (Theoden's, the Mouth of Sauron's) never spreads.
- When no such kit exists, they fall back to TAOM's lord template.
  The vanilla Calradian templates no longer compete with it for the
  six cultures built on vanilla ones; noble teens of those cultures
  now wear the TAOM teen kit too.
- Dale's fallback template is now Dale's mounted-noble armour instead
  of vanilla Sturgian gear.
- Minor factions (the Corsair Blades and the rest) and children keep
  their usual gear. A new kingdom ruler keeps the ruler regalia.

Known limitation: lords generated before this update keep the gear
they already have, including in existing saves. Khand's children can
still wear vanilla child clothes.

Review: docs/reviews/rca-generated-lord-kits-2026-10-06.md

#### fix(career): v2.0.34 - uruk careers start in uruk armour

`90b1a260`

Isengard and Mordor careers started in Mordor orc armour, the light orc
tunic and boots their level-1 orcs wear, which does not fit an uruk
player. They now start in their own uruk line: Isengard in Uruk-hai
chainmail and shoes (sk_uruk_hai_chainmail_a1, sk_uruk_hai_shoes_a1),
Mordor in uruk chainmail and boots (sk_uruk_mordor_chainmail_medium_a1,
sk_uruk_mordor_boots_a). The career kit generator now requires that line
for both cultures and stops with an error if a later art drop or troop
change ever leaves it empty.

Three ranged careers named for another weapon started with a bow. The
Uruk Crossbow (Isengard) now starts with ladder_isengard_xbow_t2 and
Isengard bolts, the Crossbow Master (Erebor) with ladder_erebor_xbow_t4
and dwarven iron bolts, and the Pezarsani Javelineer (Harad) with
javelins and a round shield, as do Harad's careerless hunter, skirmisher
and bard starts. Erebor fields no lower crossbow, so its start is a
tier-4 one (96 damage).

Gondor careers start with wm_gondor_sword_a01 and wm_gondor_light_spear,
the weakest Gondor troop weapons. A Gondor troop rework (998f054c) had
left the career file off its rule since v2.0.31.

Existing characters keep their kit: rosters are read at character
creation only. The polearm audit's four stale Mordor career entries are
gone, so that gate passes again. RCA:
docs/reviews/rca-career-kits-uruk-2026-10-06.md.

Owed in game: a new campaign per Isengard, Mordor, Erebor, Harad and
Gondor career, including the uruk armour on an orc and a human player.

Refs #743

#### fix(arena): v2.0.34 - tournament fighters wear their own armour

`ea229d82`

Tournament fighters now fight in their own armour, as in vanilla,
instead of their culture's practice kit (Mike's decision). That holds
for skipped and simulated matches too (Skip, Skip All Rounds, Leave
and every match after you are knocked out), so who wins a bracket now
depends on the armour each fighter really owns. Arena practice fights
are unchanged: every fighter still wears his own culture's practice
kit, which keeps dwarves and other races out of armour made for
another skeleton.

How: TournamentService.ArmourDummyId returns no practice kit inside a
tournament mission, and TaomTournamentModel then returns the fighter's
own battle armour itself. It keys on the tournament mission rather
than MissionMode.Tournament, because a skipped match never enters that
mode and vanilla would hand its simulation the host faction's kit.
ResolveDummyId, whose settlement fallback nothing reached, is folded
into ArmourDummyId.

Reviewed with /deep-review (six lenses and a convergence pass); its
one defect, skipped matches still taking the host's kit through the
base model, is fixed, and the RCA is
docs/reviews/rca-tournament-own-armour-2026-10-05.md. Arena docs, the
NPC handbook, the GameModel registry and the index updated; a lesson
added. Full suite: 14,032 passed, 1 failed (the #731 translation
rows), 5 skipped.

Known limitation: bannerlord-1.4.5 still gives tournament fighters
the culture practice kit.

#### fix(hooks): v2.0.34 - commit-subject gate reads the target repo's version

`12d96adc`

check-commit-subject-version.sh always read Main/_Module/SubModule.xml
from the project directory, so a commit run in another worktree (git
-C <dir> commit, or cd <dir> && git commit) was judged against the
wrong line's version: a correctly labelled bannerlord-1.4.5 commit
(v2.0.28) was refused for not saying v2.0.34. The gate now reads the
version, staged copy first as before, in the repository the commit
runs in; a directory that does not exist falls back to the project.

tools/test_hooks.sh gains five cases in section 6 against a stub
worktree at v1.2.3 (its label allowed, this repo's label denied, for
both git -C and cd, plus the missing-directory fallback): 946 passed,
0 failed. The old hook denies the git -C case the new one allows.
/security-scan: no CRITICAL; its HIGH and MED predate this change.

#### fix(mordor): v2.0.34 - Mouth of Sauron's helm at 50; review fixes

`5d90a6cf`

The Mouth of Sauron wears his own helm in battle and in town, first
shipped in the v2.0.33 testing build from 799e189e. Its head armour is
now 50 (it was 55), and it is never sold, looted or won as a prize.
Only campaigns started on v2.0.33 or later dress him in it.

From the review (#733,
docs/reviews/rca-mouth-of-sauron-gear-2026-10-04.md):
- The armour class table lists the helm as named.
- MouthOfSauronHelmDataTests pins the live helm's armour, flags and
  class, so an Armory reinstall that brings back 55 fails a test.
- tools/blender/strip_upper_mesh_channels.py now refuses hands, arms
  and bodies (they need their hand-pose channels) and a run with
  nothing to strip.
- The Armory snapshot README records the helm, its gates and what
  covers_head costs; its troll gear block names where the backups went
  and which channels have the lock.
- Docs: the armour handbook names the helm as the one covers_head
  exception; the armour acquisition doc lists it as named, with its
  test; the roster doc says no gate resolves roster ids yet; the Black
  Numenorean doc, the morph doc and a roster comment are corrected.

Known limitation: every release channel's Armory holds the helm at 55
until the next Armory package from the dev install, and whether his
hands grip under covers_head is owed in game (#733).

#### fix(tournament-rewards): v2.0.34 - review fixes and Mike's decisions

`b4decf22`

Tournament Rewards went out in the v2.0.33 testing build, and is in
the v2.0.34 tag, without a review. Its code came in 9e2a39f4, whose
message does not name it, so this body is its changelog entry.
Tournaments now pay more, and you choose how: bets have no cap by
default (MCM Tournaments, Max Bet Per Round, 0 for none); a winner's
renown and influence grow with the lords and heroes in the field and
with the winner's culture; at Join you pick one of up to three prizes,
then the combat skill the tournament trains, and every round you win
trains it.

From the review (#732,
docs/reviews/rca-tournament-rewards-2026-10-04.md), with Mike's
decisions:
- Blue Craig, the third goblin kingdom, gets the orc factors (renown
  1.5, skill experience 1.4), as Goblin-town and the Misty Mountain Orcs
  have.
- A new campaign's leaderboard, seeded with 500 past wins, keeps
  vanilla's renown: the culture factors and the MCM multiplier no
  longer reshape starting clan renown.
- The prize dialog offers "a choice of prizes" (it can show two), and
  the training message names the skill without a number, because the
  game scales the experience by your learning rate.
- An elite, lord or named prize, which an old save or vanilla's
  fallback list can still advertise, now comes with heavy-band
  alternatives.
- MCM values are clamped to their sliders, and no reward can overflow
  into a huge negative renown or influence.
- The hero count now comes from the feature's own campaign event
  instead of a Harmony patch on a private game method, and a fault in
  it no longer skips vanilla's award.
- Tests for the band rule, the wiring, the settings, the validation
  rules and the overflow; the engine pins now run in the binding gate.
- Doc fixes: what the engine really does with tournament experience,
  influence and prizes; the Arena doc now lists the two reward
  overrides, the new service members and the band rule.

Troll gear (bc39f6e4): its owed convergence pass is recorded in
docs/reviews/rca-troll-gear-tournament-prizes-2026-10-02.md,
/armory-audit now runs the troll gear gate after an Armory update and
names the release channels each Armory ships through, and the troll doc
says which channels' Armories carry the lock.

Known limitations: the patreon and public channels' Armories still let
players buy, loot and forge troll gear until their next editor package
from the dev install (Mike's decision, 2026-10-04). With no bet cap
set, the tournament screen shows 50,000,000 as the most you can bet
each round. The nine dialog strings stay English in every language
until the translation run (#731).

Verified on this commit's exact tree: against the installed Bannerlord
v1.5.4, 14,040 of the 14,042 tests run pass; the two failures (the #731
translation rows and the check that the pinned game version is
installed) fail at HEAD too. On CI's v1.5.3 reference assemblies the
unit tests show HEAD's same 13 failures and the binding gate passes 414
of 414.

### Performance

#### perf(missionperf): v2.0.34 - log busy time, frame limiter and vsync

`685c6a1e`

The mission heartbeat can now tell a capped frame rate from a busy
engine. Each [MissionPerf] line adds mainMs, the mean main-thread
busy time per frame, and renderMs, the render thread's busy time over
the engine's last window of a second or more. Both come from the
engine's own counters (GetMainFps and GetRendererFps, decompiled on
the v1.5.5 client), which measure work and leave out the frame
limiter's wait. [PerfContext] adds frameLimiter and vsync from the
graphics options.

If fps sits at the frameLimiter value and both busy times are well
under avgMs, the in-game limiter sets the rate, not the load.
tools/perf_runs.py reads the new fields, still reads older logs and
reports main_ms_median and render_ms_median. Its FRAME_CAP note now
prints the limiter settings instead of guessing whether they match.

Not yet checked in game: a 60 fps limiter with vsync off, a 30 fps
limiter, and a GPU-heavy scene.

#### perf(campaign): v2.0.34 - switch off ButterLib's Distance Matrix

`1325c36e`

The campaign map no longer stalls for a quarter second when a castle
or town falls. ButterLib, which TAOM ships, keeps a "Distance Matrix"
of every settlement, clan and kingdom. It rebuilt the clan and kingdom
tables on the spot whenever a settlement passed to another clan, about
170 ms on average and up to 281 ms in one frame on TAOM's map
(measured by yotthani on v1.5.3; the six worst campaign-map frames
were all siege ends). It also built the full settlement table, about
355,000 pairs, at every save load and new game. Nothing installed
reads these tables.

TAOM now calls ButterLib's own Disable() on that subsystem at the
first main menu, so ButterLib never adds its GeopoliticsBehavior. One
[ButterLibDistance] line in taom_debug says whether it was switched
off, was already off, or could not be (a warning with the reason).
There is no opt-in: MCM's ButterLib page applies and rewrites its own
key in the same Options.json before TAOM runs, so a TAOM opt-in could
not hold. A mod that read the tables would get null; none is
installed.

A first design, a lazy rebuild through four patches on ButterLib's
private code, was dropped in review before it shipped.

Issue #740. Review: docs/reviews/rca-butterlib-distance-matrix-2026-10-06.md.
Feature doc: docs/features/butter-lib-distance-matrix.md.

### Refactoring

#### refactor(main-menu): v2.0.34 - MainMenuStep logs the full exception

`2afea887`

The main-menu guard from #807 is renamed from LifecycleStep to
MainMenuStep. It has one use, OnBeforeInitialModuleScreenSetAsRoot, and
its old name shared a prefix with the Load-Time Stamps [Lifecycle]
lines.

Its one WARNING now carries the whole exception with its stack. Before
#807 a throw there reached Patch37's crash report, which logged the
stack; the first version of the guard kept only the type and message.
A test throws from a named method and checks that the stack names it.

docs/features/main-menu-customizer.md now shows the guard in the hook,
the engine's per-tick retry as a design constraint, the warning, and
the new tests.

Refs #807.

#### refactor(core): v2.0.34 - one guarded SyncData chunk loop for two saves

`16a34027`

The War of the Ring momentum and the War Chronicle each carried their
own count-and-chunks SyncData loop. The momentum copy passed the saved
count straight to new List<string>(count), so a corrupt save threw on
load (a negative count) or ran out of memory (an absurd one).

The chunker moves from WarOfTheRingMomentum to Core/Persistence as
SyncDataChunker, with SyncChunks doing the whole pass: a count below 0
or above 4,096 is ignored with a warning and the state loads fresh. Both
behaviors call it. The saved keys are unchanged, so existing saves load
as before, and a save from before the _v3 keys still loads fresh
momentum without a warning.

The comments now give the engine's real limit: one SyncData string may
hold 32,763 UTF-8 bytes, because the engine writes a short length of 4
plus the bytes (SaveContext.SaveStringTo).

Refs #792.

#### refactor(combat): v2.0.34 - damage model glue moves to feature hooks

`f7e9bd0d`

No change in game. The campaign's damage model, the one slot every
combat feature shares, had grown to 286 lines against the 150 that
ADR-002 allows an entry point (#737). It is now 129 lines: each
override calls base, then one hook per feature, in the order that
matters.

The glue now lives in each feature's own Hooks folder, the pattern the
race abilities and the creature siege role already follow.
CombatMechanicsHooks holds the four combat services with their context
builders and extractors; SignatureStrikeVerdicts holds the signature
knockdown and knock-back verdicts; RefugeDamageHooks holds the refuge
defender reduction. The two horse charge checks that routed knockdowns
inside the model moved into the facade that owns each verdict. Both
services already declined the other case, so every verdict is
unchanged, a NaN charge velocity included. An unused config provider
argument, dead since #610, is gone.

Tests: a ceiling test keeps the model under 150 lines; source pins
check which agent, origin and base result reach each seam; the facades
have their own tests, with the charge gates written to fail first.
Full suite: 14832 pass, 5 skipped; the one failure,
EveryLanguage_DeclaresARowForEveryEnglishKey, predates this change.

A deep review (six lenses and a convergence pass) found one medium and
six low issues, all fixed. RCA:
docs/reviews/rca-combat-mechanics-model-split-2026-10-05.md

### Tests

#### test(native-pins): v2.0.34 - pin gate reads compiled attributes

`c286e239`

NativePinGateTests now finds the native pin tables by reflection: the
test types that declare a static KnownBuilds member. It reads their
compiled TestCategory attributes. The first version matched the source
text, so a commented-out [TestCategory("BindingVerification")] still
passed, and it took the class name from the file name.

Both known tables are pinned by name, so a table that stops being found
fails the test. Checked by commenting out the skeleton-buffer class's
BindingVerification tag: the new test fails, the old one passed.

docs/features/skeleton-buffer-guard.md gets its 2026-10-15 changelog
and issue lines.

Players see no change. Refs #806.

#### test(composition): v2.0.34 - pin the VanillaTuning stand-down names

`0e02d923`

VanillaTuning v0.1.1 switches off five of its own features when the
assembly named TAOM holds four type names (its Core/TaomOverlap.cs):
NameplateCullModule, MapViewReleaseModule, MissionStartGuardModule and
SkeletonBufferModule (the last stands for both frame-buffer features).
A rename on TAOM's side would bring back the double patching, and
parking one of those modules would leave neither mod doing the work.

VanillaTuningOverlapContractTests pins the assembly name and the four
full type names, and checks that each module is listed once in
FeatureModules.All and is not parked. It is a characterization pin: it
passed on its first run.

The provenance register line for this test lands with the knowledge
base batch in the same push.

Players see no change. Refs #808.

#### test(skeleton-buffer): v2.0.34 - pin v1.5.5 targets, gate native pins

`15e52d7a`

The engine-bump gate now fails when a native address pin table has no
row for the installed Bannerlord build. Before, the two pin classes
(SkeletonBufferInstalledBinaryTests and
ClipBudgetSignatureInstalledBinaryTests) were tagged LiveInstall only,
so a missing row reported Inconclusive, which reads as a skip. That is
how the v1.5.5 bump shipped without a skeleton-buffer row.

- Add the v1.5.5 row (TaleWorlds.Native.dll, 14,212,960 bytes): watch
  0x6A1A1, pool global 0xD9E160, pool 1 guard 0x6A1D4, pool 2 load
  0x69F95, pool 2 guard 0x6B410. The game itself finds these sites by
  pattern, and the 2026-10-15 log shows "guard ON" for both pools, so
  players were never without the guard.
- Tag both classes BindingVerification. The binding gate maps
  Inconclusive to failed, so an unlisted build now fails the gate.
- NativePinGateTests keeps every KnownBuilds table tagged at class
  level and named in the engine-bump skill.
- The engine-bump skill separates the C# pins (they fail in the gate)
  from the Python pins (they skip unless TAOM_GHIDRA_IT=1, so read the
  skip count).

Players see no change. Refs #806.

#### test(core): v2.0.34 - main party scan sees nested parens

`4f27b3bd`

MainPartyNullCheckScanTests guards against null-checking the computed
PartyBase.MainParty getter. Its parenthesised form allowed only one
pair of parentheses, so ((PartyBase.MainParty)) == null, the reversed
null == ((PartyBase.MainParty)), is null, ?? and ?. on it all slipped
through. Its call exclusion looked at one character only, so a call
written with a space before the parenthesis, such as
Find (PartyBase.MainParty)?.X, was flagged although the ?. guards the
call's result.

The regex now takes any depth of balanced parentheses around the
getter, and the call exclusion skips whitespace and a generic argument
list before the parenthesis. A keyword such as return, and a lambda
arrow, still count as not a call, so return (PartyBase.MainParty)?.X
stays caught. Fifteen new rows cover these shapes; eleven of them
failed against the old regex. The production scan still finds no
offender.

The adapters rule now states the coverage and both limits: a check
through a local alias is not seen, and strings are not blanked, so a
string holding the check fails the scan.

Refs #792.

#### test(war): v2.0.34 - dry run checks every saved chronicle field

`016ad47c`

The chronicle_open dry-run tests checked the return value, the message
and the final state, so a preview that changed a saved field in place
still passed: with "if (record != null) record.Assaulted = true;"
added to PreviewOpen, all 13 dry-run cases stayed green, because a
siege's confirmed open resets Assaulted on the same fixture.

Every dry run in the tests now goes through one helper that compares a
deep value snapshot of the store before and after: every public
property of every record (state, open, close and resolved days,
Assaulted, read by reflection so a new field is covered) and every
phase-seen day. The parity test now confirms on its own equivalent
fixture and checks both fixtures start equal.

With the same mutation 10 of the 13 cases fail; the other three are
refused before the record lookup. No player-facing change.

Refs #792.

#### test(war): v2.0.34 - pin the chronicle_open dry run to change nothing

`f0b5eddf`

No player-facing change. Three test groups pin the console dry run of
taom.chronicle_open. A dry run before the chronicle has launched
creates no saved row. A dry run after Full War begins does not record
the first Full War day. For a narrative, a siege that opens, a siege
that falls at once and a failed precondition, the dry run followed by
the confirmed open on the same campaign agree on whether the open
acts, and the outcome the dry run predicts is the state the confirmed
open leaves.

Moving the phase and adoption step above the dry-run branch fails the
first two, so they guard the read-only promise.

Refs #792.

#### test(war): v2.0.34 - pin the Eye wait log to no reason-text parsing

`d371745c`

The E25 tests check that a waiting hunter's reason is logged without
asserting its wording, but every wait reason begins "Waiting:" today.
A logging decision that went back to `firstWait.StartsWith("Waiting")`
still passed all of them.

A source-text test now reads TryStartHunt in EyeOfSauronService,
comments blanked, and fails if the method inspects text at all
(StartsWith, EndsWith, Contains, IndexOf, Equals, Substring, Split or
Regex), while still requiring the wait to be logged. The wait is
logged because WaitReason returned one, never because of its words.

Proved red with the in-memory mutant
`if (!dryRun && firstWait.StartsWith("Waiting"))`: the new test failed
naming StartsWith while the other 242 passed. The service file was
restored byte for byte.

Refs #792.

#### test(core): v2.0.34 - main party scan catches every null-check form

`dc1c3703`

The scan that bans null checks on the computed PartyBase.MainParty
getter matched only `PartyBase.MainParty == null`. The hunt popup's
own former guard, `PartyBase.MainParty != null`, slipped past it, so
the scan could not stop that crash from coming back.

It now matches both polarities, the reversed forms (`null ==`,
`null !=`) and the `is null` and `is not null` patterns, with
comments still blanked first. Data rows pin each detected form and
the safe forms it must ignore: the stored Campaign.Current?.MainParty,
a plain read, and the pattern inside a comment. A focused test pins
that HunterPartyAdapter.IsMapFree checks the stored main party.

Proved red against the trunk a9437a492 HunterPartyAdapter.cs and
MessengerCampaignBehavior.cs: the scan flagged both files and the
focused test failed; the two files were then restored byte for byte.

Refs #792.

#### test(war): v2.0.34 - pin the Eye of Sauron wait log to a typed check

`3633b654`

E25: the daily tick already decides a wait by WaitReason returning a
reason (since ac0a3e0c0), not by reading the verdict's wording. Pin it:
the daily tick logs a waiting hunter's reason, a refusal that is not a
wait logs nothing, and a dry run logs nothing. The assertions name the
hunter and party only, so rewording the message cannot break them. The
busy-hunter test that called TryStartHunt now carries that name.

Refs #792.

#### test(infra): v2.0.34 - faster wiring scans, pin the comment scan

`06252ae2`

Six Main-wide wiring scans stripped comments from every source file.
MentionsOutsideComments now reads the raw text first and strips only
files that contain the name, so the offender sets are unchanged. Each
scan dropped from about 0.5s to about 0.1s.

RepoPathsTests gains rows for it: a needle only in a comment is
false, one in code is true, whole-word rejects a longer identifier
and the substring mode accepts it, and a missing file throws
FileNotFoundException.

Refs #792.

#### test(infra): v2.0.34 - one engine-faithful data store fake

`ea8f2ad5`

Four private IDataStore fakes had drifted from the engine's
BehaviorSaveData.SyncData: they skipped null on save and overwrote a
duplicate key, so a test could pass where the engine would throw. The
Eye of Sauron and SpecialResources fakes already followed the engine.

EngineDataStore is now the single fake. It uses Add on save, so a
duplicate key throws, and a missing key on load returns false and
leaves the ref alone. Its summary names the one deliberate exception
(RoundTripDataStore, which keys by type) and two engine behaviours it
does not model: SyncData runs on load only when the save holds a
record for the behavior, and a load returns the saved object rather
than a deserialized copy.

The career test that loads an empty store is renamed
SyncData_LoadWithoutTheKeys_GracefullyLoadsEmpty, because it models
"record present, key missing", not a save older than the feature.

Refs #792.

#### test(migration): v2.0.34 - pin reflection-site labels to their lines

`ee19f105`

The reflection-site catalogue labels each lookup as File.cs:N, in
ReflectionSiteBindingTests and in reflection-sites.md. A label shows
only in a failure message, so a wrong label stays wrong until someone
follows it. Plan 022 fixed two drifted labels by hand. The formation
presets review found them drifted again, and six labels that pointed
at the wrong code, three of them since the commit that added them.

Two tests now pin the labels, and neither needs the game.
EveryLineLabel_PointsAtALineThatNamesItsMember checks that each
labelled line spells its member, with comments blanked, and that a
label with no line names a file that exists.
EveryDataRow_HasAMatchingRowInTheCatalogue checks that
reflection-sites.md has a row for each DataRow. The convention (label
the line that spells the member) is written in both files. The labels
that broke it are repointed, and the missing
Mission._initialPlayerAgent row is added. Findings 8 and 18 of
docs/reviews/rca-formation-presets-2026-10-09.md.

### Documentation

#### docs(reviews): v2.0.34 - RCA addendum for the party-size cap

`6553b8c7`

The review of the party-size cap found the culture cards still
advertising the old bonuses, the third time a feat change has missed
them. The RCA records it as the second change of
rca-wotr-war-rule-2026-10-18.md, the faction-card lesson notes that a
test now guards party size, and the elite-tax lesson records that the
20% floor was removed on purpose.

Refs #818.

#### docs(reviews): v2.0.34 - RCA, lessons and generator region gate

`3fe86e22`

Root-cause analysis of the War of the Ring war rule review: 18
findings, every one fixed or decided, recorded in
docs/reviews/rca-wotr-war-rule-2026-10-18.md.

Three lessons go to the lessons files: a war veto must also gate
where the engine charges for or offers the war, a negative relation
value declares war whatever isAtWar says, and peace must not be made
from OnSessionLaunched on a load.

A hand edit inside a region a generator owns has now slipped through
three times in three days. tools/tests/test_generator_regions_in_sync
runs each owner's --check, so such an edit fails /verify instead of
being reverted silently by the next regeneration.

Refs #818.

#### docs(orientation): v2.0.34 - trim the artist-ports trap row to the cap

`cd48cb8e`

The trap row for artist commit ports grew to 187 characters in the
previous terminology pass, past the 180-character cap that
check-doc-config-drift enforces, which blocked every commit. Shortened
to 179 with the same rule: base the merge on the mirror revision
nearest the file, not on the commit's parent.

#### docs(kitbash): v2.0.34 - Carn Dum icon RCA, lessons and workflow docs

`0daece88`

The deep review of the map icon tooling made 20 findings, each fixed or
given an owner, and its convergence pass nine more, all fixed. The RCA groups them in five patterns: success that was
self-reported, steps written for one item that took many, estimates
stated as facts, defaults nobody re-read, and texture density spent on
hidden geometry. Five new lessons in build-tooling-workflow.md carry
the rules. RCA:
docs/reviews/rca-carn-dum-icon-tooling-2026-10-16.md

The docs now match the tools after the review:

- docs/kitbash/map-icons.md records both routes (decimate a Tripo
  model, or rebuild it from solids), the 0.15 draw scale, the Kit
  material rule (names equal to the FBX materials, use_specular on) and
  the icon's state: v11 built and unwrapped, texturing in progress. It
  lists the decisions still owed: the new texture names collide with
  the Tripo version's t_carn_dum_town_*, occlusion belongs in the
  albedo or in the _s blue channel but not both, and whether 1K is
  enough at full zoom.
- The texture density figure (10 to 15 px per metre) is now labelled an
  estimate for the default map camera; full zoom needs about 50.
- The _s map's AO dims ambient light and the sun's specular, not the
  sun's diffuse, and back-face culling is set per material (two_sided).
- substance-painter-mcp-setup.md names Painter 12.1.5 and the new
  script set. mcp-servers.md records that local MCP servers are keyed
  by the folder's spelling, case included.
- tools/README.md gets a section for the Tripo, Blender and Painter
  tools. The converter commands in the pipeline doc and the Animalia
  feature doc carry the now required --src and --dst.

#### docs(reviews): v2.0.34 - adopt yotthani 2026-10-16, RCA and lessons

`8dd76ddb`

The daily review of yotthani's repositories (bannerlord 784fef93 to
2ff002be, MithrilForge 0a2f4761 to ae7834aa). No code was taken; the
engine facts are restated in TAOM's docs with source, evidence kind
and engine version:

- siege map events and the order of fief finalization (fief-granting)
- clan wage payment, garrison recruitment and the measured lord party
  sizes (settlement-economy-food-prosperity)
- TacticHoldChokePoint's eligibility and score, and the choke-point
  claim that yotthani removed upstream (formations-and-team-ai)
- the map-scene view hang and what it means for MapViewRelease, plus
  texture and first-draw costs (map-view-release,
  gauntletui-viewmodel-screen, gui-sprite-system)

The MapViewRelease risk the review raised is settled as far as a read
can settle it: yotthani's own VanillaTuning makes the same call on the
map's own view, on by default; the in-game check still watches for a
hang on returning to the map. The record carries the doc review's
results, a "For yotthani" note, and the reasons the issues were not
filed. upstream-watch.json advances to the reviewed heads.

The RCA of the two deep reviews and the convergence pass is
docs/reviews/rca-upstream-review-i3-2026-10-16.md, with the items
still open after the push. Lessons: three in lessons/misc.md, one in
lessons/gamemodels-services.md. Older docs no longer say "he" or "his"
for yotthani, whose pronouns are not stated.

#### docs(reviews): v2.0.34 - KEYforce javelins port RCA and lessons

`42693d9b`

The deep review of the 03320a65 port (eight lenses and a convergence
pass) found no engine incompatibility. Its two HIGH findings were the
starter-kit block that would have stripped the javelins' melee mode
(fixed in the data and the tool) and the validator's melee pass
ignoring --moduledata (fixed). Two lessons: check which TAOM tool owns
the region an artist's merged rows land in, and prove a baseline run
read the baseline; a third repeats the rule to read the decision
record before calling data hand-authored or stale.

Refs #812

#### docs(engine): v2.0.34 - doc review fixes for the yotthani batch

`0bb1f3ca`

A find-and-verify review of the four knowledge-base commits: three
finders (fidelity to source, consistency with TAOM, routing) and one
verifier that re-read every source. 26 of 29 findings were confirmed
and are fixed; the record is in adopt-yotthani-2026-10-15.md, "The doc
review". Players see no change.

- The Modding Kit registry path had a form feed in place of "\f" in
  two docs; it reads HKCU\Software\TaleWorlds\RGL\fbx_import_settings.
- The Black Gate ash row now gives the fix's 12.2 ms, not the 2 %
  proof run's 11.6 ms.
- Upstream hedges restored: troop and level "explain almost nothing"
  for strike rates; decision state 6 is likely the defend state; the
  dynamic-cook layout is described, not stated as a rule; VanillaTuning's
  guard named as the source names it.
- Removing a behaviour at a lower index inside OnMissionTick ticks the
  current behaviour twice and skips no other behaviour.
- Corrected counts (vanilla flail_skeleton is 4), line anchors, a
  translation, a loader address's binary, two doctrine table cells,
  the register's Covers columns and pass counts, and a duplicated
  statement of the VanillaTuning contract.
- tools/README.md: the clip-rename row carries the rope-flag rule.
- New lesson in lessons/misc.md: restate an upstream fact with its
  hedge and its evidence kind, in the same sentence.

#### docs(engine): v2.0.34 - 1.5.5 perf, native and coexistence findings

`7eac3015`

yotthani's newer measurements and TAOM's own v1.5.5 checks, restated and
credited in the owning docs (MithrilForge docs/engine/perf.md and
modding-kit.md at 0a2f4761, yotthani/bannerlord at 784fef93). Players
see no change.

- VanillaTuning v0.1.1 stands aside on four TAOM type names: the
  nameplate cull, map-view release, mission-start guard and
  skeleton-buffer docs say so, and why load order does not matter.
- The frame-cost doc gains a v1.5.5 tag line and the v1.5.5 limb-ray
  and skeleton-buffer addresses, each with its build; the Black Gate
  ash emitter's share and the 21.0 to 11.6 ms proof run (v1.5.3); the
  measured drop in clip residency, with its cause marked as yotthani's
  reading; the AI pause that gave no gain; the armour flicker above
  2,340 skeletons with one guard.
- Campaign costs measured on a TAOM campaign (v1.5.3), election and
  war-declaration costs after the ButterLib fix, the server GC figures
  and the rejected non-concurrent variant, the UIExtenderEx inventory
  figure in cheat mode and first-use shader compiles.
- The A/B section gains the reference-PC core-count method and its
  caveats.
- The ButterLib doc dates its settlement count (2026-10-06); the RCA
  and the adoption record link each other.

#### docs(reviews): v2.0.34 - adopt yotthani 2026-10-15, traps and lessons

`4d79eca8`

The record of the 2026-10-15 review of yotthani's newer work
(docs/reviews/adopt-yotthani-2026-10-15.md): what was read, what TAOM
verified, the decisions, what was not taken and why, findings for the
maintainer, issue drafts, and a note for yotthani. Players see no
change.

- Traps: the "Kit clip rename" row in the trap index now carries the
  companion-clip rule. The lifecycle docs say never to remove a
  behaviour inside OnMissionTick, and that the engine re-runs every
  module's main-menu hook on every tick after a throw.
- A coexistence section for VanillaTuning v0.1.1, which stands aside
  for four TAOM features by type name, and why load order does not
  matter.
- The shader-precompilation unpark checklist gains the parked guard's
  behaviour removal.
- Lessons: a presence-based stand-aside breaks when the other mod
  stands aside too; a doc batch written beside the code is re-read
  against the reviewed code before it ships.
- Dated correction notes in the 2026-10-08 record, the provenance
  register's sixth pass, and the adoption history line.

#### docs(battle): v2.0.34 - measured battle AI and morale constants

`d690ff8d`

yotthani's battle-behaviour measurements on v1.5.5, taken with TAOM
2.0.34 loaded, restated and credited, plus TAOM's own decompile of the
AI defence properties. Players see no change.

- The first tactic decision, at 0.5 s, picks FullScaleAttack; the
  start layout matters more than the tactic; the morale casualty-shock
  constants from the v1.5.5 source; 34.5 % of AI strike events hit a
  friend; 13 unchanged mirror battles show the natural spread, so a
  single pair of runs proves nothing in an A/B.
- TAOM's decompile of v1.5.5: the native melee AI reads the block and
  parry abilities and the shield-defend value that TAOM's race
  abilities and Culture Doctrine set, so those levers are live. No
  native read was found for AiAttackingShieldDefenseChance. yotthani's
  two zeroed battles that showed no change stay unexplained.

#### docs(animation): v2.0.34 - companion clips, agent-less figures, Kit

`2b8f5ff3`

Engine and Modding Kit facts from yotthani's MithrilForge and bannerlord
repositories (pinned commits), restated and credited in the owning
docs. Players see no change.

- A clip with a rope flag samples its "{clip}_weapon" companion with
  no null check, so a renamed or cloned rope-flagged clip without its
  companion is an access violation at wind-up (decompiled on v1.5.5).
  It applies to every weapon with its own skeleton: flails, slings and
  the shackle. TAOM ships no rope-flagged clip today.
- Agent-less figures (yotthani's crowd lab): what the engine draws for
  a figure that has no agent.
- Modding Kit: the FBX import dialog remembers its flags, bo_ node
  rules, prefab body-flag names, cook caveats, cloth links as GUIDs,
  the decal-set crash, props left undrawn on a shader-sack miss, and
  where TAOM's camp packages came from.
- tools/README.md and the animation lessons gain the clip rules.

#### docs(mission-perf): v2.0.34 - shield timing covers all four targets

`fdee18f0`

From the checks of the second review's fixes (a convergence pass and a
focused Codex pass). Comments and docs only; no code changes.

- The heartbeat doc and the Patch98 header gave the second-game-start
  PatchShield timing to the camera target alone. TAOM applies its
  patches on all four shielded targets (Patch23 on SpawnAgent and both
  Patch98 categories) in the same once-per-process game-init block,
  after PatchShield's second pass, so all four are shielded only from a
  process's second game start.
- The native addresses of the engine's pre-display path now name their
  build (v1.5.5) and say the frame function calls it in one branch.
- perf_runs.py: a comment says a mixed row's cameraMs rate is a lower
  bound. NativePinGateTests: the summary says a type that fails to load
  is invisible to the scan, which is why the known tables are pinned.
- The RCA records both checks, and the Lens 1 notes and Codex items
  left unchanged, with their reasons.

Refs #809.

#### docs(reviews): v2.0.34 - second review of the yotthani code items

`b3fac302`

At the maintainer's request, a fresh seven-lens deep review and a Codex
review (gpt-6-astra, reasoning max) read the five commits of #806 to
#809. Neither found a CRITICAL or HIGH defect. The RCA's new section
lists each finding, its source, why the first review missed it, and its
fix, plus the three proposals rejected with their reasons.

- docs/reviews/rca-yotthani-review-2026-10-15.md: second review.
- docs/reviews/codex-adversarial-yotthani-review-2026-10-15.prompt.md:
  the Codex prompt.
- lessons/harmony-il.md: an empty patch category still returns true.
- lessons/native-cpp-port.md: read a reference doc's tag legend before
  a figure or address goes in.

#### docs(reviews): v2.0.34 - yotthani review code RCA and lessons

`33253ea9`

The deep review of #806 to #809 (seven lenses, then a convergence pass
on the fixes) found no HIGH or CRITICAL defect. The RCA lists the five
MEDIUM and the LOW findings with their fixes. Their common cause: a
claim about another component, written without reading that component.

- docs/reviews/rca-yotthani-review-2026-10-15.md: findings, the
  convergence pass, follow-ups.
- rca-engine-bump-v1.5.5-2026-10-14.md: F10, the missed v1.5.5
  skeleton-buffer pin row.
- lessons/harmony-il.md: a patch category fails as one unit, and a
  recurrence on install lines that claim more than was checked.
- lessons/native-cpp-port.md: a native pin table fails the gate on an
  unlisted build, and a recurrence on figures copied without their
  source tag.

#### docs(dread): v2.0.34 - correct the rout chain from the v1.5.5 engine

`f4f12a73`

The Aura of Dread doc now describes how a routed troop really leaves a
party, read from the v1.5.5 engine: only a panicked troop is lost; a
troop that leaves on a retreat order keeps its place but still counts
as a casualty; deserters come only from the defeated side; and siege
defenders are exempt only in a siege assault, not in a sally-out.

It also corrects three engine claims: the 30% formation "Flee" value
changes no behaviour (nothing reads it), only a direct Retreat()
bypasses the panic handler's bracket, and a direct Panic() skips the
morale model's veto and the siege-ladder rule.

The review RCA gains a second-review table, and the lessons file a
rule on promoting a lesson with every branch kept.

Review: docs/reviews/rca-dread-aura-local-2026-10-15.md

#### docs(reviews): v2.0.34 - dread aura review RCA and lessons

`0103a827`

Root-cause analysis of the dread aura deep review, plus four lessons:
a morale effect that routs a troop is not local; read an engine
method's native side before claiming it has no limit or fallback; a
fallback reads the compiled default and never restates its literal; a
setting with an MCM default and a JSON default gets a test that the two
are equal.

The RCA also proposes three trap-index lines, left for a follow-up
because the index is full (45 of 45 rows).

#### docs(ai): v2.0.34 - ladder brief for the artist's assistant

`9b11bd73`

A brief for KEYforce's AI assistant: TAOM's skill, armour, melee and
ranged ladders (kingdom armour caps, mesh tier names, DPS bands, ladder
bow cells), which files are generated, and the gates to run before a
commit, since TAOM's hooks do not run outside its own setup. Listed in
docs/INDEX.md.

Refs #811

#### docs(reviews): v2.0.34 - KEYforce Harad port RCA and lesson

`dde30a5b`

The deep review of the #805 port found four MEDIUM and two HIGH
findings; the port fixes each in its own data, and the scratch merge
proof and the loc coverage gate go to #813 and #815. Every MEDIUM
finding repeats a recorded lesson, so the new lesson asks a troop port
to diff the whole validator warning set against HEAD, because the
commit hook surfaces only errors.

Refs #805

#### docs(reviews): v2.0.34 - Harad repair RCA and lessons

`caefaa1f`

The deep review of the Harad repair (44e9edd48): eight lenses and a
convergence pass, 15 findings with their causes, Mike's ten decisions,
and the follow-ups. Three lessons: a player kit copied from a troop
inherits its launcher's difficulty, a gate that runs only inside Claude
Code does not gate a plain-git commit, and a name generator that globs a
folder registers whatever lands in it.

Refs #798

#### docs(reviews): v2.0.34 - WildMesh mounts review RCA and lessons

`d546054d`

The root-cause record of the WildMesh3D mounts deep review (#794): 25
findings, how each was missed and what now prevents it, Mike's
decisions, the regeneration round and the follow-ups that predate the
change (the Animalia masters' doubled and lost frames, the elk
walk-back's 60 fps source, the human and troll exports' same exporter
truncation, the market gate's fallback).

Four findings repeated lessons recorded for #646 on 2026-09-23, so the
lessons gain recurrence notes and five new entries: port a sibling
tool's refusal tests before its code; check an artefact against its
source, never against itself; Blender's FBX exporter truncates time;
a hoof plant starts a full stance; a writer of named Armory items
writes their English loc rows.

#### docs(enlistment): v2.0.34 - R1b is dormant until #796

`9563d5e8`

The enlistment doc credited rule R1b (never close an encounter that
belongs to a battle) with protecting the loot screen and holding the
#551 latch. It has done neither in a real game: the encounter adapter
works out the battle flag and never passes it on, so every live
snapshot reads "no battle" and R1b runs only in tests.

The doc now says so, names the two cleanup paths that must keep their
obligation before the flag is passed (a failed join's rollback moves
to EnlistedAttached, where the stale-latch recovery never runs; a
discharge accepts the deferral, then clears the record, so nothing
retries), and explains what held the #551 latch instead: the sweep and
TryBreakBattleLatch returned before asking the policy, and R4 skips a
joined battle's encounter because its encountered party is the enemy
leader.

The Codex prompt that found both paths is committed beside its
siblings.

Refs #796.

#### docs(reviews): v2.0.34 - Codex lessons from #765 and #792 reviews

`18615680`

Adds the review-codex step 3h lessons for the four Codex reviews to
.ai/review-reference.md, "Lessons From Prior Reviews":

- does especially well: running the analyzer's verdict function on
  ledgers that should fail it (Batch A), sending every callback of
  the chronicle state machine through its War Ended rule (Batch B),
  and following PartySizeLimit's cache to the roster version it
  reads (M8);
- look harder: the #792 scan check tried every comparison form of
  the PartyBase.MainParty guard but not ?., and seven such reads
  stayed until a later docs audit.

Refs #765, #792.

#### docs(features): v2.0.34 - volunteers, Arthedain, catalogues current

`1a2da1e3`

- troop-progression.md: TaomVolunteerModel's four overrides, the
  VolunteerProductionService row and its two test classes, and a
  changelog line for the War Chronicle step.
- castle-recruitment.md: a changelog line for the War Chronicle
  multiplier on the castle slot curve; the provider test count is 9.
- arthedain.md: its lines are translated (a9437a492); #754 is open.
- module-dependencies.md: the #371 pairing rows are back since
  95085de30; versions, the worked example, the update step, the
  gotcha and the numbers now say so; an anchor link replaces a
  drifting line cite. The #371 comment is cited at lines 11-20, and
  every other SubModule.xml line cite in the chapter was re-measured.
- file-catalogue.md: rows for war_chronicle.json and rally.json.
- tools/README.md: tier changes, not flaps; the wotr string count;
  a row for check_external_loc_coverage.py.

Refs #765, #792.

#### docs(engine): v2.0.34 - daily tick on load, SyncData size, MainParty

`ae1a6b55`

Puts three engine facts found in the #765 and #792 work where an
author reads first.

- The tick and campaign-event concept docs say a load can repeat the
  save's elapsed day, read from v1.5.5 Campaign.CreateCampaignEvents
  (in-game check owed, #793). Only the hourly event shifts the same
  way: the quarter-hourly first wait counts the quarter's elapsed
  fraction in hours (Campaign.cs:1039-1045), so it repeats only in
  the first fifth of a quarter and can skip up to four ticks, and
  with no remainder the hourly and quarter-hourly waits are half a
  period while the daily one is a full day.
- save-system.md names the 32,763-byte SyncData string limit and the
  shared SyncDataChunker.SyncChunks.
- The adapters rule names PartyBase.MainParty and its scan test.
- The gamemodels rule lists the War Chronicle's use of the volunteer
  and target score models.
- Trap index: a "Daily tick after load" row; the two skeleton rows
  are merged to stay at 45.

Refs #765, #792.

#### docs(coop): v2.0.34 - chronicle gates, hunt under Together, counts

`86d3a774`

- coop-interop.md: the War of the Ring launch reconciles before its
  phase check and only declares pairs the model still holds at war;
  the chronicle's load marker handler; a gated row for the event
  chain's router; the siege-defence split is pinned by two tests,
  not three; the transpiler count is a grep, not 7.
- bannerlord-together-compat.md: the Eye of Sauron hunt is off under
  BannerlordTogether, the War Chronicle runs on every peer there,
  TAOM's pin is v1.5.5, and the transpiler and veto counts are
  current.
- refuge.md: its session latch is the shape the amended reset rule
  no longer prescribes, tracked in #773.

Refs #765, #792.

#### docs(wotr): v2.0.34 - #764 and #772 in diplomacy and momentum docs

`cf7486cb`

- diplomacy.md: the stored-stance guard and phase-first order in the
  overview and the how-to, an accurate HasDeclaredWar line, the four
  WarPhase values, the three new test files, and changelog and issue
  lines for #764 and #772.
- war-of-the-ring.md: the skipped unknown or eliminated scripted war
  (#772 R3), the phase-day tests, a GitHub Issue section, and the
  Gundabad and Arthedain pair in the Phase 1 table, list and test.
- war-of-the-ring-momentum.md: an absent chunk count loads without a
  warning, 73 string keys, 174 tests in 12 files, and the status no
  longer says unmerged or untranslated.

Refs #765, #792.

#### docs(war): v2.0.34 - War Chronicle and Eye of Sauron docs current

`2e2a422d`

Brings the two feature docs and their catalogue rows up to the code
after the Codex fixes, the decisions and #792.

- war-chronicle.md: two behaviors, the full constructor reset, the
  game-loaded marker, the rally rebuild on every refresh, the six
  commands and the event chain's dependencies, the corrected test
  counts, translations done, a once-per-day in-game check, and the
  changelog (now in date order) and issue lines for the Codex
  fixes, #792 and #793.
- eye-of-sauron.md: the broken Steps row joined into one table row,
  grain topped up during a battle, the console refusal in another
  lord's army, the 1.5 S speed band, and #792 in the changelog.
- feature-map, gamemodel registry and dev console rows match: the
  real hunt command names, the speed band, chronicle_open after C07,
  and four commands that had no row. The naming table now lists
  every shipped exception among the 57 commands, fc_grant_merit and
  the nudge_, save_, reload_ and rescue_ verbs included.

Refs #765, #792.

#### docs(lessons): v2.0.34 - daily tick after load, MainParty, recurrences

`e8504a62`

Adds the cross-feature lessons the #765 and #792 reviews produced.

- After a load the daily tick can repeat the save's own day (#793):
  guard once-a-day work on a saved day.
- Never null-check PartyBase.MainParty; check
  Campaign.Current?.MainParty, gated by MainPartyNullCheckScanTests.
- A clan that changes kingdom moves its fiefs with no owner-change
  event (Batch B C02).
- Rebuild owned effects from the current state on every refresh
  (Batch A F3).
- Recurrence lines on four older lessons: the fallback lookup (#792
  F1), the guard never seen red (F2, F3), the changed lookup key
  (M8 E01) and Enum.TryParse (Batch B C18).
- The SyncData size lesson now says 32,763 bytes, as its own Prevent
  line already did, and the #764 pointers name the lesson they mean.

Refs #765, #792.

#### docs(reviews): v2.0.34 - #792 RCA, review log and track record

`f479d96f`

Records the review follow-ups of #792 and repairs the records of the
War Chronicle, Eye of Sauron and War of the Ring reviews.

- New RCA for #792: the scope, the reload finding behind #793 and
  Codex F1 to F4 with their fixes. The four lens reports were not
  kept, so only the recorded finding is tabled.
- REVIEW-LOG: an entry for #792 (placed before the #791 entry, whose
  work came later that day), six summary rows for #764, #772, Batch
  A, Batch B, M8 and #792, and corrected dispositions for the Batch
  A, Batch B and M8 entries; the M8 entry cites the RCA lines for
  E16 to E40.
- The M8 RCA and log cite the trunk decision commits cbacbf1f0 and
  7ab91767e, not the pre-rebase SHAs, and say that F2 predates the
  decisions. F2's cause reads as the code at 34b0e25a9 had it:
  EndHunt invalidated only the speed and ClearHunt nothing.
- Codex track record: four essays (Batch A, Batch B, M8, #792); the
  four essays they displace move to the archive.
- The #792 git ranges start at the parent bc8c9f71b, so git
  rev-list --count gives the stated 14 and 18 commits.
- Batch A RCA gains the Why missed and Preventive action columns,
  marked as reconstructed on 2026-10-14; B02 and B32 say what the
  code and tests hold. The Batch A, Batch B and M8 RCAs gain a "Why
  each agent missed it" section and the Codex effort.
- Repeat callouts for E01 and C18, the refuted C52, the done C55,
  the #764 rule amendment, #773 and the #772 F24 to F26 follow-ups.

Refs #765, #792.

#### docs(git): v2.0.34 - commit with a pathspec, never a bare commit

`385b7adf`

`git add <paths> && git commit` commits the whole index, so files
another session staged after the last `git status` go in too. On
2026-10-14 a 12-path docs commit took 10 files a WildMesh session had
staged; it was rebuilt from a temporary index before any push, and
their work stayed staged. The new row in git-and-commits.md names the
pathspec commit and, for a file that also holds another session's
hunks, the temporary-index recipe.

#### docs(tools): v2.0.34 - orc and goblin docs, review log, audit

`a6b538c4`

Documentation follow-up to 776054c0c and 2952ccc54 (#791, #795):

- docs/INDEX.md lists the orc and goblin locomotion feature doc.
- The review log has an entry for the 2026-10-14 deep review, and its
  RCA points each fixed finding at the commit that holds it.
- The feature doc links the RCA, names the shared retarget functions
  and the restyle's pure tests, and its changelog cites both commits.
  The starter kit and armour acquisition histories do the same.
- /armory-audit's locomotion step and the tools README say that
  --check also fails on an empty clip folder; the README names the
  functions the retarget tool now shares with the restyle, and the
  catalogue row count (5,038).
- The hill troll ledger and two community guides give the clip box's
  TpacTool v0.5.1 name, BlendWithAnimation, beside UnknownClipName.
- docs/audits/armory-ref-audit.md regenerated at 2952ccc54: CLEAN. It
  lists five new wildmesh_* meshes nothing uses yet (#794); the
  catalogue is not regenerated here.

#### docs(war): v2.0.34 - the day boundary is the elapsed day, not midday

`a66b5754`

Comments and docs said a save made "before midday" ticks its own day
again after a load. The War Chronicle counts elapsed campaign days
from CampaignStartTime, and vanilla's DefaultCampaignTimeModel puts
that at 09:00 on the calendar clock. The repeat follows a save in the
first half of the elapsed day, about 09:00 to 21:00 on the calendar
clock: a calendar 18:00 save repeats its day, a 06:00 save does not.

ChronicleService, the gate, tick and chronicle test comments, the
feature doc, the analyzer docstring, its README row and three RCAs now
say "first half of the elapsed campaign day" and keep elapsed days
apart from calendar hours. The "past midnight" note on
ChronicleService.Today now names the elapsed-day boundary. One
analyzer test is renamed to match; behaviour is unchanged.

The feature doc also records the unreadable-clock handling and the
updated test counts (552 under the feature folder, 629 in all).

The README row keeps its CRLF ending: the file's blob has carried
CRLF on all 538 lines since before this branch, so the flagged
"trailing whitespace" is that CR, and removing it means renormalizing
the whole file.

Also commits the Codex prompt for the follow-up review.

Refs #792.
Refs #793.

#### docs(release): v2.0.34 - ship Arthedain's towns in the TAOM_Map package

`612b3759`

Arthedain's towns (town_AN1 to town_AN3) exist only in the live
TAOM_Map: the TAOM_Map packaged in the patreon, public and testing
channels has none of them (grep on 2026-10-14). The kingdom's clans,
its home settlement and its culture's starting town name them, and so
does the War Chronicle's north event.

The release process now says the next release must ship a TAOM_Map
package with those towns, with the grep that proves it per channel, and
/release lists the check in its pre-flight.

Refs #792.

#### docs(war): v2.0.34 - War Chronicle docs and review follow-ups closed

`573dc155`

The War Chronicle doc now covers the once-per-day guard, the load
marker, the order a daily tick writes its ledger lines, the analyzer's
rewind and the new tests, and records that the north event is Skipped
on a TAOM_Map package without Arthedain's towns.

The two chronicle RCAs close their follow-ups; one proposed change to
SyncChunks was rejected as more test churn than it saves. The War of
the Ring phase-ordering RCA gets a dated correction: a load does not
re-align the daily tick to the day boundary.

Refs #792.

#### docs(war): v2.0.34 - close the Eye of Sauron review follow-ups

`49d1dc17`

The RCA now accounts for E25 and E39 and closes E41 to E47. E43 is
rejected: a per-culture ConditionalWeakTable cache for
CultureFeatAdapter.FromOrNull was tried and reverted. On .NET Framework
4.x ConditionalWeakTable.GetValue calls TryGetValue, which takes
lock (_lock) on every read, and the speed, size and morale models call
FromOrNull on the engine's parallel party tick
(CampaignTickCacheDataStore, TWParallel.For). The cache put a
process-wide lock on that path to save an unmeasured 24-byte
allocation, and the simplicity criterion rejects it. If a profile ever
shows the allocation matters, a lock-free map built on the main thread
at session launch is the alternative.

Refs #792.

#### docs(war): v2.0.34 - Eye of Sauron key files, how-to, tests, risks

`abd03e85`

E39: the co-op table's EyeOfSauronBehavior row said the frame tick only
reads the snapshot; it publishes it, and while a hunt runs it can stand
the hunter off. The row now names the gated handlers and why the battle,
party-destroyed and frame handlers stay ungated. The feature doc gains
the missing key files and the four string ids, a step-by-step how-to
(host troops, hunter order, settings), test claims matched to the tests
that exist, and the open risks, including why the sea guards are
unreachable (NavalDLC declared incompatible, naval travel parked). The
module dependency reference now says TAOM's manifest lists NavalDLC as
incompatible.

The settings how-to follows the persisted MCM default rule: a default
never flips, so rename the setting to change it. The doc no longer says
the four hunt lines are English only (all 12 languages since
a9437a492), and #765 is marked closed.

Refs #792.

#### docs(reviews): v2.0.34 - Codex review of the career forks engine

`bc8c9f71`

Codex gpt-6-astra at max reviewed the career forks engine commits and
the trunk merge (4f5b412d7..1e6a97c6d). It disputed five of six
suspects with probes of the production source and confirmed one P2:
the tree-version migration ran for the main hero only, fixed in
59c1803d2. A second pass at high on the fix found nothing.

The RCA records the finding, why the Claude lenses missed it, and the
second pass's three observations. A new lesson covers load-time
migrations over saved records. The review log, the Codex track record
and both prompts are updated.

Refs #769. RCA docs/reviews/rca-career-e4e7-2026-10-14.md.

#### docs(ai): v2.0.34 - builder template asks for docs, bounds, true text

`8c1305e2`

The career forks review traced most of its findings to builder briefs
that described only the success path. The Feature Developer spawn
template now asks the builder to update the feature doc as the code
changes and grep docs/ for deleted or renamed members, to bound every
config value and anything derived from it, to make a result message
true on every branch, and to give a saved marker one writer that
replaces the old one.

Refs #769. RCA docs/reviews/rca-career-e4e7-2026-10-14.md.

#### docs(reviews): v2.0.34 - RCA and lessons for the career forks review

`f2d4fa0d`

The /deep-review of the career forks engine change found nothing
critical or high and two medium findings: a retrain price that wrapped
negative on absurd config and made a paid switch free, and a feature
doc the change had made wrong. Both are fixed in the change itself.

Three patterns had shipped before: a result message that is false on
one branch, living docs left behind by a change, and a destructive path
that later data can switch on. The RCA records each finding, why it was
missed and what now prevents it, plus the convergence pass. Five lesson
entries cover the derived-value bound, the single writer for a saved
version marker, Gauntlet's UpdateChildrenStates, the data-gated
destructive path, and the recurrences.

Refs #769. RCA docs/reviews/rca-career-e4e7-2026-10-14.md.

#### docs(reviews): v2.0.34 - account for every finding in the two RCAs

`b8988815`

#### docs(reviews): v2.0.34 - RCAs for the chronicle events and the Eye of Sauron

`0b08eeea`

Review records for two finished #765 milestones. Each RCA lists the deep-review findings with their dispositions, the maintainer decisions of 2026-10-14 and the Codex findings with their fixes. Four lessons are added: re-check the terminal state on every callback path, a resume path reuses the start checks, invalidate an engine cache with the counter it keys on, and search a broad phase past the exact edge.

#### docs(reviews): v2.0.34 - Codex prompt for the Eye of Sauron review

`b3ddf8a1`

#### docs(reviews): v2.0.34 - Codex prompt for the chronicle events review

`ba0b77d6`

#### docs(reviews): v2.0.34 - record the fixes for two adoption findings

`46fe58b7`

The Custom Battle damage-model finding is fixed by #788 and the unused
FiefManagementNavItemVM finding by #789 (the map-bar Fiefs button);
the adoption record now names both issues and their branches.

#### docs(reviews): v2.0.34 - Codex review of the career fixes

`4f5b412d`

Records the Codex review of #766 to #771: four of five suspects
disputed with decompiled evidence, one confirmed bug fixed as #790. The
RCA, the campaign-mechanics lesson and the review log carry the result,
and the review prompt is kept for reuse.

#### docs(reviews): v2.0.34 - RCA and lessons for the career fixes review

`86ef5c7a`

Root cause analysis for the review of #766, #767 and #768 and the bugs
it found (#770, #771, #774), plus three lessons: a rule that reads a
parsed field needs a test over the shipped data, a map event with a
winner is not always a battle, and a flat party size term goes after
the last percentage factor.

#### docs(provenance): v2.0.34 - yotthani's HoN code is MIT

`eed20591`

On 2026-10-09 the maintainer reported that yotthani has put all of
his yotthani/bannerlord code under the MIT licence. TAOM carries
code from his Heir of Numenor (HoN) mods: siege dismount, mixed
formations, smart cavalry AI, fief management, quick actions,
equipment presets and companion tactics.

THIRD-PARTY-LICENSES.txt gains an MIT section for those portions,
so the notice obligation is met, and the provenance register moves
the HoN, TransferbuttonMenu and yotthani/bannerlord rows from
UNKNOWN to MIT and cleared. His repository has licence files only
in HoN/VanillaTuning and HoN/ShaderCacheKeeper so far; a root
licence file has been asked for. The adoption record, the
companion-tactics and shader-compile-notice docs and the adoption
precedent are updated to match.

#### docs(ai): v2.0.34 - pair Harmony prefix and finalizer via __state

`83ad1eab`

This adds the preventive rule of RCA finding 21 in
docs/reviews/rca-yotthani-adoption-2026-10-08.md.

The start guard's canary kept "this call's prefix ran" in a
process-wide flag. A finalizer rerun would have faked a lost guard.
The 2026-10-03 lesson in lessons/harmony-il.md already said to pair
a prefix and a finalizer through Harmony's __state, but the builder
brief did not carry it. The rule is now one line in
.claude/rules/harmony-patches.md, which loads with every patch file.

#### docs(rules): v2.0.34 - reset per-campaign state in the constructor

`9ab3952a`

The session-reset rule for process-lifetime singletons now says to
reset in the constructor of a behavior built fresh for every campaign,
never in OnSessionLaunched, OnNewGameCreated or RegisterEvents, and to
pin the per-campaign construction with a source-text test. The
OnSessionLaunched reset it used to prescribe ran too late for the War
of the Ring service (#764), because campaign-event listeners run
newest-first. The rule's first hole is also worded exactly now: the
engine calls SyncData in loading mode only when a save record exists.

RCA: docs/reviews/rca-wotr-session-reset-2026-10-08.md

#### docs(ai): v2.0.34 - fix stale facts in skills, agents and rules

`9e9f04df`

A /doctor prompt-audit pass over the instruction files found text the
repo contradicts and prompt idioms written for older models. Report:
docs/reviews/prompt-audit-2026-10-08.md.

- Skills no longer describe the removed GitHub CI, and agents no
  longer pin engine v1.5.2 (the pin is v1.5.4).
- taleworlds-researcher drops ILSpy MCP calls its tool list never
  granted; feature-builder and /release verify without deploying.
- /issue writes the body to a file, checks it with
  check_public_text.py and passes --body-file instead of a heredoc.
- /freeze, /unfreeze and /localize now agree with their own scripts,
  and gamemodels, harmony-patches and provenance drop claims the repo
  disproves.
- "Think harder" prompts and generic method text the model follows
  unprompted are gone from the agents and rules that carried them.

#### docs(lords): v2.0.34 - lords in both files merge, not override

`369eb030`

The lord skills guide, the skill-sets page and the lord-skills skill
said that a lord defined in both lords.xslt and characters/lords.xml
uses only the lords.xml copy, so the XSLT copy was dead code. That is
wrong. The engine merges the two: lords.xml wins each attribute and
skill it redeclares, the XSLT copy supplies the rest, and differing
equipment sets are both kept, so the hero picks one at random each
campaign (lesson #644, the Nazgul kit coin flip).

The docs now describe the merge and tell the reader to check both
files for the id before editing a lord.

#### docs(ai): v2.0.34 - give each specialist agent its own tier

`d0e4bc94`

The five older agents ran without an effort setting, and two of them
on a model that did not fit their work. Each now sets model and effort
in its definition:

- error-detective and taleworlds-researcher: Opus, high. A wrong root
  cause or a misread engine signature is expensive to get wrong.
- feature-builder and debugger: Sonnet, high.
- refactoring-specialist: Sonnet, medium (tests gate it both sides).

An agent runs one model, so the rule now says refactors, new features
and non-obvious debugging start with an architect plan that a Sonnet
specialist then carries out.

#### docs(ai): v2.0.34 - route subagents by model and effort tier

`fdec324f`

Sessions ran Opus 5.5 at high effort for every task, searches and test
runs included. The main session now orchestrates and sends each task to
the cheapest model and effort that does it well.

- New always-loaded rule .claude/rules/model-effort-selection.md: the
  routing table (Haiku/low, Sonnet/medium, Opus/high, Fable only after
  Opus fails), one retry one tier up, and a one-line note before each
  spawn naming the agent, model and effort.
- Three tier agents: fast-reader (Haiku, low), implementer (Sonnet,
  medium) and architect (Opus, high, read-only).
- CLAUDE.md's "Model routing" becomes "Model & Effort Selection", a
  pointer to the rule. The table lives in the rule because CLAUDE.md
  with its imports sat 543 B under the lint_docs.py cap.
- Catalog row and provenance entry for the rule.

#### docs(lords): v2.0.34 - link the generated lord kits doc to #747

`20c34f03`

The feature doc now names its issue, #747, closed with the in-game
checklist still owed.

#### docs(engine): v2.0.34 - fold in MithrilForge's newer engine findings

`b5e1d0b3`

Knowledge-base update from yotthani's MithrilForge (MIT, commit
2e6fe97). Each fact is credited, and tagged with the build it was
read on: v1.5.3 unless TAOM's own native decompile confirmed it on
v1.5.4.

Corrections to pages that were wrong:
- The per-agent limb ray crashes because its native wrapper never
  builds the capsules, so hit, distance and bone are stack garbage.
- The clip reader lock is paid per bone sample. The 8.3 s once pinned
  on DualWield's clips was the whole function's time in one profile.
- Loading Type 1 keeps the first 3 s resident; Types 0 and 1 share
  data through a cache keyed on the Animation GUID and source window.
- Look bend applies on channel 1, never channel 0.
- A decal texture outside the atlas renders a solid rectangle.

New facts: skeletons are posed only while visible (#739 tracks
TAOM's own bone reads), agent LOD saves triangles not draw items,
distant shadows use a stand-in mesh chosen by skeleton name,
particle simulation has no visibility gate, the native melee
pipeline and which agent reads cross into native code, save costs
and the post-save GC hitch, the map-view GPU leak on menu close, a
Harmony rule against patch methods on closed generic types, and the
cost of ConditionalWeakTable's lock.

The trap index row for mission logic now also warns that an
exception in AfterStart reloads the mission forever.

The thread table in the Harmony rules gains a row for engine
callbacks marked multi-thread callable. DecideCrushedThrough is
reached from one of them; an audit found the crush path read-only
after construction, so no code change.

Reviewed by an adversarial find-and-verify pass. Its 17 confirmed
findings, mostly upstream hedges restated as fact, were fixed.

#### docs(arena): v2.0.34 - Patch69 comment points to its registry entry

`2b6f6216`

Patch69_TournamentRosterGuard's class comment listed the callers of
GetParticipantCharacters itself, and that copy kept drifting from the
registry's (four, then three; there are six on v1.5.3 and v1.5.4).
A rewrite with the six callers reached origin today inside commit
75b7b666, which another session made from the shared working tree.
The /deep-review of that rewrite (lenses 1 to 6) found every engine
claim true but the copy incomplete again, so the comment is now a
five-line claim plus a pointer to the registry entry, the one place
the callers and consequences live.

Also: the registry quotes the join menu's gossip line exactly, with
its {=GuWWKgEm} key; the build-tooling lesson about counting call
sites now says six; a harmony-il lesson records the rule. RCA:
docs/reviews/rca-patch69-comment-2026-10-05.md. Comment and docs
only; the file compiles. Patch issue: #407.

#### docs(arena): v2.0.34 - vanilla tournament armour, v1.5.4 checks

`fe661961`

Two Arena docs said vanilla dresses every tournament fighter in the
host culture's kit. It does that only in arena practice fights: in a
tournament match DefaultTournamentModel.GetParticipantArmor returns the
fighter's own RandomBattleEquipment, because TournamentBehavior sets
MissionMode.Tournament before the armour is copied. arena.md and
tournament-armor-assignment.md now say so, and what TAOM's override
does in each: it fixes cross-race clipping in practice fights, and in
tournament matches it swaps each fighter's own armour for his
culture's practice kit. The second doc's override count (seven),
registration line and dependencies are brought up to date, and the
NPC handbook's line reference follows the code.

Checked against Bannerlord v1.5.4, which Steam installed today:
Tournament Rewards, Arena, Race Abilities, the Mouth of Sauron gear and
the troll gear lock need no code change (a decompile diff of every
engine member they use; the strict binding gate passes 464 of 464 on
v1.5.4; both XML merge harnesses pass). The docs record it:
- The Armory snapshot README: v1.5.4 builds the face object under a
  covers_head helm and then hides it, so the cause given for the
  Mouth of Sauron's frozen grips is gone; the in-game look (#733) is
  still owed.
- race-abilities.md: the native agent-visuals code that runs on build,
  re-equip and death changed, so three in-game checks are listed.
- The Patch69 registry entry: GetParticipantCharacters has six
  callers, not three, and the substitution also reaches off-screen
  tournaments and the TournamentFinished list (Tournament Rewards'
  hero count included, as tournament-rewards.md now notes).
- A lesson: describe a vanilla method from every branch, mode checks
  included.

#### docs(race-abilities): v2.0.34 - first in-game test, tuning later

`5fb310c2`

Mike ran two Custom Battles on a v2.0.34 dev build (Isengard against
Dunland, and against Erebor and the Iron Hills) and called Race
Abilities a success for now. That build also carried the working tree's
uncommitted edits, none of them in Race Abilities. The feature doc
records what the log showed: every trigger fired, kin rallied, no
errors, and the abilities moved each fight by a few percent of its
damage. It also records the tuning notes: the outline in a packed line,
Hill-clan Fury's low hit rate, and the off-and-on balance runs still to
do. The console doc and the doc index now mention the outline count
and the glow and sparks. #730 is closed with triage-needs-ingame and
triage-blocked-decision for the steps and decisions still open.

#### docs(release): v2.0.34 - channel numbers, version bumps, hook gap

`62157c7e`

The release docs now match what the last releases did and what v2.0.34
taught:
- /release Phase 1 reads every release channel's version before a
  number is chosen: the testing channel already carried an untagged
  v2.0.33, so this release became v2.0.34.
- The Dependencies version moves with every release, as it has since
  v2.0.29, and the live TAOM_Map and LOTRLOME_Armory versions are set
  by hand, so all four modules read the number the release note
  promises.
- release-process.md explains how to resolve a crash report that
  quotes a number git never saw (v2.0.33 on testing, v2.0.29.5 on
  patreon and public), says a later deploying build of a dirty tree
  voids the stamp gate and that Mike packages in the editor, and lists
  the pack0.tpac prune.
- The commit-subject hook judges a commit in another worktree against
  the main checkout's version; the hooks catalog and the git guide say
  so, with the workaround.
- deep-reviewer.md warns that audit_armory_refs.py rewrites a tracked
  report unless given --report -.
- The doc index lists the v2.0.34 release note.
- Lessons: pick a release number no channel carries; check a memory
  card's "uncommitted" against the release commit; a mesh tool's guard
  lists what it may edit.

### Chores

#### chore(wildmesh): v2.0.34 - remove the Custom Battle test riders

`7bfb2f94`

The five WildMesh test riders, [Test] War Boar Rider, Bristleback
Boar Rider, Iron Hills Goat Rider, Mirkwood Stag Rider and Greenwood
Moose Rider, no longer appear in the Custom Battle troop picker. They
were console test troops for the WildMesh3D mounts (#794) and were
never meant to ship. No campaign troop rides those mounts yet.

Removed: their troop file and its SubModule.xml node, and the tool
test that pinned each rider to its animal and harness. Their names
were never registered for translation, and their ladder and
recruitment exemptions went on 2026-10-15. The feature doc now tells
the in-game test to restore them into the game install only, never
into the repo. The ledger, file catalogue, feature map, index,
validation doc and the WildMesh writer's comment say so.

Review: docs/reviews/rca-wildmesh-rider-removal-2026-10-18.md. The
testing channel's v2.0.35 package of 2026-10-18 still ships the
riders; it is replaced from the v2.0.35 tag build.

Tests: Python tool suite 4,636 passed, 8 skipped; ModuleData
validator 0 errors; the release step 5 grep prints nothing.

#### chore(skills): v2.0.34 - prep-tripo-prop covers the rebuild route

`a69cd39d`

The /prep-tripo-prop skill now starts with a route choice: decimate the
Tripo model, or rebuild it from solids when decimation leaves it
looking melted. The rebuild route lists every command in order:
measure the Tripo layout, build, unwrap into texture sets, create the
Painter project from the unwrap report, paint with painter_stacks.py,
export and render a preview. The handoff converts every set, checks
names against the Kit's AssetSources and Assets folders too, and names
the Kit materials after the FBX materials with use_specular on. The
CLAUDE.md skills row is shorter.

#### chore(ai): v2.0.34 - native-branch trap and morale-write rule

`1e62adeb`

The trap index gains a "Native branches" row (a managed decompile
hides native branches; use native_decompile.py first), and the
"Persisted MCM defaults" row now says to comment why a setting was
renamed. "Culture party templates" leaves the index: .claude/rules/
xslt.md states both halves and loads on every .xslt file.

csharp-patterns.md gains one anti-pattern: a local fear effect that
routs must floor the morale or say in its doc that its routs spread.
.ai/scopes.md links that rule so other AI clients see it.

#### chore(armory): v2.0.34 - catalogue the WildMesh3D mount meshes

`e12789cc`

The Armory reference audit after the WildMesh3D art drop (#794) is
CLEAN: no broken mesh or collision body ref, no dead item id, and no
deleted or renamed mesh that an item still names.

The catalogue now carries the five new body meshes (wildmesh_boar,
_deer, _moose, _mountaingoat and _warthog), so a later rename of one
shows as a breaking RENAME instead of as new art. They are filed as
creature bodies through override rows, as the great elk and the Animalia
elk and moose are, because the parser's vocabulary has no creature
names; each row names the Horse item that wears the mesh. The audit
report no longer lists them as art nothing uses.

The audit's one warning is the starter-kit drift for Rhun and Harad,
which predates this change. No live Armory file changed in this step.

#### chore(loc): v2.0.34 - re-baseline TAOM_Map coverage for new place names

`66053a9d`

check_external_loc_coverage.py reported 21 to 23 more untranslated
TAOM_Map rows in BR, DE, FR, IT, PL, SP and TR. They are not a
reinstall regression: every untranslated row is a settlement name kept
identical to English on purpose, and the new ones came with the
settlements added since the last baseline (Minas Morgul, Linhir, and
Arthedain's Bree, Fornost Erain and Annuminas among them). The baseline
records the current counts; the Armory counts and the other five
languages are unchanged, and the check passes again.

Refs #792.

#### chore(loc): v2.0.34 - translate the backlog into all 12 languages

`a9437a49`

Translates every untranslated line in all 12 languages (BR, CNs, CNt,
DE, FR, IT, JP, KO, PL, RU, SP, TR): the War Chronicle events and
rally, the Eye of Sauron hunt, the race abilities (#731) and the rest
of the backlog in TAOM, TAOM_Map and the Armory (about 1,190 lines per
language). Six lines where the translation mixed in a word from
another script (Chinese, Japanese and Korean) were corrected by hand,
in the language file and the translation cache.

Refs #765, #731.

#### chore(engine): v2.0.34 - Bannerlord v1.5.5

`4f998585`

TAOM now targets Bannerlord v1.5.5 (Steam beta, 2026-10-09, build
124170). A fresh build needed no code change, the strict binding gate
passes against the new engine (494 passed, 0 skipped), and the released
v2.0.34 testing build still resolves every engine member it calls, so
players can take the update without a new TAOM build.

What players will notice comes from vanilla fixes TAOM inherits: weak
lords no longer attack an army that has just raided a village, a raid in
peacetime applies the crime rating, the ransom broker's blood-feud
options work with several feud clans, incidents no longer delete captive
lords, and bandit voice-over plays (vanilla Calradian lines may now play
on some TAOM characters; to be checked in game).

One behaviour change: v1.5.5 ends the game with a message box naming the
module when another mod's SubModule constructor throws. TAOM no longer
swallows that failure; the constructor guard keeps only its base
constructor site, because the engine now catches the rest itself.

Engine pins moved: the pinned version, the Native constraint in TAOM's
SubModule.xml (and in the live TAOM_Map and Armory manifests), the
per-build native address pins in three tests, and the API snapshot. The
BUTR reference assemblies stay at 1.5.4 until BUTR publishes 1.5.5.

New bump tools: tools/check_engine_member_refs.ps1 resolves a shipped
DLL's engine references against the installed engine in memory, and
tools/engine_body_diff.py diffs the body of every engine member TAOM
binds, counting every row. Both replace scratch scripts and close the
follow-ups on #736. The /engine-bump skill and the version statements in
README and the reference docs are updated.

Write-up: docs/migration/v1.5.5-impact.md. Still owed: the body diff
review (the first run hit a tool parsing fault), the v1.5.5 category
tree, and the in-game checks.

#### chore(harness): v2.0.34 - align the harness with Claude Code 2.1.292

`b84dbaeb`

Reviewed three months of Claude Code (2.1.198 to 2.1.294) and VS Code
(1.127 to 1.141) releases against the harness; Mike approved each
change one at a time. Record:
docs/reviews/adopt-claude-code-2026-10-08.md.

- harness-facts.md and CLAUDE.md now describe the installed 2.1.288
  CLI and 2.1.292 extension: path rules also load when a file is
  written or edited, hook additionalContext reaches Claude, and Stop
  or Escape in the VS Code panel no longer kills background subagents.
- /review-codex and /deep-review Step 0 give Codex a two hour
  background timeout, the most an unattended run allows.
- Subagents can no longer spawn subagents of their own
  (CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH=1), and built-in agents carry
  no tier, so every Explore or Plan spawn names its model.
- Six plugins nobody used are switched off in the shared settings.
- The VS Code "Run Tests" task no longer deploys into the game, and
  the MCP example naming removed servers is gone.
- powershell-tool.md and mcp-servers.md match the current docs.

#### chore(skills): v2.0.34 - remove the last 14 unused skills

`d6b53709`

The second half of the skill usage audit. taom-src, research,
build-fix, verify-bindings, xslt-check, commit-split, context-budget,
author-armor, new-culture, lord-skills, new-adr, new-map-prop,
refine-creature-anim and new-creature-mount wrapped scripts the model
already runs, or pointed at authoring guides it already follows. The
skill list is now the 17 that are invoked or hold their own protocol.

What those skills held that nothing else did moved to its owning doc:
- the compile-error retry budget to the agent operating manual;
- the binding gate command, the "Skipped is never green" rule and the
  failure table to the API snapshot README;
- a corrected vanilla file map to the XSLT rule (the old one pointed
  five stylesheets at SandBoxCore files that do not exist);
- the commit grouping rules to git-and-commits;
- the ADR index step to the ADR template, and the prefab test step to
  the static prop guide.

The context budget scanner moved to tools/context_budget_scan.sh, and
CLAUDE.md now routes to the script or the guide directly.

Evidence: docs/reviews/skill-usage-audit-2026-10-08.md.

#### chore(skills): v2.0.34 - remove 14 skills the models never used

`8b108312`

Seven weeks of session transcripts showed 21 of the 45 Claude Code
skills were never invoked. The model ran the scripts they wrap
directly (taom-src.ps1 about 5,100 times, lint_docs.py about 2,100)
and needed no rework, and the mistakes that did recur were caught by
hooks, tests and the compiler rather than by skill text.

This removes 14 of them: migration-status,
agent-introspection-debugging, knowledge-compile, skill-stocktake,
lint-cleanup-loop, finish-branch, context-save, context-restore,
humanizer, doc-graph, lint-docs, codex-verify, deslop and scope-check.

The few lines found only in those skills moved to the docs that own
them: the safe-deletion rules to the refactoring-specialist agent, the
"never drop part of a request silently" rule to think-before-coding,
the merge and backlink rules to git-and-commits, and the research
compile steps to docs/research/README.md. CLAUDE.md, the agents, the
rules and the docs now point at the scripts directly.

The evidence, and 14 more approved removals held until another
session commits its edits to the files they touch, are in
docs/reviews/skill-usage-audit-2026-10-08.md.

#### chore(hooks): v2.0.34 - commit gate checks the career kits

`985e0402`

The career kit file is generated from the troop files, so a troop edit
can leave it off its rule with nothing staged in it. A Gondor troop
rework did exactly that, and the file sat adrift from v2.0.31 to v2.0.34
because its --verify pin only ran by hand.

The ModuleData commit hook now also runs
tools/generate_career_kits.py --verify (about 0.8s, bounded at 10s inside
the hook's 60s registration). Drift blocks the commit with the repair
command; a missing install fails open, as the validator does. Tested with
a stubbed verify for each exit code; security scan clean for the hook.

Refs #743

#### chore(ci): v2.0.34 - remove the GitHub Actions workflows

`c8d69a0d`

The four workflows (C# build and tests without the game, Python tool
tests, Doc budget, and the 1.4.5 line's Build & Test) are deleted.
Verification runs locally: /verify builds and runs the full C# suite
against the installed game, which covers more than the hosted run could,
and now also runs the Python tool tests (python -m unittest discover -s
tools/tests -t ., about four minutes with the live install), which
nothing else ran automatically. The doc checks still gate commits
through check-doc-config-drift.sh, and tools/test_hooks.sh covers the
hooks.

The RefAsm build mode stays, so TAOM can still be built and unit-tested
without a Bannerlord install; .ai/verification.md now lists its three
commands instead of pointing at csharp.yml. References to the workflows
in the rules, skills, tools and docs are updated; ADRs and review
records keep theirs as history.

Run history at removal (gh run list, bannerlord-1.5.x): C# 22 passed /
46 failed, Python 17 / 16, Doc budget 72 / 0; Build & Test on
bannerlord-1.4.5 57 / 129. Its last failures on this branch predate the
v1.5.4 work: EveryLanguage_DeclaresARowForEveryEnglishKey (#731),
Patch93/Patch94 binding counts, and the name-localization generated-file
check.

#### chore(hooks): v2.0.34 - retire the native DLL static-CRT commit hook

`ad1992b5`

check-native-dll-crt.sh blocked a commit when the vendored
TAOM.NativeSkinFixes.dll linked a dynamic CRT. NativeSkinFixes and its
DLL are gone (9c310bc2) and its settings.json registration was removed
in 5b241c46, so the script had nothing left to guard. It is deleted,
along with its entries in tools/test_hooks.sh, the python-only gate list
in session-start.sh and the hooks catalog row; the catalog recount is
now 27 files, 25 settings registrations, 30 in total.

tools/test_hooks.sh: 941 passed, 0 failed. /security-scan: no CRITICAL;
its one HIGH and one MED predate this change (the auditor's own test
fixture and the push judge's git call). Refs #736.

#### chore(engine): v2.0.34 - Bannerlord v1.5.4, NativeSkinFixes removed

`9c310bc2`

TAOM now targets Bannerlord v1.5.4 (Steam beta, 2026-10-05). A fresh
build needed no code change: the kingdom formal-name rename in this
hotfix touches nothing TAOM uses, and every Harmony, GameModel,
reflection, prefab-clone and XML-merge gate passes against the new
engine. Players on v1.5.4 need this build or later; the v2.0.33
testing build calls a TooltipProperty constructor v1.5.4 removed.

What players will notice:
- Low-security towns lose loyalty a little faster. Vanilla raised its
  security penalty and cut the garrison's security bonus, and TAOM
  inherits both. TAOM keeps its own rebellion thresholds.
- Lords now name realms by their own titles ("Kingdom of Erebor") in
  two dialog lines.
- The "Native Skin Fixes" MCM group is gone. It had done nothing since
  July 2026; v1.5.4 says the engine now animates hands under a
  covers_head helmet with full gloves. TAOM.NativeSkinFixes.dll and
  MinHook.x64.dll no longer ship on this line.

Engine pins moved: the pinned version, the Native constraint in TAOM's
SubModule.xml (and in the live TAOM_Map and Armory manifests), BUTR's
reference assemblies for CI (1.5.4.123627-beta), the per-build native
address pins in three tests, and the handbook gate's decompile tree.

NativeSkinFixes removal: the C# installer, the C++ hook project, both
DLLs and MinHook's notice, the MCM toggle and its two strings in 12
languages, its 11 tests and the CI static-CRT step. The license notice
now covers the NuGet runtime libraries TAOM ships (DryIoc, Json.NET,
Unsafe), and tools/package_release.py refuses retired binaries so a
stale copy in the install cannot ship again. The bannerlord-1.4.5 line
keeps the feature, parked. The check-native-dll-crt.sh commit hook
retires in a follow-up once its settings.json entry is removed.

Write-up: docs/migration/v1.5.4-impact.md. Review and root cause:
docs/reviews/rca-engine-bump-v1.5.4-2026-10-05.md. Issue #736 tracks
the in-game checks.

### Commits without the version label

#### fix(docs): update terminology and clarify instructions in various documentation files

`7247797b`

#### Add painter scripts and texture sets for Carn Dum project

`71fea53c`

- Introduced new JSON file for Carn Dum texture sets defining materials and parts.
- Added painter_export.py for exporting texture sets with specified parameters.
- Created painter_new_project.py to set up new projects and bake mesh maps.
- Implemented painter_run.py to execute Python scripts within Substance Painter.
- Developed painter_stack_angmar_fortress.py for Angmar fortress layer stacks with specific recipes.
- Created painter_stack_stone_frost.py for "cold dark stone" layer stack with customizable parameters.
- Added painter_tiling_angmar.py for tiling texture sets for Angmar fortress with detailed material definitions.

#### feat(gondor): add Gondor kitbash build prompt and guidelines for Blender assembly fix(orc-goblin): update animation results for orcs and goblins in game fix(blender_probe): correct kit pitch calculation in building probe script

`bc2bf40f`

#### Add WildMesh3D mounts support to LOTRLOME_Armory

`c3fa211d`

- Introduced `apply_wildmesh_armory.py` script to manage the integration of WildMesh3D mounts into the Armory.
- Created `lotrlome-wildmesh-changes.md` to document changes related to WildMesh3D mounts.
- Updated `ModuleData/Monsters/LOTR/lotr_monster_wildmesh.xml` to define new mounts: War Boar, Bristleback Boar, Iron Hills Goat, Mirkwood Stag, and Greenwood Moose.
- Modified `SubModule.xml` to register new monsters after the Animalia block.
- Enhanced `ModuleData/action_sets.xml` with action sets for each WildMesh3D mount.
- Updated `ModuleData/LOTRLOME_items/LOTRAOM_horses.xml` to include new horse items for the mounts.
- Added unit tests in `test_apply_wildmesh_armory.py` to ensure functionality and integrity of the new mounts integration.

#### Add clip measurement JSON files for various animals and implement tests for orc and goblin locomotion binding

`776054c0`

- Created JSON files for boar, deer, moose, mountain goat, and warthog clip measurements, detailing their animation properties and movement metrics.
- Added a test suite for the `bind_orc_goblin_locomotion.py` script to ensure proper binding of locomotion clips and validate XML structure.
- Included functionality to handle missing clips and ensure compatibility with the live Armory action sets.

#### Some XML stuff which haterade will hate me for it 2

`08b30bfc`

#### feat(war): add war chronicle features including ledger, effects, and rally status

`fb79abe8`

#### fix(launchSettings): update commandLineArgs for singleplayer mode

`a7cca2d5`

style(MapBar): enhance icon alignment and fitting properties

fix(mm_panel): update mm_panel.png for improved visuals

#### fix(docs): update references to bannerlord-1.5.x in various documents

`7cd4920e`

#### Refactor and enhance various components of TAOM

`bb68eeca`

- Updated `adapters-taleworlds-api.md` to clarify the handling of mount cases in `AttackInformation` and added a new lesson on resolving an agent's party through `BattleCombatant`.
- Revised `data-content-cultures.md` to reflect that XSLT cultures no longer inherit vanilla's lord templates, ensuring that generated lords wear appropriate gear.
- Improved `harmony-il.md` by emphasizing the importance of enforcing invariants in pure rules rather than relying on fall-through prefixes.
- Enhanced `misc.md` with a lesson on the completeness of provider lists and the necessity of deriving them rather than manually selecting.
- Added a new section in `testing-qa.md` to stress the importance of checking the effect of wiring pins, not just their presence.
- Documented findings from the combat mechanics model split review, addressing issues related to damage calculations and the handling of agent origins.
- Created a new document for the deep review of generated lord kits, detailing findings and corrective actions taken.
- Updated `rca-howdah-prefab-review-2026-09-19.md` to reflect the resolution of the refuge reduction issue for howdah and mumak crews.
- Introduced a new document for the review of refuge reduction for crews and mounts, detailing the changes made to ensure correct damage calculations.
- Added a new plan to investigate offscreen bone reads and ensure that poses are correctly updated for offscreen agents.

#### refactor(hooks): remove redundant native DLL check command from settings

`5b241c46`

#### Refactor code structure and remove redundant sections for improved readability and maintainability

`75b7b666`

## v2.0.33 (2026-10-04)

Commits since v2.0.32: 74 (62 with the version label, 12 without).

### Features

#### feat(race-abilities): v2.0.32 - glow and sparks, 1 to 2 minute cooldowns

`d9b50277`

Abilities now come round every one to two minutes: every cooldown is
four times the first numbers (Berserk 60 s; Swarm, Scurry, Hill-clan
Fury and Corsair Raid 80 s; Bloodlust, Hunter's Rush and Variag
Ferocity 100 s; the rest 120 s). How long each one lasts is unchanged.

A fury, guard or dread ability now shows on the battlefield. While it
is active the soldier wears a coloured outline: red for fury, steel
blue for a guard, violet for dread. The six speed and aim abilities
stay unlit so a crowd stays readable. Sparks burst on the soldier who
fires and on every third kinsman who joins him. Only the 40 soldiers
nearest the camera are outlined (visuals.maxGlowing in
race_abilities.json), repainted every half second, cleared the moment
a window ends or a soldier falls, and hidden with Hide Battle UI. New
MCM switch: Battle Tactics, Race Abilities, Ability Glow and Sparks
(on). taom.print_race_abilities also reports how many soldiers are
outlined.

Khand keeps Rhun's troops and Variag Ferocity stays as it is (Mike's
decision). The feature doc now names everyone who carries each
ability, corrects Umbar (its own recruits, lords and armies fire
Corsair Raid), and says what Wainrider Wall and Variag Ferocity do for
a rider or a charioteer.

Reviewed by all seven deep-review lenses and a convergence pass:
docs/reviews/rca-race-ability-glow-2026-10-04.md. Tests: 13,973
passed, 1 known failure (translations owed, #731), 5 skipped. #730.

#### feat(race-abilities): v2.0.32 - battle abilities per race and culture

`a498e357`

AI soldiers now fight with a battle ability for their race, or for men
their culture, on a cooldown. A behaviour tree on each soldier waits
for his moment (wounded in melee, a kill, cavalry closing, a target in
bow range, kin falling), fires the ability for a few seconds, and kin
nearby whose ability is ready fire with him, so a whole line braces or
a pack goes berserk at once.

Races: berserkers go berserk (every swing breaks a block, no guard,
then spent), Uruk-hai fall into bloodlust (kills heal and lengthen
it), dwarves stand fast (nothing crushes through them, less damage,
no panic), elves quicken (draw, aim, feet), orcs swarm (stronger the
bigger the mob), goblins scurry, Mordor's uruks hold with iron
discipline, Gundabad's run their prey down, Dol Guldur's spread dread.
Cultures, for men: Gondor closes ranks, Rohan's riders spur on, Dale
aims true, Dunland rages, Harad's arrows bite, Rhun locks its wall,
Umbar's corsairs press, Khand's Variags fight savage, and the Shadow's
men frighten. Elite troops hit harder and pay more; lords most of all.

Works in campaign battles and in Custom Battle. Numbers live in
race_abilities.json. MCM Battle Tactics/Race Abilities: on/off, war
cries, a message-log line for big waves, and a debug log. The log
reports what each ability did every 30 s of activity and at battle
end, and the console command taom.race_abilities shows it in game.

Known limitations: a horse charge, a kick or a shield bash still
knocks back a dwarf standing fast (he stays on his feet); the ability
names show in English until the translation run.

#### feat(tools): v2.0.32 - rank the equipment assets a battle preloads

`3dac7ab5`

A battle costs about 2 to 3 GB of native memory, and nothing said which
items, meshes or textures carry that weight, so every memory lever in
the Armory's armour, weapons, hair and beards was picked by guess. The
new tools/audit_battle_equipment_memory.py ranks them, offline: the
battle counterpart of the campaign map memory audit, whose pack index
and decoders it reuses.

For every TAOM troop, or for chosen sides (--troops files or --culture
ids), it builds the equipment sets the way the engine does, one
assignment at a time, and like the engine it refuses an item that does
not fit its slot (a refusal is its own reason row, ITEM_SLOT_REJECTED).
It resolves each item to the meshes, materials, textures and collision
bodies the engine preloads for it (gender and slim variants, crafted
pieces, horses), adds each race's skin, hair and beard meshes, and sums
their bytes in the release packs, counting a shared asset once. The
floor is render buffers, texture chains and collision bodies; the upper
bound adds mesh edit data, whose residency in a battle is unverified. It
attributes pack bytes and does not measure process memory.

It writes a ranked Markdown report, TSVs (assets, items, troops,
cultures, sides, unresolved) and a run.log with one line per fallback
reason. A texture it cannot size from its pixels or its header counts
its stub or 0 bytes and makes every total that holds it a lower bound,
and the report then opens with an incomplete-totals note. Each run first
deletes the report and the TSVs an earlier run left in its folder. A run
that selects no troops, or names a troops file that is missing, stops
with an abort line and exit code 2.

First results on the testing channel: all 1,657 TAOM troops reach 2,670
items; Gondor against Mordor shares 600 MB of 3.18 GB of side floors, a
2.58 GB battle floor; the largest single asset is the crewed mumak's
platform mesh sk_mumakil_platform_a1, about 65 MiB of render buffers.

With --loose-assets, both this tool and the map scene audit read a
module's loose Assets tree instead of its cooked packs when it has one,
as the engine does, and report each cooked tree they skip; a loose mesh
is never flagged as editor stream bloat. The map audit's default output
is byte-identical.

Found on the way, not changed here: sk_uruk_hai_skirt_a1 is body armour
placed in the Cape slot of urukhai_champion and urukhai_berserker in
troops_isengard.xml, so the engine never equips it. The tool's feature
doc and feature-map row are still to be written.

Refs: #720 (plan 038)
Not-tested: in-game memory; the audit attributes pack bytes and does not
 measure a running game.

#### feat(release): v2.0.32 - sack and JIT reports; module sacks left out

`e4c2c159`

The release packager (tools/package_release.py) now prints three reports
on every run, --dry-run included, and leaves module-level shader sacks
out of the copy. The reports only read: none of them can refuse a
release or change the exit code, and a report that fails prints its
failure and lets the run carry on.

Scene shader caches: one line per shipped SceneObj scene saying whether
its ShaderCache/D3D11 folder holds terrain_shaders_header_data.bin and
compressed_shader_cache.sack, with the sack's format, then a summary
line and a WARNING for any sack whose format differs from the majority
of the shipped sacks. A scene without a sack ships as it is (the
maintainer's call, decision D8), and SceneObj/Backups is never judged.
Because the WARNING compares the shipped sacks with each other, a whole
set one format behind the engine raises nothing.

Module shader sacks, a behaviour change: the
compressed_shader_cache.sack directly in a module's Shaders/D3D11 folder
is no longer copied into a release, for any module, and the report lists
each one with its size and format under one policy line. Until now the
packager copied these from the dev install and never reported them;
issue #448's three lagging sacks were exactly these. The policy
(decision D15) is to ship TAOM's and the Armory's compiled sacks and
never TAOM_Map's, but only after a test shows the game uses a Kit-built
sack, so for now none ships. Scene sacks keep shipping, and so do
shader_mapping.bin and shader_compile_report.log beside the module sacks
(whether they should is still open).

JIT state: one line per shipped copy of TAOM.dll and
TAOM.Dependencies.dll saying OFF, ON, "ON (no DebuggableAttribute on the
assembly)" or "unknown (reason)", read from the assembly's metadata
tables without loading the DLL. TAOM ships Debug builds on purpose, so
OFF is expected and never refused. The rule was measured on .NET
Framework 4.8.1: the JIT turns optimization off only when the
attribute's first byte has its low bit set and its second byte is not
zero, so DisableOptimizations alone reads ON. A malformed attribute
reads unknown. The tests build the exact headers of the shipped and the
dev TAOM.dll.

The release skill, release-process.md step 9 and the module map describe
the reports and the policy. Not part of this change: the sweep of the
older shader texts that decision D15 also asks for
(shader-precompilation.md, precompile_scenes.txt and its provider's
comment, v1.4.8-impact.md).

Refs: #717 (plan 035)
Not-tested: a dry run on the live dev install since module sacks were
 left out, a real packaging run into a channel folder, and a packaged
 build played in game.

#### feat(perf-runs): v2.0.32 - per-mission rows and A/B compare from logs

`67eff55c`

tools/perf_runs.py turns any number of taom_debug logs into one row per
mission and compares two groups of runs, so a frame-time or memory
change is judged by numbers rather than by feel. Until now the
[MissionPerf] heartbeat had no reader, and triage_battle_load.py reads
only the last mission of a log.

Each row carries the load buckets (through triage_battle_load.py's own
parser), the spawn window kept apart from the steady state by its
position, steady medians of fps and of average and p95 frame time, the
worst frame, GC collections per minute and, when the tick profiler's
lines are present, per-behaviour cost and hitches by phase. Flags say
when the numbers may not mean what they seem (FRAME_CAP,
MEMORY_PRESSURE, DIAG_ON, DIRTY_BUILD, BUILD_PAIR_MISMATCH), and each
names its evidence.

    python tools/perf_runs.py <log> [<log> ...] [--json]
    python tools/perf_runs.py compare --a <logs> --b <logs> [--json]

compare keeps only one scene's rows with --scene, refuses to mix Debug
and Release builds or texture settings unless --allow-mixed is given,
and counts a row without a readable [PerfContext] as unchecked. Exit
codes: 0 rows found, 1 no mission, 2 a usage error, an unreadable file
or a refused compare.

Nothing is skipped in silence. The report opens with one header per log
(path, size, lines, missions, lines of a known tag it could not parse,
and a count per tag of the key=value lines a game wrote before its first
mission) and prints the first five unparsed lines verbatim. Every other
"[Tag] key=value" line is kept as extra tags, on its mission's row or,
before a game's first mission, on the log header (--json lists each
line's tag, time and fields; the text report counts them per tag);
summary lines that start with a word are kept too ("[AnimMem] summary:",
"[LoadXml] summary", "[XmlMerge] summary"). A value runs to the next
space-led key= outside brackets, and a line without the log prefix takes
the timestamp of the entry above it. [MissionPerf], [TickProfile],
[Hitch] and [PerfContext] count only where a line's tag sits, so a
message that merely names one of them is not that tag's line, and the
profilers' status lines read as prose.

Hitch counts: the tick profiler writes only the first 100 [Hitch] lines
of a mission but counts every slow frame in [TickSummary]'s hitches=, so
a row's count comes from its summaries, and [Hitch] lines with no
readable summary after them count as themselves. When the two differ the
report says what the phase breakdown covers, for example "137 (100
parsed [Hitch] lines: ...)". A [TickSummary] without a usable hitches=
counts as unparsed.

Game boundaries: a game's initialization lines (plan 040's [LoadPhase]
and [LoadXml], plan 042's [XmlMerge]) go on the log header, not on the
battle before them. A new game starts at the lifecycle trace's "STATE
initialized: InitialState" or "GameLoadingState" line, at a saved game's
"[SaveLoad] ... phase=LoadRequested" line, or at one of those
initialization lines, whichever comes first while a mission is open; the
campaign map after a battle counts toward the battle until then. A load
cancelled at the module-mismatch question still ends the row at its
request. A log with none of these lines has no boundary, and a mission
keeps every line after its start (the lifecycle trace first shipped in
v2.0.29, and the 1.4.5 line has none).

The [MissionPerf] line is pinned by the same literal in the C# and
Python tests, and the heartbeat doc now says the first line lands at
about +6 s in battles (+7 s in a tournament), not +5 s.

Limits to know when reading the numbers: lift any external frame cap
before an fps A/B, or every row carries FRAME_CAP. The tests hold a copy
of every tick profiler and hitch probe status line; a change to those
texts needs the copy changed with it.

Refs: #711 (plan 029)
Not-tested: against a real measurement session log from a build with
 plans 028, 036, 040, 041 and 042 merged, including a saved game loaded
 from the campaign map and a cancelled load.

#### feat(load-stamps): v2.0.32 - time module XML, patch groups and handlers

`346f3e09`

Most of a new campaign's 50 s load had no line in taom_debug.log saying
where the time went: about 28 s of module XML merging, and two stretches
of 3.4 s and 3.2 s with no line at all. These stamps time the load per
XML type, per patch group and per campaign handler.

Always on, for every player:
- one [LoadXml] line per MBObjectManager.LoadXML call, one per XML type
  (NPCCharacters, Items and the rest), with its files, XSLTs and time
  split into the merge and object creation, and a "[LoadXml] summary"
  per game initialization (the slowest type, calls that threw);
- a "[PatchApply] phase=... scope=total" line at the end of each of the
  four patch apply phases: patch groups, failures, summed time and the
  slowest group (timing a group costs well under a microsecond);
- one "[Lifecycle] dispatch=..." line for each campaign dispatch of a
  new game, a loaded save and the session start, saying what that
  fan-out took;
- ready lines at process start ([LoadStamps], [LoadXml], [Lifecycle]).

With "Enable Load-Time Stamps" on (MCM, Battle Load Diagnostics page,
default off):
- one [PatchApply] line per patch group, decided once per process at the
  first game initialization, so these need a restart after the toggle is
  turned on;
- [LoadPhase] lines splitting TAOM's OnGameStart and
  OnGameInitializationFinished hooks;
- [Lifecycle] lines for every campaign handler of a new game, a loaded
  save and the session start, TAOM's, other mods' and the game's own,
  each naming the assembly of the method it runs: one line per handler
  taking 10 ms or more, and a total per event split into TAOM's handlers
  and everyone else's. Handlers run in the same order with the same
  arguments; an exception from one carries one extra TAOM frame in its
  stack.

The [LoadPhase] and [Lifecycle] detail reads the toggle live, and a
[LoadStamps] detail line says when its state is first read or changes. A
fault in the stamps themselves writes one warning and the load goes on.
The three total lines carry scope=total as a key=value pair, so plan
029's log reader parses them.

The two XML patches and the five CampaignEventDispatcher patches apply
for every player, toggle on or off, and only observe. PatchShield no
longer wraps those seven methods, an exclusion maintainer decision D13
keeps, so a missing-API exception there behaves as in the unpatched game
instead of being swallowed into a silently empty XML type or a half-run
dispatch. The trade-off: another mod's patch on them loses PatchShield's
rescue. A crash through an XML load or one of those dispatches now shows
a TAOM-patched frame in the crash report; the exception is still the
engine's or the handler's.

Not timed: the rest of Campaign.OnSessionStart after the dispatcher,
Campaign.OnGameLoaded's AfterLoad calls, a loaded save's last fan-out
(OnGameLoadFinished), and any listener added during a dispatch.

Refs: #722 (plan 040)
Not-tested: in game. A new campaign with the toggle off (ready lines,
 [LoadXml] per type and summary, [PatchApply] totals, a [Lifecycle]
 dispatch line per dispatch); then, after a restart with it on, a new
 campaign and a save load (per-group [PatchApply], [LoadPhase] and
 [Lifecycle] handler lines); a custom battle (no [Lifecycle] lines);
 diag.log's shield pass count not grown by the seven methods. With plan
 042 merged, [LoadXml] lines that all read merge_ms=none would mean the
 merge finalizer is not firing.

#### feat(mission-perf): v2.0.32 - log clip memory against the 12 MiB budget

`a9f10688`

The engine loads animation clips on demand and holds their data against
a 12 MiB budget: once a load takes the total past 15 MiB, it schedules a
pass that frees idle clips toward the budget. A worker that needs a clip
that is not loaded waits for it, a suspect for battle frame spikes. This
probe shows whether TAOM battles put that budget under pressure, which
is the evidence for raising it.

"Enable Animation Clip Memory Probe" (MCM, Battle Load Diagnostics page,
Mission Performance group) is on by default and is read at each mission
start, so it needs no restart. Once per game process, under the first
mission's loading screen, it finds the engine's clip byte counter and
budget by a signature check, a scan of about 10 MB of the engine's code.
It only reads: if the signature, the section checks or the 12582912-byte
budget do not match, it turns itself off and says why. It is left out of
the co-op settings fingerprint, like the frame-time heartbeat.

In each mission it reads the counter once a second and writes to
taom_debug.log:
- "[AnimMem] mission start", then a line every 5 s: loaded KB, percent
  of the budget, whether a clip is loading, drops, the window's minimum
  and maximum, and the samples that saw a clip loading;
- at mission end, a line for the samples since the last 5 s line and an
  "[AnimMem] summary:" line (peak, samples at or above 90 percent,
  drops, loading samples), also when a mission is torn down without a
  normal end;
- "[AnimMem] armed", "disabled", "off" or "stopped" lines for each state
  change.

Reading it: values between 100 and 125 percent of the budget are normal,
since eviction starts only past 15 MiB, and a pass can leave the total
above 12 MiB while clips are in use or loading. A drop means an eviction
pass ran in that second, not how many clips it freed. A low total with
no drops shows no budget pressure and nothing more. It does not rule out
making TAOM's busiest clips resident: a clip's first load blocks its
worker whatever the total, and the probe samples after that wait has
ended, so that decision needs hitch timing (plans 028 and 041). The
engine reference's clip section now describes the eviction pass as the
binary runs it.

The cost to every player while it is on: one counter read and one native
walk over the clip records each second, and one INFO line every 5 s,
each flushed on the main thread; what the walk and the flush cost in a
battle has not been measured. The probe reads the engine module's header
page and code on trust, and the engine's loading query walks a native
list without a lock. MCM keeps a player's saved value, so changing the
default later means renaming the setting; whether it stays on for a
public release is still open.

Refs: #718 (plan 036)
Not-tested: in game. One Custom Battle: the armed line, a 5 s line, the
 mission-end line and the summary in taom_debug.log; then one
 troll-heavy and one large vanilla-troop battle, read next to
 [MissionPerf]. The probe has not run against a live game process, so
 the per-second cost of the loading query and the flush is unmeasured.

#### feat(mission-perf): v2.0.32 - hitch probe; spawn, script, clip timing

`daba6f0a`

A hitch probe now runs for every player by default, so a slow battle
frame in any player's log says where its time went. "Enable Hitch Probe"
(MCM, Battle Load Diagnostics page, Mission Performance group) is on by
default. It is installed once at game start, so turning it on needs a
restart; turning it off stops measuring from the next mission while
"Enable Tick Profiler" is off (the profiler measures through the probe's
patches).

Each frame slower than the hitch threshold (default 250 ms) writes a
[Hitch] line and right after it a [HitchDetail] line: the wait for the
agent tick, the mission tick, agent spawning, scene scripts, and whether
an animation clip was loading from disk. Every 5 s window adds
[TickProfile], [ScriptProfile], [AnimLoad] and, while agents spawn,
[SpawnProfile] lines; each mission ends with [TickSummary] and
[TickSummaryExtra]. Full [Hitch] lines stop after the first 100 of a
mission; later ones are counted. So every player's log gains two to four
INFO lines every 5 s of a mission, each written and flushed on the main
thread.

The probe brackets five whole engine methods (Mission.OnPreTick,
Mission.WaitTickCompletion, Mission.OnTick,
ManagedScriptHolder.TickComponents and Mission.SpawnAgent) with a prefix
and a finalizer, and no transpiler. An offline benchmark puts the
brackets at about 0.5 microseconds per simulated frame, against a target
of 0.5 percent of a 10 ms frame; the install line logs a separate
start-up measurement of the profiler's bookkeeping alone. The clip
loading check calls the engine once a frame. The first measured mission
logs the median of 32 calls, and the per-frame check runs for the rest
of the process only when that median is within 20 microseconds; a later
slow call does not turn it off. Its per-frame cost in a battle is
unmeasured, and the safety of the engine's unlocked walk of its clip
list while clips load rests on a reading of the engine binary, not yet
on a battle.

Per-type attribution (which behaviour, spawn callback or scene script
component) stays behind "Enable Tick Profiler", off by default, because
it needs two more transpilers. The frame boundary moved from the tick
profiler's patch to the probe's, so both share it. The "[TickProfiler]
off:" line now ends "no per-behaviour transpilers installed", and the
"probe install:" or "probe off:" line after it says whether the probe is
in.

PatchShield: Mission.WaitTickCompletion and
ManagedScriptHolder.TickComponents join its exclusion list for every
player. A swallowed exception in the wait would skip its loop and let
the next pre-tick overlap the running agent tick; TickComponents, called
once per ticking scene per frame on a thread native picks, stays
excluded as built. For any patch on those two, the missing-API swallow
and the strip of the offending patch are given up. Mission.OnTick,
Mission.OnPreTick and Mission.SpawnAgent keep the shield (maintainer
decision D13). A rescue there strips every unprotected owner's patches
on the method, TAOM's own included, so on SpawnAgent it also removes
Patch23's banner colours (armour tint falls back to vanilla's for the
process) and the probe's own prefix. Each probe call carries its own
state from prefix to finalizer, so a finalizer whose prefix was stripped
records nothing; the first one per method in a process writes one
warning, and each measured mission's end counts them.

Two earlier plans follow the frame boundary's move in this commit, so
the tree builds: plan 028's hook health check now requires the
probe's Mission.OnPreTick prefix as the frame boundary (its warning
names it "(Patch98)"), and plan 039's campaign tick swap test reads
the swap's helper list, which replaced its single helper.

Older than the probe: an exception that escapes Mission.OnTick after it
clears the agent tick's completion flag, and is swallowed above it,
leaves the next frame waiting forever, and neither exclusion stops it.
This branch does not fix it; the PatchShield follow-up plan recorded
under decision D13 takes it, with its regression tests.

Refs: #723 (plan 041)
Not-tested: in game. With default settings, one Custom Battle with
 elephants or mumakil and one campaign field battle: the probe install
 line, the anim-loading sample line, the 5 s lines, a [HitchDetail]
 after every [Hitch], the mission-end lines, and no
 "WaitTickCompletion's bracket did not run" line; then with the tick
 profiler on, and with both off; [MissionPerf] avgMs with the probe on
 against both off, within noise; the TickComponents thread line in a
 real log.

#### feat(map-perf): v2.0.32 - campaign map frame profiler, off by default

`f6bf0e58`

A new campaign ran at 7 to 8 fps in fast-forward while lords and
villagers spawned in, and the [MapLoad] heartbeat could only say that
Campaign.RealTick took about 20 ms of each frame's 130 ms. The new map
profiler splits the rest of the frame and says how much of it is TAOM's
own code.

It is off by default and installs nothing unless "Enable Map Profiler"
(MCM, Battle Load Diagnostics page, Map Performance group) is on at game
start. Turning it on needs a restart; turning it off stops measuring
from the next campaign session. With it off, every player gets one
"[MapProfiler] off:" line per game process.

With it on, taom_debug.log gets:
- [MapProfile] every 5 s while the map ticks: frames, wall time, the
  campaign's real tick, the map screen, the map state, campaign tick,
  tick event and application tick phases, everything else (otherMs),
  TAOM's own share (taomMs), main-thread allocation, the speed class,
  the party count, the worst frame, skipped frames, GC counts, and the
  slowest tick-event listeners and TAOM map views;
- [MapProfileSummary] when the campaign session ends, with the same
  totals, the skip reasons and the frames per speed class;
- [MapProfiler] status lines for the install, each session's settings,
  each degraded part and any fault. A fault stops the profiler for that
  campaign and writes the frames already measured as a summary with
  reason=fault.

Frames during a loading window, under another screen, or not followed by
exactly one TAOM application tick are counted as skipped, never mixed
in. The speed class (Stop, Play, FF, FF2, FF3) follows the engine's
simplified time mode, so a frame in which the waiting main party gets no
campaign time reads Stop. The "Tick Profiler Top Behaviours" setting
also sets how many listeners and views each line lists.

The profiler checks its own hooks at each session start, each window and
each session end. If a PatchShield rescue stripped one of its patches,
it writes one warning naming every lost hook, writes the frames closed
so far as a summary with reason=hooksLost, and stops measuring that
session. A strip on MapState.OnTick is found only at the session end.
The check runs after the frame boundary's timestamp is taken, so its
cost, not yet measured, lands in the next measured frame.

PatchShield: Campaign.Tick and CampaignEvents.Tick, which only the
profiler patches, are now on its exclusion list for every player, so
another mod's patch on either loses the shield too, profiler on or off.
When either carries a patch, a missing-API exception thrown inside it
(by a patch or by any mod's campaign tick listener) is no longer
swallowed at that method: it skips the rest of the campaign tick and of
MapState.OnTick, whose shield catches it from a process's second game
start; in a first game it unwinds the application tick to the crash
capture, every frame it recurs. Campaign.RealTick, MapState.OnTick and
MapScreen.OnFrameTick, which other TAOM patches use for every player,
keep the shield (maintainer decision D13, as plan 028 keeps it on
Mission.OnTick and Mission.OnPreTick), so a player with the profiler off
sees no change there. A rescue on one of those three strips every
unprotected owner's patches on that method, TAOM's own included, so it
also ends the profiler's measurement there for the process; when no
patch threw, the cause is swallowed again each frame it recurs and the
rest of the method is skipped each time. The older Mission.OnTick hang
that plan 028 describes is not changed here.

Refs: #721 (plan 039)
Not-tested: in game. With the map profiler on: the install line at game
 start, a session line, [MapProfile] lines with frames=0 and a growing
 skipped count during the map load, then a line every 5 s with
 speed=Stop when paused and speed=FF in fast-forward, and one
 "[MapProfileSummary] reason=gameEnd" at quit; with it off, one off line
 and the [MapLoad] fps compared with it on. A real PatchShield strip and
 the hooksLost warning have not run in a game.

#### feat(mission-perf): v2.0.32 - per-behaviour tick profiler and hitch log

`6eb2e1b9`

A new mission tick profiler says where a battle's frame time goes. It
times each mission behaviour's tick by type (OnPreDisplayMissionTick,
OnMissionTick and OnPreMissionTick), the main thread's wait for the
previous frame's agent tick, the agent tick itself, and everything else
(otherMs: the native tick, views, rendering and streaming), with the
main thread's allocation alongside.

It is off by default. To use it, turn on "Enable Tick Profiler" (MCM,
Battle Load Diagnostics page, Mission Performance group) and restart the
game: the profiler is installed once, at game start. Turning it off
stops measuring from the next mission; its patches stay until a restart
and only call through. Two more settings, read at each mission start,
set how many behaviours each line lists ("Tick Profiler Top Behaviours",
default 8) and the hitch threshold ("Hitch Threshold (ms)", default
250).

With it on, taom_debug.log gets:
- a [TickProfile] line every 5 s on the same clock as [MissionPerf],
  with the window's totals and its slowest behaviours (ms, calls, max
  ms, KB);
- a [Hitch] line for each frame at or above the threshold, naming the
  frame's phases, GC counts, allocation and three slowest behaviours,
  for the first 100 such frames of a mission; later ones are only
  counted, so a battle that stays slow does not flush a line every
  frame;
- a [TickSummary] line at mission end covering the whole mission, every
  hitch counted;
- [TickProfiler] status lines: the install result, a header for each
  measured mission, a warning for a hand-edited setting out of range,
  and one reason line whenever anything is skipped, falls back or stops.

The profiler checks its own hooks. Another mod's transpiler or a
PatchShield rescue can remove one after the install, so each mission
start asks Harmony whether the profiler's patches are still in place; if
one is missing, that mission does not measure and one warning names
everything missing. A mission already measuring stops with one warning
when a later patch takes out its call sites. A strip in the middle of a
mission is found at the next mission start.

Every player, profiler on or off, gets one [TickProfiler] line per game
process and one [PerfContext] line per mission (build flavour, runtime
and GC settings, scene, agent count, quality options, memory headroom,
and which diagnostics will run). With the profiler off, its whole cost
is those lines, one MissionTickProfilerBehavior added to every mission
(set up once per mission; after writing [PerfContext], its tick returns
at once each frame) and two static null checks per agent tick.

PatchShield: Mission.TickAgentsAndTeamsImp is now on its exclusion list
for every player. A missing-API exception swallowed there would skip the
line that marks the agent tick complete, and the next frame would wait
forever; excluded, the exception leaves the method instead. For any
patch on that method, the swallow and the strip of the offending patch
are given up. Mission.OnTick and Mission.OnPreTick keep the shield
(maintainer decision D13): the first build excluded them for cost, but
with plan 034's cheaper finalizer two calls a frame cost little, while
without the shield a broken foreign patch on Mission.OnTick unwinds the
whole application tick in a process's first game and can keep a finished
battle from closing. diag.log now names each patched method the shield
skips by name, once per process.

Older than the profiler: Mission.OnTick clears the agent tick's
completion flag before its behaviour loop, and only the agent tick sets
it again, so an exception that escapes in between and is swallowed above
it (by PatchShield or by the crash capture) leaves the next frame
waiting forever, whichever way the exclusion list reads. This branch
does not fix it; the PatchShield follow-up plan recorded under decision
D13 takes it, with its regression tests. That plan also makes a rescue
strip only the patch that threw. Until then a PatchShield rescue on
Mission.OnTick or Mission.OnPreTick also strips TAOM's own patches
there, the profiler's included, because TAOM's Harmony owner is not a
protected one.

Refs: #710 (plan 028)
Not-tested: in game. One Custom Battle and one campaign field battle
 with the profiler on: the install line, [PerfContext], a [TickProfile]
 line every 5 s beside [MissionPerf], [Hitch] on slow frames and
 [TickSummary] at the end; then the same battle with it off,
 [MissionPerf] avgMs within run-to-run noise. A PatchShield rescue on
 either tick method and the hook-check warnings have not run in a game.

#### feat(faction-ui): v2.0.32 - play as the lord you pick (#704)

`21b01894`

Picking a lord or lady of the chosen faction on the faction screen now
makes you that character. Character creation goes from the faction
screen straight to the career choice and then into the campaign, in
the hero's own clan and kingdom, with their gear, skills and fiefs.
Player Switcher performs the takeover, so its settings apply: with it
switched off, or for Sauron and the Nine without "Allow Sauron and the
Nazgul", the pick is copied as before. Legends, wanderers, troops,
Aragorn and Gimli (no clan), and Haldir and Bolg (another faction's
people) are still copied. Taking over a ruler's child makes them the
clan's leader, and so the ruler, as Player Switcher already does.

This is also the release note for the faction screen itself, which went
out in 0bd6abf3 without one: Kysaro's themed main menu, menu video and
music, splash, loading screens, skill icons, character-creation screens
and his faction and hero picker are now part of TAOM, with their images
loaded only while their screens are open.

Known limitations: the career menu's button still reads "Next"; a
takeover cannot turn on Iron Man (or, with Birth and Aging Options,
turn off the life and death cycle); a taken-over lord starts with 1,000
gold plus his culture's starting gold, as Player Switcher's own
takeovers do.

Reviewed by a six-lens deep review and a convergence pass, RCA
docs/reviews/rca-faction-ui-takeover-2026-10-01.md. Tested in game by
Mike: Boromir went from the faction screen to the career page and into
the game.

#### feat(realm-borders): v2.0.32 - parchment map at full zoom-out

`eed96b38`

As you zoom all the way out on the campaign map, a parchment map of
Middle-earth now fades in, with the realm borders, colours and names
drawn on it. The picture is traced from TAOM_Map's own terrain, so the
borders follow its drawn coasts and rivers. MCM > Realm Borders >
"Parchment Map at Full Zoom-Out" turns it off; it also hides whenever
the borders do (#698).

Border changes that come with it:
- Every Border Material choice now draws over the parchment map.
- The borders and land tint no longer draw twice on the default
  material, so Realm Colour Strength now defaults to 0.5 to keep the
  same look.
- New colours for Rohan, Harad, Gundabad, Rivendell and Mirkwood.

Console: taom.print_realm_atlas reports the parchment map's state;
taom.realm_atlas_rebuild, taom.realm_atlas_tint and
taom.realm_atlas_fade redraw it or tune it until the game restarts.

Reviewed by a six-lens deep review, a convergence pass and a Codex
adversarial pass (0 findings); record in
docs/reviews/rca-realm-borders-parchment-2026-10-01.md. The two-layer
look and its build time are not yet checked in game; the checklist is
in docs/features/realm-borders.md.

#### feat(realm-borders): v2.0.32 - Your Realm colour field in MCM

`6b00881b`

Adds a "Your Realm" field under MCM > Realm Borders > Realm Colours.
It colours your realm on the campaign map when it is none of the
kingdoms listed above it: your clan's land while it serves no kingdom,
then a kingdom you found. The colour follows you from the one to the
other, and a kingdom you leave behind takes a free colour again. Blank
keeps today's behaviour, a free colour picked to stand apart from the
other realms; setting the field hands that free colour back for a
rebel realm to use. In one of the listed kingdoms, such as Gondor, that
kingdom's own field applies. A second campaign or a loaded save in the
same session keeps the colour.

RCA: docs/reviews/rca-realm-borders-2026-09-30.md, "Your Realm colour
review".

Refs #698.

#### feat(realm-borders): v2.0.32 - kingdom borders on the campaign map

`425a0573`

Every kingdom's border is now drawn on the campaign map and moves with
the war: a captured castle, a clan changing sides or a rebellion
redraws it within a frame or two. Each town and castle owns a province
grown over the map's own terrain, so borders follow rivers, coasts and
mountain ranges, and the wild land between realms stays unclaimed.

The default Atlas look is a watercolour wash inside each realm with a
dash-dot ink line; your own realm's frontier is a gold cord, and
heraldic bands are an option. The G key switches the map between the
realms, the Free Peoples against the Shadow, and your allies and
enemies in the settlement nameplates' colours; M hides and shows the
borders (both rebindable under Keybindings > Campaign Map). Realm names
are lettered across their lands in TAOM's Tolkien font where it has the
letters, and a message tells you when your party rides into another
realm. Everything is in MCM under "Realm Borders".

Nothing is saved, so existing campaigns get the borders too. They do
not run on a dedicated server, and they stand aside when the Kingdom
Borders mod is loaded. The repaint runs on a worker thread: measured
offline at about 37 ms per capture, it would otherwise stall the map.
Not yet run in game: the checklist is in the feature doc,
docs/features/realm-borders.md. The eight new strings are drafted in all
twelve languages.

Review and RCA: docs/reviews/rca-realm-borders-2026-09-30.md. Refs #698.

#### feat(custom-battles): v2.0.32 - Edoras siege in the battle picker

`d96167ef`

Mike's Edoras siege scene (taom_rohan_edoras_town_forceatmo) joins the
Custom Battle picker as "[Rohan] Edoras".

The scene lives only in the unversioned TAOM_Map, created on
2026-09-29, so a new LiveInstall gate checks the whole picker:
CustomBattleSceneLiveDataTests fails when a row names a scene no
installed module ships, or when a siege row's scene has no siege level
(the level a Custom Battle siege loads). All 27 rows pass on the
desktop; without the install the test is Inconclusive.

Ship the scene with the release that ships this row. Owed in game: a
Custom Battle siege on Edoras, and a town visit, because the scene
spells its civilian level "civillian" (1,155 entities) while town
visits ask for "civilian". The name key is registered nowhere, like
every other picker scene name, so it shows in English everywhere.

#### feat(culture-conversion): v2.0.32 - no creature rider replacements

`1d8fc1d8`

The goblin tree's new mountain spider riders (#696) are the first
Cavalry troops of Goblin-town, the Misty Mountain Orcs and Blue Craig,
and two consumers that pick troops by role picked them up at once.
Mike decided both during the review.

- Culture conversion: a captured cavalryman in a garrison converting to
  a goblin culture became a spider rider. Now a rider of a mount-locked
  creature (the giant spider, the war elephant or the Mumakil, the same
  three CanAgentRideMount refuses) never replaces a garrison or militia
  stack, so that cavalryman keeps his tier as infantry, as before. The
  rule lives in CreatureMountRiders; the adapter flags each candidate
  from its battle mounts, and CultureTroopIndex keeps flagged troops
  out of its replacement cells but in its id set, so a garrison that
  already holds a culture's own spider riders keeps them. War rams and
  elk stay ordinary cavalry.
- Behaviour change beyond the goblins: Dol Guldur's converted garrisons
  could pick its spider riders since the swap landed (2026-09-21), and
  now they cannot. Troops already in a garrison can still upgrade into
  spider riders.
- Enlistment: generate_enlistment_rosters.py would have built goblin
  "cavalry" kits from spider-rider gear, on foot. It now drops riders of
  the mount-locked creatures as donors (drop_creature_riders, keyed on
  the mount's Monster), so goblins still get no cavalry kit. The Harad
  rosters do not change: none of their donors rode an elephant.

Tests: CreatureMountRidersTests (the three Monsters, and the real
war ram, elk, moose and warg Monsters staying eligible);
TroopCultureMapperTests (a creature rider is never picked, a garrison's
own is kept); GarrisonCultureCoverageTests (no conversion target offers
one, and the goblin cultures still recognise theirs);
tools/tests/test_generate_enlistment_rosters.py.

Review: docs/reviews/rca-keyforce-art-wiring-2026-09-29.md

#### feat(creatures): v2.0.32 - KEYforce's elephant and spider art (#696)

`98ca32e0`

KEYforce delivered new art for the Harad war elephant and the giant
spiders on 2026-09-29, and it is now wired in and verified in game.

War elephant
- The body is KEYforce's sk_elephant_basemesh_a, replacing ADOD_Beasts's
  elephant_mesh. adod_elephant_geo.tpac stays: it still carries the
  elephant_skeleton, its hit capsules and every clip.
- Six HorseHarness items, all family_type 10. Three plain armours:
  sk_elephant_armor_a (the same id, now the medium mesh, armour 60),
  sk_elephant_armor_heavy (70) and sk_elephant_armor_elite (80). Three
  howdahs: sk_elephant_armor_howdah_med (60), _heavy (70) and the
  existing _elite (80).
- The three howdahs share one deck placement, so all three get the
  howdah platform and its two archers (ElephantConfig
  .HowdahHarnessStringIds, HowdahHarness.GetsPlatform). The plain
  armours get neither. The old plain armour's crewless platform is gone.
- The Harad Elephant Rider has three rosters, one per howdah, with the
  elite one first so the troop card keeps its look. Battles mix all
  three.

Spiders
- KEYforce's whole meshes (sk_spiders_a_geo.tpac) replace the L/R split
  halves, which the June crash had made necessary. They loaded and
  rendered on the first try, so the split is retired; its tpacs stay on
  disk as the rollback until a release ships.
- The Giant Spider rides forest_a1. The Brown Spider is now the "Great
  Spider" (same id, forest_a1 at 1.10x, the brown skin is gone). The
  Pale Spider rides forest_a2. The Dol Guldur riders and the Mirkwood
  broods keep their three tiers.
- New Mountain Spider (1.0x) and Great Mountain Spider (1.25x) for two
  new goblin-tree troops, the [Goblin] Spider Rider (level 21) and the
  [Goblin] Spider Lord (level 31), a second upgrade off the Lurker. One
  tree serves Goblin-town, the Misty Mountain Orcs and Blue Craig, so
  all three field them. Their skills and weight (4.0) follow the Dol
  Guldur riders; rebalance_troops.py now skips them so a rebaseline
  keeps that parity.
- The rider and brood names changed from Brown to Great Spider.

All new and renamed item names are translated by hand in the 12
languages, in the language files and the translator cache alike.

Live LOTRLOME_Armory edits (unversioned): LOTRAOM_horses.xml and the
13 loc_LOTRAOM_horses.xml files; the howdah prefab's comment. The
previous copies are in
E:\Bannerlord_Backups\module_bak_sweep_2026-09-29.
Package the Armory in the same release as this build: the goblin
riders and the elephant rider name items that exist only there.

Tests: HowdahHarnessTests now pins the three-howdah rule (the plain
armour rows turned false and the crew tests went with CarriesCrew);
HowdahHarnessItemTests pins all six harnesses and the body mesh, and
its repo-only rider test now runs on CI; SpiderMountItemTests pins the
five spider items. Verified in game 2026-09-29 in Custom Battle: all
five spider mounts, the goblin riders, and 91 crewed howdahs. Owed in
game: the #624 hit test on the new body, a slide measurement, and the
campaign upgrade chain (listed on #696).

Review: docs/reviews/rca-keyforce-art-wiring-2026-09-29.md

#### feat(tooling): v2.0.32 - /new-map-prop and a mesh name check

`7bbe6f4d`

Put MithrilForge into the workflow for custom static props built
without the Modding Kit.

- New skill /new-map-prop, listed in CLAUDE.md: a thin procedure over
  docs/reference/tpac-static-prop-authoring.md. It stops until
  yotthani's TpacTool fork has been read, and it makes the delivery
  path the first decision and the last proof (the client log's
  "Loading packages" line, the release folder), because the camp props
  built this way were never loaded by the game.
- tools/validate_mesh_refs.py --check-name <name>: is a new mesh name
  free in every module's loose Assets and AssetPackages alike, case
  ignored? Exit 0 free, 1 taken with the holding packages listed, 2
  when an unreadable package leaves "free" unproven. MithrilForge's own
  check reads Native's packages only. About 2 s on the full install.

#### feat(creature-bandits): v2.0.32 - wild trolls: hill trolls, no armour

`59f68511`

The Wild Troll bands wear no armour and are hill trolls only for now,
after Mike's first in-game test: bands are two to four hill trolls,
replacing the earlier "same race, stats and gear" twins. A bare cave
troll's body has no cloth (its trousers belong to the armour mesh), so
the cave twin stays defined, armourless and in no band.

In place of armour a bandit troll is tougher: 300 hit points (the
race's 200 plus 100) and it takes 70% of every hit. Both are keyed to
the bandit trolls only; Mordor's own trolls are unchanged. A troll
spawned by console in Custom Battle has 200 hit points there, since
Custom Battle reads health from the race.

The band's map icon is now a hill troll. Reviewed (XML, data flow,
completeness, and the C# on the per-hit path); follow-up in
docs/reviews/rca-troll-bandits-2026-09-28.md. Full suite 11341 passed,
2 skipped; one failure is trunk's NoTranslatedString_MixesWritingSystems
on the #693 strings, not this change. Refs #694.

#### feat(creature-bandits): v2.0.32 - taom.spawn_creature_band command

`57f2b4d3`

A dev-console command to meet a Wild Troll band or a spider brood on the
campaign map without waiting for the daily spawners (cheat mode):
"taom.spawn_creature_band trolls|broods [confirm]". Without "confirm"
it is a dry run naming the settlement the band would be homed on and,
for trolls, the kingdom it counts for. With it, one band spawns within
a quarter of your sight, so it always shows on the map.

The band is saved with the campaign and only killing it removes it. It
shares the spawners' party step but none of their daily rules: it
ignores the MCM spawn switches and the cap, still takes a cap slot, and
is homed on your nearest town, castle or village. Refused before your
party is on the map, as a prisoner, in a battle, or in a siege on
either side; while enlisted it spawns beside your commander's column.

The spawn diagnostic now says origin=console or origin=spawner, so the
out-of-sight checklist step reads only spawner lines. The daily
spawners behave exactly as before.

Deep review (6 lenses + convergence): all findings fixed; follow-up in
docs/reviews/rca-troll-bandits-2026-09-28.md. Full suite 11023 passed,
2 skipped. Refs #694.

#### feat: v2.0.32 - ten more named weapons; each lord's material its bar

`40f58d14`

Tuor's two heirloom axes, Galadriel's sword and the seven Noldor
swords of Fingon, Finarfin, Finwë, Ingwë, Túrin, Voronwë and Celegorm
join the named weapons: never sold, never looted. Rivendell's weapon
rung now offers the seven swords and Tuor's axes, and Lórien's offers
Galadriel's sword. Twenty-seven hero weapons and shields are named.

Each of the thirteen lord's materials now shows its own bar in the
inventory instead of the vanilla thamaskene ingot: vanilla's bar in
the culture's metal colour (the meshes and materials live in the
LOTRLOME_Armory).

#### feat(tools): v2.0.32 - the item registry reads only Items documents

`1ebcf86a`

validate_moduledata.py counted every <Item id> row in any XML file as
an item definition, so the armour class table and the armour
acquisition config defined their own ids: a named weapon, a ladder
weapon pick or a retired armour piece that no longer existed passed
every check. The registry now reads only documents with an <Items>
root, as the engine does. The armour acquisition gate also checks the
ladder's weapon picks, lord's materials and rung quests, reading the
quest file the way the game does, and a file with a bad encoding
declaration is reported as one that does not parse instead of
crashing the run.

#### feat: v2.0.32 - the lord's gear ladder earns lord kit by slot (#693)

`046123a7`

Lord kit can now be earned piece by piece. A town armoury lists six
rungs, climbed in order: hands, legs, shoulders, head, body, weapon.
Each rung opens a quest done by the hero's own deeds (enemies struck
down by their own hand, battles won and, on the higher rungs, enemy
lords taken captive), or the rung is done at once by handing the
armourer the culture's lord's materials. A done rung is claimed at an
armoury of the lord level: the culture's lord piece for that slot,
else its best elite piece there. The weapon rung awards the culture's
named weapons where it has them (Andúril, Glamdring, Théoden's sword
and their like), else a choice set per culture in the config.

Thirteen lord's materials, one per culture that owns armour, turn up
after battles won, more often the more enemies the hero struck down,
and come from "The Deep Seam", a new village headman's quest per
culture that pays five of them for timber. No workshop makes them and
no battle loot or hideout gives them out. The one-quest Lord's
Harness, which never shipped, is gone.

Career quests gain a HeroKills objective, and LotrIssues rows can pay
several items (reward_count) and be offered only to a player of their
cultures (for_player_culture); such an offer is withdrawn once the
player's culture no longer matches.

Numbers, names and weapon picks are placeholders to tune in play, and
the materials wear the vanilla thamaskene ingot until their art is
imported.

Save-compat: new keys only (per hero, a mask of claimed rungs and one
of done rungs); no shipped save holds the retired Harness quest.
Not-tested: the kill counter, the drops and the claim need the in-game
checklist in docs/features/armour-acquisition.md.
Research: Mission.OnAgentRemoved, PlayerEncounter, IssueManager,
HideoutCampaignBehavior (v1.5.3)

#### feat(tools): v2.0.32 - armour class generator and acquisition ref gate

`04da4a0d`

tools/generate_armour_classes.py classes every character armour piece
in the live Armory (named hero kit, lord, civilian, the roster tier,
else the band nearest its kingdom-cap target) and names the piece the
armoury upgrades it into, inside the piece's own kit line. --check
reports a stale table after an art drop; the validator raises it as
ARMOUR_CLASS_TABLE_DRIFT (a warning) and says NOT verified when it
cannot read the Armory the generator reads.

A new ERROR gate, ARMOUR_ACQUISITION_REF, resolves every id the armour
acquisition configs name (LotrIssue cultures and reward items, named
weapons, upgrade metals, marketplace cultures and armour donors)
against the registry plus the items the engine registers in code, and
the commit hook enforces it. audit_deleted_mesh_impact.py now reads
reward_item and the armour config, so a mesh removal names them too.

#### feat: v2.0.32 - gated armour acquisition and the town armoury

`301cd338`

Heavy, elite and lord armour is no longer on every stall. Markets and
battle loot still hand out light and medium armour freely, but a town
sells heavy pieces only at armoury (Barracks) level 1 or more, elite at
2 and lord kit at 3. Workshops, battle loot, the tournament prize pool,
plunder and hideout loot skip the gated pieces, and a daily sweep takes
out of each market what its armoury does not allow that day. The
seventeen named hero weapons and shields, Anduril and Glamdring among
them, are never sold, looted or awarded.

"Visit the armoury" in every town menu upgrades a carried piece into
the next class of its kingdom's kit line for gold and metals; lord kit
also costs the kingdom's special resource. Lord kit is bought or forged
at a level 3 armoury, earned through the quest "The Lord's Harness"
(capture two enemy lords, reach 900 clan renown and win 12 battles, in
any order), or found after a battle won against lords. A master
armourer sometimes visits a town for a week and raises its armoury a
level, and an artisan's "Armourer's Commission" trades a heavy chest of
the town's people for steel.

Nine cultures with no armour of their own, Lindon and Lothlorien among
them, draw on a related culture's armour for their markets and their
lord kit. The Animalia moose is now guaranteed Mirkwood stock, like the
elk.

The gate itself writes nothing to the save: it is reapplied at every
campaign load, so its MCM switch takes effect from the next load. Each
piece's class comes from the generated table armour_classes.xml.

Deep-reviewed (ten lenses, then a convergence pass); the RCA is
docs/reviews/rca-armour-acquisition-2026-09-27.md.

Save-compat: adds one flat string dictionary (the harness quest,
visiting armourers, event cooldowns); an older save loads it empty.
Not-tested: in game. The checklist is the feature doc's Owed section.

#### feat(resources): v2.0.32 - affordability-checked special resource spend

`caf52758`

Adds ISpecialResourceSpender, a narrow spend of a hero's special
resource for callers outside the troop economy. GetBalance resolves the
hero's resource by kingdom, then culture; TrySpend debits only when the
balance covers the amount, and refuses a non-positive or non-finite one.

SpecialResourceService implements it and the IoC registers the same
singleton behind the new interface. The armour acquisition armoury uses
it to charge the kingdom's resource for lord kit. The troop-keyed spend
and the debug GrantAmount are unchanged.

#### feat(creature-bandits): v2.0.32 - wild troll bands, twenty broods

`b9fdaaf7`

Spider broods now reach twenty (one new brood a day) and spread over
every town, castle and village of Mirkwood and Dol Guldur, 47
settlements instead of 13.

Wild Trolls roam as bandits: bands of two to four cave and hill trolls,
about one per kingdom, spawned near that kingdom's settlements, one new
band a day. The trolls are hidden twins of Mordor's two trolls (same
race, stats and gear, their own ids), so they fight with every troll
trait while Mordor's recruitable trolls stay untouched. A troll is never
taken prisoner, meeting a band goes straight to attack or leave, and a
band never takes in freed prisoners, so it stays trolls only. New MCM
switch "Spawn Troll Bands" in the Creature Bandits group, on by
default; the tuning options below it apply to spiders only.

Also for both spiders and trolls: with the Roguery perk Partners in
Crime, telling a bandit party to serve under you no longer recruits a
nearby brood or troll band along with it (the looters still join), and
new broods and bands try for a spawn point outside your party's sight,
as vanilla bandits do.

Troll bands need a new campaign; an existing campaign's broods grow to
twenty over the new anchors. The three new names are English in every
language until the translator runs. Not yet run in game: see the
checklist in docs/features/creature-bandits.md.

Deep review (8 lenses + convergence): all findings fixed, RCA
docs/reviews/rca-troll-bandits-2026-09-28.md. Full suite 11015 passed,
2 skipped. Refs #694.

### Fixes

#### fix(race-abilities): v2.0.32 - second review: kill credit, leaner scans

`70320bbe`

Fixes from the second deep review of the race abilities (#730).

Behaviour:
- A horse is no longer a kill. Mounts carry no team, so a horse
  killed under its rider passed the same-side check and extended
  Bloodlust, healed the killer and spread fear around it.
- A fall keeps its damage under Stand Fast and the other damage
  reductions, as the documentation always said.
- race_abilities.json's own "enabled" switch now closes the mission
  gate; the gate log line and the console report show it beside the
  MCM switch.
- The message line for your side reads "N soldiers on your side": it
  always counted an allied lord's troops too.
- Stand Fast drops a knock-back resistance its shrug-off made
  unreachable, and goblins an unread kin list. Config that nothing
  reads now warns, and a trigger kind must be exactly one name.

Performance:
- Every behaviour tree's per-frame schedule copy no longer allocates:
  List.AddRange built an array the size of the schedule each frame,
  and the race abilities put every profiled soldier on it.
- The sensor gathers only what can change a decision, so a Rohirrim
  on foot or a Dale soldier without a bow no longer scans 30 to 40 m
  every second. A test proves no profile's answer changes.
- The MCM switches are read through a cached settings object, the
  30 s report no longer sums the counters every frame, and the
  battle's buffers are released at mission end.

Code: the tree's decorator, task and blackboard are one node; the
console command is taom.print_race_abilities, under Cheats/; the
mission logic is back under 150 lines.

Known limits, now in docs/features/race-abilities.md: in campaign a
full-speed charge floors any man before resistance is read; scripted
creature blows bypass the defensive effects; and Khand fields Rhun's
troops, so its soldiers fire Rhun's Wainrider Wall while Variag
Ferocity reaches only Khand's lords and town guard (the first
commit's note said otherwise).

Review: docs/reviews/rca-race-abilities-2026-10-04.md.

#### fix(troops): v2.0.32 - Uruk-hai berserker skirt worn as body armour

`569f6750`

The Uruk-hai Champion and Berserker carried the Berserker Skirt in the
cape slot. The skirt is body armour, and the game puts an item in a
slot only when its type fits that slot, so neither troop ever wore it
and its armour never counted. Both now carry it in the body slot,
which they had left empty for the bare-chested look; the skirt leaves
the chest bare.

In battle both troops now get the skirt's armour (body and arms 38,
legs 13: the Champion goes from 76 to 165 armour, the Berserker from
83 to 172) and carry its 20.7 kg, so both are slower. The Berserker
looks the same, because his race already showed this skirt as
underwear; the Champion now shows the skirt in place of the plain
Uruk-hai underwear.

The validator's bare-chested-by-design list keeps both troops, so
their body and cape slots stay out of the armour comparisons. The
tools, docs and ModuleData rule no longer say the skirt sits in the
cape slot. RCA: docs/reviews/rca-uruk-hai-skirt-body-2026-10-04.md.

Refs: #729, #720 (plan 038)
Not-tested: in game; #729 lists the checks (the Champion's skirt fit,
 one skirt on the Berserker, both troops' speed).

#### fix(perf): v2.0.32 - integration fixups and review for the perf plans

`43ca92ee`

What the perf plans needed once merged together, and the review of
those fixes. The one change a player can see is a log line.

With the tick profiler switched on and a hook it needs missing (another
mod's patch, or a PatchShield rescue), its warning at a mission's start
said "not measuring" while the default-on hitch probe went on measuring
that mission. It now says "no per-type timing" and that the hitch probe
still measures, unless the missing hook is the probe's own frame
boundary, without which nothing is measured. The status lines a mission
writes at its start moved from the profiler's mission behaviour into a
static helper beside the probe's, so their tests (12 new ones) run
without the game and in CI.

Docs and comments now match the merged code. Plan 030 deleted Patch35
on Mission.OnTick and Patch23's spawn postfix, and the text no longer
names them. Plan 034 made PatchShield's finalizer cheap: its measured
figures are stated once, in PatchShieldPolicy, and every other copy
points there. The hang an exception can cause inside Mission.OnTick is
analysed once, in the mission-perf-heartbeat feature doc, with a table
of the six mission methods the profiler and the probe patch and what a
PatchShield rescue does on each. Plans 040 and 042 no longer describe
their merge order as still to come. A test the merges duplicated is
gone, and a new binding test pins the health check's frame-boundary
hook to the method that closes frames.

The programme's records after the walkthrough: the decisions and what
came of them, the landing order and how the landing branch was built,
the squash messages, and the review's RCA
(docs/reviews/rca-perf-integration-fixups-2026-10-04.md) with two
lessons.

Refs: #710, #712, #716, #721, #722, #723, #724
Not-tested: in game. The new warning needs the tick profiler on and a
 required hook removed while the probe is on.
Save-compat: none (no saved state)

#### fix(logging): v2.0.32 - flush TAOM.Dependencies startup lines

`d31026b5`

taom_debug.log now shows what TAOM.Dependencies logs: its static init,
the Harmony fork version, the AssemblyResolve handler and every redirect
it makes, the UnpatchAll guard, UIExtenderEx's start-up and any install
failure. That module loads before TAOM, so it holds those
[TAOM.Dependencies] lines in a buffer (EarlyLog) until TAOM has a
logger. Nothing ever flushed the buffer, so every one of them was
dropped on every launch, and so was every later line, such as a blocked
UnpatchAll call during a game.

TAOM now hands EarlyLog its logger in OnSubModuleLoad, right after the
container is configured. Buffered lines are written in the order they
were logged, each at its own level and starting with "[buffered
HH:mm:ss]", the time it was logged; later lines go straight to the log.
A line logged on another thread during the flush queues behind the older
ones, so none is lost or reordered. A logger that fails costs only the
line it was writing and never stops the module loading.

The change spans both modules: the hand-over in TAOM.Dependencies'
EarlyLog and the bridge in TAOM. dr3-maintenance.md now says the
AssemblyResolve redirects are in taom_debug, not diag.log.

Refs: #725
Not-tested: in game. The [TAOM.Dependencies] startup lines in
 taom_debug.log on a real launch, and a line logged from another thread
 during the flush.

#### fix(localization): v2.0.32 - English overrides only for English

`407b1e76`

Players on a game language other than English were shown English for 313
strings, although every one of them has a translated row in all twelve
language files. The Abanissa and Shaghana culture names and
descriptions, their clan names and their notables were among them, as
were 181 reworded vanilla texts. TAOM's English override table was
applied in every language, so its English covered the translation.

The table exists because the game skips its string dictionary only for
English. It now applies only while the text language is English. In any
other language the game reads the player's own row, so those 313 strings
show in the language the player picked. English play is unchanged. A
language with no row for an id would fall back to the default English
text, as it does for every other string; all twelve shipped languages
have a row for each of the 313.

The language is read on every lookup, and a lookup in another language
stops before the table probe. taom_debug.log gets one INFO line each
time the text language changes to one that is not English
("[LocalizationOverride] Text language '...' is not English: the English
string overrides are skipped and vanilla reads that language's own
rows") and one when it changes back ("Text language is English again:
the English string overrides apply"). A session that starts in English
writes neither.

The localization override doc, the Patch25 registry entry and the
translator docs describe the rule.

Refs: #706
Not-tested: in game. In German, an Abanissa notable and the Abanissa
 culture name show their German rows; switching back to English writes
 the "English again" line and the overrides apply again.

#### fix(custom-battles): v2.0.32 - named commanders, default troops (#709)

`dffdf879`

Custom Battle no longer crashes on Start when a side picks Nord,
Vakken or Darshi. Those vanilla minor cultures have no faction banner,
and vanilla recolours each side's banner without checking it has a
layer. The faction picker now lists only cultures with a banner: the
22 TAOM factions instead of 25.

Mordor, Gondor and Rohan now show their full curated commanders in
Custom Battle: Sauron, the Witch-king and the Nazgul, Boromir, Faramir,
Imrahil, Theoden and his captains. These 24 lords are vanilla lords
that TAOM rebuilds, and vanilla loads them only in a campaign, so
Mordor's list was empty and Gondor and Rohan showed one or two names.
A Custom Battle-only stub file now lets the same rebuild create them.
Khamul also heads Dol Guldur's commander list now, and in Sergeant
mode the random general can be one of these lords.

Each formation now defaults to the culture's own TAOM troop where that
troop fits the slot. TAOM's defaults never applied in Custom Battle
before, and the six re-skinned cultures defaulted to Calradian troops
(Rohan infantry: the Vlandian Swordsman). The culture data fits 33 of
88 culture and slot pairs; the other slots keep vanilla's pick.

Most of this code landed in 9e2a39f4. This commit adds the slot check,
the fallback that keeps Abanissa and Shaghana fielding Harad troops,
and the face-gate exemption whose absence failed CI on 9e2a39f4.

Known limitation: Sauron fights in Custom Battle without TAOM's race
combat rules, which are campaign-only.

RCA: docs/reviews/rca-custom-battle-bannerless-factions-2026-10-02.md

#### fix(arena): v2.0.32 - troll gear unobtainable, prizes capped at heavy

`bc39f6e4`

Troll weapons and armour are for trolls. Troll Mace I and the troll
shield could be bought, looted and won, the bracers had no merchandise
flag at all, and every troll weapon part could be researched and forged
at the smithy. The Armory now marks all ten troll items as not for sale
and hides their twelve crafting parts from the smithy. Troll troops
still spawn armed.

Tournament prizes are now light, medium or heavy gear and nothing
above. A tournament with fewer than four lords awards light or medium
kit; a bigger one awards heavy armour or a Tier 4 weapon. Elite, lord
and named kit, Tier 5 and 6 weapons and troll gear are never prizes.
Heavy armour was never a prize before, because the armour gate hid it
from every pool. A town whose culture has nothing in the band now draws
from every culture's items instead of vanilla's Calradian list, which
also keeps an empty list from crashing the prize roll.

With Armour Acquisition switched off, culture markets no longer stock
anything the Armory marks not for sale.

For modders: CREATURE_GEAR_OBTAINABLE in validate_moduledata.py (and
the commit hook) fails when an Armory reinstall brings the troll gear
back; tools/lock_creature_gear.py puts it back, and --modules applies
it to a release channel.

Known limitation: the Armory edit is in the dev install only. Players
get it with the next Armory package, or after lock_creature_gear.py is
run against a release channel. One deep review convergence pass on the
fix diff is owed: the review hit the weekly usage limit. RCA:
docs/reviews/rca-troll-gear-tournament-prizes-2026-10-02.md

#### fix(player-switcher): v2.0.32 - a taken-over lord keeps their treasury

`59b1ff92`

Picking an existing lord at character creation (Player Switcher's
panel, or Kysaro's faction screen from #704) now keeps that lord's own
treasury. Before, every lord taken over started with the engine's flat
1,000 gold plus the culture's starting gold, whatever they held,
because the engine assigns the player 1,000 once every character
creation handler has run. Mike: "If they wanted to start with 1K they
would've made a character from scratch."

The handover records the lord's gold, and at the end of character
creation StartupResources gives it back in place of the culture's
starting gold. "Carry Over Starting Gold" works again: with it on, the
created character's starting gold is added to the treasury.

Unchanged: a character made from scratch and an adopted wanderer start
with 1,000 plus their culture's starting gold, and an Advanced Starting
Options start other than the default keeps the gold it sets.

Known limitation: such a start (King, Vassal, Mercenary, Trader,
Outlaw, Beggar) still runs against a taken-over lord, replacing their
gold and, depending on the start, their gear or their clan's kingdom.
A decision is owed.

Review: docs/reviews/rca-takeover-treasury-2026-10-02.md

#### fix(battle-corpses): v2.0.32 - fade and cap battle corpses (#701)

`c17541f2`

Players on 1.4.8 and 2.0.x reported mid-battle freezes that stopped
when they lowered the vanilla Number of Ragdolls and Number of Corpses
options. In a normal battle a fallen soldier never times out: the
engine's own corpse timer is an hour, so bodies pile up until the
corpse cap pushes them out, and on Unlimited that is 1,021 bodies,
which the engine walks every frame.

Field battles, sieges and sally-outs now fade each body 60 seconds
after it settles and keep at most 25, through the engine's own
per-battle corpse settings. If your Number of Corpses option is lower,
yours is kept; TAOM never raises it. Stealth missions, towns, arenas
and hideouts keep their bodies. Both values are sliders under Mod
Options, TAOM, Performance, Battle Corpses, and apply mid-battle.

Ragdolls cannot be limited per battle, so on the first main menu
after the game starts TAOM offers to lower Number of Ragdolls to 5 and
Number of Corpses to Low when yours are higher. Each option is only
ever lowered, nothing changes unless you click Apply, and an MCM
button does the same at any time. The notice names the options with
the game's own labels, so it matches your Options screen.

Each battle also logs one [BattleSettings] line with your ragdoll,
corpse and battle-size options, so a freeze report shows what was
set.

Known limitations: the new notice is English-only until the
translation run (#703). Whether removing a corpse also frees its
agent slot (reinforcements arriving sooner) and whether the lowered
ragdoll limit applies before a restart are still to be checked in
game. Deep review RCA: docs/reviews/rca-battle-corpses-2026-10-01.md.

#### fix(hero-race): v2.0.32 - Load Game preview rider no longer lies flat

`1e39c1b8`

The hero preview on the main-menu Load Game screen showed the rider
lying flat in bind pose while the mount stood normally (#700). Every
in-game screen was fine.

The preview poses the rider with one of the engine's cached animation
indices. On v1.5.3 those indices are broken before the game finishes
loading on almost every launch, and TAOM's repair for them only ran
once a campaign or battle started, after this screen. The preview's
existing patch now runs the repair first, so the rider stands in the
inventory idle. Nothing changes for other screens.

Pinned by a wiring test that fails if the repair stops being the first
statement of the patch. Review record:
docs/reviews/rca-load-game-preview-bind-pose-2026-10-01.md; incident
addendum in docs/reviews/rca-prone-character-tableau-2026-07-31.md.

Refs #700

#### fix(realm-borders): v2.0.32 - review fixes for the tint and colours

`1ac2d1eb`

Completes the Realm Borders look round begun in cfca3c5f, whose message
left out what players get:

Each realm's land is now tinted in its colour between the borders (MCM
"Colour Realm Lands" and "Realm Colour Strength"). Every look control
is in MCM under Realm Borders, including a colour field for each realm
under Realm Colours (blank keeps the default) and two advanced choices,
Border Blend Mode and Border Material. Harad is red, Rhûn crimson,
Khand orange, Rivendell purple, Rohan green, Dunland brown, and
Isengard and Umbar black; six neighbours moved so no two realms look
alike. Building a border tile no longer freezes the map for a quarter
of a second: heights now come from the terrain directly.

This commit fixes what the review of that round found. The province
map picture no longer shares a grey with Gundabad. Changing one of the
two advanced dropdowns keeps a console choice made for the other. A
rebel realm's colour now keeps clear of colours set in MCM. The Border
Blend Mode tip no longer suggests Factor, which turns blending off.
Umbar's default is a black with a faint sea-blue cast, #2F3C58.

Still to decide in game: the blend mode. The material's own is
AddAlpha, an additive glow that hides the ink; Modulate should show it.

Refs #698.

#### fix(tools): v2.0.32 - Codex review fixes for the KEYforce art (#696)

`babcb833`

Codex (gpt-6-astra, xhigh) reviewed the five #696 commits and found
two LOW defects, both latent, with no false positives. A seven-lens
review of the fixes found one more gap of the same kind, and a
convergence pass confirmed nothing the tools write has changed.

- The enlistment roster generator decided whether a troop rides a
  spider, war elephant or Mumakil from its first battle equipment set
  only. The engine can spawn a troop on any battle set, and it writes
  an <equipment> placed directly under <Equipments> over that slot in
  every set, which is how 88 shipped troops get their horse. The
  generator now builds each battle set the way the engine does and
  reads the kit (first set) and the mounts (every set) from that one
  model; an equipmentType="Civilian" roster no longer counts as a
  battle set. No shipped troop changes: the same four riders are
  dropped and the rosters it would write differ only in two reworded
  header lines.
- The Custom Battle picker gate searched each siege scene's text for
  one line: a commented-out declaration passed it and a reordered one
  failed. It now parses the scene's level declarations and requires
  every level a Custom Battle siege loads, siege plus level_1 to
  level_3. All 15 siege scenes pass, and two fragment tests run
  without the game installed.

Docs: the file catalogue, the enlistment and custom battles feature
docs, and the generator's header no longer describe the old gate or
say the donor pools mount mumakil and war elephants.

Review records: the Codex pass and the fix review in the RCA
(docs/reviews/rca-keyforce-art-wiring-2026-09-29.md), recurrences
under three existing lessons and one new testing lesson, the Codex
track record, the review reference, the REVIEW-LOG entry and the
prompt that was dispatched.

#### fix(creature-bandits): v2.0.32 - vanilla out-of-sight spawn retry

`dd6c3d3a`

New spider broods and troll bands now pick their spawn point out of the
player's sight exactly the way vanilla bandits do: a point inside the
player's sight is retried up to 15 times around itself, taking the
first reachable point whose path distance from the player is past his
sight, and the first point stands if none is. The earlier retry
resampled around the settlement with straight-line distance, which
could run out of tries where vanilla finds a point.

Codex review of #694 (gpt-6-astra, xhigh): no defect, all six suspects
disputed with engine code, this one LOW observation fixed. Recorded in
docs/reviews/rca-troll-bandits-2026-09-28.md and REVIEW-LOG. Full suite
11010 passed, 2 skipped. Refs #694.

#### fix(saruman): v2.0.32 - review fixes for Saruman and Sauron faces

`a728f35b`

Follow-up to 6df36909, which added Saruman the White as the head of
Isengard and gave Saruman and Sauron their faces. A deep review of that
commit found the fixes below.

Sauron is no longer offered to Mordor players in character creation.
The race was added only so his face could be built in the editor; a
player of that race would have taken Sauron's dread aura, signature
strikes and immortality, and the race has no character creation
animation sets.

Removed the taom.print_face console command. The face editor already
copies a face to the clipboard with Ctrl+C, in character creation and
the barber, and pastes one with Ctrl+V; the docs now say so.

Removed the hair and beard morph fitter (tools/blender/fit_hair_morphs.py
and hair_follow.py). Vanilla gives hair and beards no morph channels of
their own; the engine carries them with the head's channels. Vanilla is
the reference, so a beard fits when its rest shape is modelled on the
exact head. Saruman's live FBX keeps the fitted channels for now; the
original is at .bak-hairfollow.

The gold and red eye colours on the sauron race now have a reinstall
gate: tools/oneoff/add_sauron_eye_colours.py --check exits 1 while any
sauron skin lacks them, and refuses a race whose gradients it cannot
find. The script also refuses to pass the engine's 32 eye colour stops.
The Armory snapshot, its README and the tools README record the edit.

Docs: race-face-and-hand-morphs.md gains sections on exporting a lord's
face, on how hair, beards and eyebrows follow the face, and on eye
colour stops; routed from doc-lookup.md and INDEX.md. Three lessons and
the RCA: docs/reviews/rca-saruman-lord-and-faces-2026-09-28.md.

### Performance

#### perf(campaign): v2.0.32 - cheaper campaign settings reads and filters

`423d948d`

Campaign code that runs per party per hour, per party and garrison per
day, per caravan destination score, every map frame or per party icon
looked up TAOM's Mod Options object on every read. Eight settings
providers (castle recruitment, caravan trade, alignment desertion, realm
borders, battlefield promotions, quick actions, time acceleration and
map figure scale) now keep it from their first successful lookup and
read through it. A change in Mod Options still applies at once, and
until MCM is up they fall back to their defaults as before.

Cheap checks now run first:
- Alignment desertion asks whether an owner can lose troops at all (the
  feature, owner and location toggles, a Free or Evil kingdom, a rate
  above zero) before copying its roster, so the parties and garrisons of
  Neutral kingdoms cost nothing each day.
- The refuge listener skips its walk over every party in a world battle
  while the player has no refuge.
- Party speed walks the roster for the Rohan infantry penalty only for
  parties of a culture with that feat, using the same check the speed
  model uses to skip counting mounts.
- The culture marketplace builds each culture's item id sets once,
  instead of for every town every day, and counts a town's guaranteed
  items in one walk of its market. A failed market read logs one line
  naming every guaranteed item instead of one line per item.

Every recruitment, desertion, speed, price and stock result is
unchanged.

What this does not do: caravan trade scoring still asks the engine for a
town's travel distance a second time. A hand-off that reused vanilla's
distance was built and then dropped in review: an unmeasured saving on
an infrequent path, against a transpiler on a private engine method.
Castle recruitment keeps its original check order.

Refs: #719 (plan 037)
Not-tested: in game. Change Realm Borders, Battlefield Promotions, the
 fast-forward multiplier and Map Figure Scale in Mod Options on the map
 and see each apply without a restart; an AI lord recruits in a castle;
 a Free lord's party sheds Evil troops overnight; a Rohan infantry party
 is still slowed; a refuge rallies militia in a battle; an Isengard town
 stocks its warg items after a few days.

#### perf(creatures): v2.0.32 - fewer allocations in creature battles

`efe32624`

Creature trees and the spatial grid did work in every battle that
nothing used. Creatures behave as before.

- The creature behaviour trees (wargs, spiders, elephants and mumakil,
  elk, war rams) time their sleeps, waits, attack cooldowns and the
  warg's rage with DateTime.UtcNow instead of DateTime.Now: no time zone
  conversion per node per frame, and a daylight-saving change no longer
  makes an interval in flight jump by an hour. The intervals are the
  same, and the timers still follow the wall clock, so they keep running
  in a paused battle as before.
- The tree framework's selector reuses its two child lists instead of
  allocating them on every re-entry, which for a creature tree is every
  frame.
- The tree logic returns before building argument arrays, or parking a
  replay for an off-thread event, when no tree listens to that engine
  callback. An off-thread event nobody listens to is now dropped, as the
  main thread already dropped it. taom_debug.log gets one INFO line for
  the mission's first such skip and an INFO summary at mission end
  ("[BehaviorTree] Mission end: N callbacks skipped with no tree
  listener ...; M parked off-thread for the mission tick.").
- The spatial grid that wargs and spiders scan is still rebuilt every
  2 s in every battle. It now reuses its maps and cell lists, removes a
  deleted agent from the one cell its index names, and skips a scheduled
  rebuild that nothing queried; the next query on the mission thread
  rebuilds first, so a creature that starts scanning later still sees
  fresh cells, and an off-thread query never builds. One INFO line each
  per mission records the first skipped rebuild and the first rebuild
  made by a query ("[SpatialGrid] ...").

What this does not do: the spider hunt still walks the hostile teams'
agents. A version that asked the engine's nearby-agent query first was
built and reverted, because on v1.5.3 the reads it avoided are cheap
inlined field reads and the query cost more before contact. The grid
still holds the last battle's surviving agents, and through them the
finished mission, until the next mission replaces it, as before.

Refs: #715 (plan 033)
Not-tested: in game. A warg battle (bites land, rage triggers), a
 creature-bandit battle (the spider hunts the nearest soldier from the
 start) and an elephant battle (trample and side-attack cooldowns feel
 unchanged); the [SpatialGrid] and [BehaviorTree] lines in a battle
 without creatures.

#### perf(mixed-formations): v2.0.32 - lock-free path for plain formations

`13f34c1c`

Mixed Formations patches Formation.GetOrderPositionOfUnit, which the
engine calls for every AI unit in a formation about twice a second, on
its worker threads. Each call allocated an adapter, and for a formation
holding position it also read the cavalry query and took the service's
global lock, all for formations that almost never have a layout.

A formation without a TAOM layout now goes back to vanilla after one
lookup in a map of laid-out formations, keyed by object identity,
written under the lock and read without it: no lock, no allocation and
no formation query on the worker. A laid-out formation reuses the
adapter the main thread built; its cavalry check and its positions are
the same as before.

What this does not do: a laid-out formation still takes the lock for its
slot, still evaluates the formation's class-ratio queries on the worker
through its unit spacing (Formation.Interval and UnitDiameter), as
vanilla's own worker code does, and still makes its two native queries
per unit (ground height and slot availability). Only formations without
a layout are off the lock and the queries.

A throw inside the formation prefix still falls back to vanilla
positioning, but no longer silently. The first throw of each mission is
written in full at WARNING ("[MixedFormations] Patch30 position prefix
threw; ..."), later ones are counted, and the count is written at INFO
when the mission ends ("[MixedFormations] mission ended: N unit
position(s) fell back to vanilla after a Patch30 throw").

Creature bandits: the wield and missile-range guards every agent passes
through rule a humanoid out by its flags before reading its troop id or
rider, so a soldier costs two reads instead of four. The answer is
unchanged for every kind of agent.

Refs: #714 (plan 032)
Not-tested: in game. A mixed player infantry and archer formation of 10
 or more on Hold still forms its layout and the L hotkey cycles it;
 cavalry moved into and out of a laid-out formation; AI formations
 behave as before; a spider brood battle runs. No unit test shows that a
 real prefix throw reaches the fallback lines.

#### perf(battle): v2.0.32 - read MCM settings once, not per blow or frame

`74e9172d`

Battle code looked up TAOM's MCM settings object on every read, and some
of those reads run on every melee blow (about eight per ordinary hit,
even with blow diagnostics off), every frame, or every howdah seat and
agent stat update. The combat mechanics and blow diagnostics providers,
and those of Mixed Formations, Companion Tactics, Dread Aura, howdah
diagnostics, Smart Cavalry AI, Culture Doctrine and siege prop
diagnostics, now keep the settings object from their first successful
lookup and read through it. A change in MCM still applies at once,
mid-battle included, and until MCM is up every read falls back to its
default as before.

Also:
- Cleave, stagger immunity, crush-through and the race modifiers test
  the cheap facts of a hit (such as a monster id, a shield block or an
  AI attacker) before reading a setting, and the stance-cancel patch
  checks the player's team before its setting. Every result is
  unchanged.
- The English string override looks a text's {=ID} up in place instead
  of cutting it out into a new string, so its own lookup no longer
  allocates (vanilla still builds its string when no override applies).
- Mixed Formations parses its cycle hotkey only when the setting
  changes, not every frame.

Behaviour change, the maintainer's decision: the Mixed Formations cycle
hotkey must now be the name of one key, in any case (L, f5, D2). A
number in any spelling ("3" used to pick the 2 key), a list such as
"L, K" (which used to combine into the semicolon key), a chord such as
"Ctrl+L" and the name "Invalid" now switch the hotkey off instead of
quietly picking a key the player never named. Each writes one warning to
taom_debug.log when the setting changes to it and once per field battle,
for example "[MixedFormations] cycle hotkey 'Ctrl+L' is not a recognised
InputKey name; it is ignored until the setting changes". An empty value
still means no hotkey, now with the same warning. Names such as L or F5
behave as before.

Nothing else a player sees changes, and no other log line changes. The
module dependencies chapter now names the installed MCM 5.12.3.

Refs: #713 (plan 031)
Not-tested: in game. During a field battle, change a Combat Mechanics
 toggle, the Mixed Formations cycle key and Smart Cavalry AI in MCM and
 see each apply without a restart; set the cycle key to "3", then
 "Invalid", then "Ctrl+L", and find one warning for each with the key
 doing nothing, then set it back to L and see it cycle layouts; check
 that an overridden English text (an alliance notification) still shows.

#### perf(diagnostics): v2.0.32 - cheaper always-on mission diagnostics

`e0ad0db7`

Several diagnostics that run in every battle did per-frame or per-hit
work for lines they rarely wrote. They now do that work only when it can
produce a line, and what they logged is still in taom_debug.log,
aggregated where it used to repeat.

- Action-set census (the first 5 s of each mission): each agent is
  checked on an integer key first, and only a new (action set, race,
  sex) combination reads names. The census lines are unchanged. Two new
  INFO lines bracket the window: "[MissionDiag] ActionSet census open"
  and "[MissionDiag] ActionSet census closed: agentChecks=N newKeys=K
  lines=L".
- Career perks: the hit amp and hit reduction DEBUG lines are written
  once per battle for each combination of direction, hero or party
  leader, hit mask and passives, with that first hit's numbers. Every
  hit is still counted, and an INFO summary gives each combination's
  hits, multiplier range and damage totals; a hit with a non-finite
  damage value is counted as nonFinite and kept out of the sums. The
  summary is written when the mission ends, or when its behaviours are
  removed without a normal end, so only a crash or a killed process
  loses the counts after each combination's first line. It keeps totals,
  not each hit: hits of 10, 20 and 70 damage read the same as 10, 40 and
  50 under one multiplier.
- Trolls: the formation-spacing tracker and the clip trace wait for the
  mission's first Brute Force troll instead of walking every agent twice
  a second in every battle. One INFO line says when they start, or at
  mission end that no troll was built. [TrollSpacing] and [TrollClips]
  lines in troll battles are as before. One small gameplay timing
  change: the spacing tracker's 0.5 s clock now starts at the first
  troll, so a formation's re-space can land up to about 0.5 s sooner or
  later than before.
- Creature Bandits diagnostics: each engine callback first checks
  whether the mission has any creature, and a line the line budget
  refuses is no longer formatted. A mission with no creature spawn
  attempted or declined, which writes no summary, now ends with one INFO
  line saying so ("[CreatureBandits][diag] no-creatures").
- Companion Tactics: an empty Harmony postfix on Mission.OnTick that
  still ran every frame (a detour, a settings read and a PatchShield
  finalizer) is deleted. The order-of-battle preset buttons and
  companion roles are unchanged.
- Banner colours: an agent colour store that every spawned agent wrote
  twice and nothing read is deleted, with its two patch members and its
  cleanup behaviour. Troop armour tint still comes from the
  Mission.SpawnAgent prefix. With BattleLoad diagnostics on, each
  mission logs one TaomBehaviorAdded line fewer.

The cost to a player is about four new INFO lines per mission.

Refs: #712 (plan 030)
Not-tested: in game. A campaign field battle with clan-coloured troops
 (armour tint unchanged); a battle with a cave or hill troll and one
 without ([TrollSpacing] only in the first); a career hero with a
 TroopDamage pip (one hit amp line per combination, then the summary);
 the census lines in a mission's first 5 s; the companion-tactics preset
 buttons; whether the career summary is written when the game window is
 closed mid-battle.

#### perf(patchshield): v2.0.32 - find the original method only on a throw

`5ee49d05`

Every method PatchShield guards (about 160 at a process's first game
start, close to 300 later) paid a lookup on every call, so the shield
would know the method's name in case an error ever came through: its
finalizers took Harmony's original-method parameter, which makes the
patched method call MethodBase.GetMethodFromHandle on every call. The
finalizers now take only the exception, and after a throw the shield
finds the shielded method from the stack, judging only the frame that
called the finalizer. If another mod has patched PatchShield's own
finalizer, the lookup skips that frame and judges the next one.

Measured outside the game (.NET Framework, Harmony 2.4.2): the old
binding cost about 64 ns and 241 bytes of garbage per call on one
thread, and about 1.1 microseconds per call with 8 threads calling at
once. With TAOM.Dependencies as it ships (a Debug build), a patched call
with the remaining finalizer costs about 5.4 ns against 1.9 ns with no
finalizer, and allocates nothing.

What the shield does after an error is unchanged: the same exceptions
are swallowed or rethrown, and the swallow line, the owner it unpatches
and the rethrow marker name the same method. The exclusion lists and the
co-op skip are as they were.

diag.log changes (PatchShield ships in TAOM.Dependencies):
- A swallow line that repeats (the same exception type, method and
  message) is written in full the first time in a process and counted
  after that (maintainer decision D16). Until now such a line was
  written on every call, at frame rate on a hot method, whenever the
  throw kept coming after the first swallow, for example from an owner
  on the protected list or from the method's own body or a method it
  calls. Each mission start now writes the running count of every line
  that recurred, "(and N more so far this session, counted instead of
  logged)", and the summary at a clean exit gives the totals, so a crash
  loses at most the counts since the last mission start. A line whose
  write failed (diag.log held open elsewhere) is retried until it lands.
  Up to 256 distinct lines are counted; a line past that is written in
  full every time.
- If the stack lookup cannot name the method, the first miss writes one
  INFO line ("could not tell which shielded method an exception crossed
  ...") and the session summary adds "; the shielded method was unknown
  N time(s)". Nothing is unpatched in that case, so a throwing foreign
  patch the shield cannot name keeps being swallowed on every call; its
  line is written once and counted.

Refs: #716 (plan 034)
Not-tested: in game. One battle on a deployed build: diag.log shows the
 usual shield pass lines and no new CAUGHT line; after a session with a
 swallow or a rethrow marker, no "could not tell which shielded method"
 line and no "unknown N time(s)" suffix; a repeated swallow's count line
 at a mission start and at exit. The Mono path (Proton players) cannot
 run on .NET Framework.

#### perf(xml-merge): v2.0.32 - merge module XML in one document per load

`a27c6b33`

Every campaign load and custom battle start merges each module's
ModuleData XML into one document per type. The engine converts the whole
document merged so far between XmlDocument and XDocument for every file
and compiles every XSLT on every call, so the cost grows with the square
of the merged size. On the maintainer's desktop the engine's logs put
about 28 s of a new campaign's 50 s loading screen in that merge, and
about 14 s of a custom battle's load.

The new fast path runs the same loop with the engine's own loader, merge
and converters, in the engine's order, but keeps one document across the
files and compiles each XSLT once per game process. Only validated
merges take it; unvalidated ones (SPCultures, Monsters and similar) keep
the engine's code. It stands aside while another mod patches a method it
bypasses, re-runs the engine's own merge on any exception, and turns
itself off for the session if the engine succeeds where it failed. There
is no player-facing switch.

A harness merges every type of the installed module set both ways and
compares the results character for character: every type is identical,
for a campaign and a custom battle. For the four heaviest types the fast
path took about 16 percent of the engine's time in that harness for a
campaign and 9 percent for a custom battle, and 27 to 35 percent
offline; the test host slows the engine path about twice, so the offline
figure is the fairer one. The in-game load time has not been measured
yet. The harness is opt-in (TAOM_RUN_BENCHMARKS=1, about 40 s), and
/verify-bindings runs it: run it after any engine update, because the IL
fingerprint test cannot see every engine change. It fails on any
difference, on an engine exception, on a missing heavy type or module,
and on a heavy-type fast total above half the engine's.

The compiled XSLT cache holds about 11 MB of managed memory for the game
process. lords.xslt still costs about 1 s on each game process's first
load and 0.05 to 0.1 s on later loads.

taom_debug.log gets a start line ("[XmlMerge] fast path ready", or "fast
path off:" with the reason), one [XmlMerge] line per merged type (path
fast or vanilla, files, XSLTs, time with its load, XSLT and merge split,
XSLT cache counts), a summary per game, a line whenever the fast path
stands aside, fails or turns itself off, and after each failure a DEBUG
line with the exception's full text. The engine's rgl log keeps its
"opening" lines for every file.

Ship it only with plan 040, which puts CreateMergedXmlFile on
PatchShield's exclusion list: without that entry PatchShield would
attach to the method this plan patches, and a missing-API exception
inside the engine's own merge would load that data type empty instead of
failing as the unpatched game does. This plan adds no entry of its own.

Refs: #724 (plan 042)
Not-tested: in game. One new campaign and one custom battle: the
 [XmlMerge] start line, path=fast lines for validated types and
 path=vanilla reason=skip-validation for the rest, the summary, the
 loading screen against the 50 s baseline, and the rgl "opening" lines;
 with plan 040 merged, [LoadXml] lines still carrying merge times.

### Documentation

#### docs(perf): v2.0.32 - engine performance programme records and plans

`af990ca7`

The records of the 2026-10-02 engine performance and memory programme
that no single plan carries. No code changes; each plan's code lands in
its own commit, tracked by issues #710 to #724.

- plans/028 to plans/042: the fifteen plans as reviewed and revised,
  with their rows in the plans/README.md index.
- plans/_audit/2026-10-02-perf, the run folder: REPORT.md (the findings
  and the engine facts behind them), FOR-MIKE.md (the maintainer's open
  items and the protocol for the first measurement session),
  DECISIONS.md (the maintainer's answers), the run log, the baseline,
  the issue drafts, one plan review per plan, the squash messages, and
  the evidence: load-time and shader-cache readings, memory audits
  (banner atlases, UI sprite sheets, module sounds, map texture orphans,
  the inventory open cost, a Kit worklist), native decompile notes, the
  merge map and the commit-message spec check.
- docs/reference/engine/mission-frame-threads-and-native-costs.md: a
  battle frame by thread, the native costs TAOM's code pays, and
  animation clip residency, linked from docs/INDEX.md. Plan 036's commit
  corrects the page's clip eviction section; REPORT.md's eviction lines
  and the INDEX.md entry already carry that correction in this commit.
- Corrections found on the way: the inventory and party screens' memory
  jump comes from cheat mode's every-item list, and the 2026-09-12
  readings followed one engine collection, not two
  (battle-load-diagnostics.md, the 2026-09-12 memory RCA, the native
  commit audit and a state-lifecycle lesson); the Steam game runs on
  .NET Framework, not Mono (bannerlord-engine-and-toolchain.md).
- The adoption review of yotthani's engine research for this work, with
  its provenance register rows: comparison only, nothing copied.

Refs: plans 028 to 042
Not-tested: nothing here runs; the measurement session in FOR-MIKE.md is
 owed once plans 028, 029, 036 and 041 are in a build.

#### docs(realm-borders): v2.0.32 - adoption review and preview tool

`594d0ae9`

Reviewed the Kingdom Borders mod (Nexus 10699) and designed TAOM's own
kingdom borders for the campaign map. The mod gives every spot to the
nearest settlement on a coarse grid, which on TAOM's map runs borders
straight through the Misty Mountains and hands realms the open sea, and
it colours borders with banner colours that leave 9 of 22 realms near
black.

The approved design grows one province per fief over the terrain, so
coasts, mountain walls and rivers become borders, and redraws the
borders live whenever a fief changes hands. Mike approved all six
proposals: the province model, a curated realm palette, batched strips
in an "Atlas" look (watercolour outline colouring with a dip-pen
dash-dot line, the player's own realm in a gold cord), map modes, realm
names and border-crossing notices. Nothing is built yet.

tools/realm_borders_preview.py renders the model and the looks offline
from the live TAOM_Map heightmap and settlements, for tuning. The strip
textures in tools/realm_border_art/ were generated with ImagineArt; the
prompts are in its provenance.json. The mod is recorded in the
provenance register as comparison-only.

#### docs(elephant): v2.0.32 - howdah crew kills keep their merit (#627)

`9e7cc727`

Mike decided on 2026-09-30 that kills by the howdah crew keep counting
as Field Commission merit for harad_archer when the player's own party
fields a howdah elephant. The elephant doc records the decision; the
refuge damage reduction that does not reach the crew stays open there.

#### docs(rules): v2.0.32 - model the deserializer when reading troop kit

`df273ea2`

An <equipment> written directly under <Equipments> is laid over its
slot in every set a troop has. That rule lived only in the XSLT and
ModuleData lessons, and scripts under tools/ have missed it twice:
the troop roster page (#641) and the enlistment generator's
creature-rider filter (#696, fix review R1 in
docs/reviews/rca-keyforce-art-wiring-2026-09-29.md).

moduledata-validation.md loads whenever a tools/ script is read, so a
short section there now puts the rule in front of whoever next writes
a script or test that decides what a troop wears or rides.

#### docs(reviews): v2.0.32 - RCA and lessons for the KEYforce art wiring

`0aad0d94`

The deep review of the KEYforce art wiring (#696): eight lenses in two
waves plus a convergence pass, no CRITICAL or HIGH finding, every
MEDIUM and LOW fixed or decided by Mike before the commits.

- docs/reviews/rca-keyforce-art-wiring-2026-09-29.md: 16 findings with
  why each was missed. The main pattern: a troop joining a shared tree
  is read by every consumer that selects troops by role (AI upgrade
  weighting, notable slot growth, the garrison swap, the enlistment
  generator, the skill rebaseliner), and the plan traced only its
  recruiters.
- Lessons: that rule (data-content-cultures.md); a crash tied to one
  mesh is not a rule for the next (animation-skeleton.md); a Visual
  Studio launch deploys the whole working tree, a subagent's issue
  number is a claim to check, a live-module backup belongs in the
  quarantine, and a generated file goes through its owner
  (build-tooling-workflow.md).

#### docs: v2.0.32 - the mirror is Mike's to sync; elephant skeleton path

`8af881f2`

Two pieces of documentation told sessions the wrong thing.

- Edit the live Armory only. Since 2026-09-22 Mike syncs the
  lotraom-assets mirror himself, in one pass, and a session never edits
  it. The /armory-audit skill (its repair and commit steps), the
  armory-ref-audit feature doc and the ranged-ladders recipe still said
  to patch or commit the mirror; the /armory-audit run of 2026-09-29
  had to override its own skill. They now say the live Armory, and a
  commit body names which live files changed.
- bannerlord-engine-and-toolchain.md named elephant_harad_armor_01_geo
  .tpac as the file carrying the elephant_skeleton. It is a 416-byte
  stub; adod_elephant_geo.tpac carries the skeleton.

#### docs(mithrilforge): v2.0.32 - adopt prop recipe, gate the camp tpacs

`991f415f`

Adopt what TAOM can use from yotthani's MithrilForge (MIT), read in full
at commit 91149e11 and in its 2026-09-13 snapshot. Its TpacTool fork is
private and unread, so nothing is built or run; the facts are restated
and attributed.

- New docs/reference/tpac-static-prop-authoring.md: how MithrilForge
  writes a static prop into a .tpac without the Modding Kit (donor
  clone, all 14 vertex channels, flat normal and specular maps, no UV
  flip, one package), and four measured ways its output differs from
  every Kit and vanilla package.
- The animation reference docs gain its clip findings, marked as not
  measured by TAOM: a movement set naming a module's own action is
  dropped, only release clips collide, the combat parameter comes from
  CombatParameterId. Measured here: Kit and vanilla packages are packed,
  and every vanilla clip carries its own motion segment.
- New gate tools/tests/test_prefab_asset_packages.py: the prefab meshes
  the code passes to PlaceCenteredPrefab must be packaged once each in
  Main/_Module/AssetPackages, in structurally sound packages.
  tpac_clone_metamesh.py now imports lz4 and xxhash only where they are
  used, so the gate runs without them.

Known limitation: the four field camp and refuge props yotthani built
have never rendered. The editor-built releases ship only pack0.tpac,
which holds none of them, and the dev install reads TAOM's loose Assets
tree instead of AssetPackages, so every camp shows the vanilla
fallback. Where the props should live is an open decision; this commit
records it and corrects the docs that called them shipped.

Review: docs/reviews/adopt-mithrilforge-2026-09-29.md
Deep review RCA: docs/reviews/rca-mithrilforge-adoption-2026-09-29.md

#### docs(creature-bandits): v2.0.32 - #694 closed, translations in #695

`41eef590`

The Creature Bandits feature doc records #694 as closed with the in-game
checks still owed (listed in its closing comment), and names #695, the
open issue for translating the seven spider and troll names into the
12 languages. The roadmap's translation note points at #695 too.

#### docs: v2.0.32 - keep the lessons files spaced as trunk has them

`b9feae67`

The rebase onto the new trunk inserted blank lines above older lesson
headings; each file is now trunk's text with this branch's lessons
appended, nothing else changed.

#### docs: v2.0.32 - the lord's gear ladder doc, RCA and lessons

`cdbad39e`

The armour acquisition feature doc covers the ladder: how rungs,
deeds, materials and claims work, the engine facts behind them (each
cited to the v1.5.3 source), its config sections, known limitations,
the in-game checklist and what is owed. The career quest, LotrIssues,
validation, file catalogue, feature map and index docs follow suit.

The RCA of the ladder's deep review records 24 findings, eight design
changes and a convergence pass, all fixed; twelve lessons land in
seven category files.

#### docs: v2.0.32 - armour acquisition feature doc, RCA and lessons

`be1450f0`

docs/features/armour-acquisition.md describes the feature, Mike's
decisions, the configs, the known limitations and the in-game checks
still owed. The CultureMarketplace, special resources, career quest,
LotrIssues, Animalia, co-op and validation docs, the reflection-site
list, the file catalogue, the feature map and the index carry the
matching rows.

The deep review's RCA is
docs/reviews/rca-armour-acquisition-2026-09-27.md; its lessons are
appended to six category files under docs/reviews/lessons/.

### Chores

#### chore: v2.0.32 - the ladder's references in the moduledata rule

`45eaf011`

The moduledata validation rule now lists the ladder's weapon picks,
lord's materials and rung quests among the ARMOUR_ACQUISITION_REF
checks.

#### chore: v2.0.32 - armour class table in the audit and validation rules

`37be26a8`

The moduledata-validation rule registers the two new gates,
ARMOUR_CLASS_TABLE_DRIFT and ARMOUR_ACQUISITION_REF, and adds the class
table check to the Armory row of the gate-per-file-kind table.
/armory-audit now regenerates armour_classes.xml after a ref repair and
commits it with the audit files.

### ci

#### ci(python-tests): v2.0.32 - run tools/tests on bannerlord-1.5.x

`83758f1d`

Nothing ran the Python tool tests on the 1.5.x trunk: build.yml's job
triggers on bannerlord-1.4.5 only, and no hook or skill runs them. The
new workflow runs every tools/tests module on each 1.5.x push and pull
request, including the new camp prefab gate.

It also fixes what made the 1.4.5 copy error: it pins Python 3.14
(Path.read_text(newline=...) needs 3.13 or later) and installs the
packages tests import at module level (pytest, lxml, lz4, xxhash,
numpy, scipy, capstone, pefile, pyyaml). Measured in a clean venv with
exactly that list: 2,819 tests ran and none failed to import. Tests
that need the game install skip on the runner. A floor of 2,500
discovered tests stops a broken discovery from passing.

### Commits without the version label

#### fix: Update fs_portrait_lasgalen.png to improve faction UI visuals

`7f0c8446`

#### Add loading ring sheet generation tool and associated assets

`d9a8f46f`

- Implemented `build_loading_ring_sheet.py` to create a loading sprite sheet for Bannerlord, replacing the default galloping horse with a One Ring inscription.
- Added `imagineart_inscription.png` as the master image for the loading ring sheet.
- Created provenance metadata for the inscription image detailing its generation process and attributes.
- Introduced `imagineart_moonlit_smoke_arch.png` as a background for the loading screen, along with its provenance metadata.
- Added unit tests for the loading ring sheet generation to ensure correct functionality and output.

#### feat: Add comprehensive localization and mesh weight transfer tools

`9e2a39f4`

- Introduced a new documentation file detailing the RCA for the full translation run on 2026-10-01, highlighting key findings and preventive actions.
- Created a centralized source table for TAOM English localization in `tools/_loc_sources.py`, consolidating multiple hand-kept copies into a single source of truth.
- Developed a script in `tools/blender/transfer_upper_mesh_weights.py` to transfer skin weights from reference meshes to unweighted hair or beard meshes, ensuring proper movement in-game.
- Implemented unit tests for the localization source table and the mesh weight transfer tool, verifying functionality and correctness.
- Enhanced the testing framework with new tests for argument parsing, target and reference validation, barycentric calculations, and weight mixing.

#### Add unit tests for generate_dale_armor.apply() to ensure it appends missing items without overwriting existing ones

`0bd6abf3`

- Implement tests to verify that existing items remain unchanged when apply() is called.
- Ensure that missing items are appended correctly to the output files.
- Create tests for scenarios where the output file does not exist, confirming it is created and parsed correctly.

#### feat(crash-report): add diagnostic triggers for exception handling and mission start throws

`649cc30f`

#### fix(siege-dismount): guard adapter calls to prevent NRE in Custom Battle sieges

`bb85e928`

#### fix(siege-dismount): prevent exceptions in Custom Battle sieges by guarding adapter calls

`9cb6d49e`

#### Add realm fill functionality and associated tests

`cfca3c5f`

- Implemented RealmFill class to color each realm's land between borders with a translucent tint.
- Added tests for RealmFill to ensure correct tinting behavior between realms, wild land, and water.
- Enhanced RealmBorderServiceTests with additional tests for fill lands functionality based on player settings.
- Updated RealmBordersProviderTests to validate color overrides and blend modes.
- Introduced new tests for SiegeDismountAdapters to handle scenarios without a campaign.
- Improved documentation for realm borders feature, including MCM settings and console commands.

#### Add unit tests for creature fitting, animation repointing, and geometry calculations

`a8fc5c84`

- Implement tests for creature fitting logic in `test_creature_fit.py`, covering weight joining, hand pose channels, and clip metadata reading.
- Introduce `test_creature_fit_math.py` to validate geometry calculations related to creature animation, including quaternion operations, skeleton transformations, and skinning.
- Create `test_repoint_anim_clip.py` to ensure correct functionality of repointing animation clips, including master GUID changes and frame range adjustments.

#### feat: Add deep review findings for ECC re-review and implement new tuning tool for face sliders

`fdf9f38c`

- Created a new document detailing findings from the deep review of the ECC re-review changeset, highlighting critical and high severity issues, along with preventive actions.
- Introduced a new script `tune_face_slider_reach.py` to adjust face slider ranges in LOTRLOME skins.xml based on reference mesh data, ensuring sliders do not exceed acceptable deformation limits.
- Added unit tests for the new tuning tool to validate slider adjustments and ensure correct functionality.
- Implemented tests for user scope configuration auditing, ensuring proper scanning of user settings and installed plugins.
- Developed tests for the Blender harness gait functionality, verifying stance height calculations and ear flap functionality.
- Enhanced test coverage for the tuning tool, ensuring edge cases and expected behaviors are thoroughly validated.

#### feat(mordor): add Mouth of Sauron equipment sets and strip upper mesh channels tool

`799e189e`

#### Add Saruman translations and implement hair morph fitting tools

`6df36909`

- Updated translation overrides for various languages to include Saruman's name:
  - Chinese (Traditional and Simplified)
  - Italian
  - Korean
  - Russian
  - Turkish
  - Portuguese (Brazil)
  - German
  - French
  - Japanese
  - Polish
  - Spanish

- Introduced new Blender scripts for fitting hair and beard morphs to head morph channels:
  - `fit_hair_morphs.py`: Adjusts hair and beard meshes to follow head morphs.
  - `hair_follow.py`: Contains mathematical functions for hair and beard vertex adjustments.

- Added one-off script to modify Sauron eye colors in skins.xml, allowing for new eye color options.
- Created unit tests for hair morph fitting and Sauron eye color modifications to ensure functionality.

## v2.0.31 (2026-09-28)

Commits since v2.0.30: 259 (245 with the version label, 14 without).

### Features

#### feat(creature-bandits): v2.0.30 - riderless spider broods in Mirkwood

`d469914a`

Giant spider broods now roam Mirkwood as bandit parties, led by a pale
broodmother. Up to four broods patrol the Mirkwood villages, castles and
Dol Guldur. On the map a brood shows as a spider, and meeting one goes
straight to attack or leave. In a field battle every spider fights on
its own, with no rider: it hunts the nearest soldier, bites, pounces and
swipes, and soldiers and archers target and kill it like any enemy.
Spiders count on the battle scoreboard like any troop, with their
casualties and kills. Spiders are bandits only: never recruited, never
taken prisoner, never fielded by lords (#692).

New campaigns only. The brood clan is part of the campaign's starting
data, so a save from an earlier version keeps playing with no broods.

The MCM has a new "Creature Bandits" group:
- Spawn Spider Broods (on): turning it off stops new broods; broods
  already on the map stay.
- Creature Hit Points (200).
- Soldiers hit per attack: bite 1, pounce 2, swipe 3.
- Damage per attack: bite and pounce 100%, swipe 50%.
- Only Crits Knock Down (on): a normal hit staggers a soldier.
- Pounce and swipe cooldowns (5 s and 2 s).
- Damage taken: 50% from missiles, 100% from cut, pierce and blunt
  blows. Charges, kicks and shield bashes count as blunt.

The ridden spider mount is unchanged.

Known limitations: the damage-taken options do not apply to TAOM's own
scripted blows (troll brute force, signature strikes, warg and spider
bites), which makes no difference at the default 100% melee values. On
the deployment screen the spiders hold still but stay visible when the
player attacks. A defeated brood drops no items. Broods can spawn
within the player's sight.

Tests: rules for the spawn swap, route A's flags, the deployment gate,
the routed-count backstop, the patrol order, the brood switch and the
scoreboard's casualty and kill credits; the creature's tuning, clamps
and damage-taken factors; the strike cap through the service, a rider
and his horse in one slot, a loose horse never taking a soldier's slot,
and the ridden spider's unchanged numbers; the troop, clan and template
XML against the code; every patch bound against the installed engine;
the models' creature clauses in the campaign and Custom Battle. Full
suite 10,979 passed, 2 skipped, 0 failed.

Review: docs/reviews/rca-creature-bandits-2026-09-28.md

#### feat(tools): v2.0.30 - integrate_branch merges with append-only union

`d648e00c`

tools/integrate_branch.py merges one branch into a linked integration
worktree. It refuses the main checkout and a dirty tree, and runs only
non-destructive git verbs: it never aborts, resets, cleans or stashes.
Conflicts in the append-only files (the lessons files and REVIEW-LOG by
default) are resolved by union, ours then theirs, with the BOM and line
endings kept. It then adds the blank line missing before a heading the
merge brought in, stops on a duplicated heading or a leftover conflict
marker, and commits with the message file. Any other conflict exits 2
with the paths listed and the merge left for a hand resolution;
--dry-run reports the conflict set without touching the index. It runs
no test suite, so each verification stays its own call.

The merge runs in diff3 conflict style. In the default style git moves
lines both sides share out of the conflict block, so a union of two
review log entries that end with the same line leaves the first entry
without it. The scratch merge scripts this replaces ran in the default
style, recounted lessons that are no longer counted, and still carried
a CHANGELOG branch that is dead since the changelog became generated.

#### feat(tools): v2.0.30 - improve_ctl runs the /improve pipeline plumbing

`218488f4`

tools/improve_ctl.py replaces the scratch scripts the last /improve run
rewrote for each batch. Run data now comes from flags, git and the
environment instead of literals in the code.

- args writes a Workflow script's args JSON. It embeds
  .claude/skills/improve/references/dispatch-rules.md verbatim as
  rules, reads the version from Main/_Module/SubModule.xml and finds
  the main checkout from git. --run-root is always required;
  TAOM_IMPROVE_ROOT, the worktree root, supplies only scratch and tmp.
  For a review each item gains its changed files by kind and the
  deep-review lenses. A hook-only change now gets the standards lens,
  which the old scope.py skipped.
- status sets one plan row's Status cell, finding the column from the
  table header and keeping every other byte, CRLF included.
- codex-prompt writes the adversarial review prompt for a branch from
  git refs. The fixed blocks are the fenced blocks in
  .claude/skills/review-codex/references/prompt-fixed.md, and each
  STOP condition of the branch's plan is quoted whole, where the old
  generator cut wrapped conditions at their first line.
- file-issue files a TITLE:/LABEL: draft with gh once
  check_public_text.py passes it. Filing is public, so it runs only on
  the maintainer's word.
- watch lists workflow agent transcripts by last write and exits 1
  when a started agent with no result has been silent past a limit
  (default 20 minutes). Two review agents once sat 9.4 and 4.5 hours
  on a hook prompt that no timeout catches.

#### feat(improve): v2.0.30 - durable Workflow scripts for /improve runs

`1198881f`

The /improve review sprint dispatched its agents from Workflow scripts
kept in a run folder and a scratch copy. They hard-coded the report
date, the version label, a baseline commit and drive paths; the tracked
copies checked out as CRLF, which the Workflow approval check refuses;
and a null agent result (a killed agent, a usage limit) was handed on
as data. Their review loop ran one fixed second pass, so seven ad hoc
workflows had to finish the convergence work.

Five scripts now live in the skill, under
.claude/skills/improve/workflows:
- fanout.js: parallel read-only agents (audit lanes, verifiers, design
  agents), each appending to its own file, then an optional checker
  over the rows a declarative selectFrom picks;
- plans.js: a writer or extender, then up to reviewRounds cold reviews,
  each followed by a reviser when it finds blocking items;
- execute.js: one executor per item for a plan, a list of maintainer
  decisions, or ordered stages that stop at the first not DONE;
- review.js: deep-review lenses in waves, a lead that verifies and
  fixes, then at most maxRounds (default 2) convergence rounds; a fix
  pass runs only when a later round will review it, and every finding
  the loop did not close comes back as residual;
- draft-issues.js: one drafter per item writes a TITLE and LABEL draft
  for improve_ctl.py file-issue.

Every run value comes from args, built by improve_ctl.py args. A script
throws without args.rules, the dispatch rules text, and opens every
prompt with it, then states the agent's role (EDITING or READ-ONLY,
which decides the rules sections that bind it) and the values of the
rules' placeholders; each role takes its model from args.model,
defaulting to claude-opus-5-5; deep-reviewer calls pass no model; one
pool bounds the agents in flight at args.pool; every acted-on agent has
a schema; a null result is reported as a failure.

.gitattributes pins that folder to LF, not every *.js: tracked tools/
scripts are CRLF in the worktree and would churn.
tools/tests/test_improve_workflows.py checks each script statically (LF
and no hidden character, a pure literal meta, the args.rules guard, no
date, version, hash or drive path, no model on a deep-reviewer call)
and, when node is on PATH, runs it against a stub Workflow runtime that
records every agent call. Against the old scripts it reports 84
failures and 4 errors; it catches 17 of 17 mutants of the new ones.

Not-tested: a run under the real Workflow tool, which CI cannot start

#### feat(tools): v2.0.30 - check public text before it is posted

`5e5877bd`

tools/check_public_text.py checks an issue body, an issue comment or a
commit message before it goes public, and exits 1 with one
"file:line: rule: text" line per finding. It flags em and en dashes
through lint_docs.scan_text_for_dashes, so code spans, fenced blocks
and lint-allow-dash lines pass; local absolute paths (a drive-letter
path or a Git Bash /x/repos/ path, code spans included); and leftover
placeholders such as MERGE_HASH or a line that is only TODO. The words
Claude and Codex pass, since issues legitimately discuss Claude Code
hooks.

It replaces the wording check the last /improve run kept in a scratch
script and pasted into five issue filing and closing scripts. Issues
drafted in that run still carried local paths that had to be stripped
by hand before filing.

#### feat(tools): v2.0.30 - read native engine calls and strings as C

`d51e4302`

Research and debugging can now read the native engine behind a managed
call, not only behind a crash offset.

tools/native_decompile.py gains two ways in. --engine-method takes a
managed [EngineMethod] call (IMBAgent.GetCurrentActionType, or its
engine name get_current_action_type) and decompiles the native function
that implements it. --string decompiles the native code that uses an
attribute, file name or assert text, which is how the engine's own
parsing of action sets, monster usage and skins can be read. Every
printed function that implements engine methods is labelled with them,
and a thunk is followed to the code it jumps to.

The shipping DLL keeps almost none of the method names, so the new
tools/native_engine_methods.py joins the two halves of the engine's
binding by id: the generated enums and SetFunctionPointer pairings of
the installed AutoGenerated assemblies, and a capstone sweep of the
three native registration functions. On v1.5.3 all 2,282 methods
resolve with no mismatch. An id found on one side only is reported,
and the tool then refuses that assembly rather than answer.

Ghidra's own analysis had missed 947 of the 2,093 registered
implementation addresses, so a crash inside an engine API function
could not be decompiled. Each project is now seeded once with a
function at every registered address, named when one method owns it.

The tools are wired into the processes that need them: /research and
the researcher agent follow a call into native code, /investigate
routes native crashes and hangs, /native-crash-triage gains the
hang-dump procedure (moved out of machine-local memory), /engine-bump
warms the decompiler and reads the map check, and the review engine
lens treats a native claim with no decompile behind it as unverified.
AGENTS.md's research rule and the lookup tables name the tool.

Refs #688

#### feat(tooling): v2.0.30 - wire the graphify code graph into the workflow

`b8833474`

graphify's C# code graph is now part of the everyday workflow (#677,
ADR-012). Before a TAOM type is changed, reviewed or debugged, the
workflow lists everything that depends on it, with file and line.
/deep-review Step 1 hands the callers outside the diff to the
completeness and data-flow lenses, and /investigate, /new-feature,
/research, the feature-builder and refactoring-specialist agents and
the Codex skills run the same check. This reverses the 2026-08-18
review's decision to keep graphify unwired; ADR-010 now carries a
pointer and an amendment.

tools/graphify_taom.py is the only way to build or query the graph. It
runs a code-only extract and a clustering pass with no LLM naming into
E:\graphify\TAOM (a linked worktree gets its own graph), refuses an
output folder inside the repo, holds a lock across sessions, stamps
each build and reports when the graph is stale. A new PreToolUse gate,
check-graphify-usage.sh, denies raw graphify write and install verbs
from Bash and PowerShell while queries still pass. The session banner
now shows whether the graph is current.

Found on the way: an incremental graphify extract writes
graphify-out/cache/stat-index.json into the scanned repo even with
--out, because graphify fixes that cache's location from its first
caller. The wrapper pins an absolute GRAPHIFY_OUT, and a contamination
check refuses to stamp a build that leaked.

Tests: 38 new unit tests in tools/tests/test_graphify_taom.py, and
tools/test_hooks.sh gains section 7e plus gate rows in 4c, 4d and 5.
The gate was proven live: the harness refused a raw graphify call from
both shell tools.

#### feat(hooks): v2.0.30 - run the eight git gates for the PowerShell tool

`e046bdf0`

Eight PreToolUse gates (block-no-verify, check-claude-files-tracked,
check-commit-subject-version, block-dangerous-git, block-broad-git-add,
check-moduledata-validation, check-native-dll-crt and
check-doc-config-drift) were registered for the Bash tool only, so the
same git command run through the PowerShell tool skipped them: an
unlabelled commit, an AI co-author trailer, --no-verify, git reset
--hard and git add -A all ran unchecked. Maintainer decision 61 asked
for the gates to cover PowerShell, with each gate's parsing checked for
PowerShell syntax and test rows per shell.

settings.json now registers all nine git gates in one Bash|PowerShell
group; the old PowerShell group, which held only validate-push, is
folded into it. Registration alone was not enough: every gate parsed
its command as Bash text, and a correctly labelled PowerShell
here-string commit (git commit -m @' ... '@) was denied because the
subject read as `@`. Each gate now reads its command through a new
_pybin.sh helper, taom_hook_command, which runs _shellwords.py: a
PowerShell command comes back as the Bash text of the same command, and
in both shells a git named by a path or in capitals (GIT, git.exe,
C:\...\git.exe) comes back as `git`. If the reader fails, the gate
reads the raw command as Bash text, as before, and says so on stderr.

Other changes that ride with it:
- block-no-verify, block-dangerous-git and block-broad-git-add now try
  Python (the reader) first and jq only when there is no Python. jq
  first would never read PowerShell on the Linux CI runner, which has
  jq on PATH.
- block-broad-git-add strips quoted spans with a bash loop instead of
  forking sed once per git segment. A 100-line PowerShell commit
  payload took 5164 to 5443 ms with the sed, past the 5 s registration
  (a killed ask gate allows), and 308 to 422 ms without it.
- The two confirm gates' raw prefilter matches git in any case, so a
  payload whose only git is GIT reaches the reader.
- check-commit-subject-version reads a single word piped into
  git commit -F - (a PowerShell here-string, echo, Write-Output or
  printf) as the message, so an unlabelled piped subject is denied.
- check-claude-files-tracked also reports an untracked .py file, since
  the reader is the first Python helper under .claude/hooks.

tools/test_hooks.sh gains section 7e (registration parity, the
PowerShell prefilter, 58 verdict rows, commit-test traces and timing
rows for three large payloads under both tools) plus a PowerShell
contract payload and three section 6 rows. The suite goes from 482 to
651 passed, 0 failed. Part of plan 027.

Not-tested: a live PowerShell tool call through the real harness (owed after merge)

#### feat(hooks): v2.0.30 - read PowerShell commands as Bash text

`b23dc899`

Add .claude/hooks/_shellwords.py, a small reader that hands a tool
call's command back as POSIX-shell text. A PowerShell command comes
back as the Bash text of the same command: here-strings, '' and ""
quoting, backtick escapes and line continuations, # and <# #>
comments, { } and ( ) as statement breaks, and the & call operator.
In both shells a git named by a path or in capitals (GIT, git.exe,
C:\...\git.exe) comes back as `git` where it is the command. Text it
cannot follow (an unclosed quote, here-string or block comment) comes
back unchanged, which is how every gate read a command before.

It has two modes: posix (the command as Bash text) and segments (that
text split at ; & | and newlines outside quotes, with a # comment
dropped), and always writes UTF-8 with LF line ends.

Nothing calls it yet; the next commit wires the git gates to it.
tools/tests/test_shellwords.py pins it with 33 unit tests whose
PowerShell argument lists were read from PowerShell 7's own parser.

This is the first step of plan 027, for maintainer decision 61 (cover
the PowerShell tool in the git gates).

#### feat(troll): v2.0.30 - hill troll troop, 200 HP trolls, loc sweep

`4ceae903`

Hill troll on troll_skeleton_a: the hill_troll troop for Mordor, 428
retargeted human clips and the 52 Fab clips bound into the standalone
as_hill_troll_warrior, its Fab idles reused for the inventory,
conversation, cheer and bodyguard-pose codes, and the Brute Force tree
(#649) on both trolls with distances scaled by eye height. Both trolls
have 200 health (campaign via TaomCharacterStatsModel and the race's
baseHitPoints; Custom Battle via the Armory Monster), cost 50 of the
player's resource to recruit with 5 and 4 upkeep, and Bolgrukig,
Zarunik and Brughash field 0 to 2 of each. New gates:
wire_hill_troll_race.py --check, TrollHitPointsLiveDataTests.

Localization: stale translations re-translated across the three
modules, Lindon and Umbar keys split, per-quest fallback keys, Duinhir
keeps his title; the translator no longer seeds or translates a key
another source owns; LocalizationKeyConsistencyTests and the
writing-system scans gate the incident classes, rows and cache.

Deep review in two waves plus a convergence pass and a review of the
follow-up features; 52 findings, RCA
docs/reviews/rca-hill-troll-and-loc-sweep-2026-09-25.md. Open for Mike:
the reused idles are not cyclic, the culture template's 0 to 7 trolls,
the cave troll's commented TroopWeight row, and the recruit cost for a
Free-culture player.

Other sessions' hunks in shared files (the armory LOD pass, the
creature-bandits research, a Gondor peasant and the howdah and Animalia
troop names) are left unstaged. Language rows this session's
translator wrote for other sessions' pending keys are included.

#### feat(gondor): v2.0.30 - port KEYforce's Lamedon drop, noble ladders

`998f054c`

Port the TAOM side of KEYforce's lotraom-assets 429746b2 (#669): the
Lamedon and Ringlo Vale re-kit, the renamed banner spears, the Umbar
sword swaps and seven troop renames. troops_gondor.xml and
troops_umbar.xml are merged per slot against the mirror revision he
edited from, because a verbatim copy reverted #609, #617 and #631.
gondor_ring_peasant is deleted by his data; the save break is accepted
and recorded as a known limitation.

Noble lines now carry better kit than regular troops of their level:
taom_schema gains _NOBLE_LINE_TROOPS (92 troops, tier 2 to 7), judged
one tier up and anchored a band up (never below the item's own tier)
by the armour and melee tools, and _ARMOUR_LADDER_EXEMPT_ITEMS (18
troop and item pairs) for the fixer's hand decisions. The Ringlo
poleaxe troops fight two-handed with no shield. The live Armory holds
the mesh repoint (58 dead refs), the Gondor armour restat and the
blade restat; the commit carries the audit report and catalogue.

Deep review in three waves plus convergence; RCA in
docs/reviews/rca-keyforce-lamedon-port-2026-09-25.md. Follow-ups:
#670, #671, #672, #673.

#### feat(tools): v2.0.30 - CHANGELOG release section from the commit log

`ae85381d`

Plan 020 moves CHANGELOG.md from a hand-edited shared file to one
generated at release time. This commit adds the generator,
tools/changelog_from_commits.py (pure stdlib), and its tests.

It reads every non-merge commit since the previous v* release tag
(git describe --tags --abbrev=0 --match 'v[0-9]*'), groups the commits
whose subject carries the version label (the shape the subject hook
gates) by type in a fixed order (Features, Fixes, Balance, Data,
Performance, Refactoring, Tests, Documentation, Chores, then any other
type alphabetically), and lists the commits without the label in a
last group so nothing is lost. Each entry is the subject, the short
SHA and the body verbatim. Without --write it prints the section; with
--write it inserts it above the newest release section of CHANGELOG.md,
keeping the file's line endings.

It refuses with exit 2 on a malformed --version, an empty range, a git
failure, a section already present for the version, and a hand-written
"## " section above the release sections: only /release writes the
file from now on.

Smoke on real history, v2.0.29..v2.0.30: 57 commits, 53 with the
version label, 4 without; 57 entries in the groups Features, Fixes,
Balance, Tests, Documentation, Chores and the unlabelled group.

tools/tests/test_changelog_from_commits.py holds 22 tests (parsing,
rendering, insertion and an end-to-end run in a temp git repository),
and tools/README.md gains the tool's row.

Not-tested: a live /release run; the next real release is the first.

#### feat(release): v2.0.30 - refuse to package a dirty or off-tag DLL

`5d1b1bda`

tools/package_release.py gains --require-build REV (plan 017). It reads
the build stamp out of TAOM.dll and TAOM.Dependencies.dll in the source
Modules folder and refuses to package when a stamp carries .dirty,
nogit or .nogit, names a commit other than the one REV resolves to, or
when a DLL is missing or holds no single stamp. The check also runs
under --dry-run, so it works as a gate on any Modules folder before an
upload. Exit code 2 on refusal, like the other input errors.

13 new tests in tools/tests/test_package_release.py: 9 for the stamp
reader and checker, 4 for the CLI (skipped when git is not on PATH).
Verified against the real DLLs of the previous commit: HEAD passes,
HEAD~1 is refused for both modules.

#### feat(build): v2.0.30 - mark dirty and git-less builds in the stamp

`057628a2`

The .NET SDK appends HEAD's commit SHA to InformationalVersion whatever
the working tree holds, so a DLL built from uncommitted edits looked
like a clean build of that commit (plan 017).

A new TaomStampWorkingTreeState target in Directory.Build.props runs
after InitializeSourceControlInformation and before
AddSourceRevisionToInformationalVersion. It runs git status --porcelain
over Main, Dependencies, Stubs and the props file and appends .dirty to
SourceRevisionId when anything is modified or untracked; it writes
nogit when no repository information exists and appends .nogit when
the git command fails. About 40 ms per project build.

Verified on SDK 10.0.401 by building a dirty tree (+<sha>.dirty in both
TAOM.dll and TAOM.Dependencies.dll), a build with
EnableSourceControlManagerQueries=false (+nogit), a build with git.exe
hidden from PATH (+<sha>.nogit) and a clean tree, reading each DLL's
ProductVersion.

#### feat(tactics): v2.0.30 - wire OOB Auto-Assign to HeroAutoAssigner

`d1b2c44c`

The Order of Battle overlay's Assign Heroes button printed an
unlocalized placeholder and did nothing, while HeroAutoAssigner sat
registered in IoC and never resolved. Plan 022 wires the two together.

HeroAutoAssigner.PlanCaptains is a pure global greedy over the existing
role score: every hero and open-formation pair scoring above 0, highest
score first, ties to the lower formation index and then the earlier
hero, each hero and slot used once, Unset formations never filled.

OOBCaptainAutoAssigner is the boundary class. When the player is the
general it collects formations with a troop class and no captain, and
candidates from UnassignedHeroes and each HeroTroops list (never the
player, never a hero already leading, never a disabled item), then
applies each pick through vanilla's manual-drag path: OnHeroSelection,
then ExecuteAcceptCaptain. Public members only, no reflection. A pick
counts only when the formation's Captain is the hero afterwards.

OOBButtonsVM.ExecuteAssignCharacters now delegates to it and shows one
of three localized messages. The three taom_oob_autoassign_* keys are
registered in taom_module_strings.xml and seeded with English rows in
all 12 language files; the translator run is owed to the maintainer.
The seeding helper put the rows after </strings> in these mixed line
ending files, where LocalizedTextManager.LoadLanguage never reads them,
so the rows were moved inside <strings> by hand.

The class comments now name vanilla DeploymentFormationClass correctly
(5 is InfantryAndRanged, 6 is CavalryAndHorseArcher, 0 is Unset).

Tests: 10 PlanCaptains cells and 2 overlay delegation tests.

Not-tested: OOBCaptainAutoAssigner against a live OrderOfBattleVM (needs a mission); in-game check owed
Research: v1.5.3 OrderOfBattleVM, OrderOfBattleFormationItemVM, OrderOfBattleHeroItemVM, DeploymentFormationClass, LocalizedTextManager

#### feat(crash): v2.0.30 - put the TAOM build stamp in crash bundles

`743ce2e1`

The crash bundle's Identity section named only the SubModule.xml
version label and a TAOM.dll SHA1 that nothing maps back to a commit.
The build stamp (the assembly's InformationalVersion: build time plus
commit SHA) reached a bundle only through the [BuildStamp] line near
the top of the bundled taom_debug.log.

IdentitySnapshot gains a last field, TaomBuild, read by
BuildStampReport.ReadInformationalVersion (now internal). report.txt
prints it as a Build: line in the Identity section, manifest.txt as a
TAOM build: line, and report.json gains TaomBuild, so triage no longer
depends on the bundled log. The field goes last so the positional
fallback in CrashReportService cannot shift. The BuildStampReport
summary now credits the .NET SDK, not Bannerlord.BuildResources, with
the +<sha> suffix. Plan 017, commit A; four new tests.

Not-tested: IdentityCollector wiring (reads ModuleHelper; exercised only in game)

#### feat(nazgul): v2.0.30 - the Nine's scream is the clip Mike supplied

`4b5662b2`

The SCREAM (#645) now plays the clip Mike supplied, TAOM_Nazgul
Scream 2, as one variation (nazgul_scream.ogg) in place of the three
ElevenLabs takes. The module sound name is unchanged, so no code or
config value changes. The file is mono Ogg Vorbis at 44.1 kHz, 4.41 s,
with the peak raised to -1 dBFS before encoding and the input's
metadata tags dropped.

The old takes stay in the game install, because the module copy never
removes a file. Delete them after the next deploy and before the next
release.

Deep-reviewed (XML, data flow, completeness, design and engine lenses,
then a convergence pass): no runtime defect. The text findings are
fixed; rows W1 to W9 in docs/reviews/rca-nazgul-scream-2026-09-23.md.
Full suite in a clean worktree: 10235 passed, 2 skipped, 2 failed
(an Elk and an Animalia test that fail the same way without this
change).

#### feat(nazgul): v2.0.30 - the Nine SCREAM, a second signature (#645)

`b90fd3a4`

SignatureStrikes held one signature: top-level heroIds and races, a
binary Slam/Sweep kind and two hard-wired cooldown stamps. The config
is now a signatures list. Sauron's rows are unchanged. The Nine (the
nazgul_nine hero set and race nazghul, #644) Scream on an overhead and
both side swings: a 6 m ring centred on the wraith, 0.3 of the hit's
damage with the engine's boulder falloff, a knock-back but never a
knockdown, 25 morale before the engine's morale resistance and the
falloff, one 15 s timer shared by the three directions, and a
generated shriek at the wraith's head. A thrust or a ground hit does
nothing.

- StrikeKind.Scream, StrikeOrigin (Impact or Self) and per-kind stamps
  in StrikeKindTimes. The registry resolves a signature index at spawn
  (hero ids and hero sets before race, first listed wins, overlaps
  warned); the roster falls back to the character's own id in a
  Custom Battle.
- The struck foe now takes a strike's fear, never its ring blow.
  Before, the runner skipped it outright, so Sauron's slam never
  frightened the agent he hit.
- StrikeSoundPlayer plays LOTR/Mordor/Nazgul/nazgul_scream (three
  ElevenLabs takes, mono Ogg Vorbis) through Mission.MakeSound at the
  finiteness-gated eye position, and yells if the name does not
  resolve.
- The provider validates the list: it drops a nameless, repeated or
  identity-less signature and any strike whose kind has no cooldown,
  and reverts a bad field to that signature's compiled default.

Reviewed: /deep-review, seven lenses and a convergence pass. Two MED,
seven LOW and one design change, all fixed; RCA
docs/reviews/rca-nazgul-scream-2026-09-23.md. Full suite: 10186
passed, 2 skipped on the shared working tree, and 10099 passed, 2
skipped for this commit alone on 9804f67b in a clean worktree. Not
smoked in game.

#### feat(nazgul): v2.0.30 - the Nine become race nazghul (#644)

`9804f67b`

The Armory has shipped a nazghul race (1.18 scale, its own meshes)
that no hero used: six of the Nine carried no race in lords.xslt, so
they loaded as human, and three were race uruk. All nine lords.xslt
templates now emit race nazghul.

lord_1_48_1/2/3 were also defined by characters/lords.xml rows. The
engine merges a second definition per attribute (the later file wins)
and unions EquipmentSets with different ids, so every campaign dressed
those three in the Nazgul kit or a Mordor lord kit at random, and their
age of 20 came only from the row (the templates said 31, 9 and 11).
Mike's rule: vanilla lords live in the XSLT, new ones in lords.xml. The
rows are gone; race, age 20 and face age 22.19 moved into the
templates. The other 176 lords defined twice are #648.

Vanilla never saves a character's race, and RacePersistenceService
puts the captured race back at session launch, which would restore
human and uruk on every old save. The restore now leaves the Nine's XML
race alone (INazgulRegistry) and logs how many it kept. A Player
Switcher player's barber race edit on a wraith is reverted on load, by
design. All nine get orc shield-crush (orcShieldCrushRaces adds
nazghul, Mike's call).

New gate, CultureRaceConsistencyTests
.EveryCharacterRaceIsARealRegisteredRace: every race in ModuleData XML
and XSLT must be registered by the installed skins.xml, because
FaceGen.GetRaceOrDefault is a plain dictionary index that throws on an
unknown name.

Deep review: seven lenses and a convergence pass. One pre-existing
HIGH (the double definition), one MED (no gate on the new Armory
reference), three LOW, and stale docs and comments across eleven
files, all fixed. RCA docs/reviews/rca-nazgul-race-2026-09-23.md. Full
suite 10180 passed, 2 skipped, 0 failed.

Research: BasicCharacterObject.Deserialize, FaceGen.GetRaceOrDefault,
  MBObjectManager.MergeElementAttributes and MergeElements,
  NPCCharacters.xsd, Hero.SetInitialValuesFromCharacter,
  CharacterObject.MaxHitPoints, GetMoraleResistance and
  UpdatePlayerCharacterBodyProperties (installed v1.5.3)
Save-compat: existing saves adopt nazghul on the next load; a hero's
  equipment is saved, so the trio's single kit reaches new campaigns
Not-tested: in-game render (portraits, armour at 1.18 scale, bald and
  clean-shaven), the restore log line on an old save, Player Switcher
  into a wraith

#### feat(elephant): v2.0.30 - howdah log counts arrows each archer looses

`f4503650`

The howdah status line now carries shots=N per archer: arrows actually
loosed, counted from the engine's missile-fired callback
(Mission.OnAgentShootMissile, an [MBCallback] raised for every missile;
vanilla's archery training counts shots through the same hook). Until
now the log could not tell a shot from a re-nock, because prog latches
at 1.00 once any draw completes. Read shots against restarts: both
climbing is an archer shooting, since every shot restarts the draw;
restarts climbing while shots stays flat is the re-nock loop.

The count lives on each archer's own HowdahCrewAgentOrigin, so the
callback identifies a crew shot with one type check and never searches
a list of seats, and it goes through Interlocked because the callback's
thread is the engine's business. Nothing reaches the mahout's origin.

Full suite green: 9,982 passed, 2 skipped, 0 failed.

Research: Mission.OnAgentShootMissile ([MBCallback]),
  MissionBehavior.OnAgentShootMissile, TrainingFieldMissionController
Not-tested: no /deep-review on this change; committed at Mike's
  instruction. The callback itself needs a battle to confirm it fires.

#### feat(elephant): v2.0.30 - elephant 30% bigger, howdah rides its spine

`f8b3c0ea`

The war elephant is 1.3x its old size (Mike): taom_war_elephant's
body_length 100 to 130 in the live Armory, which the engine applies at
build to the skeleton, animations, capsules and the visible howdah.
Everything it does not scale for us derives from one constant,
ElephantConfig.AuthoredScale: the howdah's height and footprint, and the
trample reach (trigger 3 to 3.9 m, radius 4 to 5.2 m). The prefab is
authored at final size, not runtime-scaled, and a test pins body_length
to the constant so a resize without regenerating the howdah fails the
suite. The rider is not scaled, so the mahout needed nothing.

The howdah now takes its height from the elephant's spine. At 1.3x the
archers looked sunk: a probe on the spine bone showed the elephant
carries its back about 19 cm higher standing than at rest, and the
visible deck bobs about 18 cm per stride. A fixed platform can only sit
below that bob (sunk) or inside it (the deck rises through the archers:
stutter and re-nock, both seen in game). Following the spine's height
only matched the visible deck on 22 of 23 samples of a two-minute
walking battle; the 23rd was a bad bone read the plausibility band
rejected, falling back to the fixed height as designed. Bone tracking
was off since June because the floor rode inside the elephant's capsule
and shoved it; the floor now clears it by at least 0.66 m.

Also: the howdah floor squashed to about 2 cm (the same lintel body whose
raised ends stranded the mumakil's archers), the logged floor clearance
now scales the Monster capsule by the agent (it read 1.374 m for a real
0.564 m gap at 1.3x), and two docs that claimed body_length scales the
rider corrected, one of them the modding handbook.

Still open, not new: archers carried at 4 to 7 m/s sometimes abort a
draw; the earlier clean-looking battle spent more of its moving time in
the reload state. A per-seat shot counter is next.

Full suite green: 9,978 passed, 2 skipped, 0 failed.

Research: MissionObject bone idiom, Mat3, Monster.BodyCapsulePoint1/2,
  Agent.AgentScale, Skeleton.GetBoneEntitialFrameWithIndex
Not-tested: no /deep-review on this change; committed at Mike's
  instruction after in-game testing
Save-compat: none, no saved state touched

#### feat(mumakil): v2.0.30 - eight archers on the war tower (#627)

`1933508e`

Five on the main deck, two on the upper, one in the crow's nest. A clone of
the elephant's howdah crew per the one-feature-per-creature convention,
carrying its four engine rules unchanged and referencing its measured
constants from the pure helpers rather than copying them.

The scale is derived, never written down: the prefab is authored mount-local
and the platform multiplies its basis by the mount's own AgentScale every
tick, so BodyLength=300 appears nowhere in the data. The basis is
orthonormalised first, because ApplyScaleLocal multiplies an existing basis
and whether the native frame behind Agent.Frame already carries AgentScale
cannot be settled from managed code. Getting that wrong would put the crow's
nest at 41 m instead of 13.8; frameScale= in the log answers it for good.

The review added a rule the howdah never needed, because the howdah has one
deck. A crew frame under a higher deck's floor needs the human capsule's full
1.92 m of clearance: Barrier is in the engine's missile-exclusion mask but not
in its agent mask, so an archer whose head is inside its own deck is shoved
every frame and put back by its seat, which is the bow-draw failure again in a
new direction. One frame was there. The first correction then put it inside
the spacing rule's margin, so it moved twice. Headroom, containment, spacing
and the deadband margin are all gates now.

Also: a write-only rider field and its false comment deleted; the deadband's
stated derivation corrected; an engine float gated before a native SetFrame in
both creatures; the public reposition entry given its callers' recycled-index
guard; a missing-prefab error deduped per battle; the sealed-CharacterObject
ban widened from a filter naming the elephant to one naming the crew; the MCM
toggle relabelled for both beasts with its property name left alone so no
existing install is handed a fresh default.

Full suite green: 9,908 passed, 2 skipped, 0 failed.

Research: Mat3.ApplyScaleLocal/MakeUnit/Orthonormalize, Agent.Frame,
  UsableMissionObject.IsDeactivated, MountVisualCreator.AddMountMeshToAgentVisual,
  Native/monsters.xml human body_capsule
Constraint: harad_mumakil_rider ships at weight 20.0, not the 28.0 the crew
  implies; the identity test has only been proven to 20.0
Not-tested: everything that needs a battle. The ordered checklist is in
  docs/features/mumakil.md, and the reference-frame mismatch it measures first
  (tower on the skeleton, platform on the root) is unresolved by design
RCA: docs/reviews/rca-mumakil-platform-2026-09-20.md

#### feat(mumakil): v2.0.30 - price the beast packages, plan platform crew

`43dada47`

Two troops put more than one agent on the field, so their weight has to price
the package rather than the rider. The rule, written into the file: every
extra battlefield agent adds 1.0, which is what that archer would have cost
as a troop of its own.

The elephant's 10.0 was set while the howdah crew were parked, so it only
ever priced the beast and its mahout. Crew are back on at two archers, so it
is 12.0.

The mumakil had NO row at all, which meant the largest unit in the mod cost
one party slot, the same as a peasant, while fielding a beast, a rider and
(with Phase 2) eight archers. Its monster is the elephant's on paper, 500 hit
points and weight 9999, but it is 3.0x the body with a 12 m trample radius
against the elephant's 4 m. By the rule it should be 28.0. It ships at 20.0,
because that is the ceiling the weight system's identity was proven for
(WeightedFrameIdentityTests): above it a normal party-size limit can fall
under a single troop's weight, which makes the clamped single-body boundary
reachable and lets the displayed over-capacity verdict disagree with the
enforced one. So the mumakil is deliberately under-priced by about eight, and
raising it means re-deriving that identity first.

The mumakil doc's Phase 2 section said platform crew were deferred pending a
crew-versus-mount collision fix. That blocker is gone: #627 solved the slide
with floor clearance and a teleport deadband, measured at drift=0.000. The
section now carries the Blender measurements instead: the war tower is
authored at elephant scale and the engine applies 3.0x from the Horse item's
BodyLength=300, giving three decks at 9.00, 11.40 and 13.80 m with 15.0, 2.6
and 1.9 m2 standable inside the walls, and geometric ceilings of 47, 14 and 5
archers at the 0.74 m spacing rule.

Decisions recorded: the prefab is authored mount-local and scaled at runtime
from Agent.AgentScale, never with 3.0 baked in; the code is cloned into
Features/Mumakil per the one-feature-per-creature convention, referencing the
pure helpers rather than copying their measured constants; eight archers, as
five on the main deck, two on the upper and one in the crow's nest.

Research: Agent.AgentScale, Monster and Horse item scaling on installed v1.5.3
Not-tested: no runtime change in this commit beyond the two weights

#### feat(elephant): v2.0.30 - #627 howdah crew shoot from a moving elephant

`3d70ca55`

Crew spawn went back on for the first time since June. Nine in-game rounds
later the archers loose arrows from a moving elephant, and a battle can end
with crew aboard without freezing the game.

Four engine gates, in series, none of which logged anything:

1. A null Formation pins Agent.MissileRangeAdjusted at 0 (Agent.cs:764 ->
   GetMissileRangeWithHeightDifference, Agent.cs:5444-5450), and the seat
   nulled it to stop the "walk to your ground slot" order. The seat now keeps
   the formation; measured after, mr=75.2 per seat.
2. Formation.DetachUnit stamps BehaviorValueSet.DefaultDetached, which keeps
   Melee at (8,7,4,20,1) and cuts Ranged to (0.02,7,0.04,20,0.03), about a
   hundredth of Melee at every distance (HumanAIComponent.cs:770-777). A
   bow-only archer then chased a melee stance it had no weapon for. The seat
   overrides Ranged, Melee and GoToPos through OverrideBehaviorParams and
   reasserts them twice a second, since RefreshBehaviorValues re-stamps the
   set whenever the formation re-applies a movement order.
3. Their ranged behaviour walked them toward a firing position they could
   never reach: legs at 3.18 m/s with the elephant standing still. Zeroing
   MovementInputVector did nothing, because the agent AI tick runs on the
   asynchronous thread and writes it again after ours. The seat now sets a
   scripted position at the seat itself, DoNotRun and deliberately no
   NoAttack, which is the flag that would silence the bow.
4. Our own per-frame teleport was the last and largest gate. An archer
   settles about 0.10 m from its frame, the seat corrected that every frame,
   and the engine reads position deltas as real movement: 30 m/s with the
   elephant motionless and its legs stopped. Nothing finishes a bow draw at
   that speed, which is why the draw stalled at 85 percent of its action and
   re-nocked about once a second at every range for nine rounds. A 0.15 m
   deadband fixed it outright: velocity 0.00 standing, the elephant's own 5.2
   to 5.8 m/s walking, action progress 1.00, one restart in a whole battle.

The mission-end hang came from a dump, not a re-read. Two battles froze with
the tick dead at over 250 fps and memory flat; procdump -ma on the second,
still frozen, gave the stack: Mission.CheckMissionEnd to set_MissionEnded to
UsableMachine.OnMissionEnded to StandingPoint.IsDeactivated = true, whose
setter spins while (HasAIMovingTo) { MovingAgent.StopUsingGameObject(); }
(UsableMissionObject.cs:129-133). A seat registered with AddMovingAgent alone
can never clear it. The machine and the seat both empty their seats first,
and UsableMachine.Disable is guarded the same way because it is a second copy
of the same loop.

Crew count is decided by capsule spacing, not deck area: two 0.37 m capsules
closer than 0.74 m shove each other back into gate 4. The howdah interior is
1.26 by 1.48 m, measured from the mesh's wall faces rather than its deck, so
it holds two archers in a line. The platform also moved 0.34 m back: its
centre had been taken from the face centres of a quad grid rather than its
extents.

The crew have their own troop, harad_howdah_crew, bow and two quivers, no
melee weapon, Bow 95 on its ladder cell, hidden from the Encyclopedia.

Reviewed with all seven lenses. Their findings are applied, including a NaN
that could still reach native, a no-op SetDetachableFromFormation with the
wrong polarity, a released archer left unable to advance or defend itself,
and a deployment-phase drop where an invalid-Z WorldPosition resolves to the
ground and would have dropped every archer off the howdah.

Research: Agent, HumanAIComponent, Formation, UsableMachine,
Research: UsableMissionObject, WorldPosition, BodyFlags on installed v1.5.3
Not-tested: siege or deployment battle, campaign battle, crewed elephant
Not-tested: killed mid-battle; translations for the troop name are owed
Save-compat: no new saved field; the crew are spawned per mission

### Fixes

#### fix(tools): v2.0.30 - improve_ctl watch and args path fixes

`ac1aa578`

watch pointed at a session's subagents folder also read every
workflow below it, and a killed workflow's journal leaves its
unfinished agents running forever, so one killed run made the watch
exit 1 for good. It no longer looks inside a folder named workflows
below --dir; a workflow is watched by pointing --dir at its own
folder.

args now refuses a --run-root, --scratch, --tmp or TAOM_IMPROVE_ROOT
that is not absolute (exit 2). Git Bash drops the backslashes of an
unquoted Windows path, leaving a drive-relative one that args used to
resolve against the current folder without a word.

Tests: a session folder with a stale workflow agent below it exits 0
and lists only its direct spawn; a tool_result for another tool call
leaves an ask pending (a mutant that cleared the ask on any
tool_result survived the suite before); each relative path is
refused.

#### fix(tools): v2.0.30 - integrate_branch dry run applies the union rule

`d24aff34`

--dry-run listed every append-only conflict as a union, so it reported
no hand resolution where the real merge stops with exit 2 (both sides
appended, and one also edited a line they share). It now runs git
merge-tree in diff3 style and tries the union on each append-only
path's merged blob, the same rule the merge applies to the file.

A git failure after git merge started (a locked index during git add,
say) exited 1, which promises that no merge started, and left the
merge in progress. It now exits 3 with the error and a note that the
merge is in progress, so exit 3 always means: read git status first.

Tests: a dry run over the edited-and-appended case exits 2; a failing
git add after the merge exits 3 with MERGE_HEAD still set; a heading
after a closed code fence gets its blank line (a mutant that never
closes a fence survived the suite before).

#### fix(improve): v2.0.30 - workflows report stops and never guess CLEAN

`bc160ca8`

review.js: the lead can return BLOCKED and a fix pass reports DONE or
BLOCKED. A lead or fix pass that stopped ends the item BLOCKED, and
one that committed nothing ends it FAILED, before a convergence round
reviews nothing. A lead verdict other than READY FOR COMMIT, and a
DEFECTS verdict with no findings listed, go to residual instead of
reading as CLEAN; the second no longer starts a fix pass with nothing
to fix. Convergence runs at max effort like deep-review's reviewers,
and a CRITICAL line in the Standards lens asks the lead to raise
deep-review's adversarial escalation for the orchestrator. An item is
num plus tag, so a tagged second review gets its own labels and a
repeated item is refused; an inherited key such as constructor is no
longer a lens id.

plans.js gives a safety-gate plan two cold reviews by default, keeps
every round's unfixed items, and never reads a review without its
lists as clean. fanout.js refuses a checker selection whose match is
not a list of objects or whose field no item schema declares as an
array, counts a result without that array as a failure, and keeps
each row's attribution. execute.js and draft-issues.js refuse a
repeated num; review, plans and execute results survive one item's
exception. Each script's header lists its args and item fields and
how top-level fields ride in an items object.

The tests now run every role on its own model id, reach the pool
bound with three items, cover each dead-agent path, check the scripts
are printable ASCII, and stub parallel() as the runtime has it. The
reviewers' mutation check catches all 26 of its mutants, up from 10.

#### fix(tools): v2.0.30 - improve_ctl watch catches hook-ask stalls

`d6742dd9`

watch now flags STALLED-ASK when an agent transcript's latest
PreToolUse hook ask has no later tool_result for that tool call: the
stall that held two review agents for 9.4 and 4.5 hours, which no age
limit tells apart from a long build. It works for direct spawns,
which have no journal. STALE applies to running workflow agents past
--stale-min, now 30 minutes by default, and an agent whose journal
key was started again is superseded instead of running forever.
Replayed on the three recorded stalls it flags each; on the live
session folder it raised no false ask.

args: an items object's fields stand unless a flag names them (pool,
model and maxRounds included), review items keep their own lenses
beside the computed ones, a review range that changes no file is
refused, and every revision is checked with --end-of-options (a base
of --output=<file> made git diff write that file). codex-prompt keeps
every STOP condition instead of the first six and checks its
revisions the same way. An unexpected error exits 3 with one line.
New tests cover the Status column anywhere in the header, a row
listed twice, 12 against 120, a dash or a path in an issue title and
a missing blank header line.

#### fix(tools): v2.0.30 - integrate_branch stages the merge, never commits

`459b63ad`

integrate_branch.py no longer commits. After a clean resolution it
stages the merge and prints the git commit -F line for the
orchestrator to run through Bash, so every PreToolUse commit gate
judges the merge commit; a commit made from Python bypassed them all.

The append-only union now reads the diff3 base: each side of a block
must start with the base lines, and the result is ours followed by
what theirs adds after them. A side that edits a shared line, such as
a fixed lesson line, goes to hand resolution; before, both versions
of the line were kept. The leftover-marker check runs on every merge,
also when another path needs a hand. The tool refuses a merge already
in progress (a stale one was committed under the new message), checks
that git left the named branch in progress, refuses a revision that
is not a commit (a leading hyphen was read as a git option), and runs
check_public_text.py on the message file before merging. A star in an
append-only glob stays inside one folder, a heading-shaped line in a
code fence gets no blank line, and an unexpected error exits 3 with
one line instead of a traceback read as exit 1.

#### fix(tools): v2.0.30 - public-text gate sees every local path form

`086abc57`

The public-text gate missed drive paths written with a forward slash
and every Git Bash drive path outside a repos folder (the home and
Steam folders among them), so a commit message or an issue body could
leak them. The local-path rule now takes a drive letter followed by
either slash, not inside a word and not a URL scheme, and a Git Bash
drive path not preceded by a word character, a dot, a slash or a
hyphen: URLs and PowerShell drives such as Env:\TEMP still pass. The
placeholder rule skips code spans, so a constant quoted from the code
is no longer a leftover placeholder. New tests cover each path form,
the allowed look-alikes and findings in line order across rules.

#### fix(hooks): v2.0.30 - deny commits over an untracked .js harness file

`e4c43e59`

check-claude-files-tracked.sh denies a commit while a file under
.claude/skills, agents, rules or hooks is untracked or gitignored, but
its extension filter named only md, sh, py, json and yaml. The /improve
skill now ships its Workflow scripts as .js files under
.claude/skills/improve/workflows, and an untracked one would have
passed the gate and never shipped. The filter now names js as well.

tools/test_hooks.sh 7b gains a row: an untracked .js file under
.claude/skills must be denied by name. It fails on the old filter and
passes on the new one.

#### fix(hooks): v2.0.30 - convergence fixes for the push guard work

`d01bddf1`

The convergence review of fff6d66e and 7b342ed5 verified every
review fix by mutating it and found two accuracy defects:

- tools/test_hooks.sh 7j checked only settings.json, while the docs
  said it covered every registration; a relative path in a skill's
  frontmatter hook passed the suite. It now also reads the skill and
  agent frontmatter command lines and requires CLAUDE_PROJECT_DIR
  there. A relative /freeze registration fails the row.
- Three places still said hooks always run in the main tree: the
  block-no-verify.sh comment, the git lookup in _pushjudge.py and
  the catalog's known gaps. A hook runs in the session's directory,
  which is the main tree unless an earlier Bash cd moved it (#690).

Also: the tool_input offset is 444 to 449 (0-based), the catalog
says which registration form each surface uses, and the reader's
test class is renamed after push_candidates (#680, #690).

#### fix(hooks): v2.0.30 - anchor hook registrations on the project dir

`7b342ed5`

All 27 hook registrations in .claude/settings.json named their
script by a relative path, .claude/hooks/<name>.sh. Claude Code runs
a hook in the session's current directory, and a Bash cd into a
project subdirectory persists across calls, so after one every hook
failed to start ("No such file or directory"), which the harness
treats as a non-blocking error. Every call then ran ungated: no
force-push guard, no commit gates, no confirm prompts. The #680
review proved it live: a trunk force push reached git after cd tools.

Each registration now names its script as
"$CLAUDE_PROJECT_DIR"/.claude/hooks/<name>.sh, as the /freeze and
/investigate frontmatter hooks already did. The hooks that read
project files already cd to the project root first, so no hook code
changes. tools/test_hooks.sh 7j fails on any unanchored registration
and runs validate-push's registered command through bash -c from
tools/ and .claude/hooks/, where it must still refuse a trunk force
push; before this change it answered rc 127 there. The hooks catalog
no longer says hooks always run in the main tree, and
hook-authoring.md and a lesson record the rule (#690).

#### fix(hooks): v2.0.30 - review fixes for the push guard work

`fff6d66e`

Fixes from the four-lens review of the #680, #681 and #689 work
(74942227..343c6f48).

Judge (#680, #689): a value option is now also read by the prefix
rule the force options use, on top of the old table, so the judge
never refuses less. git runs --e as --exec, so -f --e
git-receive-pack origin on a trunk checkout force-pushed the trunk
and is now refused. The ambiguous --rec counts as a value option
(git refuses it, so this only refuses more), and --end-of-options
takes no value.

No verdict: a payload that is no JSON object makes _shellwords.py
verdict print unread, so validate-push answers it from the raw text
(an ask on a force marker) instead of a silent allow. The raw scan
looks for the "tool_input": key in the first 2 KB only: the old
search was quadratic in the key's offset (17.7 s for a key after
256 KB), and with the key escaped a command ending in "tool_input
cut the scan past itself. --m is a force marker, so a mirror push
spelled by a prefix asks. The judge's budget is capped at 3.0 s,
since EPOCHREALTIME is the wall clock and can be set back. The push
mode and push_lines are deleted: only tests called them.

Registration: validate-push's timeout goes from 5 s to 10 s by
maintainer decision. The 3.0 s internal budget stays, so an overrun
still asks after about 3.3 s and the registration is only a
backstop.

Tests: a portable no-Python row (a sandbox _pybin.sh ending in
PYBIN="") now fails the review's mutant H10, which had passed the
whole suite. The bytecode test runs a fresh copy of the reader.
Every scratch repository sets push.default in its local config, so
a global push.default=matching no longer fails correct rows. 7h
sizes the validate-push rows by payload length and adds a 65,580
byte staged name list for the three commit gates (#681); a right
answer that misses a time bar runs once more, and 7i holds each row
to an explicit 4 s. 4e reads comments and the quoted bodies a shell
runs, takes any delimiter word, flags an operator that ends a line,
and fails a registration that names a script it cannot find (#681).

Docs: an unforced delete is not refused; --force-w force-updates
only the trunk it names, and counting a --force-if-includes prefix
is a deliberate over-block; the 250 KB overrun happened under load;
the here-string window is a document of 65,537 to 65,664 bytes;
str.lower() differs from bash on nothing under Python 3.14.
hook-authoring.md is back under its 12,288 B cap, its incident
narratives now pointers to their lessons.

A differential sweep against 343c6f48 (1,768 commands, 8,840 runs
per hook) found no push refused before and allowed now; its 30
exit-code differences are all value option prefixes now refused.

#### fix(hooks): v2.0.30 - scope the coarse push check to the command

`343c6f48`

When validate-push gets no verdict from its judge (no safe Python, or
the judge failed), it falls back to a coarse check: it asks the user to
confirm a push whose payload holds a force marker and allows the rest
with a note. That check scanned the whole payload, and the session_id
and transcript_path Claude Code sends before the command hold a UUID
that usually matches the marker, so in that state most pushes asked, a
plain one included (16 of 20 harness-shaped payloads in a probe, found
in the #680 review).

The check now scans the payload from its "tool_input" key on, with one
bash parameter expansion: no fork and no here-string, about 30 ms on a
2 MB payload. With no such key it scans the whole payload as before,
and a key written with a \u escape still asks through the \u rule. A
force push still asks.

tools/test_hooks.sh 7e sends a plain push and a force push in a
harness-shaped payload to the hook with no reader: the plain push is
allowed with the stderr note and the force push asks. The hooks
catalog describes the scope, and records that a mirror push spelled by
a prefix (--mir, #689) holds no marker, so this fallback allows it.

#### fix(hooks): v2.0.30 - refuse abbreviated force options, matching pushes

`fd1d5c17`

The force-push guard let through pushes that git 2.55 runs as a forced
update of both trunks (#689). git reads an unambiguous prefix of a long
option as that option, so --force-w and --force-with ran as
--force-with-lease, --mir and --m as --mirror, and -f --al and -f --b
as a forced --all, while the guard matched only the full spellings. The
refspec : with a force flag, or written +:, pushes every branch the
remote also has, and so does a forced push with no refspec under
push.default=matching; the guard read both as a push of the current
branch.

The judge now compares a long option's name before any = as a prefix of
--force, --force-with-lease, --force-if-includes, --mirror, --all and
--branches, from 3 characters. git refuses an ambiguous prefix such as
--f or --forc, so counting one as force only refuses a command git
refuses too. A forced : or +:, and a forced push with no refspec under
push.default=matching, are refused as a push of every matching branch.
The setting is read from -c push.default=matching between git and push
(git rejects the glued -cpush.default spelling) and, at most once a run
and only for a forced push with no refspec, from git config in the
hook's directory. Each change only refuses more: a plain push of :, a
push without force under matching, and --follow-tags stay allowed.

tools/tests/test_pushjudge.py adds the #689 shapes under both shell
tools, the lazy config lookup and a command-line row in a repository
set to matching; three pins that recorded the old answers for these
shapes move into the new table. tools/test_hooks.sh 7c adds the same
shapes on a feature checkout and in one configured for matching.
A differential sweep against 60d85ed7 (1,648 commands, 555 of them
seeded from these rules; 8,240 runs per hook under both shell tools,
on a trunk, a feature checkout and one set to matching) found no push
the old judge refused and the new one allows. Its 1,027 exit-code
differences are pushes allowed before and refused now, and its 275
message-only ones were refused both times and now name every matching
branch or every branch; each is one of the shapes above.

The hooks catalog lists what the guard now refuses and the gaps left
(push.default given through --config-env or GIT_CONFIG_*, upstream
mode, remote.<name>.push and remote.<name>.mirror), and the tooling
lessons record git's option-prefix grammar.

#### fix(hooks): v2.0.30 - judge force pushes in Python within a deadline

`a4ba6b0b`

validate-push judged each candidate line in bash at about 25
microseconds a word, so a force push to a trunk carrying 250 KB or
more of quoted text holding "push" outran the hook's 5 s registration.
The harness kills an overrunning hook, and a killed gate allows, so
such a push ran unjudged; at 2 MB every shape did (#680).

The judge now runs in Python, in the same start as the command reader:
.claude/hooks/_pushjudge.py, called through a new verdict mode of
_shellwords.py that takes the protected branch names as arguments and
prints block, warn or allow. It ports the bash judge and reads every
word as bash did: split at space, tab and newline only, git recognised
after lowering A-Z only, ASCII digits for an fd number, NUL dropped.
One difference is designed: a refspec pattern holding ?, [, ], \, (, )
or | beside * is taken to match every protected branch, which can only
refuse more, since git rejects such a refspec. The bash judge is
deleted, not kept as a fallback.

The hook gives the judge what is left of 3.0 s from its first line, so
an overrun, or a slow interpreter probe, now asks the user to confirm
instead of being killed. With no verdict at all (no Python, or the
judge failed) it asks when the raw payload holds a force marker and
allows anything else with a note; the payload's session id usually
holds such a marker, so in that rare state most pushes ask. Deleting
a trunk is still allowed, and a plain push to one still only warns.

tools/tests/test_pushjudge.py pins every 7c command with the verdict
and target the bash judge gave, and 99 lines where a natural Python
choice would read differently. tools/test_hooks.sh gains 7i: shapes A,
D and E from 250 KB to 2 MB under both shell tools must be refused
inside 4 s (the old hook took 4.4 s at 400 KB and was still running at
10 s on every 2 MB shape; the new one answers in under 1 s), and three
overrun rows must ask. The 7e NOREADER row now expects the coarse ask
where it expected rc 2, deliberately, since the bash judge it relied
on is gone; validate-push moves to 7e's own list and is checked for
its verdict call. A differential sweep against 74942227 (1,035
commands, 4,140 runs per hook, both tools, a trunk and a feature
checkout) found no push the old hook refused and the new one allows.

The session-start banner, hooks-catalog.md and hook-authoring.md
describe the new design, and the tooling lessons record the port's
traps.

#### fix(hooks): v2.0.30 - split hook text without here-strings

`143f0fa8`

Git Bash 5.3 on the desktop hangs forever on a here-string or
here-document whose text is 65,537 to 65,664 bytes: below that it
writes into a pipe, above it into a temp file, and in between it
blocks writing into its own pipe. Ten hook sites fed a command or a
git name list through a here-string, so a force push to a trunk, a
reset --hard or a broad add padded into that window hung
validate-push, block-dangerous-git and block-broad-git-add until the
harness killed them, and a killed gate allows the command (#681).

Each site now splits its text into an array under set -f, with IFS
restored on the same line, and loops over it; validate-push splits
its tokens the same way. There is no fork, and the loop runs in the
current shell as before. Empty lines are dropped, which changes no
verdict at any site. Process substitution, the fix the issue first
proposed, was measured and rejected: it forks on every call and
reads a pipe a byte at a time, 2.2 s on a 400 KB command.

tools/test_hooks.sh gains 4e, which refuses a here-string or an
expanding here-document in any script a hook registration runs, and
7h, which sends payloads sized into the window through the gates
and requires each answer inside 80% of its registration. The rule
and a lesson are recorded in hook-authoring.md and the tooling
lessons.

#### fix(hooks): v2.0.30 - keep the push line hint linear on dash runs

`d2fa6d08`

A third review of dad9b169 found no verdict change (0 exit-code
differences in 2,880 hook runs per ref) and one regression it
introduced: FORCE_HINT (-\S*f) backtracked from every dash of a run,
so a commit message of 40 to 72 KB of dashes took validate-push to
2.2 to 14.4 s, past its 5 s registration, where a killed gate fails
open. It is now -[^\s-]*f, which matches exactly where the old
pattern did (0 disagreements on 100,000 random strings) and runs in
linear time: 72 KB of dashes takes 405 ms from the PowerShell tool.

The review's doc slips are corrected: the hooks catalog recount
(graphify added two registrations, the fold removed two), the open
timing windows and their measured bounds, the cost of the hint's
over-matching on prose such as trade-off or C++, a test comment and a
figure in the new lesson. The report records the third review and a
Git Bash 5.3 here-string hang at about 64 KB that predates plan 027;
the maintainer chose to ship and track the open timing items.

#### fix(hooks): v2.0.30 - judge the push lines that could force first

`dad9b169`

A second convergence pass on 5f256f70 and the plan 027 merge found
six LOW defects and no verdict change (5,640 reader cases, 1,280 hook
runs). One was a slowdown the shortest-first sort introduced: a long
refused push line now waited behind shorter long commit messages
holding push, and passed the 5 s registration at about 250 KB (a
killed gate fails open), where the order before judged it first.

_shellwords.py now puts lines that could force (a short option
holding f, --force*, --mirror, a +refspec) before lines that cannot,
shortest first within each group. It is an ordering hint only: every
line is still judged. A 250 KB refused push line followed by two
shorter messages went from 6,287 ms back to 3,351 ms, and a long
message before a short force push stays fast (400 KB: 418 ms, base
3,733 ms).

The merge's docs and tests are brought in line with the review:
- hooks-catalog.md: why the registration count changed, the 4c and 4d
  counts with the graphify gate, and its row as an allow-list that
  answers ask on an overrun.
- test_hooks.sh 7f requires one registration per tool; the 7e own
  list is for shell hooks that are not git gates.
- The deep-review report and REVIEW-LOG record the second convergence
  and keep one window open for the maintainer: 300 KB or more inside
  the push command itself is still slower than before plan 027.
- One lesson on reordering work in front of an early exit.

#### fix(hooks): v2.0.30 - judge the shortest push line first

`5f256f70`

The plan 027 convergence pass left one timing window open: on
git commit -m "<long text holding push>" && git push --force to a
trunk, validate-push judged the long commit line first and read every
word of the message as a refspec. At 400 KB that took 5.2 s of the 5 s
registration, and a killed gate fails open; base finished in 3.6 s.

_shellwords.py push now returns its lines shortest first. The gate
stops at the first refused line, so the short force push is judged
before the long message. Any line still blocks; only the stop comes
earlier. The same payload now takes 294 ms at 400 KB and 358 ms at
800 KB (base 3,588 ms and 7,264 ms), refused in every run.

Unit test test_shortest_line_first, red first. Reader tests 59 OK.

#### fix(hooks): v2.0.30 - convergence fixes for plan 027

`747b6dae`

The convergence pass on the plan 027 review fixes found four defects,
all confirmed against the code:

- validate-push dropped a push when its quote-blind split cut inside
  a quoted value (X="a;b #c" git push --force ...) after a line that
  left a quote open: refused at 96afb6fb, allowed after the review
  fixes. A # comment is now dropped only when no quote occurs anywhere
  earlier in the text, not just in its own piece.
- The argument-boundary pass ran a quadratic shlex split on every
  quoted segment holding push; it now runs on segments up to 4 KB.
- $null =git, [int] $x = git, $a.b=git, $a[0]=git and $x, $y = git
  hid git from the gates; the reader now reads an assignment's left
  side as PowerShell's parser does.
- A 7c comment claimed JSON escapes the rows did not send; both
  spellings are now sent.

A 612-shape base-versus-fixed sweep under both tool names loses only
the named trailing-comment relaxation. Reader tests 58 OK, hook suite
798 passed, TAOM.Tests 10767 passed, 0 failed. The report gains a
Convergence section, the RCA rows 16 to 19, and one lesson.

#### fix(hooks): v2.0.30 - review fixes for plan 027

`64b9f3a6`

The deep review and Codex pass on plan 027 (96afb6fb..05dbc0d4) found
validate-push refusing less than at base in eight shapes, PowerShell
statement forms that hid git from three gates, invented commit text
for piped producers, and timing near the 5 s registration.

validate-push now reads every candidate split from one
_shellwords.py push run and judges positionals with and without the
option value skip. The reader handles assignments, the . operator,
${name}, typographic quotes and value statements. Report, RCA, Codex
prompt, lessons and catalog updates are included.

This is the state the convergence pass reviewed; its fixes follow in
the next commit.

#### fix(hooks): v2.0.30 - leave an interrupted build or test unmarked

`cfd3b48f`

mark-verification-run.sh touches .claude/logs/.verification-ran after
a dotnet build, dotnet test or build.ps1 call, on PostToolUse and
PostToolUseFailure alike, which mutes the Stop hook's "you edited C#
and never verified" reminder. A failed build still counts, since its
output is evidence. An aborted call does not: the command never
finished and there is no result.

Evidence, read from the installed Claude Code 2.1.241 binary: the
PostToolUseFailure input schema carries an optional is_interrupt
boolean beside tool_name, tool_input, tool_use_id, error and
duration_ms, and the call site sets it when the thrown error is an
abort or cancel error, or when the call's abort signal fired with the
server-fallback reason. So a payload with "is_interrupt": true now
exits before marking. If the field is never true in practice nothing
changes; if it is, the only effect is one more Stop reminder.

UNVERIFIED: whether a timed-out command sets is_interrupt. The shell
path appends "Command timed out after ..." to stderr, and nothing read
so far shows it also counts as an abort, so a timeout still marks.

The hook's split is now _shellwords.py segments, shared with
validate-push.sh, so a PowerShell command splits as its Bash twin: a
dotnet test inside a script block marks, a here-string that only holds
the words does not, and a trailing # comment is dropped. The first
word is read without quotes, so PowerShell's .\build.ps1 still marks.
tools/test_hooks.sh 7d gains 7 rows; the suite is at 705 passed, 0
failed. Part of plan 027.

Not-tested: a live interrupted tool call through the real harness (owed after merge)

#### fix(hooks): v2.0.30 - close validate-push glob, option and comment gaps

`3a061406`

validate-push.sh is the only force-push guard. The plan 011 final
convergence review found shapes it still let through in either shell,
and one false refusal. It now:
- judges a pattern refspec (refs/heads/*, +refs/heads/*:refs/heads/*,
  refs/heads/bannerlord-*) as every protected name it matches. The
  plan 011 probe against a scratch bare repo showed each glob shape
  really force-updating the remote's branches.
- reads a destination heads/<name> as <name>, as git does.
- never takes the value of -o, --push-option, --repo, --receive-pack
  or --exec for the remote. Run on a trunk, git push --force -o
  ci.skip origin passed because ci.skip was read as the remote.
- sees git named by a path or in capitals (GIT, git.exe, a Windows
  path), glued to a brace, or inside a bash backtick substitution.
- reads PowerShell through the shared _shellwords.py reader: script
  blocks, the & call operator, backtick escapes and continuations.
- ignores a trunk named only in a trailing # comment, in both shells
  (git push --force origin feature # <trunk> later was refused).

The quote-aware split is now _shellwords.py segments, shared with
mark-verification-run.sh. The quote-blind split reads the reader's
text and the raw command both, so reading PowerShell never makes the
gate refuse less than before: a Start-Process argument list, & ("git")
push and a refspec in parentheses stay refused. It cuts at ; & |
before it drops a # comment, per segment, so a # inside a quoted value
cannot hide a later push on the same line.

Maintainer decisions 30 and 60 still hold: the gate is force-push only
and protects exactly master, main, bannerlord-1.4.5 and
bannerlord-1.5.x. tools/test_hooks.sh 7c gains 47 rows, among them
eleven that pin shapes refused before this change. The suite is at
698 passed, 0 failed. Part of plan 027.

Not-tested: a live PowerShell tool call through the real harness (owed after merge)

#### fix(changelog): v2.0.30 - final convergence fixes for plan 020

`5b242cee`

A second convergence review of 971d8e97 found four LOW defects; none
changes runtime behaviour.

- The generator docstring now says /release leaves no commit that
  lands mid-release out of every section. It used to say such a commit
  falls into the next section, which holds only after the release
  commit.
- The D1 parent check now reaches the C4 lesson's Prevent line, the
  RCA's C4 row and the report's C4 resolution. The RCA gains rows for
  D1, D2 and E1 to E4.
- The first convergence claimed no edited file is read by scan.sh;
  scan_skills and scan_hooks read release/SKILL.md and
  session-start.sh. The sentence is corrected.
- test_hooks.sh 5b2 selects any non-comment taom_pybin_degraded call,
  skips it only when jq is the third argument, and fails when it
  selects nothing. RED: with check-doc-config-drift's call rewritten to
  the if form and its name dropped from the banner, the old block
  passed and the new one failed that gate.

module-taom.md's release step now uses the labelled release subject and
tags the release commit by SHA, matching release-process.md.

Tests: test_hooks.sh 373 passed, 0 failed; generator tests 33 OK;
lint_docs --fail-on-drift exit 0; dotnet test 10629 passed, 0 failed,
2 skipped.

#### fix(hooks): v2.0.30 - final convergence fixes for plan 011

`764ab098`

validate-push.sh anchored on the first `push` word and returned when no
`git` preceded it. An apostrophe in a comment or heredoc line opens a
quote that never closes, so the quoted split glued the next line into
that segment, and a following `git -C "E:/R&D/TAOM" push --force origin
bannerlord-1.5.x` passed (rc 0) under both tools; the jq-only fallback
failed the same way. The hook now anchors on the first `push` token with
a `git` token before it.

The current branch is resolved once per run and a segment already judged
is skipped: 100 no-refspec push lines took 7.9 s against the 5 s
registration, and a killed gate fails open. They now take about 0.4 s.

7c gains the two defect rows, a tool-tagged table that pins each shell's
quote escape (a single-escape mutant passed every earlier row), and a
timing row. The over-block after an unclosed quote stays by decision:
ending a quote at a newline would let a quoted value spanning lines cut
git from push. Corpus parity over 596 payloads under both tool names:
only the defect payloads change, rc 0 to 2. Comments, the hooks catalog
row, the review record and CHANGELOG follow; the out-of-range probe
findings move to plan 027.

#### fix(seams): v2.0.30 - convergence fixes for plan 026

`aeb04bc4`

The convergence pass on 12b1c200 found no code or test defect and two
LOW accuracy errors in the review records, both fixed:

- CHANGELOG: the fix entry pointed to the warden promotion smoke
  "above"; it is in the refactor entry below.
- RCA top-line: the seven findings now count P1 (the missing issue)
  and P2 as one doc gap across two feature docs, matching the table.
  The report's STANDARDS line now names Agent 1's notes as its own
  section does (note 1 is S1 and S2, note 2 is S3).

The report gains a Convergence section. Docs only; full suite
10707 passed, 2 skipped, 0 failed.

#### fix(release): v2.0.30 - prune only what the tag and its build own

`3c747f4f`

The final convergence pass found that the D1 fix to the /release
Phase 8 orphan-removal step would delete files the repo cannot
recreate and still ship stale binaries.

- TAOM.Dependencies outside bin/ is no longer pruned. Its MCM UI
  assets (55 files: AssetPackages, EmAssetPackages, GUI and the two
  ModuleData language trees) exist in the install only. The TAOM
  rule now leaves RuntimeDataCache to the packager. D1's premise is
  corrected in the review report.
- bin/ keeps the names the tag tracks under _Module/bin/ (2 and 44)
  plus what the build writes, taken from each project's
  project.assets.json runtime assets, and removes the rest. The old
  text said bin/ holds only untracked DLLs.
- Directory.Build.props: the stamp comment also names assume-unchanged
  paths as not counting.

A read-only walk of the live install under the new rule: 12 TAOM
leftovers outside bin/ (115 RuntimeDataCache files left alone) and 3
retired BehaviorTree DLLs in TAOM/bin/; nothing in TAOM.Dependencies.
The RCA gains rows for convergence findings D1 to D3 and D-A to D-C,
and the review report a Final convergence section.

Tests: test_package_release.py 57 OK; TAOM.Tests 10317 passed,
2 skipped, 0 failed; lint_docs --fail-on-drift exit 0.

#### fix(seams): v2.0.30 - review follow-ups for plan 026

`12b1c200`

The six-lens deep review and the Codex pass on the seam refactor found
no reachable behaviour change. This fixes the five small defects they
confirmed and applies two behaviour-preserving improvements:

- The refuge's per-row peace war check keeps the prisoner when the
  refuge or its faction is missing, as the old walk did; it answered
  "release", which only the start-of-walk count guard prevented.
- The promotion's rename and enrol seams act on the hero the create
  seam returns (MintedHero: id plus opaque handle), not on a second
  lookup by id.
- The eight pure FortificationSearch tests move to the untagged
  FortificationSearchTests, so hosted CI runs them.
- The raid scan sizes its candidate list up front.
- Two doc comments say a dropped prison row's captor can be none; the
  Supply Lines and Field Camp docs name the new tests and types.
- New tests pin the winner-only name render and the hero hand-off.

Report: docs/reviews/deep-review-026-seam-decision-logic-2026-09-24.md
RCA: docs/reviews/rca-seam-decision-logic-2026-09-24.md
Lessons appended to gamemodels-services, testing-qa and misc; Codex
review logged in REVIEW-LOG.md.

Not-tested: the changed seam bodies (the refuge war check, the raid
scan's list size, and the mint create, rename and enrol) run only in
game; the warden promotion and raid smokes cover them. Full suite:
10707 passed, 2 skipped, 0 failed.

#### fix(build): v2.0.30 - untracked files always mark the stamp dirty

`9cedb92d`

Deep review findings 6 and 7 for plan 017. The stamp's git status
honoured a user's status.showUntrackedFiles=no, so a new untracked
source file could build a clean-looking stamp, and the pathspec left
out GameReferences.targets, which the projects import on trunk.

The orchestrator made the Directory.Build.props edit under the bypass
granted for plan 017: git status now passes --untracked-files=normal
and GameReferences.targets joins the pathspec, with two comment lines
saying so. The release skill Gotchas and the CHANGELOG name the new
path, and the CHANGELOG drops its known-limitation bullet.

Probe, with status.showUntrackedFiles=no injected through
GIT_CONFIG_COUNT and the props edit masked by assume-unchanged: an
untracked Main/_probe017_untracked.cs built
build.20260925-185259Z+d5033e4e897f3a0500ec5808ba495d8217211c42.dirty;
with the file deleted the stamp had no suffix. The review report
records both under "Orchestrator follow-ups".

test_package_release.py: 57 OK. TAOM.Tests: 10317 passed, 2 skipped,
0 failed. lint_docs.py --fail-on-drift exits 0.

#### fix(release): v2.0.30 - convergence fixes for plan 017

`d5033e4e`

The orphan-removal step in Phase 8 and release-process.md compared the
whole install with the tag's Main/_Module tree. The tag never tracks
bin/ build output, so following it literally deleted TAOM.dll and its
runtime DLLs, and TAOM.Dependencies was never compared. The step now
compares outside bin/ only and maps both module folders to their
_Module roots.

The shallow-history skip in the predates test could never fire:
rev-parse without --verify prints an unresolvable argument to stdout.
A depth-1 clone (CI's checkout) failed the test; it now resolves the
parent through resolve_commit and skips there.

SKILL.md and CHANGELOG now say the gate refuses a missing TAOM or
TAOM.Dependencies, which is what the code checks.

#### fix(hooks): v2.0.30 - record failed runs, plan 011 settings

`18961c1e`

A command that exits non-zero raises PostToolUseFailure, not
PostToolUse, so a failed build or test never touched
.verification-ran and the verification Stop reminder fired after
every red run. settings.json now registers mark-verification-run.sh
on PostToolUseFailure for Bash and PowerShell (timeout 5), the one
settings change the plan 011 review asked the orchestrator for.

tools/test_hooks.sh 7c checks that validate-push (PreToolUse) and
mark-verification-run (PostToolUse and PostToolUseFailure) are
registered for both shell tools; run against the previous
settings.json it reports the two PostToolUseFailure rows missing.
A 7d row feeds the hook a failure payload and expects a mark. The
hooks catalog is recounted (28 scripts, 29 settings.json
registrations across 9 events, 5 in skill frontmatter, 34 in
total) and its mark-verification-run row names both events. The
review report and the plan 011 CHANGELOG entry record the change;
the Codex prompt for the review is committed with them.

Verified: bash tools/test_hooks.sh 506 passed, 0 failed;
dotnet test TAOM.Tests Failed: 0, Passed: 10629, Skipped: 2;
python tools/lint_docs.py --fail-on-drift exit 0.

#### fix(docs): v2.0.30 - convergence fixes for plan 016

`35f212c6`

The convergence pass over the review-fix commit found four LOW doc
defects and two wording slips; all confirmed against the worktree.

- mcp-servers.md: scope the deny-list derivation to the pinned git and
  filesystem servers, and note serena's editing tools are also
  readOnlyHint false and not denied yet (open decision).
- mcp-servers.md: the pin rule covers the .mcp.json project servers;
  the user-level servers are machine-local and unpinned.
- INDEX.md: name .codex/config.toml among what ignores the env vars.
- LESSONS-LEARNED.md: recount 199 to 202 and 921 to 924.
- development-machines.md and REVIEW-LOG.md: wording fixes.

#### fix(changelog): v2.0.30 - convergence fixes for plan 020

`971d8e97`

The convergence review of 6e4bfedb found three defects; all confirmed.

/release now checks, before tagging, that the release commit's parent
is the commit the generator's range ended at, and stops to ask if not.
That closes the window between the Phase 6 HEAD check and the commit.
The generator docstring, the tools/README row and Phase 4 said the
range-end commit is the one tagged; it is the release commit's parent.
The Phase 7 confirmation describes the release commit, not HEAD.

test_hooks.sh 5b2 matched the gate names in a session-start.sh comment
while the banner printed short names. It now reads only non-comment
lines and selects gates by their taom_pybin_degraded call site, and the
banner prints the hook file names. Proved red on the old banner and red
when one name leaves the echo line only.

Verified: test_hooks.sh 373 passed, 0 failed; generator tests 33 OK;
lint_docs ai_dashes 0; dotnet test 10629 passed, 0 failed, 2 skipped.

#### fix(hooks): v2.0.30 - convergence fixes for plan 011

`65b405e4`

The convergence review of c80c4108 found four defects; all held.

validate-push.sh split a command at ; & | without regard to quotes,
so a quoted -C, -c or -o value holding one cut git from push: git -C
"E:/R&D/TAOM" push --force origin bannerlord-1.5.x returned rc 0,
where 43e6780e refused it. It now judges two splits and blocks when
either does: the quote-blind one (keeps bash -c "git push ...; x"
refused) and one outside quotes only, with each shell's escape. With
jq and no Python the second keeps whole lines, which over-block.

A refspec glued to its redirection (bannerlord-1.5.x>/dev/null, >&2)
was dropped whole; the text before < or > now counts unless it is an
fd number or PowerShell's *.

Three comments still called the JSON block the only Stop output
Claude reads, and the test_hooks.sh section 4 comment misdescribed
its own check; all now say what the code does. The catalog row and
the lesson drop the claim that the split only over-blocks.

7c gained eight rows, each rc 0 before the fix. test_hooks.sh: 501
passed, 0 failed; dotnet test: 10629 passed, 0 failed, 2 skipped.

#### fix(release): v2.0.30 - review follow-ups for plan 017

`db1a7166`

Deep review (seven lenses) and Codex on plan 017 (#658). The release
gate failed open four ways and read a missing .dirty flag as proof of a
clean tree; all fixed with the failing tests written first.

- package_release.py --require-build reads every bin/<platform>/ copy
  of TAOM.dll and TAOM.Dependencies.dll (the Win64 one must exist),
  casefolds module names, refuses an empty value, a requested module
  missing from --source, a rev whose Directory.Build.props predates
  the dirty-flag target, and an unreadable DLL. One stamp regex.
- Release skill Phase 8 and release-process.md: the gate proves the
  DLLs only, so compare the install with git ls-tree before packaging;
  step 1 no longer offers a worktree git refuses.
- BuildStampReport comment and test credit the .NET SDK for the SHA
  suffix; test rename; crash-report changelog line; #658 referenced.

Report: docs/reviews/deep-review-017-build-identity-dirty-flag-2026-09-24.md
RCA: docs/reviews/rca-build-identity-dirty-flag-2026-09-24.md
Deferred: git status --untracked-files=normal needs Mike to approve the Directory.Build.props edit

#### fix(docs): v2.0.30 - review follow-ups for plan 016

`1ce8c370`

Deep review (four lenses) and Codex on plan 016 (#657) confirmed ten
documentation findings, nine LOW and one NIT; all fixed here.

- Restore note uses git restore --source=b2e387db, which PowerShell
  5.1 cannot re-encode, and covers new worktrees and switching to
  bannerlord-1.4.5, which still tracks the settings file.
- The .vscode MCP example and the kingdom-voices .mcp.json snippet
  carry the pins; mcp-servers.md states the pin and deny-list rule.
- ModuleData MCP activation, the development-machines recount, the
  MCP write-tool lesson and the agent manual Build row corrected.
- README clones straight onto bannerlord-1.5.x; plan 016 CHANGELOG
  entries moved under 2026-09-25 and linked to #657.

Report:
docs/reviews/deep-review-016-repo-hygiene-pins-readme-2026-09-24.md
RCA: docs/reviews/rca-repo-hygiene-pins-readme-2026-09-24.md
Lessons: three in docs/reviews/lessons/build-tooling-workflow.md.

#### fix(changelog): v2.0.30 - review follow-ups for plan 020

`6e4bfedb`

The CHANGELOG generator and the /release flow around it, after a
six-lens deep review and a Codex adversarial review of plan 020.

- A commit body can no longer add a heading to CHANGELOG.md. Body
  lines Markdown reads as a heading are escaped, so an example
  "## v2.0.32 (" in one release's body no longer blocks the v2.0.32
  release as a duplicate. Issue references such as "#622:" at a line
  start are untouched; output for v2.0.29..v2.0.30 and for the pending
  v2.0.30 range is byte-identical to before.
- The generator's summary names the commit its range ends at.
  /release now checks HEAD is still that commit before committing and
  tags the release commit by SHA, so a commit another session lands
  mid-release goes into the next section instead of none.
- The refusal also catches a hand-written ### or #### entry above the
  releases, and says to fold such entries into the release note.
  --version with a trailing newline is refused.
- The degraded-toolchain banner at session start names all five
  python-only gates (it said four and left out the commit-label
  gate); tools/test_hooks.sh 5b2 now derives that list from the hooks.
- The completeness lens diffs CHANGELOG.md against the review base,
  so a committed hand edit is caught, not only an uncommitted one.
- Instructions that still said to update CHANGELOG by hand now say to
  write the commit body: three feature recipes, the feature-doc
  template, the commit-split routing row, new-creature-mount, two lens
  prompts, git-and-commits, the v1.5.2 owed list, and a rule that
  claimed a deleted hook enforced it. release-process.md runs in the
  skill's order (generate, then the release note), says non-merge
  commits, and links the v2.0.12 crash reports in the archive.
- Tests: 11 new generator tests, including parity between the label
  pattern and the subject gate. Five dashes on touched lines replaced.

Not changed here: elephant.md:687 (another session has uncommitted
edits to it) and the executor wrappers under plans/ (orchestrator).

Checks: generator tests 33 OK; test_hooks.sh 373 passed, 0 failed;
tools suite 1952 run, the 3 known failures only; TAOM.Tests Failed 0,
Passed 10629, Skipped 2; lint_docs --fail-on-drift exit 0.

Report: docs/reviews/deep-review-020-changelog-at-release-2026-09-24.md
RCA: docs/reviews/rca-changelog-at-release-2026-09-24.md

#### fix(hooks): v2.0.30 - review follow-ups for plan 011

`c80c4108`

Deep review (six lenses) and Codex of 43e6780e confirmed 15 defects.

validate-push.sh took the last word of a line as the refspec, so a
trunk force push with a tail (2>&1 | tail -5, && echo done, ; git
status, # note) or a second refspec passed, as did --force --all. It
now judges each command of a line, skips redirections, judges every
refspec, and refuses --all with force and --mirror. Quoted text that
reads as a trunk force push stays refused by design.

The four Stop hooks exited at the top on stop_hook_active, so a streak
Claude ended in the continuation kept its marker and muted the next
one. The guard now sits just before the block.

mark-verification-run.sh splits in Python with each shell's escape:
the bash loop took 5.4 s on 100 KB (5 s registration), marked a
backtick-escaped PowerShell mention, missed a command on a non-final
CRLF line, and stopped marking an env prefix.

test_hooks.sh 7a now checks exit status, stderr, marker clearing and
re-arming for every Stop hook; 7c and 7d gained the shapes above.
493 hook checks pass; dotnet test 10629 passed, 0 failed, 2 skipped.

Report and RCA:
docs/reviews/deep-review-011-stop-reminders-and-trunk-guard-2026-09-24.md
docs/reviews/rca-stop-reminders-and-trunk-guard-2026-09-24.md

Not-tested: live checks A and B; a failed build still does not mark
until mark-verification-run.sh is registered on PostToolUseFailure
(settings.json, listed in the report for the orchestrator).

#### fix(rules): v2.0.30 - convergence fixes for plan 021

`e809f258`

The convergence pass on d4e6273a found four LOW defects; all four were
confirmed against the worktree.

architecture.md's service rule allowed TaleWorlds access outside an
adapter only through seams, which forbade the value types ADR-007 and
the Standards lens exempt (RefugeService.Dismantle builds a TextObject
outside any seam). It now excepts the value types, as the lens does.

decompiled-code-analysis.md registered the concrete service by default
while the Phase 4 patch sample still resolved IFeatureService, which
DryIoc throws on and the sample's empty catch swallows. The sample now
resolves FeatureService, and the registration comment says the
interface line replaces the concrete one.

CHANGELOG and REVIEW-LOG now split the owed ADR text between the
orchestrator (O3, O5, O6) and Mike (O1, O2), matching the report. The
RCA's agent lists now match the report's Source column (C9 missed by
Agent 4, C6 found by Agent 6).

The report gains a Convergence section. Full suite 10,629 passed,
2 skipped, 0 failed.

#### fix(rules): v2.0.30 - review follow-ups for plan 021

`d4e6273a`

Deep review (Standards, Completeness, Data flow, Design) and Codex on
plan 021. think-before-coding.md no longer forbids the narrow hook
interface the other rules allow (Codex P2 1). The Standards lens names
both ADR-007 exceptions (value types and seams) and says TaleWorlds,
not engine. The old IHook one-liner, the always-both-interfaces Phase
4 procedure in decompiled-code-analysis.md, two audit prompts without
the seam exception, a stale engine version and an uncomputed count
are brought in line; the CHANGELOG credits the orchestrator commits.

ADR wording still inconsistent (CampaignTime listed as sealed, the
ADR-008 and ADR-002 checklists) and two decisions (private helpers in
seam bodies, decision logic in the cited seams) are listed for the
orchestrator and Mike in the report.

Full suite: 10629 passed, 2 skipped, 0 failed.

Report: docs/reviews/deep-review-021-architecture-rule-amendments-2026-09-24.md
RCA: docs/reviews/rca-architecture-rule-amendments-2026-09-24.md

#### fix(hooks): v2.0.30 - Stop reminders reach Claude, guard both trunks

`43e6780e`

Plan 011. The four Stop reminders (verification, deep review, version
tag, CHANGELOG) printed to stderr and exited 0, which Claude Code sends
to the debug log only, so none ever reached Claude. They now print a
JSON decision block through the new _stop_reminder.sh, stay silent when
stop_hook_active is true, and fire once per streak; check-deep-review
gains the streak marker it lacked.

validate-push.sh now refuses a force push to bannerlord-1.5.x as well
as bannerlord-1.4.5 (named, not bannerlord-*, per D30), judges every
line of a multi-line command instead of the first (D38), and runs for
the PowerShell tool too. mark-verification-run.sh marks the canonical
dotnet test command its env-prefix strip used to drop, and no longer
splits inside quotes (D41); it is also registered for PowerShell.
suggest-compact.sh and notify-test-results.sh are deleted with their
registrations, tests and catalog rows (D42): their stderr reached no
one. tools/test_hooks.sh 7a, 7c and 7d pin the new behaviour.

Not-tested: that Claude Code delivers a Stop hook's block reason to
Claude, and that it fires validate-push.sh for a PowerShell tool call;
both need Mike's live check in a real session.

#### fix(composition): v2.0.30 - kernel test anchors follow 009's localized reports

`0f3af957`

Merging plan 018 after plan 009: 009 now reports patch failures with
localized TextObject phase names, so 018's kernel test anchors moved
from ReportPatchFailures("...") to the TextObject form. The runner
calls themselves were already in place and in order. Full suite:
10465 passed, 0 failed.

#### fix(tactics): v2.0.30 - convergence fixes for plan 022

`1b21a214`

The convergence pass on daeb127e found no runtime defect and four
wrong statements the review fix added. Each was re-checked against
the v1.5.3 decompile or the files:

- IOOBCaptainAutoAssigner: OrderOfBattleVM can be built anywhere
  Game.Current runs, not only in a mission.
- IHeroCombatAdapter.Equipment snapshots the equipment the adapter
  was built from, not always BattleEquipment.
- Lesson and RCA: SaveConfiguration goes through SetFormationInfos,
  which writes one of four saved lists, not "_formationInfos".
- Review report: the 12 leftover LF language rows are 3 ASO rows
  (#604) and 9 taom_behavior rows (#608).

Suite: Passed 10335, Skipped 2, Failed 0.

#### fix(binding-gate): v2.0.30 - convergence fixes for plan 008

`1f504757`

The convergence pass on 37306bca..aa59f68e found one LOW and three
NIT defects; each was verified before it was fixed.

- The zero-match triage in verify-bindings Step 2 misnamed MSTest's
  discovery warnings. "Unable to load types from the test source"
  means some of the DLL's types failed to load (discovery goes on
  with the rest); a DLL that does not load at all reports "Failed to
  discover tests from assembly". The skill, the CHANGELOG bullet, the
  report's R2 fix line and the RCA's R2 row now name both.
- The R8 evidence now counts 4 RepoPaths test files (5 with this
  one), the follow-ups count 34 private TAOM.sln walkers, and the
  REVIEW-LOG's pre-decision test_hooks.sh count is 289.
- The decisions report gains a Convergence section and no longer
  says the convergence pass was not run.

Full suite: Failed 2, Passed 10245, Skipped 2 (the two known
live-Armory tests; this branch predates a39a9c86).

#### fix(specres): v2.0.30 - convergence fixes for plan 001

`0935533a`

The RCA top-line said six findings were fixed on the branch, but its
own table marks five (F2, F3, F5, F6, F7), and it filed F4 under the
plan and prose when the table lists it under Process. Correct the count
and label the findings that wait for Mike or are only recorded.

Append the convergence pass to the deep-review report. Full suite:
10318 passed, 2 skipped, 0 failed.

#### fix(cache-rebuild): v2.0.30 - convergence fixes for plan 025

`97344cf5`

The lessons recurrence note said the config-test count went from 20
to 22, but the review-fix commit removed one of those tests, so the
note now says 22 at 032481cc. The CHANGELOG credited the 41258657
provenance to the feature doc as well as the binding catalogue; only
the catalogue carries it, so the sentence is split by file.

The review report gains a Convergence section recording both
findings and the full suite run.

#### fix(tactics): v2.0.30 - review follow-ups for plan 022

`daeb127e`

Seven-lens deep review plus Codex (P2 1, P3 2) of the OOB Auto-Assign
wiring. One MEDIUM: in a siege assault vanilla spawns every agent
without a horse, but the boundary classified candidates from campaign
BattleEquipment, so a companion who owns a horse read as Cavalry and
was never placed on the foot formations a siege offers. The boundary
now passes the agent's SpawnEquipment through a new HeroCombatAdapter
overload (RED first: HeroCombatAdapterTests).

LOW fixes: tests for the boundary's early returns, the planner's
50-point fits, each result message's text and the overlay DI graph;
corrected "sealed", co-op, save, hero-troop and "companions" wording;
reflection-site labels moved to lines 60/61; the 36 seeded language
rows restored to the files' CR CR LF terminators; one stray blank line.
Planner's used sets are bool arrays (behaviour preserved).

Seven design questions are left for Mike in the report.

Report: docs/reviews/deep-review-022-order-of-battle-auto-assign-2026-09-24.md
RCA: docs/reviews/rca-order-of-battle-auto-assign-2026-09-24.md
Tests: full suite 10335 passed, 2 skipped, 0 failed
Not-tested: siege placement in game (needs a live Agent); owed check

#### fix(binding-gate): v2.0.30 - review follow-ups for plan 008

`aa59f68e`

Round-two deep review and Codex of the maintainer decisions commit
(2ca0805b..37306bca, #652). All three decisions hold; eight LOW or NIT
findings were confirmed and are fixed here:

- The hooks catalog and CHANGELOG said binding-gate.runsettings fails
  a skipped test. It fails an Assert.Inconclusive and a zero-match
  filter; an [Ignore]d test still reports Skipped (Codex P3).
- verify-bindings Step 2 triages a zero-match run by its command: on
  the Step 1 command it is a finding (DLL load or lost category).
- BindingGateRunSettingsTests uses RepoPaths.RepoPath instead of its
  own repo-root walker (2 of 2 before and after, mutation red).
- The first RCA, report and REVIEW-LOG now record that F2, D1 and the
  hook header fix lapsed with the revert, that F13 is moot and that
  there is no 1.4.5 port (both per #652), and the three earlier plan
  008 CHANGELOG headings carry #652.

Full suite: Failed 2, Passed 10245, Skipped 2, Total 10249 (the two
known live-Armory tests).

Report and RCA, both in docs/reviews/:
deep-review-008-binding-gate-no-silent-skips-decisions-2026-09-24.md
rca-binding-gate-no-silent-skips-decisions-2026-09-24.md

#### fix(specres): v2.0.30 - review follow-ups for plan 001

`8ea31505`

Six deep-review lenses and Codex (gpt-6-astra, ultra) reviewed
a39a9c86..4263535a: no CRITICAL or HIGH. Fixed here:

- CHANGELOG and feature doc: the null-local load covers a behavior
  record without the balances key (defensive; no build wrote one),
  not a save older than the feature, which has no record and never
  reaches SyncData. That load gap is written down as a known
  limitation. Balances leaked for the player's heroes, not every lord.
- Tests: fixture id caster (Castar is the display name); one
  construction path; the second test renamed ReadsNoHero, since
  Hero.MainHero throws outside a game rather than returning null;
  the fake data store records null and throws on a duplicate key,
  as the engine's BehaviorSaveData does.
- Two comments in SpecialResourcesBehavior (no code change).

Waiting for Mike: the no-record load reset (behaviour-changing) and
the GitHub issue. Full suite 10318 passed, 2 skipped, 0 failed.

Report and RCA, both in docs/reviews/:
deep-review-001-cross-campaign-singleton-resets-2026-09-24.md
rca-cross-campaign-singleton-resets-2026-09-24.md
Lessons in state-lifecycle-save, testing-qa and misc; REVIEW-LOG entry.

#### fix(cache-rebuild): v2.0.30 - review follow-ups for plan 025

`76f22e11`

Six deep-review lenses and a Codex adversarial pass (gpt-6-astra,
ultra) found no runtime defect. Seven LOW or P3 findings were
confirmed; six are fixed here, all in tests and docs:

- The permanent absence test for the deleted types is removed
  (simplicity criterion); the retired-key compatibility test stays.
- editor-cache-rebuild.md no longer says Phase 1 paths can be
  memoized for Phase 2: v1.5.3 keeps the path local and Phase 2
  pathfinds with a different cost multiplier. The unmeasured 2-3x
  claim, the stale NavigationPath dependency and the hand-kept test
  counts are gone.
- reflection-sites.md names 41258657 as the retired row's origin and
  routes any restored self-reflection to Category D.
- CHANGELOG: the two keys were in the shipped JSON from 6a80bac6 to
  b5cb3018, before any release tag.

The seventh (plan 025's Step 6 sentence contradicts its own done
check) is recorded in the RCA; the executed plan is not edited.

Report:
docs/reviews/deep-review-025-delete-unreachable-scaffolds-2026-09-24.md
RCA:
docs/reviews/rca-delete-unreachable-scaffolds-2026-09-24.md

Full suite: 10287 passed, 2 skipped, 0 failed.

#### fix(hooks): v2.0.30 - convergence fixes for plan 013

`ee1929fa`

The convergence pass on 7aa658e3..6514fdfe found three LOW defects,
each re-checked against the code and fixed.

- test_hooks.sh 4c gives validate-push.sh a `git -C <dir> push` row.
  A prefilter narrowed to `git push` passed every earlier row; with
  the new row it fails (391 passed, 1 failed on that mutant).
- The 4d header and hooks-catalog.md name the five gates 4d covers
  and the five it does not, dropping a reason the code contradicted:
  check-changelog-changed.sh denies on a command-line pathspec with
  nothing staged, and the subject gate reads SubModule.xml.
- The lesson and REVIEW-LOG say the committed suite caught neither
  gap (it failed three rows on the other mutant), and REVIEW-LOG no
  longer counts R8 as both fixed and for Mike.

Hook suite 392 passed, 0 failed. dotnet: 10235 passed, 2 failed (the
two known live-Armory tests; the branch predates a39a9c86).

#### fix(reviews): v2.0.30 - convergence fixes for plan 010

`1e989944`

The convergence pass on 2628c66c found five defects in the evidence
the plan 010 decision records state. All five were re-checked against
the code and git objects and fixed; none was a false positive.

- The records no longer say the old tests.md sentence contradicted
  all 102 class-level tags. It governed only classes whose other
  tests pass on the stubs, and nobody counted those.
- The registration check ran in no CI step before D45 either, so
  KS10 is re-graded to agree and N1 is described as adding a check.
- GameReferences.targets is cited as :9-10, the RCA quotes the lesson
  title as written, and the two known failures are traced to
  709649c3, not a39a9c86.

2628c66c's body keeps the 102 count; the report's Convergence
section supersedes it. Docs only. Full suite: Failed 2, Passed
10256, Skipped 2 (the two known live-Armory tests).

#### fix(docs): v2.0.30 - convergence fixes for plan 005

`01b97018`

The convergence pass on a0fa3cff found six LOW defects in the review
records and the checklist line; each was re-checked and all are fixed.

- The credential sweep's archive loop now finds .tar.gz and .tgz files
  at any depth through find. A seeded scratch folder proves the old
  loop missed a nested and a .tgz archive; on the real vendor folder
  it still lists exactly the three BUTR archives.
- The CHANGELOG names both reasons the first check missed the
  credential: it ran in a worktree, and grep cannot read gzip.
- The RCA gains the missing-issue finding and quotes the lesson title
  correctly; the lesson index counts are 824 and 181.
- The review report records the convergence pass, and the REVIEW-LOG
  heading says so.

Docs only. dotnet test: 10313 passed, 0 failed, 2 skipped.

#### fix(battlebalance): v2.0.30 - convergence fixes for plan 003

`ab70555d`

The convergence review of 02157b18 found three test and doc defects,
no runtime one. Each was re-checked before the fix.

The read-through test flipped all twelve settings at once. Three bools
default to true, so all three ended false and a getter wired to the
wrong one passed. It also read no getter before the edit, so a getter
that caches its first read passed. The test now reads every getter,
then edits one setting per pass on a fresh TaomSettings and checks all
twelve. Proven with two mutants run against the old and new test: the
casualty-ratios getter reading the troop-power setting, and a
first-read cache on Tier7Power. Both passed the old test and fail the
new one.

The DryIoc comment said two public constructors fail at resolve; a
probe showed the throw comes from Register, so it now names IoC.cs:127.

The coverage claim is corrected in CHANGELOG, battle-balance.md, the
testing-qa lesson and the RCA. The deep-review report gets a
Convergence section and re-accounts Agent 6 D1.

Full suite: 10323 passed, 2 skipped, 0 failed.

#### fix(docs): v2.0.30 - convergence fixes for plan 009

`aa7a5518`

The RCA summary broke the ten confirmed findings down as 4 LOW and
2 NIT; its own table has 8 LOW and 2 NIT rows. The lessons index still
said 49 Localization & UI lessons after two were appended (52). The
Harmony & IL (63) and Build, Tooling & Workflow (173) counts had
drifted earlier and are re-derived in the same edit.

Convergence section appended to the plan 009 decisions review.
Full suite: Failed 2, Passed 10256, Skipped 2, Total 10260 (the two
known live-Armory tests on a branch based before a39a9c86).

#### fix(career): v2.0.30 - convergence fixes for plan 002

`8186cc9c`

The convergence pass on 96832589 found five inaccuracies in the
docs and records it added; all five re-checked and fixed.

- career-system.md: the calculator step says float parameters (GetInt
  exists and is out of scope), and the Tests table gains the
  MutationParamsTests row (7 tests).
- testing-qa lesson and RCA: the over-constrained mutant returns the
  default, so a negative mutation is dropped, not zeroed; the plan is
  quoted from its line 329 instead of an invented phrase.
- RCA: the overflow follow-up came from Agents 1, 2, 5 and 6, not
  every lens.
- LESSONS-LEARNED index recounted: 66, 86, 823 in total.
- Deep-review report: corrected test file count (42) and a new
  Convergence section.

Docs only; full suite 10320 passed, 2 skipped, 0 failed.

#### fix(hooks): v2.0.30 - decision review follow-ups for plan 013

`6514fdfe`

Deep review (six lenses) and Codex of the D39/D40 follow-up found no
changed gate decision; the confirmed findings are in its tests and text.

- tools/test_hooks.sh 4c: the commit gates get a git -C <dir> commit
  trigger row, and the default escaped row holds neither git nor
  commit. Two planted mutants passed the committed suite and fail the
  new one. Suite: 391 passed, 0 failed.
- Twelve hook comments: the escape arm is "never skip", not "fail
  open", with the JSON reason it is safe; comments only.
- CHANGELOG: the D39/D40 before-case is now the payload the base
  actually let through, and the parity claim names what it compared.
- hooks-catalog.md: safety rests on JSON grammar, 4d covers five
  blocking gates, and the savings claim notes the description field.
- First review record: seven silent gates, the suggest-compact
  deviation from D39's text, and #661 filed after the commit.

Report:
docs/reviews/deep-review-013-bash-hook-prefilter-decisions-2026-09-24.md
RCA:
docs/reviews/rca-bash-hook-prefilter-decisions-2026-09-24.md

#### fix(crash-report): v2.0.30 - convergence fixes for plan 006

`9e8c99bd`

The convergence pass over 8c84fa20 found five comment, doc and test
defects; runtime parity held. OnUnhandled is now internal so a test
can raise it on a chosen thread: it pins that the hook passes the
service offMainThread false on the subscribing thread and true on a
worker, since the parameter defaults to the unsafe false. The Finalizer
wiring test now covers the worker direction too. Both new assertions
fail against the matching mutations.

Stale comments reworded: IsOffMainThread names the watchdog as the
open exception, the main-thread id note no longer says "mark", and the
HandleAndSwallow arity and the helper test header are current. The
config row no longer claims the AppDomain hook hands exceptions back
with master off, and the deep-review record points at the comment that
still exists.

#### fix(tests): v2.0.30 - review follow-ups for plan 010

`2628c66c`

Second deep review and Codex pass on the maintainer decisions commit
c139bc50 (D44 to D46). Codex found nothing; the six lenses confirmed
five LOW or INFO text defects, all fixed here:

- CHANGELOG: restore the convergence entry's heading, which the
  decisions commit had overwritten.
- tests.md: a class tag stays the default; a method tag is permitted,
  not required (the old sentence contradicted 102 class-level tags).
- GameReferences.targets and the first report: carry decision 44's
  1.4.8 re-check, name the tagger manifest row that would restore
  the Patch86 class tag, and record the replay's scratch roots.

Needs Mike: tag the Patch86 registration check BindingVerification
so it runs on the hosted gate.

Full suite: 2 failed (the known live-Armory tests), 10256 passed,
2 skipped.

Report and RCA, both in docs/reviews/:
deep-review-010-ci-on-hosted-windows-decisions-2026-09-24.md
rca-ci-on-hosted-windows-decisions-2026-09-24.md

#### fix(patchshield): v2.0.30 - convergence fixes for plan 007

`b0a93fb4`

The convergence pass over the review-fix commit found five defects in
the review record and none in code or tests. The reword list for plan
006's merge now names lessons/harmony-il.md:613, the fourth text on
this branch that says nothing on a shim preserves the stack; the
decisions report, its RCA and the build-tooling lesson say six texts
and two misses, and the lesson's search matches every inflection.

The decisions report now records the convergence pass before its
verdict, and corrects the lesson count (173), the SESSION SUMMARY line
(PatchShield.cs:423) and the claim made for the named-argument call.

Full suite: 10247 passed, 2 skipped, 2 failed (the two live-Armory
tests; this branch is based before a39a9c86).

#### fix(tools): v2.0.30 - review follow-ups for plan 005

`a0fa3cff`

Deep review (five lenses) and Codex on 6b34fd00. The faction-map argv
fix was correct; the prose and its verification were not.

The adoption checklist grep could not see inside the .tar.gz drops
TAOM keeps, so it reported clean while three gitignored BUTR archives
under Dependencies/.vendor-source/ still hold a
packageSourceCredentials block. The line now sweeps archives, uses a
-E pattern ripgrep reads the same way, drops an em dash and names the
harvest finding by path. The CHANGELOG no longer says the credential
is gone from disk; 6b34fd00's commit body still does (Mike's call at
merge).

New tools/tests/test_process_faction_map.py pins the path fix: a
plain folder, a quote and Python text in the folder name. The last
two fail on the a39a9c86 tool and pass here.

Report: docs/reviews/deep-review-005-security-hygiene-2026-09-24.md
RCA: docs/reviews/rca-security-hygiene-2026-09-24.md
Three lessons in docs/reviews/lessons/build-tooling-workflow.md.

Tests: dotnet test 10313 passed, 0 failed; the new module 3 of 3.

#### fix(battlebalance): v2.0.30 - review follow-ups for plan 003

`02157b18`

Deep review (six lenses) and Codex gpt-6-astra on 7feca96b found no
runtime defect. The follow-ups remove a latent start-up trap and the
test gap behind it.

BattleBalanceSettingsProvider now caches TaomSettings.Instance lazily
on the first non-null read (the NameplateRelationSettingsProvider
pattern) instead of in its constructor. The constructor read was safe
only because the one resolve runs under OnGameStart, after MCM sets
BaseSettingsProvider.Instance; a future resolve in OnSubModuleLoad
would have pinned the defaults for the session with no log line. On
today's wiring the values read are identical.

Tests first: the IL rule now requires the private lazy accessor, not a
constructor, to read Instance (RED against the constructor form); a
read-through test edits all twelve settings after construction, each
to its own value (a Tier8-reads-Tier9 mutation fails it); all twelve
fallbacks pinned against the TaomSettings defaults; the provider
resolves from a real DryIoc container. Full suite: 10323 passed,
2 skipped, 0 failed.

Docs: battle-balance.md and file-catalogue.md no longer describe a
per-access proxy; CHANGELOG corrected.

Report and RCA, in docs/reviews/:
deep-review-003-hot-path-resolve-and-grid-caching-2026-09-24.md
rca-hot-path-resolve-and-grid-caching-2026-09-24.md
Lessons: lessons/state-lifecycle-save.md, lessons/testing-qa.md.

Not-tested: in game; change a Battle Balance slider mid-campaign and
check auto-resolve follows it without a restart.

#### fix(harmony): v2.0.30 - review follow-ups for plan 009

`27fd23bb`

Second deep review and Codex of the maintainer decisions
(4c728dac..b6cb6ff5). Every lens and Codex found the translated
notice rows outside <strings>; 7eae4704 moved them, and a new gate,
LanguageDataXmlTests.AllTranslationFiles_StringRowOutsideRootStrings_
IsNeverPresent, fails any row LocalizedTextManager.LoadLanguage would
skip (red on the b6cb6ff5 blobs, green now).

The German phase names gain their genitive article and the French and
Japanese sentences read correctly once {PHASE} is filled; XML rows and
translation caches changed together. PatchCategoryIndexTests pins the
null-category guard (mutation-checked) and multi-class parity, and two
test names follow the convention. Apply and TryApply now say that a
category which lost a class at index time still reports success;
whether it should fail is left for Mike. Line references, the
architecture tree and the CHANGELOG test snapshot are corrected.

Full suite: 10256 passed, 2 skipped, 2 failed (the two known
live-Armory tests; this branch predates a39a9c86).

Report: docs/reviews/deep-review-009-guarded-patch-category-apply-
decisions-2026-09-24.md
RCA: docs/reviews/rca-guarded-patch-category-apply-decisions-
2026-09-24.md

#### fix(career): v2.0.30 - review follow-ups for plan 002

`96832589`

Deep review (six lenses) and Codex follow-ups for the MutationParams
finiteness guard. Two MutationParamsTests pin the unparseable fallback
and a negative finite value passing through; a mutant that accepted
any parse and rejected negatives turned both RED. The career-system
feature doc tells calculator authors to read floats only through
GetFloat, and the CHANGELOG claim about FiniteFloatValidator is
narrowed to what the loaders actually do. No production code changed.

Open for Mike: the GitHub issue, the silent fallback against config
rule 5, and a finiteness gate on the calculated value in
MutationService.ApplyMutation. Full suite: 10320 passed, 2 skipped,
0 failed.

Report: docs/reviews/deep-review-002-nan-infinity-config-guards-2026-09-24.md
RCA: docs/reviews/rca-nan-infinity-config-guards-2026-09-24.md

#### fix(crash-report): v2.0.30 - review follow-ups for plan 006

`8c84fa20`

The second deep review and Codex review of the plan 006 decisions
found one runtime defect. The callback bridge told the crash service
about an off-main-thread capture only by writing a mark to
Exception.Data inside a swallowing catch, and the service read a
missing mark as the main thread. Exception.Data is virtual and is
read-only for the runtime's preallocated out-of-memory and
stack-overflow exceptions, so those would have run the Mission and
Campaign collectors and the inquiry on a worker thread. The verdict is
now a HandleException parameter from both the bridge and the AppDomain
hook, through one AppDomainExceptionHook.IsOffMainThread, and the mark
is gone. The hook logs the main thread id it records.

Tests now reach the swallow path with a reachable service
(RecordingCrashService), which kills the four mutations the earlier
tests let survive. The native-capture MCM hint, reference counts, the
tableau caller list, the combat-callback limits and the owed probes
are corrected, and the allowlist cap test is folded into the pin.

Report: docs/reviews/deep-review-006-crash-capture-boot-cost-decisions-2026-09-24.md
RCA: docs/reviews/rca-crash-capture-boot-cost-decisions-2026-09-24.md
Full suite: 10271 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

#### fix(patchshield): v2.0.30 - review follow-ups for plan 007

`900920dc`

Deep review (six lenses) and a second Codex pass over the maintainer
decisions commit 0bf2409e. No behaviour defect; six text and test
findings confirmed and fixed:

- a test comment names the renamed local alreadySeen
- ShieldCoverageTests follow the MethodName_State_Expected convention,
  one test is split, and its message claims only what it shows
- dr3-maintenance.md and the feature map count 19 Foundation classes
- the plan 007 review record lists every text to reword once plan 006
  lands (dr3-maintenance.md:261, :304, lessons/harmony-il.md:572)
- the dr3 log-sample lead-in no longer reads as the old format
- the decisions-round Codex prompt is committed

The FormatShieldPassSummary call now passes named arguments.
Full suite: 10247 passed, 2 skipped, 2 failed (the two live-Armory
tests).

Report:
docs/reviews/deep-review-007-patchshield-skip-callback-shims-decisions-2026-09-24.md
RCA:
docs/reviews/rca-patchshield-skip-callback-shims-decisions-2026-09-24.md

#### fix(loc): v2.0.30 - move the notice translations inside <strings>

`7eae4704`

b6cb6ff5 seeded the five taom_patch_apply_* keys through the
translator's sync_missing_ids, which in these language files put the
rows after </strings>, directly inside <base>. The files mix line
endings (rows end LF, the </strings> line CR CR LF), and the seeding
anchors on the last <string> line it splits out, which there already
holds </strings>. The game's LocalizedTextManager reads only <string>
children of <strings>, so all 60 translations would have been ignored.

The same 60 rows now sit just before </strings> in the 12 files, same
text and order, with the LF ending of the rows around them. Checked by
parsing each file: all five ids inside <strings>, 2,670 <string>
elements before and after. Trunk's language files have no stranded
rows (scanned). The seeding bug in tools/translate_with_claude.py and
a coverage test that cannot see placement remain open follow-ups.

Full suite unchanged: 10253 passed, 2 skipped, 2 failed (the two known
live-Armory tests). validate_moduledata.py: 0 errors.

#### fix(specres): v2.0.30 - a new campaign no longer keeps the old balances

`4263535a`

Special resource balances live in SpecialResourceStorageService, a
Reuse.Singleton that outlives a campaign. A second campaign started
without restarting the game kept every balance the first one wrote, and
its first save then persisted them into the new save file.

OnNewGameCreated now wipes the storage with RestoreData(null) before the
character-creation finalize seeds the new hero. The handler no longer
reads Hero.MainHero: the two resets it gated never used the hero, and a
new campaign must wipe the old state either way. The SyncData load reads
into a null local instead of the live dictionary, because the engine
leaves the ref unchanged on a missing key and so handed the previous
campaign's balances back. Saving is unchanged and a present key
round-trips exactly.

This is the SpecialResources half of plan 001; the CareerSystem half
landed earlier in f4273639. Five tests in
SpecialResourcesBehaviorSessionResetTests run against real storage.
Full suite: 10318 passed, 2 skipped, 0 failed.

Save-compat: no new save fields; the key and its format are unchanged
Not-tested: live new-game event dispatch, second campaign in one run

#### fix(siege): v2.0.30 - convergence fixes for plan 019

`272ac1f0`

The convergence pass on bdc80a5a found 4 defects (1 LOW, 3 NIT); all
were confirmed against the code and fixed. No production code changes.

- The fresh-copy isolation test now mutates a GetMessages("") result
  and asserts the next lookup is a different object. Without that, a
  mutant returning DefaultMessages for an empty id passed all 31 tests.
  With the mutant applied, only this test fails.
- REVIEW-LOG and the RCA called the 2,256 nullable warning count a
  "test count"; it is now "the test project's nullable warning count".
- The lesson's "Why missed" line said the warnings sat at untouched
  lines; two of the three came from the commit's own new test code.
- The review record's NOT APPLIED and FOLLOW-UP lists now cite
  SiegeDefenseService.cs by HEAD line numbers.

Test project CS86xx: 2,256, Main 0. Full suite: 10,258 total, only the
two live-Armory failures.

#### fix(enlistment): v2.0.30 - convergence fixes for plan 014

`aac0e05b`

The convergence pass on a67792c4..fd61757b found four defects, all in
tests or review docs, none in runtime code.

- EnlistmentStopEndTests now also pins the OnSettlementLeftEvent
  subscription in EnlistmentMaintenanceBehavior. Before, deleting that
  line kept the suite green; a mutation run with it commented out now
  fails the pin.
- The stop-end sentinel is renamed ReachedTheStopEnd, after its throw
  site in OnStopEnded.
- The RCA summary and the state-lifecycle lesson credit four lenses,
  not five, matching the RCA's per-agent record.
- The deep-review report gains a Convergence section, and its verdict
  now follows the completed Step 4.

#### fix(ci): v2.0.30 - apply maintainer decisions for plan 010

`c139bc50`

Apply Mike's answers to the three NOT APPLIED items in the plan 010
review (#421, the C# half; the issue stays open for its Python half).

D44, applied: delete the SandBoxCore reference from Main/TAOM.csproj
and both TaomSandBoxCoreModuleBin properties in GameReferences.targets.
The folder holds no DLL on v1.5.3 and BUTR ships no package for it.
Reference snapshots of all three projects are identical before and
after in install and RefAsm mode (6 of 6); both builds have 0 errors.

D45, applied: move RequiresGame from the
Patch86HideoutBossFightBindingTests class to
PatchClasses_AreRegisteredInAllThreePlaces. The two prefix IL checks
now run in the CI unit step and pass on the reference assemblies
(replay: total 8220, executed 8196, failed 0). tests.md allows a
method tag when the rest of the class runs on the stubs.

D46, measured and reverted: pointing the unit step at refasm-game
turned 24 skips into 14 passes and 10 failures (Patch71FillTests and
TeamCombatantSelectorTests hit stub constructors), so csharp.yml is
unchanged. Both sets of numbers are in the review record.

Full suite in install mode: Failed 2, Passed 10256, Skipped 2, Total
10260 (the two known live-Armory tests).

#### fix(hooks): v2.0.30 - apply maintainer decisions for plan 013

`7aa658e3`

Apply the maintainer's 2026-09-24 decisions on the plan 013 review.
The plan 013 issue is not filed yet, so no issue number is cited.

D39: each gate prefilters on the word it gates instead of `git`. The
six commit gates filter on `commit`, validate-push.sh on `push` and
block-no-verify.sh on `no-verify`, as each body requires; git status,
git diff and git log now start no Python in them. The two confirm
gates keep `git`, and suggest-compact.sh is untouched because plan 011
deletes it.

D40: a payload holding any JSON backslash-u escape takes the full
parse in the twelve prefiltered hooks, so an escaped letter cannot
hide the gated word. The hooks share no prefilter, so the condition
sits on each hook's prefilter line.

D38, D41 and D42 are handled in plan 011, which runs on top of this
branch; the review record says so.

Tests first: tools/test_hooks.sh went 341/24 red for D39 and 365/17
red for D40, then 382 passed, 0 failed. Old versus new hooks over 240
payload cases: 0 differences. dotnet test: the two known live-Armory
failures only.

Not-tested: the edited hooks under a live harness (a worktree's hooks
do not run in the session; the live proof is owed after merge)

#### fix(warg): v2.0.30 - convergence fixes for plan 015

`516380d2`

The convergence pass on 23f6f85b..5d9d4cc9 found the code and tests
sound and one LOW process defect: the decisions RCA left out two
confirmed findings.

- RCA: F8 added, the reordered window-end gate in
  BoneCheckDuringAnimation.Tick shipped with no NaN test. It repeats
  the testing-qa lesson "A gate moved into new code is new code",
  written on this branch the same day. F9 added, InjectedNodes skipped
  NoEnemyCloseDecorator. The count is now nine, not seven (the
  5d9d4cc9 body also said seven).
- F8's preventive action goes up a tier because it is a repeat: one
  line in .claude/rules/csharp-architecture.md says a moved, extracted
  or reordered engine-float gate gets its NaN test in the same commit,
  whichever polarity it keeps. The lesson gains a repeat note.
- Stale lines the pass reported: warg-combat.md said
  WargAttackServiceTests has 7 tests and that #178 was open (22 tests,
  2 ignored; the service takes IAgentAdapter since 5a61e174). The
  WargTickCostTests comment said no IL scan reaches the type
  initializer; DeclaredBodies does scan it. Comment only.
- The decisions report gains a Convergence section.

Full suite: Failed 2, Passed 10282, Skipped 2, Total 10286 (the two
known live-Armory failures).

#### fix(enlistment): v2.0.30 - review follow-ups for plan 014

`fd61757b`

Deep review (six lenses) and Codex of the maintainer-decisions commit
a67792c4 (#656). Decision 6 cleared the arrival offer's settlement latch
only in the exit sweep, which a shore-leave pass suspends, so an
accepted offer never re-armed the town. EnlistmentMaintenanceBehavior
now ends the stop on the commander's settlement-left edge (it takes the
wait-menu presenter), RED test first in EnlistmentStopEndTests.

Also: the game-end test is renamed to what it proves, ignores comment
lines and uses RepoPath; the heap-release claim and the teardown-order
comment are corrected; stale reset docs updated; tests added for the
wiring, the co-op client load with data, a throwing ColumnLeftSettlement
subscriber and the maintenance hook's container resolution.

Full suite: 10266 passed, 2 skipped, 2 failed (the known live-Armory
tests), total 10270.

Report and RCA, both in docs/reviews/:
deep-review-014-enlistment-session-scope-decisions-2026-09-24.md
rca-enlistment-session-scope-decisions-2026-09-24.md

#### fix(tools): v2.0.30 - faction-map helpers take paths as arguments

`6b34fd00`

Plan 005, ported from the June branch impl-005 (4310aa6e, 4bc520a1)
through the review pipeline (Mike's decision 17).

tools/process_faction_map.py pasted each file path into two child
python -c scripts as r'<path>', so a quote in a path broke the script
and let the path's text run as code. The children now read their
paths and numbers from sys.argv, and the doubled f-string braces the
old template needed are gone.

The external-repo vetting checklist gains a grep for inline package
credentials in vendored drops. The vendored BUTR credential behind it
is already gone from disk (checked: no nuget.config and no
packageSourceCredentials under Dependencies/); MCP pinning moved to
plan 016.

Probe (a PNG under a folder named "it's here"): trunk fails both
calls with a SyntaxError in the generated source; the port returns
bbox (1, 1, 2, 1, 4, 3) and writes the crop. py_compile passes.
Not-tested: the full faction-map pipeline on the real region art.

#### fix(config): v2.0.30 - reject NaN and Infinity in mutation floats

`78889a85`

Plan 002, ported from the June branch impl-002 (cfc47206) through the
review pipeline (Mike's decision 17). MutationParams.GetFloat returns
the default when a value parses to NaN or plus or minus Infinity, via
the existing FiniteFloatValidator, so a "NaN" in a career mutation's
XML can no longer poison later comparisons.

cfc47206's troop-weight half already landed on trunk in bee07b48, so
the cherry-pick's conflicts on TroopWeightXmlLoader and its tests were
resolved to trunk's versions; only the mutation half is new.

MutationParamsTests (5): 3 RED against trunk's unguarded GetFloat (NaN,
+Infinity, -Infinity), all GREEN with the guard. Full suite: 10318
passed, 2 skipped, 0 failed.

#### fix(siege): v2.0.30 - review follow-ups for plan 019 decisions

`bdc80a5a`

Second deep review (six lenses) and Codex round on 503b933e. No
runtime defect; 11 findings fixed, all LOW or NIT.

- SiegeDefenseServiceTests: the three nullable warnings the
  KingdomSiegeMessages? change added are gone, so TAOM.Tests is back
  at its 2,256 baseline (counted on a --no-incremental build).
- A new test mutates results from the defaults path, and the
  known-faction test asserts all five fields. Both failed under
  mutants (return DefaultMessages; AcceptMessage read from
  AcceptButton) before the real code was restored.
- Patch registry: decision 2 recorded as closed (#660), not open.
- siege-defense.md: tokens listed per field, 31 tests with GetMessages
  and Reset/Restore bullets, KingdomSiegeMessages.cs row, #660.
- siege.md: caller wording, Status Open (#660). CHANGELOG and the
  GetMessages comment no longer promise "never blank" for spaces.

Needs Mike: a warning log for incomplete KingdomMessages entries,
and whether a value of only spaces counts as empty.

Full suite: Failed 2 (the known live-Armory tests), Passed 10254,
Total 10258.

Report:
docs/reviews/deep-review-019-nullable-ratchet-decisions-2026-09-24.md
RCA: docs/reviews/rca-nullable-ratchet-decisions-2026-09-24.md

Refs #660

#### fix(warg): v2.0.30 - review follow-ups for plan 015

`5d9d4cc9`

Second deep review and Codex pass on the plan 015 maintainer decisions
(56eb4bc8..23f6f85b). No HIGH, no runtime defect; seven LOW findings
fixed, four design choices left for Mike.

- BoneCheckDuringAnimation.Tick is now driven by substitute tests
  (default ActionIndexCache needs no engine: the v1.5.3 struct is
  beforefieldinit). They replace the IL order rule, which a mutant
  fetching the skeleton on every wind-up frame passed.
- WargTickCostTests: one resolve predicate that also catches
  IoC.ResolveAll, and one node list, so NoEnemyCloseDecorator's
  constructor is scanned too.
- WargTreeNodeInjectionTests drives the attack task and the facing
  decorator with substitute services.
- Comments and docs: the service-locator claim is scoped to the four
  service nodes (LogTask still resolves), the wind-up skeleton
  difference states the recovery case, the NaN polarity is documented
  and pinned, IsWarg is gone from warg-combat.md, and How-to-Add names
  the injection pattern.

Report:
docs/reviews/deep-review-015-warg-tick-costs-decisions-2026-09-24.md
RCA:
docs/reviews/rca-warg-tick-costs-decisions-2026-09-24.md
Full suite: 10282 passed, 2 skipped, 2 failed (the two live-Armory
tests).

#### fix(patchshield): v2.0.30 - apply maintainer decisions for plan 007

`0bf2409e`

Applies the maintainer's 2026-09-24 answers to the plan 007 review
(#651):

- The pass-2 entry label in diag.log now names every start that
  reaches it: "game start: campaign, custom battle or editor". No
  test or tool reads the old text.
- PatchShield's single _shielded set is split into seen and attached
  (new ShieldCoverage). The shield pass line reports
  "(seen: N, attached: A)" with "already-seen", and the session
  summary "shielded A of N patched method(s) seen", so skipped
  methods no longer count as shielded. Which methods get a finalizer
  is unchanged. Test first: ShieldCoverageTests failed on the old
  set (attached 3, expected 1), then passed.
- Recorded without code change: the whole ManagedCallbacks namespace
  stays excluded; the fallback stack preservation was fixed on plan
  006's branch in 42624b95, not here.
- The CHANGELOG heading cites #651.

The review record gains a "Maintainer decisions applied" section.
Full suite: 10246 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

#### fix(crash-report): v2.0.30 - apply maintainer decisions for plan 006

`8e6b0935`

Second pass on the maintainer's #650 decisions of 2026-09-24,
continuing 42624b95.

D43: the callback bridge now marks a capture made off the main thread.
AppDomainExceptionHook keeps the id it records in Subscribe() at
OnSubModuleLoad in a static, read through an internal MainThreadId.
With native capture on, Native2ManagedBridge sets
ex.Data["TAOM.CrashReport.OffMainThread"] when the current thread is
not that id, or when no id was recorded (the safe direction), so
CrashReportService skips its Mission and Campaign reads and the
inquiry. MissionThreadGuard and SubModule.cs are unchanged.

D47: the ten mission combat callbacks traced into TAOM code are back
on the Native2ManagedTargets allowlist (melee, missile, charge, fall
and area damage, hit blocked, defend collision results, agent removed,
agent deleted, shoot missile), each pinned by literal name; the cap
in All_IsASmallDistinctAllowlist is now 16. Mount, dismount and
alarmed-state stay out. The attach time is not measured.

Tests first: three bridge tests and a hook test failed before the
change, and the sixteen-name pin failed with 16 expected, 6 actual.
Full suite: 10265 passed, 2 skipped, 2 failed (the known live-Armory
tests). Docs: crash-report.md, the Patch37 registry entry, the
CHANGELOG entry and the review record's maintainer-decisions section.

#### fix(crash-report): v2.0.30 - apply maintainer decisions for plan 006

`42624b95`

Applies the maintainer's 2026-09-24 answers to the plan 006 review
(#650), each test first:

- Allowlist entry 6 is now RenderTargetComponent_OnPaintNeeded, the
  tableau render callback (character, item, banner and map
  conversation tableaus), verified in the installed v1.5.3 engine.
  It replaces a tableau-setup callback nothing in v1.5.3 arms.
- The callback bridge has one exit, and every exception it hands
  back goes through RethrowStackPreserver.PreserveForRethrow.
- CrashReportPatchHelper.HandleAndSwallow routes all four hand-back
  paths through the preserver, so Harmony's rethrow keeps the
  throw site.
- Recorded without code change: bridge priority stays 400, the
  suppression log keeps its powers-of-ten cadence, capture stays on
  by default with EnableNativeToManagedCapture as the toggle. The
  in-game probe stays owed.
- Cited #650 in the CHANGELOG heading and a GitHub Issue section in
  the feature doc.

Not applied: adding the mission combat callbacks back. The trace
finds ten with a verified path into TAOM code (recorded in
crash-report.md), but several arrive off the main thread, where an
untagged bridge capture would run the mission collectors and the
on-screen inquiry. Stopped for the maintainer's call.

Full suite: 10261 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

#### fix(loc): v2.0.30 - localize the patch failure notice for plan 009

`b6cb6ff5`

Maintainer decision on review finding 15 (#653): the startup patch
failure notice is localized. TakeFailureSummary returns a
{=taom_patch_apply_failed} TextObject with PHASE and GROUPS; the three
phase names and the inquiry title are registered taom_patch_apply_*
keys; the button reuses vanilla's {=oHaWR73d}Ok row.

The five keys are translated into all 12 languages as AI first drafts.
Only these keys were sent to the model: the stock --sync-ids run would
also have translated unrelated untranslated rows and seeded 26 keybind
ids. Each language file and cache file gains exactly 5 rows, and
{PHASE} and {GROUPS} survive in every language.

Full suite: Failed 2, Passed 10253, Skipped 2, Total 10257; the two
failures are the known live-Armory tests.

#### fix(hooks): v2.0.30 - convergence fixes for plan 013

`b34d615a`

The convergence pass over the review-fix commit found no behaviour
defect and three wording defects, all confirmed against the files:

- CHANGELOG: the JSON escape for `g` had been decoded into the letter
  itself on both lines. The Edit tool decodes backslash-u escapes, so
  the fix went in through a script.
- hooks-catalog: 256 to 451 ms was the whole hook's time on an `ls`
  before the prefilter, not the cost of the two Python starts; it now
  gives the before and after figures.
- REVIEW-LOG and RCA: each proof is now credited to the finding it
  proves (slow fake for F1, planted mutations for F2 and F3), and
  F4 to F6 are no longer said to have a failing proof.
- Lessons: "Eleven gates" is now "The ten git gates".

Hook suite 341 passed, 0 failed; dotnet suite failed 2 (the known
live-Armory pair), passed 10235, skipped 2.

#### fix(harmony): v2.0.30 - apply maintainer decisions for plan 009

`7912fdd8`

Applies the maintainer's 2026-09-24 decisions on the plan 009 review
(#653).

Decision 1, review finding 3: one patch class whose [HarmonyPatch]
names a type the engine no longer has made Harmony's assembly-wide
category index throw, so all 84 categories failed. The new
PatchCategoryIndex builds that index class by class from Harmony's
public API, skips a class whose attributes cannot be read, and applies
each category's classes the way Harmony.PatchCategory does. The
skipped class is logged as [PatchApply] SKIPPED and named in the
startup inquiry; every other category still applies.
PatchCategoryIndexTests proves it on an emitted probe assembly (RED
through Harmony's own index, GREEN through PatchCategoryIndex), and
the source gate now allows no direct PatchCategory call in Main.

Decision 3: the CHANGELOG heading cites #653.

Decision 2 (localizing the notice) is not in this commit.

Not-tested: live engine apply path and the notice (needs the game)
Save-compat: no save data touched

#### fix(enlistment): v2.0.30 - apply maintainer decisions for plan 014

`a67792c4`

Apply Mike's answers (2026-09-24) to the plan 014 review's NEEDS MIKE
items, for #656. Each behaviour change is test-first.

- The commander-loss modal's shown-once latch (_lossAnnouncedFor) is
  cleared by EnlistmentReconciler.ResetForNewSession and on every
  discharge (a DischargeService.EnlistmentEnded subscription), so a
  later loss under the same lord is announced again.
- OnGameLoaded runs ResetSessionCaches above the co-op authority gate,
  so every peer resets on load; only normalization stays host-only.
- A loaded save with no Enlistment data clears the store before
  normalizing, instead of normalizing the previous session's term.
- The shore-leave offer latch clears when the stop ends: the exit path
  raises ColumnLeftSettlement and the presenter forgets the settlement
  (the 24-hour cooldown stays).
- SubModule.OnGameEnd calls ResetSessionCaches, one call inside the
  existing best-effort teardown block, so the finished campaign's
  cached commander party and army are released.
- The CHANGELOG heading cites #656.

The review record gains a "Maintainer decisions applied" section with
the RED output for each change. Full suite: 10259 passed, 2 skipped,
2 failed (the two known live-Armory tests), total 10263.

#### fix(warg): v2.0.30 - apply maintainer decisions for plan 015

`23f6f85b`

Applies Mike's 2026-09-24 answers to the plan 015 review's NEEDS MIKE
items (#659).

1. Services in tree nodes: WargBehaviorTree.BuildTree resolves
   IMissionAdapterFactory and IWargAttackService once per tree and
   passes them to the constructors of
   PeriodicallyCheckIfCanAttackAnyone, CheckOnceIfCanAttackEnemy,
   WargAiControlledIsNotFacingEnemy and WargAttackTask. The nodes keep
   them in private readonly fields and contain no IoC.Resolve. Every
   construction site is in WargBehaviorTree.cs. New IL tests (RED
   first) scan every body of the four nodes, constructors included,
   and pin one resolve per service in BuildTree, with a
   field-initializer control.
2. BoneCheckDuringAnimation.Tick tests the action and the progress upper
   bound first, reads the action progress once, and fetches the attacker
   skeleton only once the hit window is reached. One behaviour change: a
   missing attacker skeleton during the wind-up now ends the bite when
   the hit window opens instead of at once. IL rule tests (RED first)
   pin one progress read and the skeleton fetch after it. The owed
   in-game warg Custom Battle is the proof that bites land and end as
   before.
3. The wider scan results between grid rebuilds are kept: recorded, no
   code change.
4. #659 is cited in the CHANGELOG heading and in the GitHub Issue
   sections of warg-combat.md and advanced-combat.md.

The review record gains a "Maintainer decisions applied" section.
Full suite: 10273 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

Not-tested: in game; the warg Custom Battle is owed.

#### fix(siege): v2.0.30 - apply maintainer decisions for plan 019

`503b933e`

Applies Mike's 2026-09-24 answers to the NEEDS MIKE items in the
plan 019 deep review (#660).

1. Per-field fallback for siege defense popup text: a KingdomMessages
   entry that leaves Title, Body, AcceptButton, AcceptMessage or
   RewardMessage null or empty takes that field from DefaultMessages,
   and a JSON null entry gets every default. GetMessages returns a
   fresh copy, so neither the static defaults nor the config entry is
   written. KingdomMessages values are now nullable so the Siege error
   tier enforces the check. Four SiegeDefenseServiceTests, RED then
   GREEN, load their config through JsonConvert.
2. The no-settlement path in the siege camp guard is closed as
   unreachable from vanilla in v1.5.3; its log line stays as the
   tripwire. The /build-fix scope extension is kept. No code change.
3. #660 is cited in the CHANGELOG heading and in siege.md.

siege-defense.md documents KingdomMessages and its fallback; the
review record gains a maintainer decisions section.

#### fix(composition): v2.0.30 - convergence fixes for plan 018

`ecbc2199`

The convergence review of fa0b24e1 found two prose defects, both
confirmed against the code.

The Patch37 known limitation still described the code before the
fix. A faulted save owner now throws on every retry of the loading
step, so with crash capture on (the default) the load never finishes;
with it off the exception reaches the engine. The CHANGELOG, the
state-lifecycle-save lesson and the RCA summary now say so, and the
FeatureModuleHooks summary says "throw" rather than "rethrow".

The ordering note left out the co-op Harmony census after the
GameInit phase. FeatureModules.cs, the CHANGELOG and RCA row 5 now
name four hand-wired blocks after the module call.

No behaviour change. Full suite: Failed 2, Passed 10289, Skipped 2,
Total 10293 (the two known live-Armory tests).

#### fix(ci): v2.0.30 - convergence fixes for plan 010

`2897fcca`

The convergence deep-reviewer pass on b8c00045..a4b90e4d found 5 LOW
defects, 0 HIGH or MED; all confirmed against the worktree and fixed.

- D1: .ai/verification.md sent the reader from a Release build to the
  Debug csharp.yml test commands, so the recipe tested a stale or
  missing DLL. It now runs the build, unit and gate steps as written.
  The review report and REVIEW-LOG no longer claim the earlier replay
  ran as written (it added -c Release by hand).
- D2: the recipe says to unset the game variables before the build,
  since the build records the install as TaomGameFolder.
- D3: tests.md lists the ReflectionTypeLoadException signature seen in
  the executor's 6b.log.
- D4: GameReferencesTargetsTests gains one fixture row per rejected
  spelling (7 of 10 red first, the Import's own Condition row proven by
  mutation). Property names match without case; an import under a
  conditional ImportGroup, When or Otherwise no longer counts.
- D5: the report's verdict is set from the convergence pass.

No-game recipe replayed from a clean copy: build 0, unit executed 8194
failed 0, gate 338/338. Full suite: Failed 2 (known live-Armory),
Passed 10256, Skipped 2.

#### fix(hooks): v2.0.30 - review follow-ups for plan 013

`787fd366`

Deep review (six lenses) and Codex found the 13 prefilters correct
and the new test and docs wrong in six places. All fixed:

- test_hooks.sh 4c counted starts of a pinned fake interpreter;
  _pybin.sh drops a pin that misses its 0.8 s probe, so under load
  the check blamed a correct prefilter. It now reads a bash -x trace
  for the source of _pybin.sh, which has no timing.
- Section 4 gains a Bash payload with git and dotnet, so the exit
  code and JSON contract reaches each Bash hook's parse path again.
- 4c gives suggest-compact its dotnet and build.ps1 rows and finds
  Bash hooks by regex matcher, PostToolUseFailure included.
- The premise is Claude Code's payload, not JSON: hook comments,
  CHANGELOG and hooks-catalog.md say so and name the re-check.
- hooks-catalog.md paragraph moved below the one it broke.

Two planted mutations passed the old suite and fail the new one; a
fake interpreter slowed to 1 s no longer causes a false failure.
test_hooks.sh 341 passed, 0 failed; dotnet test at the known two.

Report: docs/reviews/deep-review-013-bash-hook-prefilter-2026-09-24.md
RCA: docs/reviews/rca-bash-hook-prefilter-2026-09-24.md

Deferred: validate-push.sh reads only the first line of a command,
so a force push after a cd line passes (HIGH, pre-existing, out of
plan 013 scope); needs its own issue and a multi-line test row.
Not-tested: live proof in a Claude Code session (plan 013:695).

#### fix(composition): v2.0.30 - review follow-ups for plan 018

`fa0b24e1`

The deep review and the Codex review of 4c728dac..44045b34 found one
real runner gap, dormant because no module owns save data yet: the
faulted-module skip ran before the fail-closed check, so a save owner
that faulted in a fail-open step, or on a retried campaign start, was
left out of the campaign silently. It now throws in every fail-closed
step, and a parked save owner no longer fails closed.

Also: ModuleRunner.RunCampaignStart owns the campaign-start flag; the
hooks gain overloads that take the runner and resolver, tested against
real CampaignGameStarter and BasicGameStarter instances; a failed fault
notice is logged; the ordering comments name the three hand-wired
blocks that still follow the module call; new guards pin the
Modules = modules hand-off, keep AddGameStartContent outside the
campaign branch, give the OwnsSaveData IL check a positive control,
keep IoC.Resolver inside the hooks, and feed the reader a real CRLF
file. Design: the single-implementation ITaomFeatureModule and the
FeatureState enum are deleted (a parked module has a ParkedReason),
the pilot's text test is replaced by its container test, and the decl
factories pass their delegates through.

Mike's calls: TAOM's Patch37 finalizer swallows the campaign-start
throw (CHANGELOG known limitation), the GitHub issue, the CHANGELOG
plan deviation, the stale plan precondition, one startup inquiry.

Report:
docs/reviews/deep-review-018-composition-root-first-steps-2026-09-24.md
RCA:
docs/reviews/rca-composition-root-first-steps-2026-09-24.md

#### fix(bindings): v2.0.30 - apply maintainer decisions for plan 008

`37306bca`

Applies the three decisions Mike took on 2026-09-24 for the plan 008
review's NEEDS MIKE items (#652).

1. No skip banner. notify-test-results.sh is back to its content at
   7f02fc8d and tools/test_hooks.sh section 7c is removed. The banner
   went to stderr from an exit-0 hook and only reached the debug log.
   The signal is the Skipped: count in dotnet test's own output plus
   the strict binding-gate runsettings; the hooks catalog row, the
   CHANGELOG, the RCA and REVIEW-LOG say so.
2. binding-gate.runsettings sets TreatNoTestsAsError. A zero-match
   filter under it exited 0 before and exits 1 now; the strict gate
   still passes 368 of 368 with 0 skipped. BindingGateRunSettingsTests
   pins it and MapInconclusiveToFailed (red first on the new row), and
   the verify-bindings skill names the zero-match message.
3. The resolver order stays as built: the two environment variables
   first, the build's game folder last. Recorded only.

The deep-review report gains a maintainer decisions section.

#### fix(ci): v2.0.30 - review follow-ups for plan 010

`a4b90e4d`

Deep review (six lenses) and Codex adversarial review of plan 010
confirmed 9 defects, 0 HIGH, 1 MED; all fixed here.

- GameReferencesTargetsTests now compares the BUTR version's fourth
  part with ApplicationVersion.DefaultChangeSet, so a same-label BUTR
  build no longer passes; the reference guard reads the whole element
  (HintPath included) and counts only an unconditional import.
- The binding gate step fails when any check did not execute.
- Accurate text: the csharp.yml header, both build errors, the no-game
  recipe in .ai/verification.md (run as written from a clean tree), the
  CI failure signatures in tests.md, the CHANGELOG (#421) and the
  feature map.
- Behaviour-preserving: one _TaomNuGetRoot property (9 of 9 reference
  snapshots identical) and no stub copies in refasm-game/bin (gate
  338 of 338).

Report: docs/reviews/deep-review-010-ci-on-hosted-windows-2026-09-24.md
RCA: docs/reviews/rca-ci-on-hosted-windows-2026-09-24.md
Lessons: testing-qa (2), build-tooling-workflow (1); REVIEW-LOG 133.
Full suite: Failed 2 (known live-Armory), Passed 10246, Skipped 2.

#### fix(nullable): v2.0.30 - convergence fixes for plan 019

`de288136`

The ratchet gate now parses <NoWarn> the way the compiler does: it
splits on ';', ',' and space, and expands the "nullable" alias to the
seven ratchet ids. Before, "1701,8602" or ";nullable" turned off every
graduated folder while the gate stayed green. New rows cover each
spelling.

The graduation procedure builds with -p:DisableModuleCopy=true
-p:ModuleId=, so following it from a worktree no longer deploys into
the game install. siege.md now says the catch, like the no-settlement
path, hands vanilla a camp-1 array it throws on.

#### fix(warg): v2.0.30 - convergence fixes for plan 015

`56eb4bc8`

The convergence pass over fe8f30c7 confirmed four LOW defects:

- The mixed-list CheckTargets test could not fail for a lost i--,
  because every entry after a removal was a far target. The list now
  puts a must-drop entry after each removal, includes a null-visuals
  target and asserts each far target's frame read. Deleting each of
  the three i-- lines locally turned this test red.
- IAgentAdapter.IsWarg() lost its last caller in the review fix;
  deleted, and the test comment now names WargConfig.IsWargMonster.
- advanced-combat.md diagram: TakeDamage back under the callback.
- The testing-qa lesson's control-fixture clause is narrowed to IL
  rule tests.

Full suite: 10266 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

#### fix(siege): v2.0.30 - review follow-ups for plan 019

`155e3ea5`

Six deep-review lenses and Codex (gpt-6-astra, ultra) on plan 019 found
no runtime defect: 12 findings confirmed, 1 false positive, 0 HIGH.
Eleven are fixed here.

- /build-fix told a builder to fix CS8602 with `!`; the ratchet made
  that advice live. One row now covers all seven ids and points at the
  rules, and a DON'T line bans NoWarn and lowering a folder's severity.
- The graduation procedure, fix rules and hotfix escape move from the
  plan into code-quality.md; both .editorconfig comments point there.
- NullableRatchetGateTests fails if a ratchet id returns to a
  production NoWarn (shown failing on a temporary CS8602).
- SiegeCampGuardPatchTests covers all five paths of the prefix; the
  camp-2 test asserts the array itself is handed over.
- Patch8 comment, siege.md and the registry entry say the no-settlement
  path hands vanilla an array it throws on, and that vanilla cannot
  reach it; the harmony-il lesson no longer calls Patch8 a safe defer.
- The DTO comment, siege.md ring and test lines are corrected, and the
  no-op NoWarn line in Main/TAOM.csproj is gone (NoWarn evaluates to
  1701;1702 before and after).

Main build: 2 warnings, 0 errors, 0 CS86xx. Full suite: 10246 total,
10242 passed, 2 skipped; only the two known live-Armory tests fail.

Report: docs/reviews/deep-review-019-nullable-ratchet-2026-09-24.md
RCA: docs/reviews/rca-nullable-ratchet-2026-09-24.md
Not fixed, needs Mike: the GitHub issue for plan 019.

#### fix(warg): v2.0.30 - review follow-ups for plan 015

`fe8f30c7`

Deep review (six lenses) and Codex gpt-6-astra ultra on plan 015 found
no HIGH finding and no false positive. Fixes, tests first:

- BoneCheck's moved range gate is a positive requirement, so a NaN
  visuals frame no longer reaches the native skeleton fetch.
- WargTickCostTests loads the game assemblies, fails on a body it
  cannot read, rejects a List<Agent> built per scan, checks no node
  keeps a static service or buffer, and proves each IL rule on a
  control fixture.
- CheckTargets skip guards, a mixed target list, grid column order
  and a point moved since the rebuild are pinned.
- WargRiderHandManager.Tick reads the mount's Monster directly; the
  factory plumbing into WargMissionBehavior is gone. The attack scans
  look up the warg's adapter only once a candidate passes.
- CHANGELOG and feature docs: the scan-membership change between grid
  rebuilds, the spider tie order, test counts and this suite run.

Report: docs/reviews/deep-review-015-warg-tick-costs-2026-09-24.md
RCA: docs/reviews/rca-warg-tick-costs-2026-09-24.md

#### fix(enlistment): v2.0.30 - convergence fixes for plan 014

`41754a03`

The convergence review of the plan 014 review-fix commit found two
text defects, both confirmed against the code.

The narrowed reset wording still overclaimed. CommanderLordAdapter's
one-slot MapEvent cache and BattleMeritAccumulator._pending are
singleton fields the reset never clears, yet the CHANGELOG said the
reset drops "the feature's cached engine handles" and both "not reset"
lists read as closed. The CHANGELOG, enlistment.md and the
IServiceMaintenanceService doc now name what is cleared and list the
two fields.

RCA row 3 and the EnlistmentSessionResetTests summary still gave the
rhythm cache the "a moment ago" hazard; its cache test is an equality
check, so its hazard is a same-hour reload. Both now say so.

Text only, no behaviour change. Full suite: 10246 passed, 2 skipped,
2 failed (the two known live-Armory tests).

#### fix(patchshield): v2.0.30 - convergence fixes for plan 007

`31a31f16`

Three prose defects from the convergence pass, all confirmed against the
code and the live diag.log:

- Pass 2 at a later game start does not shrink: it attaches TAOM's late
  batch (about +140), which now costs more than the first pass 2. The
  OnGameInitializationFinished comment and the CHANGELOG say so.
- dr3-maintenance.md's diag.log sample drops the AliasStub line (never
  logged) and moves SESSION SUMMARY under process exit, its only caller.
- dr3-maintenance.md no longer claims a Native2Managed-off swallow path;
  that flag only decides at launch whether the finalizers attach.

Full suite: 10243 passed, 2 skipped, 2 failed (the known live-Armory
tests).

#### fix(crash-report): v2.0.30 - convergence fixes for plan 006

`70727529`

The convergence pass on the review follow-ups found three wrong text
claims. The registry now limits the tick finalizers' reach to the
callbacks Module.OnApplicationTick dispatches and names
OnSubModuleUnloaded as outside it. PatchShield's pass 2 is described as
shielding every patched method not declared in a TAOM assembly, not
every foreign-patched one. The coexistence how-to gives priority 800
only to the Patch37 rows and 400 to the callback shims. The priority
lesson names the HarmonyMethod constructor the bridge actually uses.

#### fix(enlistment): v2.0.30 - review follow-ups for plan 014

`b9d18458`

Six deep-review lenses and a Codex gpt-6-astra (ultra) pass over
7f02fc8d..d1221b7f found no runtime defect and 11 text and coverage
findings, all fixed here.

- The attachment service's session reset now also drops the adapter's
  cached commander party; the InvalidateCommanderCache pass-through and
  its separate call in ResetSessionCaches are gone (design lens, RED
  test first).
- New tests pin the load hook's reset and its order before normalizing
  (a sentinel thrown before CampaignTime.Now), and the new-campaign
  reset after a loading SyncData.
- The CHANGELOG, feature doc and comments no longer claim every
  per-session value is reset; the loss-modal latch and the duty pace
  estimate are named as follow-ups. A test comment this change made
  false is corrected.

Behaviour-changing proposals (reset before the authority gate on load,
the commander-loss latch, a stale record normalized on load) wait for
Mike. No GitHub issue exists for plan 014 yet.

Report:
docs/reviews/deep-review-014-enlistment-session-scope-2026-09-24.md
RCA: docs/reviews/rca-enlistment-session-scope-2026-09-24.md

#### fix(patchshield): v2.0.30 - review follow-ups for plan 007

`578ac7d6`

Six deep-review lenses and a Codex pass (one P3) on 7f02fc8d..0ad253d5.
The code did what the plan asked; the findings were about its claims.

- The ManagedCallbacks prefix reaches 88 classes in v1.5.3, not the 3
  shims: 79 ScriptingInterfaceOf* wrappers Native2Managed does not wrap
  are unshielded too. Now stated in the comment, dr3-maintenance.md and
  a CHANGELOG known limitation; narrowing it waits on Mike.
- On HandleAndSwallow's fallback paths nothing on a shim now swallows
  the missing-API trinity or preserves the stack. Disclosed; the fix
  belongs in CrashReportPatchHelper (plan 006's file) and waits on Mike.
- Corrected claims: ButterLib BEW puts blank transpilers on three shims;
  the 30x Harmony.Patch swing is one machine over time; pass 2 in the
  v1.5.2 log began at 20:11:39; pass 2 runs at every game start.
- New BindingVerification test selects the shims from the installed
  DLLs as Native2ManagedPatcher does (RED with the entry misspelt).
- dr3 log sample in write order with both passes; stale symbol refs in
  arena.md and harmony-il.md; test and policy summaries.

PatchShieldPolicyTests 24 pass. Full suite: 10243 passed, 2 skipped,
2 failed (the two known live-Armory tests).

Report:
docs/reviews/deep-review-007-patchshield-skip-callback-shims-2026-09-24.md
RCA:
docs/reviews/rca-patchshield-skip-callback-shims-2026-09-24.md
Not-tested: pass-2 attach count and timing in game (custom battle).

#### fix(crash-report): v2.0.30 - review follow-ups for plan 006

`65b691b6`

Six deep-review lenses and a Codex adversarial pass on
7f02fc8d..6fe83bca found no HIGH defect and 22 confirmed ones in tests,
comments and docs, all fixed here.

Tests: the native-capture-off bridge test now throws for real and checks
the recorded throw site (a bare return fails it), the allowlist is
pinned to its six names, every skip path in
Native2ManagedTargets.Resolve is tested, both engine-name tests run
under BindingVerification, and the shape test shares
HarmonyPatchBindingTests' resolver. The bridge is
attached by nameof, so a rename is a build error.

Docs: the bridge runs at priority 400, the boot saving is scoped to the
maintainer's desktop (players measured 0 to 1 s), BUTR stays disabled
after a capture until re-enabled or a restart, the dev triggers do not
reach the callback shims, the hero-race catch point follows the real
call graph, and the dropped callbacks are listed as a known limitation.

Full suite: 10258 passed, 2 skipped, 2 failed (the two known live-Armory
tests). Eight decisions for Mike are in the report.

Report:
docs/reviews/deep-review-006-crash-capture-boot-cost-2026-09-24.md
RCA:
docs/reviews/rca-crash-capture-boot-cost-2026-09-24.md

#### fix(siege): v2.0.30 - null-clean the Siege folder, nullable as errors

`7ad7a307`

Main/Features/Siege had 8 nullable warnings once the suppression moved
into .editorconfig: 7 annotations and 1 guard fix them all.

- Patch8_SiegeCampGuard now has an explicit branch for a besieger camp
  with no settlement and no camp frames. Before, settlement.GatePosition
  threw an NRE that the catch-all logged as a patch exception before
  returning true. The branch logs and returns true: the same outcome
  (defer to vanilla), without throwing on purpose.
- KingdomSiegeMessages properties are string? because Newtonsoft
  leaves a key null when a siege_defense_config.json entry omits it;
  Resolve takes string? and spells out the null check (net472's
  string.IsNullOrEmpty does not narrow).
- ActiveSiegeDefenseEvent's two ids default to an empty string; every
  construction site already sets both.
- The folder's new .editorconfig sets the seven nullable ids to error,
  so a new null-flow mistake here fails the build.

New tests: SiegeCampGuardPatchTests (2), calling the prefix directly on
an uninitialized BesiegerCamp. Plan 019, steps 2 to 7.

Not-tested: live siege with a settlement-less BesiegerCamp (not reachable in a normal campaign; SiegeEvent's constructor sets the settlement)

#### fix(enlistment): v2.0.30 - reset session caches on a new campaign

`e99d705a`

The engine fires OnNewGameCreated, not OnGameLoaded, for a new
campaign, so the commander party handle, the army handle, the
stale-battle anchor and the three new latches all leaked into a second
campaign in the same process. The reset is field clears only, so it
is not authority-gated.

Not-tested: a real second campaign in one process (needs the game)

#### fix(crash-report): v2.0.30 - make both capture toggles live

`9cfb91b1`

Plan 006, step 3 of 4. SubModule.OnSubModuleLoad gated the crash
patches on CrashReportSettings.Instance, but MCM builds its settings
provider in its own OnBeforeInitialModuleScreenSetAsRoot, after every
OnSubModuleLoad. The instance is always null there, so both gates
always took their ?? true fallback: players could not switch the
native-to-managed capture off, and the MCM restart prompt promised an
effect that never happened.

The gates are removed (runtime parity: they were always true), the
patches install unconditionally, and both toggles are read when an
exception arrives. Native2ManagedBridge gains HandleOrPassThrough: with
the toggle off it hands the exception back through
RethrowStackPreserver so the throw site survives. Both settings now
carry RequireRestart = false with hints that say what the code does,
and SettingRequireRestartPostureTests drops them from its allowlist so
it enforces the new posture. Defaults stay ON.

Not-tested: MCM toggling in a running game

#### fix(enlistment): v2.0.30 - ResetSessionCaches drops the session latches

`f6ac2d33`

ResetSessionCaches is the feature's one reset point, so the three new
resets are called from it. The maintenance service takes the wait-menu
presenter and the rhythm service to do so. The container wiring test
now substitutes IPlayerContextAdapter and IDutyOrchestrationService,
which the presenter's graph needs and which other feature modules
register in the live container.

#### fix(enlistment): v2.0.30 - reset the dwell anchor, offer latch, rhythm

`6fb37c9f`

The dwell anchor, the arrival-offer settlement id and cooldown stamp,
and the per-hour rhythm snapshot are absolute-clock state on
process-lifetime singletons. Each now exposes ResetForNewSession, and
the rhythm service's uncalled Invalidate is renamed to match. Wiring
follows in the next commit.

#### fix(crash-report): v2.0.30 - drop four finalizers on empty virtuals

`8bd44b92`

Plan 006, step 1 of 4. Four Patch37_CrashReport finalizers targeted
base virtual methods (MissionBehavior.OnMissionTick,
MBSubModuleBase.OnSubModuleLoad, MissionView.OnMissionScreenTick and
ScriptComponentBehavior.OnTick). Harmony rewrites the exact method it
is given, and an override is a different method, so these finalizers
only ran when an override called the empty base body. They could never
capture the throws they were written for.

Patch37TargetShapeTests resolves every Patch37 target from its
[HarmonyPatch] attributes against the installed engine and fails on an
overridable virtual, so the shape cannot come back.

#### fix(map-load): v2.0.30 - convergence fixes for plan 012

`a4492fa8`

The convergence pass on the review-fix commit found three prose
defects. The screen list the review called complete still left out
the barber and the face generator, which lower the loading window on
every ready frame through BodyGeneratorView.OnTick; the patch summary,
feature doc and CHANGELOG now name them and read as non-exhaustive,
and the lessons, REVIEW-LOG, RCA and report drop "full" and "every".
A test comment described the helper-hop mechanism wrongly (the tracer
skips frames by position, so a helper pushes the Postfix into the
first-caller slot). "Two lines above" became "one paragraph above".

Comments and docs only. Full suite: 10244 passed, 2 failed (the two
known live-Armory tests), 2 skipped.

#### fix(harmony): v2.0.30 - convergence fixes for plan 009

`4c728dac`

The convergence review of bdf7d515 found no runtime defect and three
text defects, all confirmed against the code.

- Plan 018 anchors on the ReportPatchFailures calls this fix changed
  (5 before, 4 now) and its ReportFaults repeats finding 1. Listed
  under FOLLOW-UP for a re-cut after 009 merges; plans/ added to the
  rename-grep lesson.
- The SubModule comment, crash-report.md and the lifecycle doc now name
  the assembly-wide category index case, where one attribute naming a
  missing engine type fails every category.
- RCA summary count corrected to 8 LOW and 2 nits.

Nits: the chat-log lesson names both subscribers (MPChatVM and
ChatLogMessageManager), and the lifecycle row for
OnBeforeInitialModuleScreenSetAsRoot points at :623 and mentions the
startup inquiry. Comment and doc changes only; full suite unchanged.

#### fix(bindings): v2.0.30 - convergence fixes for plan 008

`2ca0805b`

The convergence pass on the review-fix commit 549afffd reported three
LOW defects. All three were confirmed against the code and are fixed.

- notify-test-results.sh: the all-skipped branch fired before the
  "Failed" fallback, so a "Test Run Failed." or "Test Run Aborted."
  response whose only count was Skipped: printed PASSED WITH SKIPS.
  With no Passed: count the branch now needs "Test Run Successful.".
  Two new test_hooks.sh 7c cases went red first.
- verify-bindings Step 2 no longer says every failure is in its table;
  it names "Main/SubModule.cs not found" as a precondition to report.
- Two test comments state the resolver fallback exactly and drop the
  stale ~37 model count.

Full suite: Failed 2 (the known live-Armory tests), Passed 10243,
Skipped 2. test_hooks.sh: 289 passed, 0 failed.

#### fix(map-load): v2.0.30 - review follow-ups for plan 012

`15e5475a`

Deep review (six lenses) and Codex gpt-6-astra ultra on plan 012
found no HIGH or MED code defect. This commit fixes the four LOW
findings and records the review.

- A round-trip test runs the real Prefix into the Postfix; a Prefix
  hard-coded to true now fails it (mutant run).
- The lowered-trace test asserts one LogInfo, no fallback caller
  chain and a first caller that is not the patch class; a helper hop
  before TraceWithCallers now fails it (mutant run). The two
  signature tests are renamed to the three-part shape.
- The screen list (inventory, clan, kingdom, quests, character,
  crafting, banner editor), the gate's "unconditionally", the class
  summary, the feature-map row and a registry identifier corrected;
  the feature doc says raises are traced per call.

Still needs Mike: a GitHub issue for the change (/issue is public).
Full suite: 10244 passed, 2 skipped, 2 failed (the two known
live-Armory tests).

Report and RCA, both in docs/reviews/:
  deep-review-012-loading-window-trace-per-frame-2026-09-24.md
  rca-loading-window-trace-per-frame-2026-09-24.md

#### fix(harmony): v2.0.30 - review follow-ups for plan 009

`bdf7d515`

The module-load failure notice was sent from OnSubModuleLoad, where
InformationManager.DisplayMessage has no subscriber yet, and the list
was cleared as it was sent. A chat line at the first main menu would
also be cleared after the splash video. Startup failures now go into
one inquiry from the main-menu one-shot, which Native's query manager
queues and the initial screen does not clear.

The summary no longer says a failed group is wholly off (Harmony keeps
the classes it applied before the failing one), and the docs name the
case that is not category-local: an unreadable attribute fails
Harmony's assembly-wide category index for every call.

The Data Flow lens and the Harmony lesson now grep for
TryPatchCategory, and the battle-load triage tool names the
[PatchApply] line instead of the deleted Patch43 warning. Stale
comments at the Patch77, Patch61 and Patch83 sites are corrected.

Tests: PatchCategoryApplierTests 13 (RED first for the notice and the
wording). Full suite 10248 passed, 2 skipped, 2 known live-Armory
failures.

Report:
docs/reviews/deep-review-009-guarded-patch-category-apply-2026-09-24.md
RCA:
docs/reviews/rca-guarded-patch-category-apply-2026-09-24.md

Deferred: failures left by an aborted game-init batch are labelled
"mission start"; rides on the plan's residual bare initializers.

#### fix(bindings): v2.0.30 - review follow-ups for plan 008

`549afffd`

Deep review (seven lenses) and a Codex adversarial pass on plan 008
confirmed 13 defects, 0 HIGH, and 1 false positive. Ten are fixed here:

- two tests pin the resolver's override and game-dir guards; deleting
  either guard turns one of them red
- notify-test-results.sh names the skips of an all-skipped run at
  normal verbosity (a new test_hooks.sh 7c case, red first)
- the hooks catalog and CHANGELOG no longer claim the banner reaches
  Claude: stderr from an exit-0 hook goes to the debug log only
- the verify-bindings skill states which gate tests go Inconclusive
  and triages the two red forms plan 008 added
- reflection-sites.md gives the strict gate command, the floor
  messages drop stale counts, the GameAssemblies comment is exact,
  and the CHANGELOG entry moves to its local date

Left for Mike: the banner's delivery channel (with Codex P3),
TreatNoTestsAsError, the CI step's if:, build folder first, and the
GitHub issue.

Report: docs/reviews/deep-review-008-binding-gate-no-silent-skips-2026-09-24.md
RCA: docs/reviews/rca-binding-gate-no-silent-skips-2026-09-24.md

#### fix(harmony): v2.0.30 - apply every patch category through one guard

`45bcf80b`

SubModule applied its Harmony patches with bare
_harmony.PatchCategory("PatchNN_X") calls, 64 of 84 with no guard.
Harmony 2.4.2 has no catch around a category, so one patch class whose
target no longer resolves threw out of the SubModule hook: in
OnSubModuleLoad the engine rethrows and the game does not start; in
OnGameInitializationFinished the once-per-process flag is already set,
so every later category (the Patch65, Patch82 and Patch84 crash guards
among them), the watchdogs, ManualPatchApplicator and the Harmony
census were skipped for the process.

Every category now goes through TryPatchCategory, backed by the new
PatchCategoryApplier. A failure logs [PatchApply] at Error with its
full cause, costs only that category, and is named in one red
on-screen line per phase. The delegate passes typeof(SubModule)
.Assembly explicitly because the one-argument overload reads the
caller's stack frame. The Patch37, preview-batch and Patch77 sites
keep their side effects (hooks subscribe only on success, the preview
log says FAILED, a Patch77 failure still disables the switcher); nine
per-category try/catch blocks collapse into the helper. A failed
Patch61 or Patch89 main category no longer skips its sub-categories,
on purpose.

The Patch37 comment claimed its finalizers covered the rest of
OnSubModuleLoad; they cannot, and the comment now says why. Nine text
tests now pin TryPatchCategory( and the Patch65 test comment no longer
claims the batch is unguarded. PatchCategoryApplierTests adds 9 tests,
including the real Harmony 2.4.2 throw and a source gate against any
new direct PatchCategory call in Main.

Implements plan 009 (plans/009-guarded-patch-category-apply.md).

Not-tested: live engine apply path and the red notice (needs the game)
Save-compat: no save data touched

#### fix(config): v2.0.30 - Review 130 follow-ups: list entries, formations

`b2e387db`

The four owed follow-ups from Codex review 130, reviewed and fixed
where they were a problem.

- DreadAura, UncapturableHeroes and BannerBearers config loaders drop
  a null or blank list entry with a warning and trim a padded one.
  Before, a padded name matched nobody without a word; a padded
  excludeHeroIds entry left its hero uncapturable.
- AllowedFormationGroups kept anything Enum.TryParse accepted
  (" infantry ", "2", "Infantry, Ranged"), which the service's name
  compare never matches: as the only entry it switched every banner
  bearer off. It now takes declared names only through the new
  TAOM.Core.Validation.EnumNames (StrikeNames' private parser, moved
  and shared), stored as the enum prints them.
- CustomAttacksUtils comments and tests no longer give the spider AV's
  retracted cause and name the real native sinks; comments only.
- Sharing one compiled lords.xslt would save under a second (one
  full-file test). Still owed: nazghul in the Load Game thumbnail's
  safe list, after an in-game render test.

No change with the shipped configs. Other instances of the same
classes are listed in REVIEW-LOG Review 130 for Mike's word; no issue
filed. Reviewed: /deep-review, six lenses and a convergence pass; RCA
rows in docs/reviews/rca-nazgul-scream-2026-09-23.md. Full suite:
10303 passed, 2 skipped. This commit alone on c79a5852 in a clean
worktree: 10235 passed, 2 skipped, 2 failed; both failures
(ElkConfigTests, AnimaliaMountWiringTests) fail the same on bare
c79a5852, whose elk and moose data is still uncommitted.

#### fix(nazgul): v2.0.30 - Codex review follow-ups for #644 and #645

`437d5911`

Codex (gpt-6-astra, ultra) reviewed 9804f67b and b90fd3a4 in one run:
no P1 or P2, two P3 observations, both fixed.

- A signature whose identity list held only a null or blank entry
  loaded as valid and matched nobody. ValidateList now removes a null
  or blank entry with a warning and trims a padded one, as the id and
  the sound already were.
- The #644 race gate read one regex spelling of the XSLT race
  attribute. RacesEmittedByXslt now parses each stylesheet, reads every
  race verbatim as the engine does, and fails on a race computed at
  transform time; it finds the same 19 emissions.

Verifying Codex's notes corrected four sentences: the restore-skip
comment and hero-race.md now name all three runtime race writers
(co-op join reconciliation was missing), the #644 CHANGELOG entry says
176 (not 173) and that only a new campaign gets the one kit, and the
sound gate's comment and doc no longer cite the spider AV as a proven
NaN crash.

The fix diff got its own six-lens deep review (two MED, several LOW,
all fixed); Mike kept the stylesheet scan over checking lords.xslt's
transform output. RCAs: the "Codex pass" sections of
docs/reviews/rca-nazgul-race-2026-09-23.md and
rca-nazgul-scream-2026-09-23.md; REVIEW-LOG Review 130. Full suite:
10208 passed, 2 skipped on the shared tree, and 10106 passed, 2
skipped for this commit alone on b90fd3a4 in a clean worktree. Not
smoked in game.

#### fix(mission): v2.0.30 - park off-thread callback writes (#634)

`f75288eb`

A player froze mid-battle on the 1.4.5 line: heap flat, no exception,
no crash report. The #595 tripwire had caught OnAgentRemoved,
OnObjectUsed and four more engine callbacks off the main thread, which
the committed thread map said could not happen. The next frame's
OnPreTick waits in Mission.WaitTickCompletion for the agent tick, so a
stuck agent tick freezes the game with exactly this log shape.

Every TAOM writer those callbacks reach now keeps off main-thread state:
DeferredCallbackQueue.RunOrDefer runs a write inline on the main thread
and parks it for the owner's next OnMissionTick anywhere else. Parked:
the behaviour-tree component's removal, mount despawn's kill record and
forget, the career teardown when the player falls and its hit notice,
SpatialGrid removals and the warg dismount reset. Each owner marks the
main thread itself. RegisterKill takes a lock (its map's insertion
order breaks kill ties), the SignatureStrikes roster is a concurrent
map, the enlistment kill count is Interlocked. Off-thread reports go to
the file log at WARNING, never to an on-screen logger.

The cause is not proven, so Patch91 brackets the agent tick and the
main thread's whole mission frame (MissionState.TickMissionAux, armed
only while Continuing) and MissionTickStallWatchdog logs each stuck
thread's stack as [MissionStall] at 10/20/40 s. New MCM toggle, default
ON, independent of the master toggle, local-only for co-op.

Deep review (6 lenses + convergence) widened the writer census from
three to nine, moved the frame probe off Mission.OnTick (blind to a
hang inside native Mission.Tick, and it false-alarmed on teardown), and
reverted one proposal that would have scrambled kill-tie order. RCA:
docs/reviews/rca-offthread-agent-removed-2026-09-22.md.

Research: Mission.OnTick/OnPreTick/TickAgentsAndTeamsImp/OnAgentRemoved, MissionState.TickMissionAux (v1.4.8 dump + v1.5.3 taom-src, identical)
Constraint: native raises OnAgentRemoved and friends on the thread it chooses; no managed code shows which
Rejected: ConcurrentDictionary for the field commission kill map (EndBattle ties follow insertion order)
Not-tested: Thread.Suspend capture on a live thread; SignatureAgentRoster and Enlistment races (a live Agent cannot be built in tests)
Save-compat: no saved state touched

#### fix(mumakil): v2.0.30 - the tower's crew stand on a navmesh (#627)

`3515c5c5`

All eight archers confirmed drawing and loosing in game. The feature shipped
two days ago with fifteen green gates and a design that did not work at all:
every archer was placed on its seat correct to the centimetre in x and y, and
fell nine metres to the ground.

A teleport cannot hold an agent up. TeleportToPosition is a bare native
SetPosition with no ground snapping, so it reads as though it must work, and
at the elephant's 3.2 m it has worked for months. At nine it holds nobody:
25,000 teleports in one battle lifted no one off the ground. NavMeshPrefabName
appears in exactly two engine classes, SiegeTower and MissionShip, and neither
contains a single call that moves an agent. Both attach a navmesh prefab whose
faces ride their own entity. This one now does the same, face group 1, the
group that travels unconnected; 2 and 3 would splice a deck nine metres up
into the world navmesh.

Navmesh is necessary and not sufficient. Stripping the floor slabs while
keeping it dropped everyone again: navmesh makes a position valid to stand at,
a body is what you stand on. The slabs stayed, squashed 61 cm to 3 cm, because
the body is a door lintel whose raised ends stood 70 cm proud at the platform's
scale and four archers were standing on them.

Two placement rules, both now gates. A 0.74 m body overhanging its navmesh or
its deck edge gets resolved elsewhere and sits at a fixed height above its
seat, immovable; three held there through 7,619 teleports each. And the most
forward frame on each deck re-nocked forever while its deckmates shot, three
times in three battles: the beast's head is at their height and the mahout is
under the sightline. Frames now sit a body radius inside the deck, behind the
shoulders.

The prefab is authored at final in-game size with no runtime scale, since no
vanilla object carrying physics or navmesh is ever runtime-scaled; a test pins
body_length=300. Locomotion is pinned once at seating rather than on the
re-assert clock, after re-applying it mid-draw matched the draw dying on all
eight seats in lockstep.

Full suite green: 9,912 passed, 2 skipped, 0 failed.

Research: MissionObject.AttachDynamicNavmeshToEntity, SiegeTower, MissionShip,
  Agent.TeleportToPosition, Agent.SetMaximumSpeedLimit, Mat3.Orthonormalize,
  Native/monsters.xml human body_capsule
Constraint: the navmesh .bin's face encoding is not understood, so its topology
  cannot be changed without a fresh Kit export; vertex positions can be and were
Not-tested: a campaign battle with several mumakil, mission end with crew
  aboard, and shooting at a target directly below
RCA: docs/reviews/rca-mumakil-navmesh-2026-09-22.md

#### fix(ranged): v2.0.30 - #617 second review: guards, cache, crossbow rule

`b904e02a`

The tools half of the #617 second deep review and of the review of
its fixes. The live Armory's 27 ladder crossbows were regenerated from
the new spec; the lotraom-assets mirror commit carries them. RCA:
docs/reviews/rca-ranged-rebalance-second-review-2026-09-18.md

- Crossbows out-hit and out-aim every bow of their kingdom at the same
  tier (Mike): crossbow_bonus 3 to 9, crossbow damage T7 to T10
  107 / 126 / 134 / 142. Pinned by a shipped-spec test.
- rebalance_ranged_ladders refuses a planned cell no file defines
  (the placeholder merge had let it through with exit 0).
- The sync tool keeps the 12 translation caches in step (-130 / +123
  ids each), backs up live files, refuses an unparseable ladder row, a
  bad spec, an unreadable cache or a missing --cache-dir.
- restat validates its tables, including an id in both of them, in
  the shared validator; generator and restat write no sidecar into
  the mirror.
- rebalance_troops: the Iron Hills nobles leave SKIP_TROOP_IDS; the
  militia regex is the validator's object; one battle_sets reading
  with the ladder; undefined_ladder_ids reads the rosters as XML, so a
  single-quoted reference is seen.
- analyze_troop_balance references a ladder troop's cell (it judged
  179 of 227 archers by the level curve).
- planned_skill_edits no longer refuses a templated ladder troop: on
  1.5.3 an inline skill row overrides the template.
- enlist_ rosters join the hero ceiling sweep; one ladder id regex;
  shared mirror path and tier numeral regex; dead code removed.

Tools suite 1773 passed; generator, restat and sync --verify OK in
both trees; roster tool 0 pending edits; validator no error and no
RANGED_* finding.

Research: DefaultItemValueModel.CalculateAmmoTier (1.5.3)
Research: BasicCharacterObject.Deserialize (1.5.3 against 1.4.8)
Research: CustomBattleAgentStatCalculateModel.GetWeaponInaccuracy
Not-tested: in game (Custom Battle, campaign crossbow against bow)

### Data

#### data(creatures): v2.0.30 - chariot and spider hit capsules fitted

`68529428`

The chariot's and spider's per-bone hit capsules were Modding Kit
defaults (57 of 60 and 40 of 62). Fitted to the skinned mesh with
tools/skeleton_hit_capsules.py and patched into the live Armory
packages: chariot 8.8% to 98.0% of the skin hittable (53 refit),
spider 49.9% to 97.2% (38 refit; the Brown and Pale Spiders share
the skeleton). Both read back through TpacTool.Lib with zero
mismatches. The chariot's cart body and wheels stay mostly
unhittable; whether cart hits should damage it is left open. The
Yotthani adoption report records the finished download matches.

Not-tested: in game; load the Armory in the Kit once, then a Custom
Battle hit test on each
Refs: #624

### Performance

#### perf(battlebalance): v2.0.30 - read the MCM settings once per process

`7feca96b`

Plan 003, ported from the June branch impl-003 (6eb5955c) through the
review pipeline (Mike's decision 17); impl-003's warg half is
superseded by plan 015.

BattleBalanceSettingsProvider resolved TaomSettings.Instance on every
property read, and GetDefaultTroopPower reads up to seven per troop per
simulation round. It now takes the reference once in its constructor
and reads through it, so live MCM edits still apply, as in
NameplateFadeSettingsProvider.

Precondition, checked: the only resolve is RegisterBattleBalanceAndTargeting
under OnGameStart, after MCM creates the settings; the eager pass at the
end of IoC.Configure (OnSubModuleLoad) does not reach it, so the
constructor never caches a null.

Tests: 6eb5955c's six no-MCM default pins (green before and after), and
a new IL rule, Getters_NeverReadTaomSettingsInstance_TheConstructorDoes,
RED against the old provider (the getters read Instance). Full suite:
10320 passed, 2 skipped, 0 failed.

#### perf(hooks): v2.0.30 - skip Python in Bash hooks on non-git calls

`90141357`

Every Bash call ran 13 hook scripts, each starting Python twice (the
_pybin.sh probe, then a JSON parse) before looking at the command.
Each Bash hook now tests the raw payload for its trigger text (git,
dotnet or build.ps1) first and allows without Python when it is
absent: 256-451 ms per hook on an ls drops to 60-150 ms. New
tools/test_hooks.sh section 4c pins both directions; an old-versus-new
run over 156 payloads found no changed decision. Plan 013.

Committed by the orchestrator: the plan's pre-commit STOP fired only
because another session had staged files in the main tree's index,
which the harness gates read; this commit is made in the worktree's
own index and carries its own CHANGELOG entry.

Not-tested: the edited hooks under a live harness (a worktree's hooks
do not run in the session; proof owed after merge)

#### perf(combat): v2.0.30 - range-gate bone targets before skeletons

`7577894d`

While a warg's bite is live, the bone check asked the engine for a new
native Skeleton wrapper (a ref-count call, a lock, a GCHandle and a
finalizer) for every agent captured within 20 m on every frame, before
testing whether that agent was inside the roughly 4.5 m gate it can
actually hit (plan 015).

The target loop moves into CheckTargets and tests the same gate (the
target's visuals frame origin against the attacker's, same comparison)
before fetching the skeleton; a target outside it stays for a later
frame. The attacker's skeleton is fetched once per tick and its bone
positions reuse one list. The stale #219 comment in
BoneCheckDuringAnimation now names the real gate.

Per the orchestrator's amendment to plan 015, the target skeleton's
null test is `is null`: `== null` binds to NativeObject.operator ==,
whose first use runs NativeObject's native static constructor and threw
TypeInitializationException in the test host. The operator returns the
same result for a null comparison.

BoneCheckRangeGateTests pins the gate: a far target's skeleton is never
fetched and it stays a target; one on the gate edge is fetched.

Not-tested: BoneCheckDuringAnimation.Tick (ActionIndexCache, engine).

#### perf(combat): v2.0.30 - key SpatialGrid cells on x and y only

`e5f644da`

The grid was keyed on x, y and z at 20 m cells, so the warg's 60 m
"no enemy close" scan looked up 7 x 7 x 7 = 343 cells on every tick
while a battlefield is a few metres tall (plan 015). Cells are now
keyed on (x, y): that scan looks up 49 cells and a 10 m scan 4. The
distance test stays 3D, so every query returns the same set of agents,
which SpatialGridQueryTests' brute-force comparison confirms.

Order within one 20 m column is now rebuild order rather than lower
height band first. Only a stop-on-first-hit bite with two targets in
reach on the same frame, straddling a multiple of 20 m in height, can
land on the other one.

#### perf(crash-report): v2.0.30 - log repeat suppressions at powers of ten

`fb52e619`

Plan 006, step 4 of 4 (code). CrashReportService.HandleException wrote
one suppression log line per occurrence, so a crash that recurs every
frame wrote a line per frame into the taom_debug.log that the next
bundle tails.

CrashBundleThrottle.IsLoggedOccurrence now picks occurrences 1 and 2
(the only trace of a cap or cooldown suppression, and the first repeat
of a bundled crash) and then every power of ten. Suppression itself is
unchanged; only the log line is gated.

Not-tested: the gated LogError inside HandleException (needs the full collector graph)

#### perf(warg): v2.0.30 - resolve tree services once, scan into buffers

`87f9f862`

A warg tree's root runs on every mission tick, so four node types and
the player's rider-hand manager paid a DryIoc lookup per frame, and the
three enemy scans allocated a fresh List<Agent> each call (plan 015).

The nodes now resolve IMissionAdapterFactory (and WargAttackTask its
IWargAttackService) once into private readonly instance fields when
the tree is built, and scan into a reused buffer through the
zero-allocation SpatialGrid overload. WargRiderHandManager.Tick takes
the factory WargMissionBehavior resolves in its constructor, whose
signature is unchanged. The warg adapter lookup is hoisted out of the
two attack-check loops; the factory caches adapters per Agent.

WargTickCostTests pins both rules in the IL. Its one-level-down
descent skips a same-type helper whose body names a
TaleWorlds.MountAndBlade.View type, which the test host cannot load.

Not-tested: node constructors resolving in game (needs a mission).

#### perf(patchshield): v2.0.30 - skip the ManagedCallbacks callback shims

`2a3f71b1`

PatchShield pass 2 runs inside the loading screen of the first game
start and attached 372 finalizers in about 69 s on the maintainer's
machine (about 186 ms per Harmony.Patch). About 247 of those land on
the engine's native-to-managed callback shims in namespace
ManagedCallbacks, which TAOM's Native2ManagedPatcher already wraps
with a finalizer that swallows every exception while crash capture is
on. PatchShield adds nothing there: its rescue strips prefixes,
postfixes and transpilers, and those shims carry only finalizers.

The hot-layer exclusion list moves from PatchShield into
PatchShieldPolicy (unchanged for the three #331 entries) behind a pure
IsExcludedTargetNamespace, and gains "ManagedCallbacks". PatchShield's
IsExcludedTarget delegates to it and still fails open.

The shield pass line in diag.log now ends with the pass's elapsed time
and ms per attach, formatted by the tested FormatShieldPassSummary with
the invariant culture; the existing prefix up to "(total: N)" is
byte-identical.

Seven new PatchShieldPolicyTests pin the moved list, the gameplay
namespaces that must stay shielded, the new entry and the formatter.

Plan 007 (plans/007-patchshield-skip-callback-shims.md).

Not-tested: live pass-2 attach count and timing (needs a game start)

#### perf(crash-report): v2.0.30 - allowlist six callback shims, not 247

`1d94df9c`

Plan 006, step 2 of 4. Native2ManagedPatcher patched every static
method of the engine's three *CallbacksGenerated classes (247 on the
live log). Each harmony.Patch costs about 120 to 190 ms on the
maintainer's desktop, so the sweep took 29 to 33 s of every launch,
and in 30 logged sessions it never captured an exception.

The patcher now attaches the finalizer to the six shims listed in the
new Native2ManagedTargets, each chosen because it covers managed work
no Patch37 finalizer already wraps: the screen early, late and input
ticks, ManagedScriptHolder.TickComponents (the real dispatcher of every
ScriptComponentBehavior.OnTick override), the thumbnail completion
callback and the character tableau setup. A missing name costs one
warning line, and the attach now logs its own time.

Native2ManagedTargetsTests pins every name against the installed
engine and keeps the list at most 12 entries.

Not-tested: the live attach and its boot time need a game launch
Research: v1.5.3 decompile of ManagedCallbacks.*CallbacksGenerated

#### perf(map-load): v2.0.30 - log a loading-window lower only when it drops

`e6c03411`

Plan 012, step 2 of 3. The Patch89 lifecycle postfix on
LoadingWindow.DisableGlobalLoadingWindow walked the managed stack and
wrote a flushed LogInfo line on every call. The engine calls it on
every frame of the main menu, the party screen and character
creation, so one 35-minute session wrote an 84 MB log that was
262,763 lowered lines, and three hours on the main menu wrote
1.16 GB.

The engine clears IsLoadingWindowActive unconditionally before any
postfix runs, so a Prefix now captures the flag into __state and the
Postfix traces only when LoadingWindowTraceGate.IsRealLower says the
window went from up to down. TraceWithCallers is still called
directly from the Postfix, so its two-frame skip still lands on the
real caller. The Enable patch is unchanged. Four tests pin the
Prefix and Postfix shapes (Harmony binds __state by name) and both
behaviours.

Not-tested: Harmony applying the Prefix and handing __state to the
Postfix in game, and the frame-time saving; both need an in-game
session.

#### perf(map-load): v2.0.30 - add the loading-window real-lower gate

`8028bf3d`

Plan 012, step 1 of 3. LoadingWindowTraceGate.IsRealLower decides
whether a DisableGlobalLoadingWindow call actually took the window
down: the flag was true before the call and is false after it. The
engine calls that method on every frame of the main menu, the party
screen and character creation and clears the flag unconditionally,
so only the before and after values together separate a real lower
from a no-op. The gate is a pure function of two bools so the patch
stays a thin boundary (ADR-002, ADR-008); four tests pin the whole
before-by-after table.

### Refactoring

#### refactor(hooks): v2.0.30 - drop the push line ordering

`60d85ed7`

The force-push guard sorted the lines it judges so that lines able to
force came first, shortest first. That order only existed to make the
old bash judge reach a short force push before a long commit message
holding "push" in time. The judge now runs in Python inside a
deadline and takes every shape in well under a second in any order,
so the sort, its FORCE_HINT pattern and the four tests that pinned it
are gone (#680). The lines are judged in the order they are produced,
and a new test pins that order.

Which pushes are refused does not change. What changes is the branch
a message names when one command refuses or warns about more than
one: the guard still stops at the first refused line and keeps the
last warning, so the branch named now follows the order in which it
reads the command rather than line length. A sweep of 1,093 commands
(4,372 runs per hook, both shell tools, a trunk and a feature
checkout) against the previous commit found 0 exit-code differences
and 349 message-only ones: 319 BLOCKED and 30 WARNING lines name a
different branch.

The 7i comment in tools/test_hooks.sh and the tooling lessons record
the change.

#### refactor(seams): v2.0.30 - move decision logic out of nine seams

`a3957541`

Nine protected-virtual seams in SupplyOrderService, RefugeService,
WardenService and CampService carried TAOM decisions that no unit
test ran, because the test subclasses override every seam: the supply
payee routing and its unreachable-payee fallback; the refund route,
count filter and volunteer-slot walk; the refuge raid-target filters
and nearest pick; the peace release of refuge-held prisoners; the
warden companion filter; the promotion policy (culture, template
fallback, age and rename tolerance); and the nearest town-or-castle
search behind the camp and refuge keep-outs.

Each decision moves into its service as an internal method, and each
seam is now one engine operation with ids, primitives and TAOM data
classes in its signature, per the ADR-007 seam rule (plan 021,
decisions 49, 55, 56 and 57). The two identical fortification
searches now share FortificationSearch.NearestDistance, declared in
CampService.cs. Behaviour is unchanged: amounts, flags, log texts,
filter order, the strict comparisons, the tie-break, the RNG draws,
the silent drop of surplus recruits and the backwards prisoner walk
are as before. The war checks stay separate seams, so the engine's
MapFaction is read only where it was before. 76 new tests pin the
moved logic. This implements improvement plan 026.

Not-tested: the seam bodies (GiveGoldAction, the roster, item and
volunteer-slot writes, the MobileParty.All and Settlement.All scans,
EndCaptivityAction, HeroCreator and the enrol actions) run only in
game.

#### refactor(cache-rebuild): v2.0.30 - delete two unreachable scaffolds

`032481cc`

IEditorSceneAdapter had no implementation and no reference. The
EditorCacheRebuild Caching/ folder (PathReuseCache,
PersistentPathCache, their interfaces, NavigationPathCloner,
SortedPathKey) was registered in EditorCacheRebuildIoC but never
resolved or injected, so it never ran. Both came in with 6a80bac6
(2026-05-12) and never gained a caller.

Removed with them: the two registrations, the reserved
EnablePathReuse and EnablePersistentPathCache config properties
(not in the shipped JSON, never read), their 26 tests, and the
ReflectionSiteBindingTests row for PathReuseCache._store, which
named a TaleWorlds.Engine type that does not exist and resolved
only to TAOM's own class. A new test pins that a config still
carrying the two keys loads; another pins the types' absence.

Git history is the recovery path: 6a80bac6 holds the scaffold if
Phase 2 path reuse is ever built. Plan 025, maintainer decision 21.

Full suite: 10288 passed, 2 skipped, 0 failed.

Not-tested: in-game distance cache rebuild; no runtime path changed.

#### refactor(composition): v2.0.30 - move WandererAllegiance into a module

`44045b34`

Plan 018, step 2: the first feature moved off the two single-owner
files. WandererAllegianceModule registers the feature's services
(WandererAllegianceIoC now takes an IRegistrator) and declares its
dialog behavior; FeatureModules.All lists it, and its registration line
in Main/IoC.cs and its AddBehavior line in Main/SubModule.cs are gone.

The behavior is still a container singleton. It is now added after
every hand-wired behavior, which is order-free: its two lines are the
only TAOM lines on companion_hire and outrank vanilla's reply by
priority (110 over 100), and LotrIssueSuppression.SuppressAll removes
only vanilla issue types.

FeatureModulesTests gains generic declaration tests: a type declared by
a module and still wired by hand in SubModule.cs, a behavior, slot or
category declared twice, and a behavior that persists data without
OwnsSaveData all go red. WandererAllegianceWiringTests replaces its two
text asserts with a list check, a no-hand-registration check and a
handshake against a real DryIoc container. The feature doc and
CHANGELOG describe the new wiring.

Not-tested: in-game wanderer refusal (needs a campaign)
Save-compat: the behavior's SyncData is empty; no save data touched

#### refactor(composition): v2.0.30 - add the feature-module runner, empty

`9ed3ee30`

Plan 018, step 1. Every feature is wired by hand into Main/SubModule.cs
and Main/IoC.cs, so most feature commits edit one of the two
single-owner files and parallel sessions collide there. This adds the
contract a feature can own instead, with an empty module list, so
behaviour is unchanged.

Main/Composition holds the module contract (ITaomFeatureModule and the
TaomFeatureModule base with empty defaults), the declaration types for
patch categories, campaign behaviors, game models and mission
behaviors, the ordered FeatureModules.All list, and ModuleRunner: it
visits modules in list order, logs and skips a module that throws for
the rest of the session, and fails closed only for a module that owns
save data in the three steps that decide whether its SyncData runs.
Module categories go through plan 009's guarded TryPatchCategory, so a
failed category is reported by ReportPatchFailures, not as a fault.
FeatureModuleHooks is the engine-facing half; it holds startup faults
for one main-menu inquiry and shows in-game faults as a red line, the
startup inquiry rule plan 009 follows.

The kernel gains one line per phase: IoC.Configure registers modules
after the last hand-wired feature and initialises their statics last;
SubModule calls the runner at the end of each phase's feature block,
before 009's report for that phase. FeatureModulesTests pins each call
between its anchors.

Trade-off: six small types and eight kernel lines before a second
module uses them. In return every later migration only deletes lines
from the two single-owner files.

Not-tested: FeatureModuleHooks against a live engine (needs the game)
Save-compat: no save data touched

#### refactor(combat): v2.0.30 - pin SpatialGrid queries behind helpers

`b8b14463`

The grid's cell build and radius query only ran through Agent, which
no test can construct, so nothing pinned what a scan returns (plan
015). Both now live in internal static generic helpers, BuildCells and
CollectInRadius, which never touch an Agent; UpdateGrid and the buffer
overload of GetAgentsInRadius delegate to them. The query reads each
agent's position once instead of three times and reports how many
cells it looked up. The grid is still keyed on x, y and z here, so
behaviour is unchanged.

SpatialGridQueryTests checks the helpers against a brute-force sphere
scan over 600 points, five radii and 25 centres each, plus the
inclusive boundary, a tall column, the include filter and buffer
clearing.

### Tests

#### test(tools): v2.0.30 - cover a never-retried failure and a URL scheme

`0cc91b32`

Two gaps the mutation check found after the tools fixes. The watch
fixture's only failed agent also had its key retried, so a watch that
ignored failed rows still passed; an agent that failed and was never
retried now stays quiet only when the failed row is read. The pinned
local-path rule declines a drive letter followed by two slashes, so a
one-letter URL scheme such as s://bucket/key is not a local path; a
test now holds that case.

#### test(ci): v2.0.30 - tag the sprint's engine-bound tests RequiresGame

`e9b28e2a`

Replaying the C# CI workflow on the merged sprint (reference
assemblies, no game) failed 69 unit tests the sprint branches added
before plan 010's CI existed. 68 threw from engine code the metadata-
only reference assemblies stub out (CampaignBehaviorBase, Mat3, Vec3,
ViewModel, Debug.DebugManager, ActionIndexCache, TextObject and
others); the last is a module-isolation test whose engine base
constructor throws on the stubs, so the isolation count reads 0.

Five classes where every test needs the engine get the class tag; in
ten others only the failing methods are tagged, as the tests rule
permits, so the stub-safe tests keep running in CI.

Replay after tagging: unit 8494 executed, 8494 passed, 0 failed
(floor 7700); binding gate 344 of 344.

#### test(composition): v2.0.30 - read SubModule and IoC through one reader

`a0db0558`

Plan 018, step 0. The 26 test files that guard the wiring in
Main/SubModule.cs and Main/IoC.cs each located and read those files
their own way, as raw text. A raw read cannot tell code from a comment,
and a missing file sent most of them Inconclusive, so a gate could stop
guarding without saying so.

RepoPaths gains ReadSource (CRLF normalised, either separator, a missing
file fails the test) and StripComments (lifted from
CoopVetoClassificationTests: blanks comments to spaces, keeping length
and line breaks). All 26 files now read both files through
RepoPaths.ReadSource with stripComments on; their guards and orphaned
locator helpers are gone, and no assertion changed.

The switch exposed one false pass: GameModelOverrideBindingTests counted
TaomPartyNavigationModel as registered only because a commented-out
AddModel line contained "new TaomPartyNavigationModel(". The parked
model is now an explicit ParkedModels entry with its reason, and a new
test keeps that list honest in both directions.

Not-tested: none (test-only change)

#### test(bindings): v2.0.30 - make the binding gate fail loudly on skips

`8c89e042`

The BindingVerification gate passed by skipping. GameAssemblies found
the install only through BANNERLORD_OVERRIDE_DIR or BANNERLORD_GAME_DIR
in the test process, so a DLL built against the game but run without
them went Inconclusive, which MSTest reports as Skipped with exit 0.
The test-results hook then printed "PASSED (33 tests)" over 335 skips.

- TAOM.Tests.csproj records the build's GameFolder as TaomGameFolder
  assembly metadata; GameAssemblies falls back to it after the two
  variables, which keep precedence. Six new resolution tests.
- TAOM.Tests/binding-gate.runsettings maps Inconclusive to Failed for
  the gate only. The verify-bindings skill, the docs that give the gate
  command and a new CI step pass it explicitly. The default suite still
  skips when the game or the Armory is absent, as decided.
- The three discovery floors (fewer than 30 patch types or 20
  GameModels) now fail, since they only run once the game has loaded.
- notify-test-results.sh names skips (PASSED WITH SKIPS, and a skip
  count on a red run), pinned by tools/test_hooks.sh section 7c.

Evidence, strict gate on the desktop (368 tests): with the environment,
0 failed, 0 skipped; with both variables unset, 0 failed, 0 skipped
(the fallback); with a fake game folder, 335 failed and exit 1 (the
mapping); the same fake folder without the settings file, 335 skipped
and exit 0 (the decided default skip). Full suite: 10241 passed,
2 skipped, 2 failed (the two live-Armory tests that fail at the base).

Implements plan 008 (binding gate, no silent skips).

Not-tested: the discovery floors firing (needs a type-load break)
Not-tested: the new CI step (build.yml ignores pushes to 1.5.x)

### Documentation

#### docs(skills): v2.0.30 - Codex effort is sized to each review

`ef9559b1`

Codex reviews no longer always run at the ultra reasoning effort. The
session now chooses the effort for each dispatch from the size and risk
of the change: ultra or max for a large or risky first pass, xhigh for
a mid-size change, high for a focused pass over a few fixes. Every
codex exec template in the review-codex, codex-verify and deep-review
skills, the completion workflow, the Codex integration reference and
the review guide passes -c model_reasoning_effort="<level>", and the
message sent at dispatch names the level, the reason and the expected
window. The .codex/config.toml pin of ultra stays as the default and
ceiling, and the model stays gpt-6-astra.

Reviewing a git worktree: the skill now says to dispatch from the
worktree itself. codex doctor run in a worktree reports the same model
and three MCP servers as the main checkout, so the project config loads
there, and the sandbox then covers only the worktree. /improve keeps
dispatching from the main checkout on purpose, since its prompt forbids
every edit.

Stale text corrected on the way: codex-verify claimed AGENTS.md was over
32 KB and prescribed a check for a string AGENTS.md does not contain,
its report template named an old model, and the review guide still sent
readers to a retired Codex plugin.

#### docs(skills): v2.0.30 - hang triage confirms the process is the game

`9c6d4962`

The hang procedure in /native-crash-triage now opens with a step that
identifies the process before anything else. The Modding Kit also runs
as TaleWorlds.MountAndBlade.Launcher.exe, from bin\Win64_Shipping_wEditor,
and its long jobs (the settlement distance cache) sit at Not Responding
for minutes, so a process name proves nothing. The step reads the image
path, matches the PID to the session's rgl_log_<pid>.txt, and forbids
telling the user to end a process that has not been identified.

The lesson behind it is appended to
docs/reviews/lessons/build-tooling-workflow.md: a Custom Battle closed
at its victory screen was mistaken for a hang, and the editor was dumped
and nearly ended instead.

#### docs(improve): v2.0.30 - quote the last path templates, exit 1 wording

`6c4b0981`

The last convergence round found two text defects, fixed without a
third round as the skill's stop rule allows for a doc LOW: the TEMP
and commit -F templates in the dispatch rules now quote their paths
(Git Bash strips the backslashes of an unquoted Windows path), and
integrate_branch's exit 1 wording says a merge already in progress is
refused and left as it was. Also: no trailing backslash in the watch
folder example, and the run folder is passed to tools as an absolute
path.

#### docs(improve): v2.0.30 - merge, cleanup and watch text match the tools

`56e9970f`

The run protocol's cleanup deleted the improve/* branches from the
main checkout, whose unpulled trunk does not contain them, so git
refused each one and suggested the forced form, a hook ask. The
branches are now deleted from the integration worktree before it is
removed, and the integration branch, which tracks the pushed trunk,
after it.

The merge steps now match the tools: the dry run applies the union
rule, exit 1 means no merge started and exit 3 that one may be in
progress, a hand resolution is checked with git diff --cached --check
before its commit, and the lint step runs the worktree's own copy and
reads its ai_dashes count, since --fail-on-drift ignores dashes. The
watch text says it skips workflows folders.

The dispatch rules name branch --delete --force as a hook ask and no
longer claim a bash -c body is asked on; the rule forbids such text
anywhere in a command. A needs_mike line starting ADVERSARIAL
ESCALATION now tells the orchestrator to run the adversarial reviewer
before the branch counts as reviewed. Every path in the command
templates is quoted, since Git Bash drops an unquoted path's
backslashes.

#### docs(improve): v2.0.30 - name integrate_branch's exit 3 in the merge steps

`014e2302`

integrate_branch.py exits 3 on an unexpected error, possibly with a
merge already in progress; the merge checklist now says to report it
and read git status first.

#### docs(improve): v2.0.30 - close the 2026-09-23 run's PROGRESS

`4f1a7ed9`

The Opus review sprint's PROGRESS.md still showed seven RUNNING rows, a
TODO wrap-up and a Next pointing at phase W1, so a resume would have
landed in a run that ended days ago.

Each RUNNING row was a launch row whose result sits in another row; it
now reads SUPERSEDED and names that row (R4, which never got one, names
MERGE ROUND 1, where all four of its branches merged). A banner and a
closing row say the run is closed, and Next lists what its wrap-up still
owes: the merged improve/012 branch to delete, the Codex lessons
consolidation and REVIEW-LOG numbers (not recorded either way), and the
open FOR-MIKE items.

#### docs(improve): v2.0.30 - skill text matches the fixed run tools

`dc260635`

The /improve skill and its references now describe the run tools as
they behave after this round's fixes, and close the gaps the text
review found.

Merging: integrate_branch.py merges and stages but never commits. It
prints the commit command, and the orchestrator runs it through Bash so
every commit gate judges the message. The checklist gives its exit
codes, its diff3 union rule, the public-text check on the message file,
lint with --dash-base at the integration tip (HEAD sees nothing after a
commit), and a cleanup order that removes worktrees before git branch
-d, which refuses a branch checked out in one.

Liveness: watch flags STALLED-ASK when an agent's last hook "ask" has no
later tool result (direct spawns included) and STALE past 30 minutes,
and must be pointed at the running workflow's folder, since a killed
workflow's agents read as stale forever.

Workflow args: every documented improve_ctl.py args call passes
--scratch and --tmp (it exits 2 without them), and the run's worktree
root goes in the PROGRESS.md header so a resumed run has it. Each
script's header comment names its args; top-level fields ride in an
items object for plans.js and a fanout checker. Review items keep the
lenses they list, so lens 2 for changed text about engine behaviour is
added there. Blast radius runs the worktree's own graphify copy.

Other fixes: the four-agent pool is per workflow; a residual HIGH gets
one more round or a recorded deferral as deep-review requires, never
only a FOR-MIKE line; Codex output goes under the run's scratch folder,
because removing a worktree deletes its gitignored docs/reviews/raw/;
the plans commit names each path it wrote; the hook suite runs in the
background everywhere it is prescribed; hard rule 1 allows the CI
replay's extra flags.

#### docs(improve): v2.0.30 - dispatch rules: every ask form, retries

`37ddf739`

The standing rules every /improve agent receives now match what the
repository's hooks do.

HOOK-ASK lists every git form the confirm hooks answer "ask" on, each
checked against block-dangerous-git.sh and block-broad-git-add.sh: git
restore (all but --staged alone), checkout with --, -f, --force or a .
path, reset --hard, clean -f or --force, stash drop or clear, branch
-D, and add -A, --all, -u, --update or ., commit -a or --all. The old
list left out restore, checkout ., branch -D and the long flag
spellings, so an agent could stall a whole unattended run on one.

TIMEOUT gives the hook suite's full command: CLAUDE_PROJECT_DIR set to
the agent's worktree, run in the background because the suite takes
about 12 minutes and a foreground Bash call is capped at 10, then the
Summary line read from the log.

Shell hygiene says why python3 is banned: here it resolves to the
Microsoft Store alias under WindowsApps, which hangs from Git Bash.
Naming WindowsApps also clears the hook suite's section 1 check, which
failed on the unexplained python3 mention (937 passed, 1 failed).

Editing roles gain one line: a retried or resumed agent restarts in the
worktree its earlier attempt changed, so HEAD past its start commit or
a dirty tree means reading git log and status and continuing, never
resetting.

#### docs(improve): v2.0.30 - keep the playbook section names others cite

`84c606fa`

The audit playbook rewrite had renamed three sections ("Tech Debt &
Architecture", "Dependencies & Migrations", "DX & Tooling") while
removing its dashes. docs/reference/rule-provenance.md points readers
at "audit-playbook § Tech Debt & Architecture" for the deepening
deletion test, so the original names are back and that pointer holds.

#### docs(claude): v2.0.30 - /improve index row names the run variants

`989c8749`

The skills index in CLAUDE.md is what survives /compact, so its
/improve row now matches the rebuilt skill: besides audits, "what
next" and handoff plans, it covers executing, reviewing, merging and
resuming a plans backlog, and its gate column says that merge, push,
issues and paid calls happen only on the maintainer's word. CLAUDE.md
with its imports stays under the entry-doc budget (23,795 of 24,576
bytes by lint_docs.py).

#### docs(reviews): v2.0.30 - drop lesson counts, retire sprint scripts

`43db0b47`

The lessons index no longer prints a lesson count per category or a
total. Nothing computed those numbers, so they drifted, and every
merge that touched a lessons file had to recount them by hand or by a
script copied per worktree. The index keeps its category links and
names the one command that counts: grep -c '^### ' over the files. No
tool or test read the counts (checked across tools/, .claude/ and
.github/), so nothing else changes.

The 2026-09-23 sprint's orchestration scripts under
plans/_audit/2026-09-23-opus/machinery/ are removed; git history keeps
them as the run's record. The tracked copies were stale and unsafe to
run again: they check out with CRLF endings, which the Workflow tool
refuses, they lack the TIMEOUT and HOOK-ASK rules the later copies
carried, and they hard-code that run's dates, version, baseline and
paths. Their successors are the /improve workflows and the
improve_ctl.py, integrate_branch.py and check_public_text.py tools.
The run's PROGRESS.md log records the move.

#### docs(skills): v2.0.30 - non-deploying builds in /verify and Codex

`785b2703`

/verify and /review-codex built and tested without the flags that stop
a build copying into the game install, so running either one deployed,
and failed outright while Bannerlord was open. Both now use the
non-deploying forms from AGENTS.md: -p:DisableModuleCopy=true
-p:ModuleId= on build and test. /verify also drops --no-build from its
test step, because its build step compiles Main only and the test step
would otherwise run a stale test DLL.

The two fixed blocks every Codex prompt carries (the TAOM ID cheatsheet
and the prior review lessons) move verbatim from /review-codex Phase 2d
into .claude/skills/review-codex/references/prompt-fixed.md, so the
skill and the /improve Codex prompt builder read one copy.

/review-codex Phase 3h pointed at a "Lessons From Prior Reviews"
section of AGENTS.md that now lives in .ai/review-reference.md, with
the essays in docs/reviews/codex-track-record.md; it now names both
and says an /improve run consolidates them at wrap-up. Its note that
AGENTS.md overflows Codex's 32 KB read limit was also stale: the file
is held under 8 KB, and the check for a complete read now looks for a
rule that is in it.

#### docs(improve): v2.0.30 - rebuild the skill around the run pipeline

`1d1abd69`

/improve now describes the whole run that the 2026-09-23 review sprint
had to invent outside the skill: preflight and baseline, audit lanes,
a verify wave and critic, plans written, cold-reviewed and revised,
execution on improve/NNN branches in worktrees, review with a bounded
convergence loop, maintainer decisions, issues, merge and wrap-up.

SKILL.md is about 10 KB, so the whole body survives /compact. It holds
the authority rules (what needs the maintainer's word each run), 13
hard rules with their sources, the run folder, the variants (new:
review, merge, resume) and a phase map naming the workflow script or
tool for each phase. After compaction, a usage limit or a restart the
orchestrator reads the run's PROGRESS.md first.

New references:
- dispatch-rules.md: the standing rules every dispatched agent gets at
  the top of its prompt (HOOK-ASK, TIMEOUT, disk, non-deploying build
  and test, TDD, protected files, commit form, evidence).
- run-protocol.md: run folder shapes, resume, the liveness watch for an
  agent stalled on a hook ask, the live probe form, status updates,
  issue filing, the merge checklist and the wrap-up checklist.
- execute-and-review.md: replaces closing-the-loop.md with the
  preconditions, worktrees, the executor, the spec check, the review
  stop rule (at most two convergence rounds, then "ship and track"
  offered first), stopped plans, decisions and reconcile.

audit-playbook.md and plan-template.md are updated: every build and
test command carries -p:DisableModuleCopy=true -p:ModuleId= (the old
forms deployed into the game install), the tools that write on a
plain run are named, and the plan template gains a baseline line, the
graphify blast radius, a Step 0 for protected files, the versioned
commit subject and no CHANGELOG step. Their prose has no em or en
dashes.

The skill names the workflows under .claude/skills/improve/workflows/
and the tools improve_ctl.py, integrate_branch.py and
check_public_text.py, which land in their own changes.

Not-tested: the workflows and tools the skill names are built on
parallel branches; their command lines follow the pinned contract.

#### docs(plans): v2.0.30 - plan 027 merged, issues and a harness fact

`6fe4c8fe`

Plan 027 is merged into bannerlord-1.5.x (35bdf96d, with the fixes
dad9b169 and d2fa6d08). The plans index and the sprint progress
record say so, and name the issues: #682 closed, #680 (validate-push
timing on commands of 250 KB and more) and #681 (a Git Bash
here-string hang that predates the plan) open, shipped and tracked
by the maintainer's choice.

harness-facts.md gains one row: a PreToolUse ask prompts the user
even in bypass mode, and the tool call waits for an answer, so an
unattended agent keeps dangerous git text out of its shell commands.
Seen on 2026-09-26 from a plan 027 review agent; a headless claude -p
run did not wait.

#### docs(plans): v2.0.30 - plan 027 and its two cold reviews

`05dbc0d4`

The plan behind this branch (decision 61) and both cold reviews. Each
review found a blocking fail-open in the planned validate-push code
(an unescaped brace in a bash expansion; comment stripping and
PowerShell forms the new reader dropped) and a reviser fixed it with
proof on scratch copies of the hooks before execution. Plans only.

#### docs(hooks): v2.0.30 - catalogue the PowerShell gate coverage

`2d7770b6`

Record what plan 027 changed where the next hook author will look:
- hooks-catalog.md: the recount (27 files on disk, 26 registrations
  in settings.json, 31 in all, now that the PowerShell group is folded
  into one Bash|PowerShell group), a paragraph on how every git gate
  reads both shells through _shellwords.py and what it does not read,
  the eight gate rows marked PreToolUse (Bash, PowerShell), and the
  validate-push and mark-verification-run rows brought up to date
  (glob and heads/ refspecs, option values, # comments, the raw
  command judged beside the reader's text, is_interrupt).
- hook-authoring.md: a new git gate registers in the Bash|PowerShell
  group and reads taom_hook_command posix, with a table of the
  PowerShell forms the reader resolves.
- harness-facts.md: the PowerShell tool's payload shape, and the
  PostToolUseFailure input with is_interrupt read from the 2.1.241
  binary; whether a timeout sets it stays UNVERIFIED.
- mcp-servers.md: the git safety hooks match Bash|PowerShell.

lint_docs reports hook-authoring.md and harness-facts.md over the
path-scoped size cap (report-only; hook-authoring.md was already over
it before this change). Part of plan 027.

#### docs(plans): v2.0.30 - fix a mangled path in PROGRESS

`1b6b3b53`

A backslash-escaped Windows path in the round 2 PROGRESS row was
written with control characters in place of \r, \t and \b. Plans
only.

#### docs(plans): v2.0.30 - round 2 recorded, plan 027 started

`dd628416`

PROGRESS records round 2: 021, 016, 011, 026, 017 and 020 merged
into bannerlord-1.5.x in that order, trunk 96afb6fb, hosted CI green.
The index gains plan 027 (decision 61, PowerShell coverage for the
Bash-only gates), now being planned.

The dispatch scripts follow plan 020: executors and follow-ups no
longer hand-write CHANGELOG entries (the commit body is the entry);
x-plans.js can write plans into a worktree, and x-review.js passes a
binding orchestrator note to the review lead. Plans only.

#### docs(plans): v2.0.30 - plan 020 merged, the sprint's last branch

`96afb6fb`

Plan 020 is merged, so every sprint branch is on bannerlord-1.5.x.
From here CHANGELOG.md is generated at /release; the commit body is
the changelog entry. Plans only.

#### docs(plans): v2.0.30 - plan 017 merged

`ffa80908`

Plan 017 is merged. A TAOM.dll built from the merge commit reads
build...+6be50e8f70a2... with no .dirty suffix; the packager tests,
the full suite (10,767 passed, 0 failed) and a local replay of the
hosted CI (unit 8,572 passed, binding gate 344 of 344) are green. The
packager's HEAD-bound tests fail mid-merge by design, because HEAD
then predates the stamp target. Plans only.

#### docs(release): v2.0.30 - compare the prune case-insensitively

`9ca99437`

The final convergence pass on plan 017 found the release prune rule
case-sensitive as written: the tag spells GUI/PreFabs/ and the install
GUI/Prefabs/, so a literal comparison against git ls-tree deletes all
49 live TAOM prefabs before packaging, and nothing after it rebuilds
them. The walk that proved 'removes these 12' had lowercased both
sides without saying so. Both copies of the rule (/release Phase 8
and release-process.md step 8) now say to compare case-insensitively,
and why; the report's walk and the RCA row say how they compared.

Also corrects the mirror sentence: the build mirrors
Win64_Shipping_Client into _Server for both modules and into _wEditor
for TAOM only. Docs only; test_package_release.py 57 OK, lint_docs
--fail-on-drift exits 0.

#### docs(plans): v2.0.30 - plan 026 merged

`8c60d134`

Plan 026 is merged: the decision logic of seven protected-virtual
seams now lives in their services with 78 new unit tests (merged suite
10,763 passed, 0 failed). Its index row now links the plan file, which
the merge brought onto trunk. Plans only.

#### docs(reviews): v2.0.30 - plan 026 Codex prompt and RED-step note

`5685676e`

Commits the adversarial review prompt and records, in the review
report, the executor's deviation behind the review's one P3: the RED
steps required CS0122 as well as CS0115, and the compiler printed only
CS0115, a subset of the allowed codes. Docs only.

#### docs(plans): v2.0.30 - plan 011 merged

`79792aed`

Plan 011 is merged. Its two live harness checks passed in headless
sessions in the worktree before the merge: a Stop reminder reached the
session once and was answered, and a PowerShell force push to
bannerlord-1.5.x was refused by validate-push.sh with git never run.
The parsing gaps the last review found outside 011's range go to plan
027. Plans only.

#### docs(plans): v2.0.30 - plan 016 merged, decisions 57 to 61

`4d912ee5`

Records the plan 016 merge and five maintainer decisions of
2026-09-25: 57, plan 026 takes three more seams; 58, Serena's editing
tools are denied except its memory tools; 59, Mike ports to
bannerlord-1.4.5 himself; 60, the trunk guard stays force-push only;
61, a new plan 027 covers the Bash-only gates for PowerShell. Plans
only.

#### docs(plans): v2.0.30 - plan 026 and its two cold reviews

`af6329ea`

The plan behind a3957541 (decisions 56 and 57): written against
31cc629f, cold-reviewed, extended from four seams to seven, and
cold-reviewed again. After the second review the orchestrator made the
fortification site seams yield lazily instead of building a list of
every settlement per keep-out check, because those checks run as
game-menu option conditions. Plans only.

#### docs(plans): v2.0.30 - plan 021 merged, decisions 51 to 56

`e452b0c7`

Records the maintainer's decisions of 2026-09-25 and the plan 021
merge. Decision 51: plan 016 pins Serena to the newest main commit
7a296833 instead of the v1.7.0 release. Decisions 52 to 54: plan 005's
vendored credential stays where it is, BUTR is not told, and the issue
is filed trimmed (#668). Decisions 55 and 56 settle two plan 021 review
items: a private helper only seams call is part of the seam body, and
the four seams that carry decision logic get refactored in new plan
026. The index marks 021 merged (b177ecbc) and adds the 026 row, which
ADR-007 now cites. Plans only.

#### docs(reviews): v2.0.30 - close the plan 021 review records

`ec5a1927`

The final convergence pass on 16e45bf8..6a2ce8cb found the ADR, lens
and ADR-008 edits clean and three LOW defects in the review records:
the commit table credited 9dcf1a4d with wording b22edd47 wrote, the
REVIEW-LOG update left the GitHub issue unsettled, and an open item
(codex-verify still says v1.5.2) was dropped. All three fixed; the RCA
gains a row per convergence round.

It also noted that ADR-007 presented the four seams plan 026 refactors
as a complete list, while three more hold similar logic. ADR-007 now
says the plan 021 review found four and that any seam breaking
condition 2 is a finding (protected file, edited under the bypass Mike
granted for plan 021). Whether plan 026 takes the other three is
Mike's call. No further reviewer pass: these are records, checked
directly against git show.

#### docs(adr): v2.0.30 - seam helpers, seam debt, convergence fixes

`6a2ce8cb`

Maintainer decisions 55 and 56 on the plan 021 review, and the three
LOW defects the convergence pass on the orchestrator commits found.

Decision 55 (review item O1): a private helper that only seams call,
such as an id lookup, is part of the seam body; condition 1 applies to
the calling seam's signature. One sentence in ADR-007 after condition
4, mirrored in the Standards lens check 1.
Decision 56 (O2): the four seams that still carry decision logic are
refactored by new plan 026. ADR-007's rationale names them and the
plan, and says test subclasses, since RefugeServiceTests has two.

Convergence fixes: ADR-008's recommended CI step grepped for
CampaignTime.Now in services with no seam exemption, contradicting the
edited checklist; it is removed, because a grep cannot tell a seam from
a violation. The CHANGELOG no longer lists csharp-architecture.md among
the hook-interface files (it changed only interface and seam rows).
The review report and REVIEW-LOG entry now record what was applied and
decided.

ADR edits made by the orchestrator under the protected-file bypass
Mike granted for plan 021. Verified: lint_docs --fail-on-drift exits
0, reviewctl and ai_documentation tests OK, test_hooks.sh 392 passed,
0 failed; no C# changed.

#### docs(reviews): v2.0.30 - Codex prompt for plan 021

`16e45bf8`

The adversarial review prompt the orchestrator ran against
bec0389d..9c29d732, kept with the review record. Its output stays in
the gitignored docs/reviews/raw/.

#### docs(changelog): v2.0.30 - rewrap the plan 021 entry, name b22edd47

`9dcf1a4d`

The previous commit's CHANGELOG edit joined two lines into one of 150
characters and referred to its own commit as 'a third'. Rewrapped at
100 and named b22edd47. Text only.

#### docs(adr): v2.0.30 - three ADR lines the plan 021 review found

`b22edd47`

The deep review of plan 021 listed three one-line ADR contradictions
for the orchestrator, because the ADRs are protected files (report
table O3, O5, O6). Applied under the bypass Mike granted for plan 021.

O3: ADR-007 listed CampaignTime among the sealed classes that need
adapters, but it is a struct, and the seam conditions allow it in seam
signatures. It leaves the Singletons line.
O5: ADR-008's review checklist and its Related line still forbade the
CampaignTime and other static calls that its Rule 1 exception now
allows inside a boundary seam. Both now name the seam.
O6: ADR-002's entry-point checklist asked only for mocked adapters,
narrower than guideline 7; it now accepts a test subclass that
overrides the service's boundary seams.

Not applied: O4 (tighten condition 1 from 'sealed TaleWorlds class' to
'any other TaleWorlds reference type') changes wording Mike chose in
decision 49, so it is his call, as are O1 and O2. The CHANGELOG entry
now says which ADR commit rests on which approval.

#### docs(adr): v2.0.30 - let seams call the engine, interface optional

`7d1b7a54`

Two wording fixes the adversarial review of plan 021 found (P2 2 and
P2 3), made by the orchestrator because the ADRs are protected files.

ADR-008's new exception allowed only a static read inside a boundary
seam, while ADR-007's amended conditions (decision 49, the looser
variant) allow the engine calls for one action. RefugeService's
ChargePlayer and RefundPlayer are such seams and mutate gold, so a
reviewer following ADR-008 would reject what ADR-007 permits. It now
says a static method call or property access.

ADR-002's migration step 4 still said to create a service interface
every time, contradicting the amended Service Design Guideline 2. It
now creates one only when a test fakes the service or a second
implementation exists.

#### docs(workflow): v2.0.30 - the commit body is the changelog entry

`30b54df7`

Plan 020, decision 18. AGENTS.md "Documentation duty" drops
"CHANGELOG.md is updated every session" and says instead: the commit
body is the changelog entry, written for a reader of the release note;
/release generates CHANGELOG.md from commit subjects and bodies, and
nobody edits that file by hand.

/release Phase 4 now runs tools/changelog_from_commits.py --write to
generate the section, and Phase 5 writes the release note from that
section; Phase 6 names what the release commit stages (the version
files, CHANGELOG.md and the release note). release-process.md step 5
gives the same command.

/verify Step 5 and the deep-review completeness lens used to demand a
CHANGELOG edit; they now flag one, since a hand edit outside /release
is the defect. Every other instruction that told an agent to update
CHANGELOG.md now points at the commit body: CLAUDE.md, README.md, the
builder role, the taom-build Codex skill, the Codex guide, the Serena
checklist, completion-workflow.md, external-repo-adoption.md, the lord
skills and new culture guides, five rules (external-skill-ports,
troops, gui-ui, simplicity-criterion, working-discipline) and the ship,
deep-review, finish-branch, author-armor, verify-bindings,
lint-cleanup-loop, scope-check, new-adr, improve and armory-audit
skills. finish-branch loses its CHANGELOG step and hook note and is
renumbered.

Left alone on purpose: lines that name CHANGELOG as a kind of prose or
record history (hook-authoring.md:59 among them), every code comment
that cites a past CHANGELOG date (those entries now live in the
archive), docs/ai-includes/git-and-commits.md (its shared-file rules
still protect other files) and new-creature-mount/SKILL.md (another
session's file; its line 92 is a follow-up).

#### docs(rules): v2.0.30 - align three architecture rules with the code

`9c29d732`

Sprint decision 19 amends three written rules that disagreed with the
code and with each other. The ADR text (ADR-002, ADR-007, ADR-008) is
the previous commit on this branch; this one brings the rule files,
the review reference and the docs/ai-includes guides in line with it.

A, seams: the review reference, csharp-architecture.md and the
/deep-review Standards lens accept a protected-virtual boundary seam
that meets the four ADR-007 "Exceptions" conditions.

B, interfaces: a service gets an interface only when a test fakes it
or a second class implements it; every adapter keeps one. The
Standards lens, think-before-coding.md, the feature-builder agent and
/new-feature now say so.

C, hooks: the hook interface between a patch and its service is
optional (a narrow seam or a test fake) in AGENTS.md, the review
reference, csharp-patterns.md, harmony-patches.md and three
docs/ai-includes guides.

No code changed and no interface or hook was deleted; those go when
their files are next touched. Plan 021.

Not-tested: docs and rules only; no code changed

#### docs(plans): v2.0.30 - merge round 1 recorded, decisions 49 and 50

`31cc629f`

The sprint record after merging 19 reviewed branches into
bannerlord-1.5.x: README statuses, PROGRESS entries for the merge round
and the protected-plan round, decisions 49 (looser seam wording in
ADR-007) and 50 (the merge), and a status banner in FOR-MIKE.md.
Plans only.

#### docs(readme): v2.0.30 - drop stale counts, fix version and test command

`8a638831`

README: Bannerlord v1.5.3 in all three places (was v1.4.8 twice and
v1.5.2), development branch bannerlord-1.5.x, the setup script's
real job, and the non-deploying dotnet test command. Hand-kept
engineering counts removed rather than refreshed; nothing computes
them and several were wrong (50 careers vs 67).

agent-operating-manual.md: build and test rows now pass
-p:DisableModuleCopy=true -p:ModuleId=, as the binding-gate row
already did. Plan 016, commit 2 of 2.

#### docs(adr): v2.0.30 - record boundary seams, conditional interfaces

`1cdf8eb0`

Plan 021 Step 0, made by the orchestrator on Mike's approval (bypass for
plan 021) with the looser seam wording Mike chose (decision 49):
ADR-007 gains the protected-virtual boundary seam exception and its
checklist lines; ADR-002 makes a service interface conditional on a
fake or a second implementation; ADR-008 allows a static read inside a
qualifying seam. Plan 021's executor does the rule and doc edits.

#### docs(release): v2.0.30 - build and package at the release tag

`ff84e1b8`

Plan 017 documentation. The /release skill gains Phase 8: the Phase 2
build predates the release commit, so rebuild at the tag (from a clean
worktree of the tag when another session is editing), gate the DLLs
with package_release.py --require-build vX.Y.Z --dry-run, then package.
Its version-vs-stamp gotcha now explains the +<sha> and .dirty/nogit
suffixes.

release-process.md step 1 now agrees with the skill's Phase 1: a clean
tree, never a release built beside another session's edits, because
build.ps1 compiles and deploys every file in the tree. It gains step 8
(the gate) and a triage paragraph on reading the Build: and TAOM build:
lines of a crash bundle. crash-report.md's Identity row names the new
field, and the CHANGELOG entry covers the whole plan.

#### docs(reviews): v2.0.30 - recount lessons after the sprint merges

`b2363ed0`

Each merged branch appended lessons; the category counts and the
total are recomputed with grep -c '^### ' per lessons file (921
lessons), the rule the index states.

#### docs(patchshield): v2.0.30 - shim comments after merging 006 and 007

`88408869`

With plans 006 and 007 both merged, the PatchShield exclusion comment
and dr3-maintenance.md no longer said what the code does: the
Native2Managed bridge wraps the 16 allowlisted shims, not all 247,
and its fallback hand-backs now keep the throw site (plan 006's
HandBack). The 231 shims off the allowlist carry no TAOM finalizer.
Comment and doc only; the review of 007 asked for this at merge.

#### docs(reviews): v2.0.30 - Codex prompt for plan 022

`0192a858`

The adversarial review prompt its review used, kept beside the
deep-review record like the other branches' prompts.

#### docs(changelog): v2.0.30 - cite #664 for plan 003

`aee17290`

The plan 003 issue was filed after the branch's entry was written.

#### docs(changelog): v2.0.30 - cite #663 for plan 002

`82ae6cf1`

The plan 002 issue was filed after the branch's entry was written.

#### docs(changelog): v2.0.30 - cite #661 for plan 013

`2feb70ad`

The plan 013 issue was filed after the branch's entry was written.

#### docs(reviews): v2.0.30 - Codex prompt for plan 005

`337372d8`

The adversarial review prompt its review used, kept beside the
deep-review record like the other branches' prompts.

#### docs(reviews): v2.0.30 - Codex prompt for plan 010

`2bc6695e`

The adversarial review prompt its review used, kept beside the
deep-review record like the other branches' prompts.

#### docs(reviews): v2.0.30 - Codex prompt for plan 009

`4da415e2`

The adversarial review prompt its review used, kept beside the
deep-review record like the other branches' prompts.

#### docs(reviews): v2.0.30 - Codex prompt for plan 003

`cf6e06aa`

The adversarial review prompt its review used, kept beside the
deep-review record like the other branches' prompts.

#### docs(tactics): v2.0.30 - document OOB Auto-Assign

`66e3fd59`

Plan 022 documentation for the Assign Heroes button wired in the
previous commit.

companion-tactics.md gains an Auto-Assign subsection (general only,
open slots, candidates, the global greedy and its tie-breaks, the
vanilla accept-captain path, messages, visibility, no co-op gate, no
save data, the DeploymentFormationClass numbering), the new boundary
class in the solution tree, a known limitation, and corrected test
counts: 97 tests across 9 files, with TroopStanceManagerTests at the 9
tests it really has.

feature-map.md gains the missing CompanionTactics row, and CHANGELOG.md
gains the player-facing entry.

#### docs(plans): v2.0.30 - plans 020-025, night progress, FOR-MIKE

`1091f3b6`

Overnight record of the 2026-09-23 Opus sprint (Mike: "continue
throughout the night"):

- Plans 020 (CHANGELOG generated at /release, decision 18), 021 (three
  architecture rule amendments, decision 19), 022 (wire the Order of
  Battle Auto-Assign button, decision 21) and 025 (delete the
  unreachable scaffolds, decision 21), each written, cold-reviewed and
  revised; their reviews and briefs are in the run folder. 020 and
  021 each need one protected edit from Mike first (Step 0).
- FOR-MIKE.md: what waits on Mike, including the protected edits the
  auto-mode classifier would not let the orchestrator make.
- README status cells and PROGRESS entries for the night's branches.
- Plan 015 amendment 2 (decision 32) and its issue number (#659).
- machinery: the plan writer (x-plans.js) and issue drafter
  (x-draft-issues.js), a binding note field and an updated test
  baseline in the executor, a disk rule in the reviewer and plan
  prompts, a file-name tag for second reviews, and a tolerant title
  lookup in scope.py.

Plans only; nothing under Main/, TAOM.Tests/ or the harness changes.

#### docs(changelog): v2.0.30 - cite #662 for plan 018

`71fd2169`

The plan 018 issue was filed after the branch's entry was written.

#### docs(rules): v2.0.30 - new patches need no ResetForUnload

`636b50ab`

Mike's decision 22 (sprint finding COMP-05). Nothing reloads TAOM in
one process: the engine's only caller of OnSubModuleUnloaded is
Module.FinalizeModule at shutdown (v1.5.3 Module.cs:242-248 and
1296-1314), and a rebuild means stopping the game and pressing Play
again. The harmony-patches rule now says a new patch's static service
cache needs no ResetForUnload(); the existing ones stay until touched,
and ResetForUnloadSweepTests still requires each existing one to be
wired into OnSubModuleUnloaded.

#### docs(plans): v2.0.30 - sprint decisions, branch status, machinery

`a39a9c86`

The 2026-09-23 Opus sprint record, after the overnight execution and
Mike's decision rounds of 2026-09-24:

- DECISIONS.md: decisions 1 to 48, asked one at a time, each with its
  follow-through.
- NEXT-STEPS.md: the start-now summary.
- PROGRESS.md: runs, branch tips and verification per step.
- README.md: status cells for plans 006 to 019 with branch tips and
  issue numbers.
- Plan amendments made during execution: 010 (System.Numerics.Vectors
  from netstandard2.0; copy it into the RefAsm test output), 015 (`is
  null` on NativeObject), 018 (re-cut against 4c728dac, its review, the
  COMP-05 answer and the campaign-start fail-closed precondition).
- machinery/: the orchestration scripts the runs used (executor,
  review and follow-up workflows, Codex prompt builder, scope and
  status helpers).

Plans only; nothing under Main/, TAOM.Tests/ or the harness changes.

#### docs(reviews): v2.0.30 - Codex prompt and re-cut plan 018

`667002d8`

The adversarial review prompt the plan 018 review used, and the plan
as re-cut against 4c728dac, which the executor and reviewers followed.
The plan text matches the copy in the bannerlord-1.5.x plans record.

#### docs(reviews): v2.0.30 - Codex prompt for plan 013

`5dcef67a`

The adversarial review prompt the plan 013 review used, kept beside
the deep-review record like the other branches' prompts.

#### docs(nullable): v2.0.30 - per-folder nullable ratchet procedure

`19ca72d3`

docs/ai-includes/code-quality.md gains "How nullable is enforced": the
root .editorconfig sets the seven nullable ids to none for Main and
Dependencies, a null-clean folder raises them to error in its own
.editorconfig, and the ids must never return to a csproj NoWarn.
Main/Features/Siege is the first graduated folder.

docs/features/siege.md describes the prefix's new no-settlement branch
and its tests, and CHANGELOG.md records the change. Plan 019, step 8.

#### docs(changelog): v2.0.30 - warg tick-cost entry for plan 015

`66a85b08`

Records plan 015's four code commits under 2026-09-24: services
resolved once per warg tree node, reused scan buffers, (x, y) grid
keys, and the bone check's range gate ahead of the target skeleton
fetch. It notes the orchestrator's amendment (the `is null` test on a
NativeObject value), the full-suite totals from this worktree and the
owed review and in-game check.

#### docs(warg): v2.0.30 - record the warg tick-cost changes

`2420761b`

warg-combat.md's Performance bullets described the per-query list,
the per-tick bone list and the per-frame IoC lookups as open work;
plan 015 fixed all three, so they now say what the code does: (x, y)
grid keys and 49 cells for a 60 m scan, reused scan buffers, one
attacker skeleton per tick, a target skeleton only inside the range
gate, and services resolved once per node.

The test notes in both docs name the three new test classes in place
of the "SpatialGrid and BoneCheck are untestable" lines.

#### docs(crash-report): v2.0.30 - correct capture cost and coverage

`6fe83bca`

Plan 006, docs. The crash-report doc, the MCM doc, the patch registry
and the API snapshot described ten finalizers, a 247-method sweep that
costs zero, and CrashReport toggles that need a restart. They now
describe five finalizers, the six-shim allowlist in
Native2ManagedTargets, the per-attach boot cost and the small non-zero
steady-state cost, toggles that apply live, and the 1, 2, 10, 100
suppression-log cadence.

hero-race.md and gauntletui-viewmodel-screen.md credited the
Native2Managed sweep with catching a MapConversationTableau.OnTick
throw; the verified v1.5.3 call chain runs inside ScreenManager.Tick,
so the Patch37 finalizer there is what catches it, before and after
this plan. patch-targets.md drops the four deleted finalizer rows by
hand (the snapshot already drifted at the base, so it was not
regenerated). CHANGELOG entry added.

#### docs(reviews): v2.0.30 - drop the scream clip's source site name

`473e4ccc`

Mike asked for the website named in the clip's metadata tags to be
removed from the scream swap's RCA row W1 and its REVIEW-LOG
paragraph. Both now say the tags named a third-party website.

#### docs(changelog): v2.0.30 - enlistment session reset (plan 014)

`d1221b7f`

Records plan 014: Enlistment's per-session clocks and caches are now
cleared on a game load and on a new campaign, so an earlier save or a
second campaign in one process no longer inherits the dwell anchor,
the shore-leave offer latch, the rhythm snapshot or the cached
commander and army handles.

Not-tested: load-earlier-save and second-campaign runs (need the game)

#### docs(changelog): v2.0.30 - PatchShield skips the callback shims

`0ad253d5`

Records plan 007's two commits in the 2026-09-24 section: PatchShield
no longer re-shields the 247 ManagedCallbacks shims that TAOM's crash
capture already wraps, the shield pass line in diag.log reports its
timing, and the docs now say pass 2 runs at game start, not the menu.
Includes the full-suite result and the in-game check still owed.

Plan 007 (plans/007-patchshield-skip-callback-shims.md).

#### docs(enlistment): v2.0.30 - session reset now covers a new campaign

`74fb3d35`

The reconciler comments and the feature doc said a new campaign never
reaches ResetSessionCaches. It now does, and the doc lists the three
latches it clears. The reset's interface doc also loses an em dash.

#### docs(patchshield): v2.0.30 - pass 2 runs at game start, not the menu

`73792944`

TAOM.Dependencies' OnGameInitializationFinished is reached from
MBGameManager.OnGameInitializationFinished, which Campaign.OnInitialize
and CustomGame call at the end of a game's initialisation, inside its
loading screen. The doc comment, the diag.log label and two migration
docs said it fires when the main menu is reached, which hid where
PatchShield pass 2's cost lands.

The SubModule doc comment and entry label now say game start. The
crash-loop marker rows in dr3-maintenance.md now state that a session
which quits from the main menu without starting a game leaves the
marker behind (documented, behaviour unchanged). The same doc gains a
line on the new ManagedCallbacks exclusion and the verification sample
shows the new timing suffix on the shield pass line; the v1.5.2 impact
note places pass 2 inside the campaign's loading screen.

Plan 007 (plans/007-patchshield-skip-callback-shims.md).

#### docs(harmony): v2.0.30 - correct Patch37 coverage, name the guard

`9da9b5b9`

crash-report.md said Patch37 is registered first "to maximise coverage
of our own init". Its MBSubModuleBase.OnSubModuleLoad finalizer patches
the base method, and TAOM's override is already running when it
attaches, so it cannot catch a throw from that method. The entry now
says so and names TryPatchCategory as what keeps a drifted binding
from failing the load.

submodule-lifecycle-and-harmony.md now names TryPatchCategory and
PatchCategoryApplier as the apply path, and records that Harmony stops
a category at its first unresolvable target and throws.

Part of plan 009 (plans/009-guarded-patch-category-apply.md).

#### docs(changelog): v2.0.30 - plan 012 loading-window trace entry

`6f7ddd39`

Plan 012 CHANGELOG entry: the Patch89 Disable postfix now traces a
loading-window lower only when the window was actually up, instead
of once per rendered frame on the main menu, the party screen and
character creation. Records the full-suite result and the owed
in-game check.

#### docs(map-load): v2.0.30 - loading-window lowers trace on real drops

`bce3c27f`

Plan 012, step 3 of 3. The map-load diagnostics feature doc and the
Harmony patch registry described the lifecycle trace as logging
every raise and lower of the global loading window. They now say a
lower is traced only when it actually took the window down, why the
Disable patch needs a Prefix (the engine calls the lower on every
frame of several screens and clears the flag unconditionally), and
what the per-frame lowers cost in v2.0.29 and v2.0.30.

#### docs(plans): v2.0.30 - Opus review sprint report and plans 006-019

`7f02fc8d`

The /improve review sprint 2026-09-23-opus, run against b2e387db on
bannerlord-1.5.x: a measured baseline, a reconcile of June plans 001
to 005, a re-triage of the June harvest's 99 findings, six audit
lanes (42 findings), adversarial checking (35 confirmed, 1 refuted),
a completeness critic and two follow-up measurements.

Top findings: no C# is compiled by CI on any branch; the binding gate
can pass with 335 of 368 checks skipped; 64 unguarded PatchCategory
calls can fail the boot or loop a failed load; the four Stop reminders
never reach Claude; the loading-window trace logs a stack walk every
frame; TAOM patches the same 247 callback shims twice behind a dead
MCM toggle (about 1 to 3 s for players, about 100 s per launch on the
maintainer's slow desktop).

Adds plans 006 to 019 (each written, cold-reviewed and revised), the
rewritten index with the reconciled 001 to 005 rows, and the run record
under plans/_audit/2026-09-23-opus/ (REPORT.md first). No source file
changes. Not pushed.

#### docs(herorace): v2.0.30 - #635 CTD triage, voice-over doc corrected

`aed9dea8`

A player crashed silently after opening a voiced elf wanderer's
encyclopedia page during a menu-started map conversation (#635). No
code: the root cause needs a dump. This records what the triage read.

- Engine doc: the encyclopedia is a second layer on MapScreen, idles the
  map state, never finalizes the conversation tableau; the tableau ticks
  only while its widget renders; lip-sync path; latent OnTick NRE.
- kingdom-voices.md was wrong: an empty accent falls back to any
  gendered vanilla voice, so TAOM characters are lip-synced routinely.
- hero-race.md: MapConversationTableau is a fifth, unguarded render
  path; open #635 section.
- Two misc lessons; lesson counts re-derived (777 committed).

Research: DefaultVoiceOverModel.GetSoundPathForCharacter,
MapConversationTableau, GameStateManager.OnTick, TextureWidget.OnUpdate

#### docs(ranged): v2.0.30 - #617 second review records, RCA, lessons

`449d80a8`

The records half of b904e02a. RCA:
docs/reviews/rca-ranged-rebalance-second-review-2026-09-18.md
(17 findings of the second review, the audit of the first RCA, and
the review of the fixes, F1 to F8); REVIEW-LOG Reviews 119 and 120.

- The first RCA corrected in place: one fabricated "why missed", the
  1.4.8 template premise, an incomplete refutation, two miscounts.
- Feature doc: current cells and recipe, the per-spawn ItemModifier
  for bows and crossbows, TAOM's models in the damage chain (career
  passives reach troop hits), the arrow's missile_speed (neither tier
  nor price), the 12 cells above the hero ceiling, and a campaign
  battle for the crossbow accuracy rule (Custom Battle cannot show it).
- troop-skill-balance: the Iron Hills line left SKIP_TROOP_IDS; the
  template gates rest on the 1.4.8 loader (#626); the balance report
  references ladder cells. field-commission: open #625.
- The CLAUDE.md trap row back under 400 characters; the validation
  rule row and doc, troops rule, localization map, translator guide,
  balance levers, items chapter and feature map updated.
- Lessons: three new (testing-qa, adapters-taleworlds-api, misc),
  three extended (localization-ui, data-content-cultures,
  xslt-moduledata).

#### docs(elephant): v2.0.30 - War Sails lessons for howdah archers

`f59ea59b`

Four research passes over the War Sails decompile, the base engine
and the ship prefabs, read as a design reference only: TAOM will not
require the DLC. Ships keep crews on a navmesh island attached to the
moving entity through base MissionObject calls the siege towers also
use, hold fighting crew in a ship-local detachment, flag every moving
body moveable and release machine users from OnMissionEnded. TAOM's
howdah flags no body moveable, its walls are 1 m tall, and it
teleports seated archers every tick. Recommendation: four gated
steps, the seat model as fallback.

Research: MissionObject.AttachDynamicNavmeshToEntity, SiegeTower
Research: UsableMachine.OnMissionEnded, UsableMissionObject (v1.5.3)

### Chores

#### chore(animalia): v2.0.30 - remove the Custom Battle test riders

`d85fe6dc`

The two Animalia test riders, [Test] Animalia Elk Rider and [Test]
Animalia Moose Rider, no longer appear in the Custom Battle troop
picker. They were console test troops for the elk and moose (#646)
and were never meant to ship. Rochenlas still rides the elk, and
Thranduil and the Mirkwood lords the moose.

Removed with them: their troop file and its SubModule.xml node, their
name strings in all 12 languages, their armour and melee ladder
exemptions, the taom_test_ exemption in the recruitment reachability
test, and the test that pinned their mounts. The Animalia doc, the
quadruped workflow and the release skill say so.

Tests: full suite 10,978 passed, 2 skipped, 0 failed; ModuleData
validator 0 errors; the allowlist and ladder tool tests pass.

#### chore(mcp): v2.0.30 - newest Serena pin, deny its edit tools

`7d13adc3`

Two maintainer decisions on the plan 016 review, applied by the
orchestrator (the settings file under the protected-file bypass Mike
granted for plan 016).

Decision 51: .mcp.json pins Serena to its newest main commit
7a296833 (serena-agent 2.0.0.dev0), the code sessions already ran
unpinned, instead of the older v1.7.0 release. Checked first: the
pinned start-mcp-server --help exits 0 with --context and --project,
and ide-assistant still maps to claude-code.

Decision 58: Serena's editing tools went around every Edit, Write and
Bash hook, like the git and filesystem MCP write tools already denied.
The sixteen tools the pinned commit marks can-edit, other than its
four memory tools, join the deny list (25 entries now), including
execute_shell_command and jet_brains_inline_symbol, whose class name
lacks the Tool suffix. docs/reference/mcp-servers.md lists them, the
memory tools' real write scope, and the rule for re-deriving the list
from the class markers on a pin bump.

The final convergence pass found that last tool missing and four
record and wording defects; all are fixed here, including the freeze
skill's stale line that still called Serena's editors unblocked.

Verified: a marker-based re-derivation against the Serena source
prints missing [] and extra []; test_hooks.sh 392 passed, 0 failed;
audit_claude_config HIGH:1 INFO:7 (the known fixture); lint_docs
--fail-on-drift exits 0.

#### chore(changelog): v2.0.30 - archive the hand-written CHANGELOG

`ef5b7ff4`

Plan 020, decision 18. The hand-written CHANGELOG.md moves unchanged
(the same blob, BOM and old header included) to
docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md, next to the
2026-H1 archive. lint_docs.py already treats that directory as
verbatim history, so the move adds no lint findings.

The new CHANGELOG.md is only a header: it says the file is generated
at each release by tools/changelog_from_commits.py, that the commit
body is the changelog entry, and links both archives. It stays a
header until the next /release writes the first generated section.

The archive holds the trunk's file as of this branch's base. At merge
time the orchestrator re-cuts it from the trunk's committed CHANGELOG
(the merge recipe in plan 020), so entries other branches add before
this one lands are kept.

#### chore(hooks): v2.0.30 - retire the two CHANGELOG hooks and their rules

`c91bb95f`

Plan 020, decision 18: /release now generates CHANGELOG.md from commit
subjects and bodies, so the per-session duty to hand-edit the file is
gone, and the two hooks that enforced it go with it.

check-changelog-changed.sh (PreToolUse on Bash) denied a git commit
that staged anything under .claude/, CLAUDE.md or AGENTS.md without
CHANGELOG.md. check-changelog-updated.sh (Stop) printed a reminder
when a .cs, .xml, .xslt or .json file was dirty and CHANGELOG.md was
not. Both scripts are deleted; Mike removed their two registrations
from .claude/settings.json by hand, since the config-protection hook
keeps agents out of that file.

session-start.sh no longer prints the newest dated CHANGELOG section
(generated sections start "## v", so it would print nothing; the
recent-commits list above it stays), and its python-degraded warning
now names four gates. Comments naming a retired hook in three other
hooks are trimmed. hooks-catalog.md drops both rows and is recounted:
27 scripts, 26 registrations across 9 events, 31 total with the five
skill-frontmatter registrations. harness-facts.md and hook-authoring.md
stop citing the retired gate.

The plan 013 prefilter work landed on the trunk after this plan was
written, so this commit also drops check-changelog-changed.sh from
PF_NARROWED_LIST in tools/test_hooks.sh (the plan's own merge recipe
for 013), and moves the prefilter counts in test_hooks.sh comments and
hooks-catalog.md to five commit gates, eleven prefiltered hooks and
nine blocking gates. bash tools/test_hooks.sh: 368 passed, 0 failed
(382 before; the drop is the retired scripts' rows).

Not-tested: a live session reloading settings.json without the two
  hooks; running sessions keep calling the missing scripts until
  they restart.
Rejected: repurposing check-changelog-changed.sh as a hand-edit gate
  (the generator refuses a hand-written section; a commit gate would
  fail in-flight sessions at the cut-over)

#### chore(security): v2.0.30 - untrack local files and pin MCP servers

`470ee64e`

Untrack .claude/settings.local.json, _taom_loc.pkl and crashz/ and
ignore them. Move the nine MCP write-tool denies into the tracked
.claude/settings.json first so every clone keeps them; the MCP trust
list stays per-user. Repoint the three crashz/report.json citations
to git show b2e387db:crashz/report.json, and unlink the one Markdown
link to settings.local.json (moduledata-validation.md).

Pin serena (v1.7.0, 949a27ef), server-filesystem 2026.8.31,
mcp-server-git 2026.8.18 and elevenlabs-mcp 0.12.2 in .mcp.json
and .codex/config.toml. audit_claude_config.py: MED 1 -> 0.

Merging this deletes settings.local.json from each checkout's disk;
restore it from b2e387db (see CHANGELOG). Plan 016, commit 1 of 2.

Not-tested: MCP startup inside Claude Code and Codex (needs a restart)

#### chore(debug): v2.0.30 - managed-only default launch profile

`37ff41e4`

Mike's decision 23 from the Opus review sprint. The Bannerlord launch
profile now attaches only the managed debugger; a new profile,
"Bannerlord (mixed native debugger)", keeps the managed plus native
attach for native crash work.

The sprint found this desktop's Harmony patching 20 to 40 times slower
than players' (PatchShield pass 2 about 69 s on the first game start,
0 to 1 s in player logs) and named the mixed debugger the likely cause
(UNVERIFIED). The profile name is unchanged, so the saved
ActiveDebugProfile keeps pointing at it. The file still parses, with
the four profiles in order.

Not-tested: the next Play is the test; the diag.log shield pass line
should fall from about 69 s to a few seconds.

#### chore(build): v2.0.30 - move nullable suppression into .editorconfig

`4d2a6c9a`

The seven main nullable ids (CS8600 to CS8604, CS8618, CS8625) leave
the NoWarn lists of Main/TAOM.csproj and TAOM.Dependencies.csproj.
The compiler's /nowarn beats any .editorconfig severity for the same
id, so while they sat in NoWarn no folder could opt back in.

The root .editorconfig now sets them to none for Main/ and
Dependencies/ only, and a nearer folder .editorconfig can raise them.
Build output is unchanged: 2 analyzer warnings, 0 CS86xx. TAOM.Tests
is not covered by the block, so its 2,256 nullable warnings stay
visible exactly as before.

Plan 019, step 1.

#### chore(armoury): v2.0.30 - audit XML references and clean up dead mesh entries

`d5986f87`

### ci

#### ci(tests): v2.0.30 - build and test C# on hosted Windows runners

`b8c00045`

Plan 010. No CI job compiled TAOM's C#: the only C# job needed a
self-hosted runner that was never registered. The new csharp.yml runs
on GitHub-hosted Windows for pushes and pull requests on
bannerlord-1.5.x and bannerlord-1.4.5, building against BUTR's
metadata-only reference assemblies for the pinned Steam build. The
self-hosted build job and the check-build-config warning are deleted.

GameReferences.targets now owns every game reference. The default,
TaomGameRefs=Install, evaluates to the same Reference items as before
(normalized -getItem diff IDENTICAL for all three projects);
-p:TaomGameRefs=RefAsm switches to the BUTR packages. Two new tests pin
the BUTR build to the pinned game version and forbid $(GameFolder) in
a project's Reference items. 142 TestCategory tags (RequiresGame 103,
RequiresGameIL 29, LiveInstall 10) let CI skip what needs the game.

Amendment 1: RefAsm uses the System.Numerics.Vectors lib\netstandard2.0
copy (the net46 one is a forwarding facade the SNV extern alias cannot
follow), and package paths go through EnsureTrailingSlash.
Amendment 2: in RefAsm mode only, that copy is placed in the test
output and in refasm-game\bin, and ConsoleCommandBindingTests,
LiveTableauRefTests and Patch86HideoutBossFightBindingTests are tagged
RequiresGame.

Evidence, local with no game in the environment: unit step 8,183
executed, 0 failed; binding gate on refasm-game 338 executed, 0
skipped, 0 failed; the workflow's build, unit and gate run blocks
replayed through pwsh all exit 0. Install mode: full suite 10,249
total with only the two known live-Armory failures, strict gate on the
real game 368 passed, 0 skipped.

Not-tested: the GitHub-hosted run itself (it needs a push; every step was replayed locally)

### Commits without the version label

#### Add unit tests for eye follow and skin restoration functionality

`0934f65d`

- Implement tests for eye movement analysis in `test_check_eye_follow.py`, ensuring correct behavior when eyes are still while sockets move, and vice versa.
- Create comprehensive tests in `test_eye_follow.py` to validate the eye following logic based on socket movements, including edge cases for scaling and translation.
- Introduce `test_restore_adult_woman_dwarf.py` to verify the restoration process of skin attributes in a synthetic XML file, covering various scenarios including dry runs, applying changes, and handling errors.

#### Add unit tests for animation clip generation and native decompilation

`71907407`

- Introduced `test_gen_troll_anim_clips.py` to validate the functionality of the animation clip generation script, ensuring proper handling of keyed and unkeyed templates, as well as verifying the integrity of generated clips.
- Added `test_native_decompile.py` to implement unit tests for the native decompilation process, including cache key generation, engine version reading, and reporting functionality, with a mock backend to simulate Ghidra's behavior.

#### Add unit tests for various tools and functionalities

`30abf550`

- Implement tests for adding hill troll hammer items, ensuring correct line terminators and handling of anchors.
- Create tests for checking race morph channels, validating mesh channel counts and handling missing or unreadable FBX files.
- Add tests for setting clip balance names, ensuring proper metadata handling and validation of clip packages.
- Introduce tests for transferring hand morphs, focusing on seam weights and mirror functionality.

#### feat: Add custom battle banner bearers model to restrict trolls from carrying banners

`25995dc9`

feat: Implement patch for troll formation spacing to adjust unit diameter based on troll width

feat: Introduce troll action tracing for crash forensics during melee interactions

feat: Create tracker for troll formation spacing to manage unit counts and spacing dynamically

test: Add binding tests for Patch92 to ensure proper method targeting in troll formation spacing

docs: Document shader pre-compilation feature park and findings from review

feat: Add face morph channels to hill troll head in Armory FBX for proper engine integration

#### Add unit tests for FBX LODs and LOD fill batch functionality

`e6312852`

- Created `test_audit_fbx_lods.py` to test the functionality of the FBX LOD reader, including mesh reading, triangle and vertex counts, morph channels, and error handling for ASCII FBX files.
- Implemented `test_lod_fill_batch.py` to validate the LOD filling logic, ensuring proper interpolation of gaps, handling of existing levels, and correct behavior for tiny meshes.

#### fix: update animation paths for hill troll and adjust script for human clip handling

`fef273bf`

#### Add script to configure hill troll race in LOTRLOME Armory

`9354b0b2`

This script, `wire_hill_troll_race.py`, automates the process of updating the hill troll race configuration in the LOTRLOME Armory. It modifies the skins, monsters, and action sets XML files to ensure that the hill troll race uses the correct skeleton and mesh attributes. The script supports a dry run mode to preview changes before applying them, and it creates backups of modified files. Additionally, it checks for consistency and correctness in the edited files to prevent errors during gameplay.

#### Add unit tests for Animalia armory writer functionality

`709649c3`

- Implement comprehensive tests for the apply_animalia_armory.py script.
- Create a synthetic game directory for isolated testing.
- Validate various scenarios including dry runs, missing clips, and backup functionality.
- Ensure tests cover edge cases and maintain file integrity across runs.

#### Add scripts for generating and validating animal animation clips

`c79a5852`

- Introduced `gen_animalia_anim_clips.ps1` to create animation clips for elk and moose based on existing horse animations.
- Implemented logic to clone vanilla horse clips, adjusting parameters such as GUIDs, durations, and step points.
- Added `wire_anim_master_skeletons.ps1` to ensure all skeletal animations reference the correct skeleton GUID, with options for patching empty or incorrect references.
- Both scripts include detailed documentation and error handling for robust execution.

#### Add unit tests for collision body handling in Blender tools

`b23a27ef`

- Implement tests for argument parsing in add_collision_body.py to ensure required arguments are validated and handled correctly.
- Create tests to verify the comparison logic for collision bodies, checking for added, lost, or changed objects.
- Introduce tests for borrowed body validation in validate_moduledata.py, ensuring that collision bodies adhere to ownership rules and do not borrow from other kits improperly.
- Validate that specific cases, such as variants and shared bodies, are handled correctly without errors.

#### Add comprehensive tests for melee ladder and restat melee blades

`ea0592eb`

- Introduced `test_melee_ladder.py` to validate the functionality of the melee ladder specification and the fixer logic, ensuring constraints and edge cases are properly handled.
- Added tests for the target calculation, validation of specifications, and handling of exempt troops.
- Created `test_restat_melee_blades.py` to verify the behavior of the restat process for melee blades, focusing on the write path and ensuring that only specified pieces are modified while maintaining document integrity.
- Implemented tests to check for backup creation, preservation of BOM and line endings, and proper handling of exempt troops and couchable weapons.

#### feat(elephant): Refactor howdah crew spawning and diagnostics; add tests for crew lookup

`8b29f3b1`

#### feat: Implement career kit generation from troop data (#629)

`925db38d`

- Added `generate_career_kits.py` to derive starting kits for player careers based on the lowest-level troops of each culture.
- Introduced command-line options for dry run, applying changes, and verifying the career file against the derived kits.
- Implemented logic to select items based on difficulty, level, and culture-specific exceptions.
- Created unit tests in `test_generate_career_kits.py` to ensure correctness of the kit generation logic and item selection criteria.
- Added a deep review document for skill-template parity changes (#626) detailing findings and improvements made during the review process.

#### Add unit tests for HowdahSampleClock and ShippedFertilityConfig, and implement howdah platform prefab

`83bdad85`

- Introduced HowdahSampleClockTests to validate the timing and firing logic of HowdahSampleClock.
- Added ShippedFertilityConfigTests to ensure the integrity of shipped fertility data and configurations.
- Created a new howdah platform prefab XML file for the TAOM war elephant, detailing its structure and components.
- Conducted a comprehensive review of the howdah platform rebuild, addressing issues and ensuring compatibility with existing systems.
- Implemented a new test suite to validate skill template mismatches in character definitions, ensuring consistency with defined templates.
