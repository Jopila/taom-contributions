# Co-op dedicated server (operator page)

## Overview

How to stage, boot, stop and diagnose TAOM on the BannerlordCoop dedicated server. This is the operator page: the load work is [ADR-013](../adrs/013-server-safe-core-client-ui-submodule.md) and [taom-headless-server](../research/taom-headless-server.md), and the gating rules are [coop-interop](coop-interop.md). It restates none of them.

## Why This Exists

- **Vanilla behavior:** the v1.5.5 dedicated server loads the vanilla modules and a vanilla world.
- **TAOM requirement:** the server has to load TAOM's simulation, its map and the Armory's creature data.
- **Without this page:** the boot steps, the stop trap and the known log noise lived in a research plan, one stale bullet in coop-interop and a lookup row.

## Status

| Claim | State |
|---|---|
| Vanilla v1.5.5 server | Booted to SERVING, clients joined, battles fought, a client rejoined (2026-10-21 and 2026-10-22) |
| TAOM, spike build | Reached SERVING on 2026-10-22 and again on 2026-10-23 (probe boots, including the skeleton package). The spike is a throwaway worktree (`taom-headless-spike`, branch `spike/headless-local`, never pushed) staged as `stage-spike` |
| TAOM, committed build | **Boot to SERVING still owed.** A server boot of the B2 build ended at the save refusal (exit 2) |
| Clients joining a TAOM server | Owed (epic #964) |

## Parts and owners

| Part | Owner | What it does |
|---|---|---|
| `bin/Win64_Shipping_Server/` mirror, `DsAssetPackages/` | TAOM (this repo) | The assembled client folder mirrored for the server, and the creature skeleton package |
| `windows/run-windows.ps1` | `BannerlordCoop.DedicatedServer` repo (Garrett's) | Stages the engine and the extra modules, data only |
| `MapEventLoadCleaner`, the deserter fix | BannerlordCoop repo | Server-side repairs; nothing in this repo |

## Stage and boot

Stage with the server repo's script (its `-KeepSubModules` option and the rules for extra modules are in [taom-headless-server](../research/taom-headless-server.md), "Staging"). The script's exclude list does not name `DsAssetPackages`, so a module's package folder reaches the server.

The data directory holds `eula.txt`. The operator accepts the EULA there. Never pass `--accept-eula` for someone else.

Boot, from the staged folder:

```
.\BannerlordCoopServer.exe --data-dir <dir>
```

Add `--no-tui` for a log-only run. A healthy boot reaches LOADED, then SERVING, then an autosave. With no client connected the pulse line reads `timeMode=Stop players=0`.

## Stopping

**Trap:** if the launcher is ended hard (Task Manager, `taskkill /f`, `Stop-Process -Force`), its engine child `dotnet.exe TaleWorlds.Starter.DotNetCore.dll ... /dedicatedcustomserver` keeps running and holds UDP 4200. The next boot cannot bind and exits 3. The launcher kills the child only from managed hooks (Ctrl+C, process exit, the TUI stop path), which a hard kill skips.

- Stop with the TUI stop or Ctrl+C, or
- end the engine process too, then boot.

## Creature skeleton package

`Main/_Module/DsAssetPackages/taom_creature_skeletons.tpac` (40,259 bytes, 6 skeletons: `chariot_skeleton`, `dwarf_skeleton_a`, `elephant_skeleton`, `skeleton_warg`, `spider_skeleton`, `troll_skeleton_a`). The server engine loads `<module>/DsAssetPackages/*.tpac` for every loaded module; the game client never reads that folder. The server stages the Armory's ModuleData but not its art, so without the package every Monsters bone lookup asserts. With it, Monsters objects loaded in 11.32 ms instead of 479,705 ms and the boot logged 0 bone warnings; a control boot without it reproduced the warnings.

| Task | Command |
|---|---|
| Plan only | `python tools/build_ds_skeleton_pack.py` |
| Rebuild | `python tools/build_ds_skeleton_pack.py --apply` |
| Drift gate | `python tools/build_ds_skeleton_pack.py --check` |

`--check` exits 0 when the committed file matches, 1 when it differs or is absent (an Armory art drop changed a skeleton), and 2 when an input is unreadable or a skeleton is ambiguous or missing. Inside `audit_armory_refs.py` a difference is a warning and never exit 1; `/armory-audit` runs it after an Armory sync. `/release` does not run it: run it before packaging ([release-process](../reference/release-process.md)).

It is not the deprecated `tpac_skeleton_extract.py`. That tool reused an item GUID as the package GUID and wrote TOC size 0, and the copy loaded beside the bundled one crashed the client (2026-06-14). The package here carries the cooked-package GUID every cooked tpac shares and the real TOC size, and sits where only the server looks. Never put a skeleton-only package in a path the client loads.

## Known log noise

Both are pre-existing and unfixed (2026-10-23).

- 75 to 90 "Voice def can not be found" message boxes per TAOM boot (dwarf and uruk voices): a staging gap found by the spike.
- Hundreds of `Failed to get id for object of type "TaleWorlds.CampaignSystem.Clan"` per co-op boot.

## Damaged saves and the co-op load repair

From the BannerlordCoop repo, not this one. `b18e9a7a2` adds `MapEventLoadCleaner`: at CampaignReady, before ServerRunningState, it finalizes or repairs half-finished player map events. Each stage's throw is logged as an Error, and parties that left the campaign are unregistered from the co-op object manager. Before it, one throw exited the server (exit 3).

- **Checked** (`5994a8322`): on a copy of a damaged save, 7 half-finished events were released and 7 parties unregistered; the server reached LOADED, SERVING and an autosave.
- **Owed:** that no `set_MapEventSide` NullReferenceException appears once time runs. With no client connected the pulse line reads `timeMode=Stop players=0`, so the check waits for a client.
- **Deserters** (`66b2f7bbb`): v1.5 deserter parties, created with a null `PartyTemplate`, no longer log a server error per spawn.

## Open co-op follow-ups

Recorded in `doc/porting/bannerlord-1.5.x.md` in the BannerlordCoop repo.

- An unlisted live event is finalized without restoring unsaved state.
- The player pass `FinalizeEvent` publishes no `MapEventFinalized`.
- The E2E harness client looter sits at (0,0).
- A bandit component with null init args throws in the send handler.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/DsAssetPackages/taom_creature_skeletons.tpac` | The skeleton package |
| `tools/build_ds_skeleton_pack.py` | Builds and checks it |
| `tools/audit_armory_refs.py` | Warns on drift after an Armory sync |
| `Main/TAOM.csproj` | `MirrorWin64ShippingClientToServer` |

## Tests and gates

- `TAOM.Tests/Infrastructure/ServerLoadGateTests.cs` reads a staged server root from `TAOM_DS_STAGE` (default `E:\repos\BannerlordCoop.DedicatedServer\.stage-win\engine`); inconclusive when absent.
- `python tools/build_ds_skeleton_pack.py --check`
- `python tools/audit_armory_refs.py`

## Changelog

- 2026-10-23: page created. Skeleton package (`61276c182`, #972) and the audit check (`12afef4d2`) landed; the TAOM boot to SERVING is recorded for the spike build only.

## GitHub Issue

- **Issue:** #964 epic, #972 skeleton package (closed)
- **Status:** Open (committed-build boot and client checks owed)
