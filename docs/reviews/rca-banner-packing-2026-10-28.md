# RCA: banner packing, realm banners and the Kit mirror (#1047), 2026-10-28

The batch landed as `9985f2db3`: 20 partly used banner sheets packed into 10, new Lindon, Cardolan and
Arthedain banners, the sprite rebake, and the `TAOM.Dependencies` editor mirror that lets the Modding
Kit load TAOM. A tailored deep review (data flow, XML, tooling, engine compatibility, then
completeness and design on the packer) found one HIGH in the packing tool and a set of MEDIUM and LOW
gaps. The maintainer chose to push the batch first and fix in a second commit; that commit is this
RCA's companion. Nothing that shipped in `9985f2db3` crashes or renders wrongly: every finding is in
the tool, in colours that sit beside the banners, or in docs.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | A second `pack_banner_sheets.py --apply` numbered new sheets from `_01` again, wrote over the full `packed_alpha_01` and `_03` and stacked 70 rows onto used cells. Confirmed by a dry run on the packed tree. | tooling, idempotency | The tool was written as a one-off for one run, while its doc already described the next use ("a new emblem goes into a free packed cell"). Its tests covered the plan on fresh input only. | `plan()` names new sheets past every name on disk, never moves a packed sheet, and returns nothing unless it saves a sheet; `test_a_second_pack_writes_nothing`, `test_packing_the_packed_tree_again_moves_nothing`. Lesson below. |
| 2 | MED | `--apply` copied only named cells and `--prune` then deleted the source, so art without an `<Icon>` row was lost; `--prune` kept only what the repo XML named, so following the doc without a build left the install's 150 icons on deleted sheets. It had no dry run, no game or Kit guard, and deleted a texture before the `.rdc` it names. | tooling, live-install writer | The live-install writer rules (dry run default, refuse while the game runs, backups) were met only in part, because the tool did not look like a live-install writer: its main job is repo data. | `pack()` refuses on missing sheets, empty or shared cells and unnamed art; `prune()` lists by default, keeps what either XML names, refuses on other findings and while the game or Kit runs, deletes each `.rdc` first. Seven prune tests. |
| 3 | MED | The gate could not fail: no test drove a finding kind red; the "mergeable" check (`len > ceil(sum/16)`) disagreed with the packer; the sprite check read PNGs, not `TAOMSpriteData.xml`; a sheet changed after its Kit import was invisible. | testing, gate design | The gate was written against the one clean tree it was born in (`.claude/rules/tests.md` gate rule: prove a gate on the states it will meet). | One red test per kind on temp module trees; "mergeable" is `plan()` itself; the audit reads sprite parts and checks each `_mtl` names its `_tex` and each `_tex` holds its source's xxh64. |
| 4 | MED | Cardolan's clans kept brown `color`/`color2` under the new green and white banners, so Patch23 dressed its troops and map parties in brown. | data, identity consumers | The banner change was made where banners are defined; the other consumers of a realm's look (clan tint, culture colour, the faction-selection map) live in other files and tools. | Cardolan `clan_colors` now green and white (`test_cardolan_houses_dress_in_the_banner_green_and_white`); lesson below. |
| 5 | LOW | `factions.json` and the culture colours kept the old Lindon and Cardolan colours, and two tool tables would have written them back. | data, identity consumers | Same as 4. | Both data files and both tables changed together. |
| 6 | LOW | A Modding Kit session left TAOM.Dependencies' crash-loop marker, so the next game logged a false crash loop. | side effect of the mirror | Loading TAOM.Dependencies in the Kit was the point of the fix; what else its startup does in the Kit was not traced until the data-flow lens did. | `IncompatibleModDetector.IsEditorBin` skips the marker in the editor bin, with a test. |
| 7 | LOW | Docs said the Kit skips a missing submodule DLL silently, that installs no longer carry the community modules, and that the tags make submodules load in a "no-render context". The Kit shows a "Cannot find" box per DLL and aborts; the ids remain as one-file stubs; the tags only never block a non-server process. | docs, engine claims | The claims were written from the observed failure, not checked against the Kit's `Module.LoadSubModules`. | Corrected in `scene-scripts.md`, the toolchain doc and the csproj comment from the engine lens's decompile. |
| 8 | LOW | #1047 promised a sprite-shape check and the inventory update; the issue named a `--check` flag the tool never had. | completeness | The issue was written before the tool, and not reread at commit. | The source-hash and sprite-part checks replace the shape idea; inventory rewritten; issue body updated. |
| 9 | LOW | Lindon's and Cardolan's new realm colours exist in the palette, so the vanilla recolour now runs for them: a clan that joins mid-session shows the realm colours until the next load. | behaviour change, accepted | The palette match was a side effect of adding the colours. | Accepted (Isengard already behaves so); stated in the `9985f2db3` commit body. |
| 10 | HIGH (if it had happened) | The XML named 36 untracked files; a commit of only the reviewed list would have shipped 150 rows naming absent materials. | commit hygiene | The audit checks files on disk, not what is staged. | Did not happen: the commit staged them by path. The audit's package check now also fails a clean checkout that lacks them. |

The tooling lens re-reviewed the rewrite (required after a HIGH fix) and confirmed findings 1 to 3
fixed. It found one regression and six small items, all applied in the same commit: the rewrite had
dropped the old per-cell self-check, which was the only guard against duplicate `<Icon>` ids, so
`--apply` could repoint unrelated rows (now a `duplicate_id` finding that blocks packing, with tests);
malformed input exited 1 like a finding (now 2); three guards had no test that failed when they broke;
a sheet that would sit alone in a new packed sheet was a pure rename (now it stays put); conflicting
flags were ignored; two texts named sheets "past every name on disk" while the code fills gaps.

Out of scope and accepted by the maintainer: an existing save keeps the old realm colours (kingdom
colours are saved state); the commit body says realm colours change in new campaigns only.

## Root-cause patterns

**A tool written for one run but documented for the next.** Findings 1 to 3 share one cause: the
packer was built to do the 2026-10-28 merge, and its idempotency, refusals and gate were judged on that
single run. The doc beside it already described repeat use. A tool that a doc tells people to run
again is a standing tool and gets the standing-tool checks.

**A visible identity has more than one owner.** Findings 4 and 5: a realm's look is its banner key,
its kingdom colours, its clans' tint colours, its culture colours and its faction-map colour, written
by four tools. Changing one and reviewing that one leaves the others contradicting it.

## Why each lens missed or caught these

- **Tooling** caught 1, 2, 3 and 6c, and proved 1 with a dry run.
- **Data flow** caught 4, 5, 6 and 9 by tracing who reads the new colours; it did not review the tool.
- **XML** caught 4 and 10 from the data side and confirmed every key and cell.
- **Engine compatibility** caught 7 and 9 and refuted none of the packing claims.
- **Completeness and design** (second wave) caught the free-cell gap, the stale-package gap and the
  data-loss path in 2, and proposed the structure the fix uses.
- No lens ran before the first commit's build of the tool: the tool was reviewed only after it had
  already been used once on live data. Its single use was safe because the tree was fresh.

## Follow-up

None open from this RCA: every finding is fixed in the companion commit or accepted above.
