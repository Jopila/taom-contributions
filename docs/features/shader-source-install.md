# Shader Source Install

## Overview

TAOM ships the generated source files of its own shaders in `Main/_Module/Shaders/Sources/` and copies
them into `<Bannerlord>\Shaders\Sources` at the start of `OnSubModuleLoad`. Today that is the three
metallic bow deformers, `taom_deformer_bow`, `taom_deformer_longbow` and `taom_deformer_crossbow` (six
`.rs` files each). A release gate in `tools/package_release.py` refuses a package whose Armory uses a
`taom_` shader that the package does not carry.

## Why This Exists

- **Vanilla behavior:** a material names a shader asset. The engine compiles that shader's `.rs` entry
  files, one per render pass, and reads them only from `<Bannerlord>\Shaders\Sources`. Every
  `compile_shader` line in `rgl_log_*.txt` reads `$BASE/Shaders/Sources/...`, and no official module
  ships a `Shaders/Sources` folder.
- **TAOM requirement:** the Armory's bow materials use TAOM copies of the native deformers with the
  metallic workflow, so metal on bows does not render white (lotraom-assets `db981663`, see the
  shader notes in that repo's `tools/shaders/`). The Modding Kit's "Generate Shader" writes the `.rs`
  files into the author's own game folder only.
- **Without this feature:** on every other machine the engine logs `Missing shader from sack:
  taom_deformer_longbow_gbuffer`, compiles a file that does not exist, and the bow has no shader. The
  agent that holds the bow does not render at all, in character creation and in game, and the bow
  thumbnails stay on the spinner (#916). A module shader cache does not help: it holds only variants
  that compiled, and compiling needs the sources.

## Architecture

### Design Challenge

The sources must reach a folder outside every module, before the engine's first compile pass (about
six seconds after TAOM loads), on installs TAOM does not control. A Steam file check or update can
also remove extra files from the game folder.

### Solution Approach

`SubModule.OnSubModuleLoad` calls `ShaderSourceInstallEntry.Run` right after the logger exists and
before the shader compile notice starts. The service copies each `*.rs` and `*.rsh` file from
`Modules/TAOM/Shaders/Sources` when the game copy is missing or its bytes differ. It never deletes a
file and never touches a file it does not ship. A failing file is logged and the rest still copy. If
any copy failed, `ShowPendingWarning` (from `OnBeforeInitialModuleScreenSetAsRoot`) shows one red
message naming the folder. The copy runs on every start, so a Steam file check that removes the files
is repaired on the next launch.

`.gitattributes` marks `Main/_Module/Shaders/Sources/*` as `-text`, so Git keeps the generated CRLF
bytes and the byte comparison stays exact.

### Component Diagram

```
SubModule.OnSubModuleLoad
        |
ShaderSourceInstallEntry.Run (engine paths: BasePath.Name, ModuleHelper.GetModuleFullPath("TAOM"))
        |
ShaderSourceInstallService (copy if missing or different, per-file errors)
        |
IShaderSourceFileSystem -> ShaderSourceFileSystem (System.IO)
```

## Configuration

None. The set of files is whatever `Main/_Module/Shaders/Sources/` holds.

## Key Files

| File | Purpose |
|------|---------|
| `Main/_Module/Shaders/Sources/taom_deformer_*.rs` | The 18 generated shader sources |
| `Main/Features/ShaderSourceInstall/ShaderSourceInstallService.cs` | Copy logic |
| `Main/Features/ShaderSourceInstall/ShaderSourceInstallEntry.cs` | Engine paths, log line, warning |
| `Main/Adapters/IShaderSourceFileSystem.cs`, `ShaderSourceFileSystem.cs` | File system seam |
| `Main/SubModule.cs` | The two calls |
| `tools/package_release.py` | `missing_armory_shaders` release gate |

## Dependencies

- `IModLogger` (Core): the `[ShaderSourceInstall] copied N, up to date M, failed K` line.
- `IShaderSourceFileSystem` (Adapters): file listing, reads and writes.

## Tests

- `TAOM.Tests/Features/ShaderSourceInstall/ShaderSourceInstallServiceTests.cs`: 7 tests (copy when
  missing, skip when equal, overwrite when different, one failure does not stop the rest, empty or
  missing source folder, other extensions ignored, unrelated files untouched).
- `tools/tests/test_package_release.py` `TestArmoryShaderSources`: the release gate.

## How to Add a TAOM Shader

1. Make the shader asset in the Kit and click Generate Shader (recipe in lotraom-assets
   `tools/shaders/05_making_shaders.md`). Name it `taom_<what>`.
2. Diff each generated `.rs` against the native original it copies; only the intended defines should differ.
3. Copy all six pass files (`<name>.rs`, `_gbuffer`, `_shadowmap`, `_pointlight`, `_constant_output`,
   `_overdraw`) and any own `.rsh` into `Main/_Module/Shaders/Sources/`.
4. Build. The next launch copies them; check the log line and `compile_shader` lines with no errors.
5. `python tools/package_release.py ... --dry-run` must pass before a release.

## Changelog

- 2026-10-19: created for the invisible-archer bug (#916).

## GitHub Issue

- **Issue:** #916 [Bows on taom_deformer shaders make their holder invisible](https://github.com/haterade22/TAOM/issues/916)
- **Status:** Open
