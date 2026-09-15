# Playnite DLSSG Plugin

**English** | [中文](README.md)

---

## Overview
Playnite DLSSG Plugin deploys DLSSG SM86 files (`version.dll` + `dlssg_sm86.ini`) to game directories containing `nvngx_dlssg.dll`.

Based on [sdli1995/dlssg_for_sm86](https://github.com/sdli1995/dlssg_for_sm86).

## Features
- **Auto Detection**: Scans game library for games with DLSS 3 Frame Generation (`nvngx_dlssg.dll`)
- **One-Click Deploy**: Copies `version.dll` proxy and `dlssg_sm86.ini` to target game directories
- **Flexible Configuration**:
  - Deploy scope: Current filter / Selected games / All games
  - Custom INI parameters: KernelImage, HardwareBilinear, MaxGeneratedFrames, LogLevel (kernel family is selected automatically by the runtime)
  - Optional DLL injection methods: version.dll / dinput8.dll / dxgi.dll / dbghelp.dll / d3d12.dll / winmm.dll
  - Bundled dlssg_for_sm86 0.3.1: RTX 20/30 series frame generation, 4X factory default, up to 6X
- **Deploy Report**: Shows success/failed game lists for troubleshooting
- **Responsive UI**: Three-column masonry layout, adapts to Playnite settings window width
- **Theme Aware**: Follows Playnite font scaling and light/dark themes

## Installation
1. Download latest `.pext` package
2. In Playnite: `Settings` -> `Plugins` -> `Install Plugin` -> Select `.pext` file
3. Restart Playnite

## Usage
1. Open Playnite Settings -> Plugins -> **Playnite DLSSG Plugin**
2. (Optional) Set source directory, leave empty to use embedded resources
3. Select default deploy scope
5. Click **Deploy Files** button
6. Review deployment report

## File Reference
| File | Description |
|------|-------------|
| `version.dll` | Standard proxy DLL, place in game root |
| `dinput8.dll` / `dxgi.dll` / `dbghelp.dll` / `d3d12.dll` / `winmm.dll` | Alternative injection methods (in `alternatives/`) |
| `dlssg_sm86.ini` | Factory configuration (kernel family auto-selected per GPU) |

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
