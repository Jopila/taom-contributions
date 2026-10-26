# RCA: dead PreviewPanel prefab and the Brush property gate (#1022, 2026-10-26)

## Top-line

`PreviewPanel.xml` set `Brush.Fontsize`, a name Gauntlet's case-sensitive property lookup never finds, and nothing in
the repo or the install loads the prefab. The fix deletes it and adds a gate that fails any `Brush.<Property>` attribute
that is not a real `TaleWorlds.GauntletUI.Brush` property. The same change records the co-op race sync research in
`hero-race.md` (no code change: the co-op module's `Race` sync patch never blocks TAOM's restore). The tier 2 review ran
Codex at `xhigh` and two Claude lenses (data flow, XML). None found a CRITICAL, HIGH or MEDIUM. They confirmed 6 LOW
findings, all fixed in the same session, and named related defects outside the change, filed as #1024.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The new co-op paragraph says the transfer save raises `OnBeforeSave`, but `hero-race.md:36`, `:84` and two code comments still said it never does (Codex, lens 5). | Old claim not swept | The paragraph was written from new research; the older text was not searched for the same claim. | All four places reworded as history plus the current fact. |
| 2 | LOW | The paragraph treated the host as one process that is both server and player; on Steam the hosting player joins a separate server as a loopback client (Codex, lens 5). | Topology assumed | The research question named a "listen host" without checking how hosting starts. | Paragraph corrected; the after-co-op `Unable to resolve ISyncPolicy` log noted. |
| 3 | LOW | The gate checked `Brush.<Property>` only in prefab XML, not in C# PrefabExtension sources, where five such attributes live (all three reviewers). | Gate scope narrower than the input | The XML scan was extended; the sibling C# scan was not. | The gate scans both. |
| 4 | LOW | The new test inherited the class's `LiveInstall` category although it needs no install, so a no-game run skipped it (lens 7). | Category inherited, not chosen | Added to an existing class. | Own test class without `LiveInstall`. |
| 5 | LOW | The two rule clauses named neither the method nor the scope, and one could be read as the widget's `Brush` property (lens 7, lens 5). | Rule wording | Written as one clause each. | Reworded. |
| 6 | LOW | A deploy only adds files, so the deleted `PreviewPanel.xml` and the earlier deleted `EventXml.xml` stayed in the installed `Modules/TAOM/GUI/Prefabs` (lens 7). | Delete does not reach the install | Nothing compares the installed prefabs with the repo. | Both installed copies deleted after a byte comparison with their git blobs; a deploy guard is part of #1024. Lesson in `build-tooling-workflow.md`. |

FOLLOW-UP (#1024): misspelled attributes in live prefabs (`VertialAlignment`, `VerticalAlinment`), three more prefabs
nothing loads (`mainmenuHUD.xml`, `PoppyHUD.xml`, `ROTsprite.xml`), a general attribute-name gate, the deploy guard
of finding 6, and a stale line in `docs/migration/v1.5.2-impact.md`.

**Not applied:** the gate accepts `Brush` properties the engine cannot set from a literal (get-only or unparsable types)
and three-segment names it never resolves (lens 5, lens 7, Codex). None is used today, and `Font` does work through a
binding, so tightening would need the value type; the simplicity criterion rejects it for now.

## Root-cause pattern

Findings 1 and 6 share one cause: a change removed or replaced something (a claim, a file) where it was made, and the
other copies of it (older text, the deployed install) were not swept.

## Why each reviewer caught or missed what it did

- **Codex (`xhigh`):** read the co-op source and the decompile, and found the contradiction (1) and the host topology (2).
- **Data flow (5):** traced every place the claim and the gate's inputs live, and found 1, 2, 3 and the dead prefabs of #1024.
- **XML (7):** compared the install with the repo, and found the stale copies (6), the category (4) and the wording (5).

## Feedback memories to codify

None. The lesson carries the rule.
