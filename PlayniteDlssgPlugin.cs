using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfPath = System.Windows.Shapes.Path;

namespace PlayniteDlssgPlugin
{
    public class PlayniteDlssgPlugin : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly PlayniteDlssgPluginSettingsViewModel settings;

        private static readonly Guid PluginId = Guid.Parse("e3e4de51-1731-43fc-9008-d40767b4d513");

        public override Guid Id
        {
            get { return PluginId; }
        }

        public PlayniteDlssgPlugin(IPlayniteAPI api) : base(api)
        {
            settings = new PlayniteDlssgPluginSettingsViewModel(this);
            Properties = new GenericPluginProperties { HasSettings = true };

            DetectAndSetDefaultRouter();
        }

        private void DetectAndSetDefaultRouter()
        {
            try
            {
                var gpuName = GetGpuName();
                if (!string.IsNullOrEmpty(gpuName))
                {
                    logger.Info("Detected GPU: " + gpuName);

                    var gpuLower = gpuName.ToLowerInvariant();
                    bool isRtx20Series = gpuLower.Contains("rtx 20") ||
                                         gpuLower.Contains("geforce rtx 20") ||
                                         (gpuLower.Contains("rtx") && (gpuLower.Contains("2060") || gpuLower.Contains("2070") ||
                                                                       gpuLower.Contains("2080") || gpuLower.Contains("2050")));

                    if (isRtx20Series && settings.Settings.Router != "SM75")
                    {
                        logger.Info("RTX 20 series detected, setting default Router to SM75");
                        settings.Settings.Router = "SM75";
                    }
                    else if (!isRtx20Series && settings.Settings.Router != "SM86")
                    {
                        logger.Info("Non-RTX 20 series detected, setting default Router to SM86");
                        settings.Settings.Router = "SM86";
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to detect GPU, using default Router");
            }
        }

        private string GetGpuName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var name = obj["Name"] != null ? obj["Name"].ToString() : null;
                        if (!string.IsNullOrEmpty(name) &&
                            (name.Contains("NVIDIA") || name.Contains("GeForce") || name.Contains("RTX") || name.Contains("GTX")))
                        {
                            return name;
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = "配置 Playnite DLSSG 插件",
                MenuSection = "@Playnite DLSSG Plugin",
                Action = _ => PlayniteApi.MainView.OpenPluginSettings(Id)
            };
            yield return CreateMainMenuItem("释放到当前筛选游戏", () => PlayniteApi.MainView.FilteredGames);
            yield return CreateMainMenuItem("释放到当前选中游戏", () => PlayniteApi.MainView.SelectedGames);
            yield return CreateMainMenuItem("释放到全部游戏", () => PlayniteApi.Database.Games);
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            yield return new GameMenuItem
            {
                Description = "配置 Playnite DLSSG 插件",
                MenuSection = "Playnite DLSSG Plugin",
                Action = _ => PlayniteApi.MainView.OpenPluginSettings(Id)
            };
            yield return new GameMenuItem
            {
                Description = "释放 DLSSG 文件",
                MenuSection = "Playnite DLSSG Plugin",
                Action = actionArgs => DeployToGames(actionArgs.Games)
            };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return new SidebarItem
            {
                Title = "DLSSG 设置",
                Type = SiderbarItemType.View,
                Icon = new Grid
                {
                    Width = 28,
                    Height = 28,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(-1, 0, 0, 0),
                    Children =
                    {
                        new WpfPath
                        {
                            Width = 25,
                            Height = 25,
                            Stretch = Stretch.Uniform,
                            Stroke = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                            StrokeThickness = 3,
                            Fill = Brushes.Transparent,
                            Data = Geometry.Parse("M16.9,6.1 A6.8,6.8 0 1,0 16.9,13.9 M11,10 H19"),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                },
                Opened = () =>
                {
                    var view = new PlayniteDlssgPluginSettingsView();
                    view.DataContext = settings;
                    return view;
                }
            };
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            var view = new PlayniteDlssgPluginSettingsView();
            view.DataContext = settings;
            return view;
        }

        private MainMenuItem CreateMainMenuItem(string description, Func<IEnumerable<Game>> gameSource)
        {
            return new MainMenuItem
            {
                Description = description,
                MenuSection = "@Playnite DLSSG Plugin",
                Action = _ => DeployToGames(gameSource())
            };
        }

        public void DeployUsingConfiguredScope()
        {
            if (settings.Settings.ReleaseScope == "当前选中游戏")
            {
                DeployToGames(PlayniteApi.MainView.SelectedGames);
            }
            else if (settings.Settings.ReleaseScope == "全部游戏")
            {
                DeployToGames(PlayniteApi.Database.Games);
            }
            else
            {
                DeployToGames(PlayniteApi.MainView.FilteredGames);
            }
        }

        private void DeployToGames(IEnumerable<Game> sourceGames)
        {
            var games = sourceGames == null
                ? new List<Game>()
                : sourceGames.Where(game => game != null).GroupBy(game => game.Id).Select(group => group.First()).ToList();

            if (games.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage("没有可部署的游戏", "DLSSG SM86");
                return;
            }

            var sourceDirectory = settings.Settings.SourceDirectory;
            var dllFileName = settings.Settings.DllFileName;
            var iniFileName = settings.Settings.IniFileName;

            var pluginDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            var sourceDll = string.IsNullOrWhiteSpace(sourceDirectory) ? null : Path.Combine(sourceDirectory, dllFileName);
            if (string.IsNullOrWhiteSpace(sourceDll) || !File.Exists(sourceDll))
            {
                if (!string.IsNullOrWhiteSpace(sourceDirectory))
                {
                    var altnativeDll = Path.Combine(sourceDirectory, "altnative", dllFileName);
                    if (File.Exists(altnativeDll))
                    {
                        sourceDll = altnativeDll;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(sourceDll) || !File.Exists(sourceDll))
            {
                var submoduleDll = Path.Combine(pluginDir, "dlssg_for_sm86", dllFileName);
                if (File.Exists(submoduleDll))
                {
                    sourceDll = submoduleDll;
                }
            }
            if (string.IsNullOrWhiteSpace(sourceDll) || !File.Exists(sourceDll))
            {
                var altnativeDll = Path.Combine(pluginDir, "dlssg_for_sm86", "altnative", dllFileName);
                if (File.Exists(altnativeDll))
                {
                    sourceDll = altnativeDll;
                }
            }

            var sourceIni = string.IsNullOrWhiteSpace(sourceDirectory) ? null : Path.Combine(sourceDirectory, iniFileName);
            if (string.IsNullOrWhiteSpace(sourceIni) || !File.Exists(sourceIni))
            {
                if (!string.IsNullOrWhiteSpace(sourceDirectory))
                {
                    var presetsIni = Path.Combine(sourceDirectory, "config", "presets", iniFileName);
                    if (File.Exists(presetsIni))
                    {
                        sourceIni = presetsIni;
                    }
                }
            }
            if (string.IsNullOrWhiteSpace(sourceIni) || !File.Exists(sourceIni))
            {
                var presetsIni = Path.Combine(pluginDir, "dlssg_for_sm86", "config", "presets", iniFileName);
                if (File.Exists(presetsIni))
                {
                    sourceIni = presetsIni;
                }
            }
            if (string.IsNullOrWhiteSpace(sourceIni) || !File.Exists(sourceIni))
            {
                var submoduleIni = Path.Combine(pluginDir, "dlssg_for_sm86", iniFileName);
                if (File.Exists(submoduleIni))
                {
                    sourceIni = submoduleIni;
                }
            }

            var customIni = settings.Settings.UseCustomIni ? settings.Settings.BuildCustomIniContent() : null;

            if (!File.Exists(sourceDll) || (customIni == null && !File.Exists(sourceIni)))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "源文件不存在，请在设置中检查源目录和文件名\n\n" +
                    "DLL: " + (sourceDll ?? "未找到") + (customIni == null ? "\nINI: " + (sourceIni ?? "未找到") : ""),
                    "DLSSG SM86");
                return;
            }

            var confirmation = PlayniteApi.Dialogs.ShowMessage(
                string.Format("将向 {0} 个游戏释放 DLSSG 文件，并覆盖目标中的同名文件。是否继续？", games.Count),
                "DLSSG SM86");

            if (confirmation != MessageBoxResult.OK)
            {
                return;
            }

            DeploymentSummary summary = null;
            PlayniteApi.Dialogs.ActivateGlobalProgress(
                progressArgs =>
                {
                    summary = DeployFiles(games, sourceDll, sourceIni, customIni, progressArgs);
                },
                new GlobalProgressOptions("正在部署 DLSSG SM86 文件")
                {
                    IsIndeterminate = false,
                    Cancelable = true
                });

            if (summary != null)
            {
                PlayniteApi.Dialogs.ShowMessage(summary.ToString(), "DLSSG SM86 部署结果");
            }
        }

        private DeploymentSummary DeployFiles(
            IReadOnlyList<Game> games,
            string sourceDll,
            string sourceIni,
            string customIni,
            GlobalProgressActionArgs progressArgs)
        {
            var summary = new DeploymentSummary();
            for (var index = 0; index < games.Count; index++)
            {
                if (progressArgs.CancelToken.IsCancellationRequested)
                {
                    summary.Cancelled = true;
                    break;
                }

                var game = games[index];
                progressArgs.CurrentProgressValue = (index * 100) / games.Count;
                progressArgs.Text = game.Name;

                if (string.IsNullOrWhiteSpace(game.InstallDirectory) || !Directory.Exists(game.InstallDirectory))
                {
                    summary.AddFailure(game.Name, "没有有效的安装目录");
                    continue;
                }

                var targets = FindTargetDirectories(game.InstallDirectory).ToList();
                if (targets.Count == 0)
                {
                    summary.AddFailure(game.Name, "未找到包含 nvngx_dlssg.dll 的目录");
                    continue;
                }

                foreach (var target in targets)
                {
                    try
                    {
                        File.Copy(sourceDll, Path.Combine(target, Path.GetFileName(sourceDll)), true);
                        var targetIni = Path.Combine(target, Path.GetFileName(sourceIni));
                        if (customIni == null)
                        {
                            File.Copy(sourceIni, targetIni, true);
                        }
                        else
                        {
                            File.WriteAllText(targetIni, customIni, Encoding.UTF8);
                        }
                        summary.CopiedDirectories++;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Failed to deploy files to " + target);
                        summary.AddFailure(game.Name, target + ": " + ex.Message);
                    }
                }
                if (!summary.HasFailureForGame(game.Name))
                {
                    summary.AddSuccess(game.Name);
                }
            }

            progressArgs.CurrentProgressValue = 100;
            return summary;
        }

        private IEnumerable<string> FindTargetDirectories(string root)
        {
            var directories = new Stack<string>();
            directories.Push(root);
            while (directories.Count > 0)
            {
                var directory = directories.Pop();
                string[] files;
                try
                {
                    files = Directory.GetFiles(directory);
                    foreach (var child in Directory.GetDirectories(directory))
                    {
                        directories.Push(child);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.Warn(ex, "Cannot access directory " + directory);
                    continue;
                }
                catch (IOException ex)
                {
                    logger.Warn(ex, "Cannot enumerate directory " + directory);
                    continue;
                }

                if (files.Any(file => string.Equals(Path.GetFileName(file), "nvngx_dlssg.dll", StringComparison.OrdinalIgnoreCase)))
                {
                    yield return directory;
                }
            }
        }

        private sealed class DeploymentSummary
        {
            private readonly List<string> failures = new List<string>();
            private readonly List<string> successes = new List<string>();

            public int CopiedDirectories { get; set; }
            public bool Cancelled { get; set; }

            public void AddSuccess(string game)
            {
                successes.Add(game);
            }

            public bool HasFailureForGame(string game)
            {
                return failures.Any(f => f.StartsWith(game + ":"));
            }

            public void AddFailure(string game, string reason)
            {
                failures.Add(game + ": " + reason);
            }

            public override string ToString()
            {
                var builder = new StringBuilder();
                builder.AppendLine("成功处理游戏: " + successes.Count);
                builder.AppendLine("失败游戏: " + failures.Count);
                builder.AppendLine("成功处理目标目录: " + CopiedDirectories);
                if (Cancelled)
                {
                    builder.AppendLine("操作已取消");
                }

                if (successes.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("=== 成功 ===");
                    foreach (var success in successes.Take(30))
                    {
                        builder.AppendLine(" ✓ " + success);
                    }
                    if (successes.Count > 30)
                    {
                        builder.AppendLine("  其余 " + (successes.Count - 30) + " 项未显示");
                    }
                }

                if (failures.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("=== 失败 ===");
                    foreach (var failure in failures.Take(30))
                    {
                        builder.AppendLine(" ✗ " + failure);
                    }

                    if (failures.Count > 30)
                    {
                        builder.AppendLine("其余 " + (failures.Count - 30) + " 项未显示");
                    }
                }

                return builder.ToString();
            }
        }
    }
}
