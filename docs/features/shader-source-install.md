# Shader Source Install

## Overview

TAOM ships the generated source files of its own shaders in `Main/_Module/Shaders/Sources/` and copies
them into `<Bannerlord>\Shaders\Sources` while the module loads. Today that is the three metallic bow
deformers, `taom_deformer_bow`, `taom_deformer_longbow` and `taom_deformer_crossbow` (six `.rs` files
each). A release gate in `tools/package_release.py` refuses a package whose shader reports name a
`taom_` shader that the package does not carry in full.

## Why This Exists

- **Vanilla behavior:** a material names a shader asset. The engine compiles that shader's `.rs` entry
  files, one per render pass, and builds the path as `$BASE/Shaders/Sources/<file>.rs` (native
  `FUN_1801dda00`; its include opener formats `$BASE/Shaders/Sources/%s`). No module folder is searched,
  and no official module ships a `Shaders/Sources` folder.
- **TAOM requirement:** the Armory's bow materials use TAOM copies of the native deformers with the
  metallic workflow, so metal on bows does not render white (lotraom-assets `db981663`; the shader notes
  are in that repo's `tools/shaders/`). The Modding Kit's "Generate Shader" writes the `.rs` files into
  the author's own game folder only.
- **Without this feature:** on every other machine the engine logs `Missing shader from sack:
  taom_deformer_longbow_gbuffer`, tries to compile a file that does not exist, and the bow has no shader.
  The agent that holds the bow does not render at all, in character creation and in game, and the bow
  thumbnails stay on the spinner (#916). A shader cache does not help: it holds only variants that
  compiled, and compiling needs the sources. A failed compile is not cached, so a player picks up the
  fix on the first launch that has the files.

## Architecture

### Design Challenge

The sources must reach a folder outside every module, on installs TAOM does not control, before the
first draw of a bow. The startup compile pass begins only after every module's `OnSubModuleLoad` has
returned, and the `taom_deformer` variants compile later still, on first draw.

### Solution Approach

`ShaderSourceInstallModule` is a `TaomFeatureModule`, so it owns its registration and wiring;
`SubModule.cs` and `IoC.cs` carry nothing for it.

- **ProcessLoad** (inside `OnSubModuleLoad`): unless the process is a dedicated server, which compiles
  no shaders, `ShaderSourceInstallService` copies each `*.rs` and `*.rsh` file from
  `<module>/Shaders/Sources` to `<game>/Shaders/Sources` when the game copy is missing or its bytes
  differ. It never deletes a file and never touches a file it does not ship. A failing file is logged and
  the rest still copy. A missing module folder, or one with no `.rs` file, is itself a failure, because
  it means the install is broken. The log line names both absolute folders:
  `[ShaderSourceInstall] <module> -> <game>: copied N, up to date M, failed K`.
- **MainMenu** (once per process): if anything failed, `ShaderSourceInstallEntry` shows one inquiry
  that names the absolute game folder and says to run the game once as administrator, else reinstall
  TAOM; the log line names the cause. It is an inquiry, not a chat line, because the initial screen
  clears the chat log after the splash video.

Because the copy runs on every start, a file removed by a Steam file check comes back on the next
launch. The 18 files stay in the game folder after TAOM is uninstalled; nothing loads them unless a
material names their shader.

`.gitattributes` marks `Main/_Module/Shaders/Sources/*` as `-text`, and `.editorconfig` turns off
trailing-whitespace trimming there, so the bytes stay exactly as the Kit generated them.

### Component Diagram

```
FeatureModules.All -> ShaderSourceInstallModule
        |  ProcessLoad (not on a dedicated server)      |  MainMenu (only after a failure)
ShaderSourceInstallService                         ShaderSourceInstallEntry.ShowFailureInquiry
        |
IShaderSourceFileSystem -> ShaderSourceFileSystem (System.IO)
```

### The release gate

`tools/package_release.py` reads `Shaders/D3D11/shader_compile_report.log` from every planned module
that ships one, and judges what the TAOM plan's copy list ships:

- a shader named `taom_*` needs all six files (`<name>.rs`, `_gbuffer`, `_shadowmap`, `_pointlight`,
  `_constant_output`, `_overdraw`) in the shipped `TAOM/Shaders/Sources`;
- any other shader passes when `<game-root>/Shaders/Sources/<name>.rs` exists (`--game-root`, default
  the parent of `--source`; a release-channel folder has none, so pass the game folder);
- the report's rows must add up to its `Total variants:` line, an unreadable report refuses, and a
  planned Armory without a report refuses.

## Configuration

None. The set of files is whatever `Main/_Module/Shaders/Sources/` holds.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/Shaders/Sources/taom_deformer_*.rs` | The 18 generated shader sources |
| `Main/Features/ShaderSourceInstall/ShaderSourceInstallModule.cs` | Registration, ProcessLoad copy, MainMenu warning |
| `Main/Features/ShaderSourceInstall/ShaderSourceInstallService.cs` | Copy logic |
| `Main/Features/ShaderSourceInstall/ShaderSourceInstallEntry.cs` | The failure inquiry |
| `Main/Adapters/IShaderSourceFileSystem.cs`, `ShaderSourceFileSystem.cs` | File system seam |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `taom_shader_source_install_failed` |
| `.gitattributes`, `.editorconfig` | Keep the source bytes unchanged |
| `tools/package_release.py` | The shader-source release gate |

## Dependencies

- `IPathService` (Core): the module root.
- `IDedicatedServerProvider` (CoopInterop): skips the copy on a server.
- `IModLogger` (Core): the summary and failure lines.

## Tests

- `TAOM.Tests/Features/ShaderSourceInstall/ShaderSourceInstallServiceTests.cs`: copy, skip, overwrite,
  per-file failure, listing failure, missing folder, no `.rs` file, extension filter, unrelated files.
- `ShaderSourceInstallWiringTests.cs`: module listed once, services resolve, no copy on a dedicated
  server, nothing wired in `SubModule.cs`, the warning is an inquiry and never a chat line.
- `TaomShaderNativeTwinTests.cs` (LiveInstall): the folder holds exactly the 18 files, and each equals
  its native twin plus the known change. Run it after every engine update.
- `tools/tests/test_package_release.py` `TestShaderSources`, `TestLiveShaderReports`: the release gate.

### Native twins

Each TAOM file is the native file of the same pass with three changes: `#define TableauMaskMap texture10`
added, `#define USE_METALLIC_WORKFLOW 1` added, and `my_material_id` changed from
`MATERIAL_ID_STANDART` to `MATERIAL_ID_METALLIC`.

| TAOM shader | Native twin |
|---|---|
| `taom_deformer_bow*` | `bow_deformer*` |
| `taom_deformer_longbow*` | `longbow_deformer*` |
| `taom_deformer_crossbow*` | `crossbow_deformer*` |

## How to Add a TAOM Shader

1. Make the shader asset in the Kit and click Generate Shader (recipe in lotraom-assets
   `tools/shaders/05_making_shaders.md`). Name it `taom_<what>`: the release gate relies on the prefix.
2. Diff each generated `.rs` against the native original it copies; only the intended defines should differ.
3. Before the next game or Kit launch, copy all six pass files and any own `.rsh` into
   `Main/_Module/Shaders/Sources/`. Every launch overwrites a game copy whose bytes differ from the
   module's, so a regenerated file left only in the game folder reverts.
4. Update `TaomShaderNativeTwinTests`: its file list must name every file in the folder (an own `.rsh`
   included), and a shader that copies a native one gets its twin check.
5. Build and launch: the log shows `copied N`, and the `compile_shader` lines show no errors.
6. `python tools/package_release.py --source <...> --dry-run` must pass before a release.

**Never change a shipped `taom_` shader in place.** A player's shader cache keeps variants already
compiled until the game build or the module list changes, so new bytes can mix with old variants.
Ship a changed shader under a new name.

## Changelog

- 2026-10-19: created for the invisible-archer bug (#916); after `/deep-review`, moved to a feature
  module, the warning became an inquiry, and the release gate was rewritten.

## GitHub Issue

- **Issue:** #916 [Bows on taom_deformer shaders make their holder invisible](https://github.com/haterade22/TAOM/issues/916)
- **Status:** Open
