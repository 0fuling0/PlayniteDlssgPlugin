# Playnite DLSSG Plugin

**English** | [中文](README.md)

---

## Overview
Playnite DLSSG Plugin deploys DLSSG SM86 files (a proxy DLL, `dlssg_sm86.ini` and the `dlssg_sm86` folder) to game directories containing `nvngx_dlssg.dll`.

Based on [sdli1995/dlssg_for_sm86](https://github.com/sdli1995/dlssg_for_sm86), bundled **dlssg_for_sm86 0.4.2**.

> **0.4.x is not file-compatible with 0.3.x.** Since 0.4.0 the runtime, the backend and the kernel packs are no longer embedded in `version.dll`; they live in the `dlssg_sm86\` folder, so copying the DLL alone does nothing. This plugin deploys the whole folder.

## Features
- **Auto Detection**: Scans game library for games with DLSS 3 Frame Generation (`nvngx_dlssg.dll`)
- **One-Click Deploy**: Copies the proxy DLL, `dlssg_sm86.ini` and the whole `dlssg_sm86` folder into the target directories
- **One-Click Uninstall**: Removes all three again (only folders that carry this mod's payload are deleted)
- **Options arranged by 0.4.2**: Engine (DLSS / XeSS), MaxGeneratedFrames (up to 6X), Optimized, Preset, CatmullRomWarp, FlowResolution, UseHudless, super-resolution Mode, Level, FrameTiming
- **Small package, on-demand cache**: only the payload everyone needs is bundled (about 23 MB); the XeSS model pack, the super-resolution pair and the alternative proxies are downloaded on first use and cached locally
- **Deploy Report**: Shows success/failed game lists for troubleshooting
- **Responsive UI**: Card masonry layout, adapts to Playnite settings window width
- **Theme Aware**: Follows Playnite font scaling and light/dark themes

## Installation
1. Download latest `.pext` package
2. In Playnite: `Settings` -> `Plugins` -> `Install Plugin` -> Select `.pext` file
3. Restart Playnite

## Usage
1. Open Playnite Settings -> Plugins -> **Playnite DLSSG Plugin**
2. (Optional) Set source directory, leave empty to use embedded resources
3. Select default deploy scope and options
4. Click **Deploy Files**
5. Review the deployment report, then enable DLSS Frame Generation in the game

### Upgrading from 1.3-1.6
1. Run **Uninstall** with the old plugin first: 0.3.x copied up to four proxy DLLs into each game folder and they have to go.
2. Or clean up by hand: delete `version.dll` / `winmm.dll` / `dbghelp.dll` / `dinput8.dll`, `dlssg_sm86.ini` and the `dlssg_sm86` folder from the game directory.
3. Install 1.7 and deploy again.

> 1.4 / 1.5 / 1.6 only followed the upstream runtime (0.4.0 -> 0.4.1 -> 0.4.2) while the plugin still used the 0.3.x
> file layout and options. On 0.4.x the payload lives in the `dlssg_sm86` folder, so those three releases deployed a
> proxy DLL and an INI and nothing else - frame generation did not actually turn on. 1.7 is the first release that
> deploys the whole folder.

## File Reference
| File | Description |
|------|-------------|
| `version.dll` | Standard proxy DLL, place in game root; only one proxy at a time |
| `winmm.dll` / `dbghelp.dll` / `dinput8.dll` / `dxgi.dll` / `d3d12.dll` | Alternative proxies, tried in that order when the game does not load `version.dll`; `dxgi.dll` and `d3d12.dll` sit on the render path, last resort, never both |
| `dlssg_sm86.ini` | Configuration file, must sit next to the proxy DLL |
| `dlssg_sm86\backend\sm86_backend.dll` | Backend (no kernels) |
| `dlssg_sm86\kernels\fg_kernels.dll` | Frame-generation kernel pack (required) |
| `dlssg_sm86\runtime\nvngx_dlssg.dll` | Bundled DLSS-G runtime 310.9.16 |
| `dlssg_sm86\xefg\xefg_kernels.dll` | XeSS-FG model pack (`Engine=XeSS` only, downloaded on demand) |
| `dlssg_sm86\sr\nvngx_dlss.dll` + `sr_kernels.dll` | Super-resolution pair (`Mode` other than `Off`, downloaded on demand) |

## Options (dlssg_sm86.ini)
The plugin generates the INI; sections and keys match upstream 0.4.2:

| Section | Key | Values | Meaning |
|---------|-----|--------|---------|
| `[General]` | `Enabled` | 1 | Always on; turning it off hands frame generation back to the game |
| `[FrameGeneration]` | `Engine` | `DLSS` / `XeSS` | Frame-generation engine; XeSS is Intel XeSS-FG model 15 |
| | `MaxGeneratedFrames` | `1`-`5` | 1 = 2X, 2 = 3X, 3 = 4X (factory), 5 = 6X; `Engine=XeSS` accepts 1-3 |
| | `CatmullRomWarp` | `0` / `1` | Catmull-Rom motion-compensation sampling, sharper fine detail, +3-4 % |
| `[DLSSG]` | `Optimized` | `0`-`3` | Consistency tier; `1` is bit-identical to the official runtime (factory) |
| | `Preset` | `A` / `B` / `C` | `A` factory, `B` UI recomposition, `C` FG Lite |
| `[XeSSFG]` | `FlowResolution` | `Coarse` / `Middle` / `Fine` / `CoarseAt4K` | Optical flow resolution, finer is slower |
| | `UseHudless` | `Auto` / `1` / `0` | `Auto` equals `0` (whole image, UI included, is interpolated) |
| `[SuperResolution]` | `Mode` | `Off` / `M-INT8` / `L-INT8` / `M-FP16` / `L-FP16` | RTX 30/20 only, factory `Off` |
| `[Logging]` | `Level` | `0`-`3` | Factory `1` (errors only); use `2` when troubleshooting |
| | `FrameTiming` | `0` / `1` | `1` also writes `frame_timing.txt` |
| `[Runtime]` | `Mode` | `Bundled` | Always use the packaged runtime |

There is one log: `game directory\dlssg_sm86\logs\dlssg_sm86.log`.

> Keys 0.4.x no longer reads: `KernelImage`, `HardwareBilinear`, `Optimized` / `Preset` in `[Compatibility]`. Values left over in old plugin settings are ignored and the new defaults apply.

## Component cache

So the package does not grow to 180 MB, only the payload everyone needs ships in the `.pext`:

| Payload | Size | Needed when | Source |
|---------|------|-------------|--------|
| Proxy DLL + INI + backend + fg_kernels + runtime | about 23 MB | always | bundled |
| XeSS model pack `xefg_kernels.dll` | about 17 MB | `Engine=XeSS` | downloaded on first use |
| Super resolution `nvngx_dlss.dll` + `sr_kernels.dll` | about 180 MB | `Mode` other than `Off` | downloaded on first use (whole release archive) |
| Five alternative proxies | about 0.8 MB each | proxy other than `version.dll` | downloaded on first use |

- Cache location: `%LOCALAPPDATA%\PlayniteDlssgPlugin\components\<upstream version>\`
- Downloads only happen when needed and the size is shown first; later deploys reuse the cache
- Caches of older upstream versions are cleaned up automatically after a plugin update
- The **Component cache** card in the settings shows what is cached and how much space it uses, opens the folder and can clear it

## Upstream sync

The bundled runtime comes from the `dlssg_for_sm86` submodule, which is pinned to an
upstream release tag rather than a floating branch, so every release is reproducible.

- **Automatic**: `.github/workflows/sync-upstream.yml` checks upstream daily. When a newer
  release exists it updates the submodule, rolls the plugin version, writes the changelog,
  commits and tags, then invokes the build workflow to publish the release. Otherwise it
  does nothing.
- **Manual**: `./sync_upstream.ps1`. Add `-DryRun` to preview without writing anything.
- **Versioning**: every upstream bump rolls the plugin version **+minor** (1.3 -> 1.4);
  the patch component is reserved for fixes within the same bundled runtime. The changelog
  automatically picks up the upstream release notes.

> When upstream moves files around, update `UpstreamVersion`, `RawFiles` and `ZipOnlyFiles`
> in `ComponentCache.cs` and the bundled file list in `PlayniteDlssgPlugin.csproj`.

## Build
Requirements: Visual Studio 2019/2022, .NET Framework 4.6.2, PlayniteSDK

```bash
# Restore NuGet packages
nuget restore

# Build
MSBuild PlayniteDlssgPlugin.sln /p:Configuration=Release

# Package
Playnite Toolbox.exe pack "bin\Release" "output_dir"
```

## License
MIT License
