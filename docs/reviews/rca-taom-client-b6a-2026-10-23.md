# RCA: B6a, client-only patch classes left out on a dedicated server (ADR-013, #968), 2026-10-23

**Summary.** B6a marks the 107 TAOM patch classes whose target, patch signature or target selector lives in
a client-only assembly with a new `[ClientOnly]` attribute. On a dedicated server, `PatchCategoryIndex.Build`
leaves them out before it reads their Harmony attributes, and reports them in one line. Gate 4 checks the
marker in both directions, a known-list gate names the core classes that carry a client-only attribute type,
and `RefugeIoC` no longer runs the Refuge clan screen patch's static initializer on a server. B6 plan decision
D1: the 13 categories that mix client and core classes are not split, because `Build` drops the marked
classes. Risk tier 1: Codex (ultra) and the Data flow and Engine compatibility lenses; both lenses found the
same HIGH, so a second wave (Standards, Completeness) and a re-review of the fix (Codex at high and the Data
flow lens) followed. The HIGH is closed.

Sources: Codex outputs `docs/reviews/raw/codex-adversarial-b6a-clientonly-2026-10-23.md` and
`codex-adversarial-b6a-fixes-2026-10-23.md` (local, ignored), with their prompts in `docs/reviews/`.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | The marker check (`Type.IsDefined`) ran outside the per-type try. `IsDefined` resolves the type of every attribute on a class; two unmarked core screens carry a class-level `[GameStateScreen]` whose type is in client-only `TaleWorlds.MountAndBlade.View`, so on a server `Build` threw and `OnSubModuleLoad` failed | Unguarded attribute read | The plan reasoned about patch classes and attribute arguments only, but the loop visits every type; the first test emitted only the argument case; Codex tested that case and disputed the risk | Check moved inside the try (`PatchCategoryIndex.cs`); a test with an attribute assembly deleted from disk; a known-list gate of core types with a client-only attribute type (the two screens; B7 moves them) |
| 2 | MEDIUM | Gate 4 found target selectors only by the names `TargetMethod`/`TargetMethods` with no parameters | Gate does not match the framework | The helper copied TAOM's own selector shapes, not Harmony 2.4.2's discovery rules | Selectors found the way Harmony finds them (`[HarmonyTargetMethod(s)]`, any name, a parameter assignable from `Harmony`); five fixtures |
| 3 | MEDIUM | No test pinned the body of `SubModule.IsDedicatedServer()`; an inverted body would drop all 107 UI patch classes on every player's PC with the suite green | Test cannot fail | The call was pinned, not the body | A source pin on the exact positive body |
| 4 | LOW | Two new tests built a `TextObject` without the `RequiresGame` tag | Test category | | Tagged |
| 5 | LOW | The report line printed short names; stale ungated line labels after a one-line insertion in 100 files; a test comment stated an inference as a server observation | Text accuracy | | Fixed |

## Evidence from the 2026-10-23 probe boots

A spike build (`cdd5fe38c` plus local changes, not the 6a tree) booted on the v1.5.5 dedicated server this
morning (`E:\CoopDS\probe-boot-*.out`; TAOM log
`E:\CoopDS\stage-spike\engine\bin\Win64_Shipping_Server\Logs\taom_debug_2026-10-23_09-23-19.log`).

- **Observed:** both screens' attribute types cannot be resolved on a server: they were logged SKIPPED with
  `FileNotFoundException` for `TaleWorlds.MountAndBlade.View` (log lines 88 and 720), inside the old try.
- **Observed:** the Refuge clan screen patch's static initializer throws `TypeInitializationException` on a
  server (`probe-boot-C2.out`), which is why the guard exists.
- **Observed:** Patch41 cannot apply headless (log line 814); a mixed category stops at its first client class
  (Patch95 at `BodyGeneratorViewConstructorPatch`, line 835).
- **Inferred, then reproduced by test:** the first 6a shape would have thrown out of `Build`. No server ran it.
- **Owed:** the 6a server boot. Expected: the line naming 107 client-only patch classes not indexed, exactly
  two SKIPPED screens, then a stop at `CommanderSelectorRebuilder.Initialize` (6c fixes it).

## Lesson

Any attribute check over every type (`IsDefined`, `GetCustomAttributes`) resolves the type of every attribute
on a class, so it belongs inside the per-type guard. This repeats the #653 lesson in
`docs/reviews/lessons/harmony-il.md`. And a gate that predicts what a framework will do must discover its
inputs the way that framework does.
