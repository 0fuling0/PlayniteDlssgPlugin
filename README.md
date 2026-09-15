# Playnite DLSSG 插件

[English](README_EN.md) | **中文**

---

## 简介
Playnite DLSSG 插件，用于将 DLSSG SM86 文件（`version.dll` + `dlssg_sm86.ini`）部署到包含 `nvngx_dlssg.dll` 的游戏目录中。

基于 [sdli1995/dlssg_for_sm86](https://github.com/sdli1995/dlssg_for_sm86) 项目。

## 功能特性
- **自动检测**：扫描游戏库，识别已安装 DLSS 3 帧生成（`nvngx_dlssg.dll`）的游戏
- **一键部署**：将 `version.dll` 代理文件和 `dlssg_sm86.ini` 配置文件复制到目标游戏目录
- **灵活配置**：
  - 选择释放范围：当前筛选结果 / 当前选中游戏 / 全部游戏
  - 自定义 INI 参数：KernelImage、HardwareBilinear、MaxGeneratedFrames、LogLevel（内核架构由运行库按显卡自动选择）
  - 可选 DLL 注入方式：version.dll / dinput8.dll / dxgi.dll / dbghelp.dll / d3d12.dll / winmm.dll
  - 内置 dlssg_for_sm86 0.3.1：支持 RTX 20/30 系帧生成，出厂默认 4X，最高可调 6X
- **部署报告**：显示成功/失败游戏列表，便于排查问题
- **响应式界面**：三栏瀑布流布局，自适应 Playnite 设置窗口宽度
- **主题适配**：跟随 Playnite 字体大小与深/浅色主题

## 安装方法
1. 下载最新 `.pext` 安装包
2. 在 Playnite 中：`设置` → `插件` → `安装插件` → 选择 `.pext` 文件
3. 重启 Playnite

## 使用说明
1. 打开 Playnite 设置 → 插件 → **Playnite DLSSG Plugin**
2. （可选）设置源目录，留空则使用内置资源
3. 选择默认释放范围
5. 点击 **释放文件** 按钮开始部署
6. 查看部署结果报告

## 文件说明
| 文件 | 说明 |
|------|------|
| `version.dll` | 标准代理 DLL，放在游戏根目录 |
| `dinput8.dll` / `dxgi.dll` / `dbghelp.dll` / `d3d12.dll` / `winmm.dll` | 替代注入方式（放在 `alternatives/`） |
| `dlssg_sm86.ini` | 出厂配置文件（内核架构按显卡自动选择） |

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
