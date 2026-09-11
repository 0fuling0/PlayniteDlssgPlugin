# Playnite DLSSG Plugin

**English** | [中文](README.md)

---

## Overview
Playnite DLSSG Plugin deploys DLSSG SM86 files (`version.dll` + `dlssg_sm86.ini`) to game directories containing `nvngx_dlssg.dll`.

Based on [sdli1995/dlssg_for_sm86](https://github.com/sdli1995/dlssg_for_sm86).

## Features
- **Auto Detection**: Scans game library for games with DLSS 3 Frame Generation (`nvngx_dlssg.dll`)
- **One-Click Deploy**: Copies `version.dll` proxy and `dlssg_sm86.ini` to target game directories
- **GPU Auto-Detection**: Detects GPU architecture on first run (RTX 20 -> SM75, RTX 30/40 -> SM86)
- **Flexible Configuration**:
  - Deploy scope: Current filter / Selected games / All games
  - Custom INI parameters: Router, KernelImage, HardwareBilinear, MaxGeneratedFrames, LogLevel
  - Optional DLL injection methods: version.dll / dinput8.dll / dxgi.dll / winhttp.dll / winmm.dll
  - Multiple INI presets: Default / Performance / Custom
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
4. Choose Router (SM75/SM86) based on GPU or enable auto-detection
5. Click **Deploy Files** button
6. Review deployment report

## File Reference
| File | Description |
|------|-------------|
| `version.dll` | Standard proxy DLL, place in game root |
| `dinput8.dll` / `dxgi.dll` / `winhttp.dll` / `winmm.dll` | Alternative injection methods (in `altnative/`) |
| `dlssg_sm86.ini` | Custom configuration |
| `sm86-default.ini` / `sm86-performance.ini` | Preset configs (in `config/presets/`) |

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
