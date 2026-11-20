# Playnite DLSSG 插件

[English](README_EN.md) | **中文**

---

## 简介
Playnite DLSSG 插件，用于把 DLSSG SM86 文件（代理 DLL + `dlssg_sm86.ini` + `dlssg_sm86` 文件夹）部署到包含 `nvngx_dlssg.dll` 的游戏目录中。

基于 [sdli1995/dlssg_for_sm86](https://github.com/sdli1995/dlssg_for_sm86) 项目，内置 **dlssg_for_sm86 0.4.2**。

> **0.4.x 与 0.3.x 的文件结构不兼容。** 0.4.0 起运行库、后端、内核包都不再内嵌在 `version.dll` 里，而是放在 `dlssg_sm86\` 文件夹中——只复制 DLL 什么都不会发生。本版插件已按新结构释放整个文件夹。

## 功能特性
- **自动检测**：扫描游戏库，识别已安装 DLSS 3 帧生成（`nvngx_dlssg.dll`）的游戏
- **一键部署**：把代理 DLL、`dlssg_sm86.ini` 和整个 `dlssg_sm86` 文件夹复制到目标游戏目录
- **一键卸载**：删除代理 DLL、INI 与 `dlssg_sm86` 文件夹（只删带本项目载荷的目录，不会误删同名文件夹）
- **按 0.4.2 排布的选项**：Engine（DLSS / XeSS）、MaxGeneratedFrames（最高 6X）、Optimized、Preset、CatmullRomWarp、FlowResolution、UseHudless、超分 Mode、Level、FrameTiming
- **小体积 + 按需缓存**：安装包只带最常用的载荷（约 23 MB），XeSS 模型包、超分文件与备用代理在第一次用到时才下载并缓存到本地
- **部署报告**：显示成功/失败游戏列表，便于排查问题
- **响应式界面**：卡片式瀑布流布局，自适应 Playnite 设置窗口宽度
- **主题适配**：跟随 Playnite 字体大小与深/浅色主题

## 安装方法
1. 下载最新 `.pext` 安装包
2. 在 Playnite 中：`设置` → `插件` → `安装插件` → 选择 `.pext` 文件
3. 重启 Playnite

## 使用说明
1. 打开 Playnite 设置 → 插件 → **Playnite DLSSG Plugin**
2. （可选）设置源目录，留空则使用插件内置资源
3. 选择默认释放范围与选项
4. 点击 **释放文件** 按钮开始部署
5. 查看部署结果报告，进游戏在图形设置里开启 DLSS 帧生成

### 从 1.3–1.6 升级
1. 先用旧版对同一批游戏执行一次**卸载**——0.3.x 会把最多四个代理 DLL 复制进游戏目录，必须删掉。
2. 也可以手动清理：删掉游戏目录里的 `version.dll` / `winmm.dll` / `dbghelp.dll` / `dinput8.dll`、`dlssg_sm86.ini` 和 `dlssg_sm86` 文件夹。
3. 再装 1.7，重新释放。

> 1.4 / 1.5 / 1.6 只是跟着上游换运行库（0.4.0 → 0.4.1 → 0.4.2），插件的文件结构和选项还是 0.3.x 那套，
> 而 0.4.x 的载荷全在 `dlssg_sm86\` 文件夹里——那三个版本只释放了代理 DLL 和 INI，实际开不出帧生成。
> 1.7 起才真正按 0.4.2 的结构释放整个文件夹。

## 文件说明
| 文件 | 说明 |
|------|------|
| `version.dll` | 标准代理 DLL，放在游戏根目录；一次只放一个代理 |
| `winmm.dll` / `dbghelp.dll` / `dinput8.dll` / `dxgi.dll` / `d3d12.dll` | 备用代理（游戏不加载 `version.dll` 时按此顺序换）；`dxgi.dll` 与 `d3d12.dll` 在渲染路径上，最后才用且不要同时放 |
| `dlssg_sm86.ini` | 配置文件，必须与代理 DLL 同目录 |
| `dlssg_sm86\backend\sm86_backend.dll` | 后端（不含内核） |
| `dlssg_sm86\kernels\fg_kernels.dll` | 帧生成内核包（必需） |
| `dlssg_sm86\runtime\nvngx_dlssg.dll` | 随包 DLSS-G 运行库 310.9.16 |
| `dlssg_sm86\xefg\xefg_kernels.dll` | XeSS-FG 模型包（仅 `Engine=XeSS`，按需下载） |
| `dlssg_sm86\sr\nvngx_dlss.dll` + `sr_kernels.dll` | 超分文件（仅 `Mode` ≠ `Off`，按需下载） |

## 选项说明（dlssg_sm86.ini）
插件按设置生成 INI，分区与键名与上游 0.4.2 一致：

| 分区 | 键 | 取值 | 说明 |
|------|----|------|------|
| `[General]` | `Enabled` | 1 | 固定开启；关掉等于让游戏用回自己的 DLSS-G |
| `[FrameGeneration]` | `Engine` | `DLSS` / `XeSS` | 插帧引擎；XeSS 为 Intel XeSS-FG model 15 |
| | `MaxGeneratedFrames` | `1`–`5` | 1=2X 2=3X 3=4X（出厂）5=6X；`Engine=XeSS` 只能 1–3 |
| | `CatmullRomWarp` | `0` / `1` | 运动补偿取样换 Catmull-Rom，细结构更清晰，+3~4% 开销 |
| `[DLSSG]` | `Optimized` | `0`–`3` | 一致性档位；`1` 与官方逐位一致（出厂） |
| | `Preset` | `A` / `B` / `C` | `A` 出厂；`B` UI 重组；`C` FG Lite |
| `[XeSSFG]` | `FlowResolution` | `Coarse` / `Middle` / `Fine` / `CoarseAt4K` | 光流分辨率，越细越慢 |
| | `UseHudless` | `Auto` / `1` / `0` | `Auto` 等同 `0`（整幅画面含 UI 一起插值） |
| `[SuperResolution]` | `Mode` | `Off` / `M-INT8` / `L-INT8` / `M-FP16` / `L-FP16` | 仅 RTX 30/20，出厂 `Off` |
| `[Logging]` | `Level` | `0`–`3` | 出厂 `1`（只记错误）；排查问题改 `2` |
| | `FrameTiming` | `0` / `1` | `1` 时另写 `frame_timing.txt` |
| `[Runtime]` | `Mode` | `Bundled` | 固定使用随包运行库 |

日志只有一份：`游戏目录\dlssg_sm86\logs\dlssg_sm86.log`。

> 0.4.x 已不再读取的键：`KernelImage`、`HardwareBilinear`、`[Compatibility]` 段的 `Optimized` / `Preset`、`[General] Enabled` 以外的旧键。插件的旧设置里如果存过这些值，会被忽略，按新默认值处理。

## 组件缓存

为了不让安装包膨胀到 180 MB，只有「人人都会用到」的载荷打进 `.pext`：

| 载荷 | 大小 | 何时需要 | 来源 |
|------|------|----------|------|
| 代理 DLL + INI + backend + fg_kernels + runtime | 约 23 MB | 总是 | 随包内置 |
| XeSS 模型包 `xefg_kernels.dll` | 约 17 MB | `Engine=XeSS` | 首次使用时下载 |
| 超分 `nvngx_dlss.dll` + `sr_kernels.dll` | 约 180 MB | `Mode` ≠ `Off` | 首次使用时下载（整个发行包） |
| 备用代理 5 个 DLL | 各约 0.8 MB | 选了 `version.dll` 以外的代理 | 首次使用时下载 |

- 缓存位置：`%LOCALAPPDATA%\PlayniteDlssgPlugin\components\<上游版本>\`
- 下载只在需要时发生，会先弹窗告知大小；下载完成后后续释放直接复用
- 升级插件后旧版本的缓存会自动清理
- 设置页「组件缓存」卡片可以查看已缓存组件与占用、打开缓存目录、清理缓存

## 上游同步

内置的运行库来自子模块 `dlssg_for_sm86`，它固定在上游的某个 release tag 上（而非浮动分支），
因此每次发布的内容都可复现。

- **自动**：`.github/workflows/sync-upstream.yml` 每天检查上游最新 release。有新版本时自动更新
  子模块、滚动插件版本、写入更新日志、提交打 tag，并调用构建工作流发布 Release。没有更新时不做任何事。
- **手动**：`./sync_upstream.ps1`。加 `-DryRun` 可只预览不落盘。
- **版本规则**：每次跟随上游更新，插件版本 **+minor**（1.3 → 1.4），patch 位留给同版本内的热修。
  更新日志会自动带上上游的更新说明。

> 上游改了文件布局时，记得同步 `ComponentCache.cs` 里的 `UpstreamVersion`、`RawFiles`、
> `ZipOnlyFiles` 与 `PlayniteDlssgPlugin.csproj` 里的随包文件清单。

## 编译构建
需求：Visual Studio 2019/2022，.NET Framework 4.6.2，PlayniteSDK

```bash
# 还原 NuGet 包
nuget restore

# 编译
MSBuild PlayniteDlssgPlugin.sln /p:Configuration=Release

# 打包
Playnite Toolbox.exe pack "bin\Release" "output_dir"
```

## 许可证
MIT License
