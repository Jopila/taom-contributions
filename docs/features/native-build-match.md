# Native Build Match

## Overview

`tools/native_build_match.py` carries a native address (RVA) in `TaleWorlds.Native.dll` from one
engine build to the next. Give it an old build, a new build and an old RVA: it prints the same
code's RVA in the new build, or says it cannot tell. An offline tool: no game, no Ghidra.

## Why This Exists

- **Before:** a native offset holds for one binary only, and the shipping DLL has no symbols. After
  each engine bump, every address TAOM knew (crash sites, hang frames, the skeleton-pool spins, the
  `KnownBuilds` pin rows) was found again by hand. `native_sig_author.py diff` still holds the old
  NativeSkinFixes targets and has no current use.
- **The need:** the session-start hook archives each new client and wEditor DLL to
  `E:\Decompiled_Bannerlord\_native` with its engine-method map and this tool's index
  (`tools/native_archive.py`, #1004; v1.5.2 to v1.5.5 today, and v1.5.2 has no map), and a
  player's crash report can come from an older build than the one installed.
- **Without it:** an old address is useless after an update until someone re-derives it by reading
  disassembly.

## Architecture

### Design challenges

- MSVC x64 code is full of RIP-relative operands, so the same function at a shifted address has
  different bytes.
- 46% of the engine-method implementations (942 of 2,060 single-owner addresses, measured on
  v1.5.4) are leaf functions with no `.pdata` entry.
- The engine renumbers its method ids between builds (165 changed from v1.5.4 to v1.5.5), so a
  self-test keyed on ids reports false errors.
- Small stubs repeat: one normalized body is shared by 484 units.
- When an engine class gains a member, its field offsets shift, and a neighbour's new code can be
  byte-identical to the old function. Measured on v1.5.5 client against the v1.5.5 wEditor build
  (a separate compile): before the shift check, 15 wrong matches, 14 of them by unique hash
  (`IMesh.SetVectorArgument2` landed on the editor's `SetVectorArgument`).

### Solution approach

```
old.dll, new.dll
    |  .pdata entries (chained entries keep their root) + leaf units cut from the gaps at 0xCC padding
    v
normalize each unit with capstone: mask rel32 branches out of the unit, RIP-relative disp32,
in-image imm64, jump-table disp32; keep in-unit branches as offsets; stop at an inline jump table
    |
index per binary, cached as <key>.build-match.json beside the Ghidra projects (E:\ghidra\TAOM)
    v
stage 1: a hash unique on both sides -> MATCHED (hash), the offset inside the unit carries exactly
stage 2: leftovers whose string set is unique over ALL units, size within 0.75x -> MATCHED (strings), start only
shift check: a match stays MATCHED only if 2 of the 2 nearest matched units on each side share its
address shift; otherwise AMBIGUOUS (shift)
else AMBIGUOUS n (shared) or UNKNOWN; never "removed"
```

A hash match means identical normalized code, unique in both builds. It is not proven identity:
the shift check is what rejects a neighbour's identical code, because code moved by one neighbour
disagrees with the shift of the functions around it. Adjacent functions of a successor build move
together, so the check cost no oracle-correct match on either successor pair.

**The oracle.** `--oracle` scores the carry against the engine-method registrations of both builds,
which `tools/native_engine_methods.py` reads independently. Methods are keyed by assembly,
interface, method and engine name. An archived DLL has no managed DLLs beside it, so the map comes
from the cached `*-<sha16>.engine-methods.json` of an earlier `native_decompile.py` run, and the
output says `(cached map, managed hashes unchecked)`. v1.5.2 has no cached map, so the oracle
refuses it.

The method is a behavioural port of the cross-build matching that morluto/rea (MIT) documents
(exact keys first, ambiguity kept, no counterpart means unknown). The displacement masking is
TAOM's own, because rea does not normalize relocations
([provenance](../reference/provenance-register.md), [review](../reviews/adopt-rea-2026-10-24.md)).

## Configuration

None. Needs capstone, which the system Python and `E:\Tools\ghidra-venv` both have (the venv's
package is 5.0.9, though `capstone.__version__` prints 5.0.7). No venv re-run is needed.

## Key Files

| File | Purpose |
|------|---------|
| `tools/native_build_match.py` | units, normalization, index cache, match, carry, oracle, CLI |
| `tools/tests/test_native_build_match.py` | unit tests on synthetic code, plus the opt-in oracle runs |
| `tools/native_crash_triage.py` | `Pe`: sections, `.pdata`, string references |
| `tools/native_engine_methods.py` | the engine-method map the oracle scores against |

## Dependencies

- `native_crash_triage.Pe`, `native_decompile.cache_key` and `DEFAULT_PROJECT_DIR`,
  `native_engine_methods.load_or_build`
- capstone

## Tests

- `tools/tests/test_native_build_match.py`: 56 unit tests. They cover:
  - each masked encoding, the kept immediates and non-indexed field offsets, and internal branch
    targets;
  - inline jump tables, an undecodable tail, chained `.pdata` roots and leaf cutting at padding;
  - hash and strings matching, with string uniqueness counted over all units, ambiguity (35
    identical stubs) and the shift check;
  - unknown results with no "removed";
  - the oracle's name keying and its strict strings scoring, the cached-map fallback, the index
    cache, a truncated PE, and every exit-2 path.

  The decoding tests skip without capstone. Three mutants an earlier review found surviving now
  fail a test.
- Opt-in integration (`TAOM_NATIVE_MATCH_IT=1`, skips when a DLL or its cached map is missing):
  v1.5.4 to v1.5.5 and v1.5.3 to v1.5.4 must give zero wrong. A negative control, v1.5.5 client
  against the installed wEditor DLL (another compile, where neighbour code collides), must also
  give zero wrong.

## How to verify in game

Not applicable: an offline tool that reads two DLLs and changes nothing in the game or the module.

## How to use it

**Carry an address after an engine bump.**
`python tools/native_build_match.py --old E:/Decompiled_Bannerlord/_native/TaleWorlds.Native.v1.5.4.client.dll --rva 0x69DA4`
(`--new` defaults to the installed client DLL). A `MATCHED ... (hash)` row gives the new RVA with
the offset carried. Run `--oracle` on the pair first, and read the new address with
`native_decompile.py --rva` before you trust it in a pin.

**A player's crash from an older build.** Run `native_crash_triage.py --dump` against the archived
DLL of the report's build, then carry the fault RVA to the installed build.

**Check a new pair of builds.** Run `--oracle` once per new build. Exit 1 means a wrong match:
do not use the tool on that pair until the cause is found. Exit 2 with no engine-method map (v1.5.2
has none) means the pair is unscored: decompile and compare every carried address.

Read the result word for word. `AMBIGUOUS` and `UNKNOWN` mean the tool cannot tell, not that the
function was removed. A `strings` match maps only the function start.

## Performance

Measured 2026-10-24 on the desktop, archived client DLLs:

| Run | Result |
|---|---|
| Oracle, v1.5.4 to v1.5.5 | 2,060 methods: correct 1,802 (hash 1,797, strings 5), **wrong 0**, ambiguous 253, unknown 5 |
| Oracle, v1.5.3 to v1.5.4 | 2,060 methods: correct 1,803 (hash 1,798, strings 5), **wrong 0**, ambiguous 253, unknown 4 |
| Negative control, v1.5.5 client to v1.5.5 wEditor | 2,061 methods: correct 152 (hash 149, strings 3), **wrong 0**, ambiguous 428, unknown 1,481 (15 wrong before the shift check) |
| Units per binary | about 18,870 primary, 10,600 chained, 16,020 leaf |
| Index build | about 87 s for three DLLs, cold; about 1 s warm per run |

The oracle covers thin engine-method wrappers only. A separate check on deep code: the v1.5.4
skeleton-pool spins at `0x69DA4` and `0x6AFD7` carry to `0x6A264` and `0x6B497` on v1.5.5, and
both new addresses hold the same `movzx eax, byte [rbx]; test al, al; jne` loop
(`native_sig_author.py disasm`). The strings stage has only 5 oracle samples per pair.

## Changelog

- 2026-10-24: added (#997).

## GitHub Issue

- **Issue:** #997, [tools: carry native addresses across engine builds](https://github.com/haterade22/TAOM/issues/997)
- **Status:** Closed (merged in fc5408508)
