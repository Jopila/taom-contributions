# RCA: B4a, front-end types to TAOM.Client (ADR-013, #968), 2026-10-22

**Summary.** B4a moves 15 front-end files (11 Gauntlet widgets, the runtime sprite types and
`TaomFactionSelectionVM`) from `TAOM.dll` into `TAOM.Client.dll` and inverts their core callers through
three core seams with null implementations. Codex (gpt-6-astra, xhigh, 286,934 tokens) and four
deep-review lenses (Standards, Engine compatibility, Data flow, Tooling) reviewed it before commit. No
CRITICAL or HIGH was found. Three MEDIUM findings and about twenty LOW ones were confirmed against the
code and fixed in the same commit. The pattern behind the MEDIUMs: each check was written for the old
layout (one assembly, one adapters folder, an rgl tripwire) and was not re-read when the layout changed.

Sources: Codex output `docs/reviews/raw/codex-adversarial-taom-client-b4a-2026-10-22.md` (local, ignored),
prompt `docs/reviews/codex-adversarial-taom-client-b4a-2026-10-22.prompt.md`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MEDIUM | A rendering client without TAOM.Client keeps the null seams, yet `IsUiReady` is true and the sprite service logs success; `ClientComposition.Apply` logged only the info line a server logs | Missing guard | The null seams were designed for the server case only | `Apply` logs an error on a non-dedicated process with no client, and warns when the client and core build stamps differ; tests in `ClientCompositionTests` |
| 2 | MEDIUM | `SettlementLookupBindingTests` scanned only `Main/Adapters`; three adapters moved to `Client/Adapters` | Gate scope | The plan's gate triage grepped `RepoPath("Main")`; this test built its path with `Path.Combine(..., "Main", "Adapters")` | Scans every `RepoPaths.CodeRoots` root; lesson below |
| 3 | MEDIUM | The plan's runtime proof of widget discovery (no `builtin widget type not found` in the rgl log) can never fail: on a client `Debug.FailedAssert` writes nothing (v1.5.5 `MBDebugManager.Assert` is empty) | Engine assumption | The tripwire was read from the decompile's assert text, not from where the assert goes | Research doc and plan corrected; the boot's visual checks are the proof |
| 4 | LOW | Five texts still said FactionMap resolves its hook while registering, and a test pinned a registration order nothing needs | Stale rationale | The DI timing fix removed the premise | Texts corrected, order test deleted, launcher test renamed |
| 5 | LOW | No gate that `IoC.Configure` resolves nothing before `ClientComposition.Apply` and initializes every patch static after it | Missing gate | The hazard was fixed for one feature, not pinned for all | `FeatureModulesTests` source gate |
| 6 | LOW | Plan test C2 missing: nothing checked the three culture-stage patches received their hooks (`_hook?.` fails silently) | Test gap | Covered by a test that resolved but asserted nothing | `ClientFrontEndCompositionTests` asserts the three statics and resets them |
| 7 | LOW | Rename tool and its docs said Main only; its bin/obj filter used absolute path parts | Tooling scope | The B3 widening covered the function body only | Docstring, README and settlements doc; `relative_to(root)` with a test |
| 8 | LOW | Test names, dead bin/obj filters, a field out of place, a stale rules row, stale research lines | Convention | Mechanical drift from the move | Fixed |

Not changed, recorded: `CustomBattlesIoC.InitializeHooks` throws headless (pre-existing; added to the B6c
list in the research doc). `PrefabElementTypeBindingTests` accepts any class name, not only widgets, and
the rename tool exits 1 on a crash (both pre-existing, follow-ups). `PolygonWidget._allInstances` keeps the
last culture stage's widgets alive (pre-existing, cost unverified).

## Why each reviewer missed or found these

- Codex found #1 by reading the absent-client path end to end; its test run failed in its sandbox, so
  its gate checks were source-only.
- The Tooling lens found #2 because it searched every scan by its path construction, not by the plan's
  grep.
- The Engine lens found #3 by reading the IL of `MBDebugManager.Assert` (one `ret`).
- Data flow found #1 independently (T15) plus #5 and #6.

## Lessons

The adapters-folder gate is a repeat of the B3 class (a check scoped to one member of a set: one bin,
one root): `docs/reviews/lessons/build-tooling-workflow.md` "A deploy target that writes into a folder
with no clean leaves stale files". When files move between roots, grep the test tree for every
`Path.Combine` or `RepoPath` that names the old root, not only `RepoPath("Main")`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/lessons/build-tooling-workflow.md](lessons/build-tooling-workflow.md)

<!-- backlinks-end -->
