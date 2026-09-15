using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = Loc.Get("LocDlssgMenuConfigure"),
                MenuSection = "@Playnite DLSSG Plugin",
                Action = _ => PlayniteApi.MainView.OpenPluginSettings(Id)
            };
            yield return CreateMainMenuItem("LocDlssgMenuDeployFiltered", () => PlayniteApi.MainView.FilteredGames, false);
            yield return CreateMainMenuItem("LocDlssgMenuDeploySelected", () => PlayniteApi.MainView.SelectedGames, false);
            yield return CreateMainMenuItem("LocDlssgMenuDeployAll", () => PlayniteApi.Database.Games, false);
            yield return CreateMainMenuItem("LocDlssgMenuUninstallFiltered", () => PlayniteApi.MainView.FilteredGames, true);
            yield return CreateMainMenuItem("LocDlssgMenuUninstallSelected", () => PlayniteApi.MainView.SelectedGames, true);
            yield return CreateMainMenuItem("LocDlssgMenuUninstallAll", () => PlayniteApi.Database.Games, true);
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            yield return new GameMenuItem
            {
                Description = Loc.Get("LocDlssgMenuConfigure"),
                MenuSection = "Playnite DLSSG Plugin",
                Action = _ => PlayniteApi.MainView.OpenPluginSettings(Id)
            };
            yield return new GameMenuItem
            {
                Description = Loc.Get("LocDlssgGameDeploy"),
                MenuSection = "Playnite DLSSG Plugin",
                Action = actionArgs => DeployToGames(actionArgs.Games)
            };
            yield return new GameMenuItem
            {
                Description = Loc.Get("LocDlssgGameUninstall"),
                MenuSection = "Playnite DLSSG Plugin",
                Action = actionArgs => UninstallFromGames(actionArgs.Games)
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
                Title = Loc.Get("LocDlssgSidebarTitle"),
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

        private MainMenuItem CreateMainMenuItem(string descriptionKey, Func<IEnumerable<Game>> gameSource, bool uninstall)
        {
            return new MainMenuItem
            {
                Description = Loc.Get(descriptionKey),
                MenuSection = "@Playnite DLSSG Plugin",
                Action = _ =>
                {
                    if (uninstall)
                    {
                        UninstallFromGames(gameSource());
                    }
                    else
                    {
                        DeployToGames(gameSource());
                    }
                }
            };
        }

        public void DeployUsingConfiguredScope()
        {
            if (settings.Settings.ReleaseScope == "Selected")
            {
                DeployToGames(PlayniteApi.MainView.SelectedGames);
            }
            else if (settings.Settings.ReleaseScope == "All")
            {
                DeployToGames(PlayniteApi.Database.Games);
            }
            else
            {
                DeployToGames(PlayniteApi.MainView.FilteredGames);
            }
        }

        public void UninstallUsingConfiguredScope()
        {
            if (settings.Settings.ReleaseScope == "Selected")
            {
                UninstallFromGames(PlayniteApi.MainView.SelectedGames);
            }
            else if (settings.Settings.ReleaseScope == "All")
            {
                UninstallFromGames(PlayniteApi.Database.Games);
            }
            else
            {
                UninstallFromGames(PlayniteApi.MainView.FilteredGames);
            }
        }

        private List<Game> DeduplicateGames(IEnumerable<Game> sourceGames)
        {
            return sourceGames == null
                ? new List<Game>()
                : sourceGames.Where(game => game != null).GroupBy(game => game.Id).Select(group => group.First()).ToList();
        }

        private void DeployToGames(IEnumerable<Game> sourceGames)
        {
            var games = DeduplicateGames(sourceGames);

            if (games.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage(Loc.Get("LocDlssgNoDeployGames"), Loc.Get("LocDlssgTitle"));
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
                    var alternativeDll = Path.Combine(sourceDirectory, "alternatives", dllFileName);
                    if (File.Exists(alternativeDll))
                    {
                        sourceDll = alternativeDll;
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
                var alternativeDll = Path.Combine(pluginDir, "dlssg_for_sm86", "alternatives", dllFileName);
                if (File.Exists(alternativeDll))
                {
                    sourceDll = alternativeDll;
                }
            }

            var sourceIni = string.IsNullOrWhiteSpace(sourceDirectory) ? null : Path.Combine(sourceDirectory, iniFileName);
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
                var message = string.Format(Loc.Get("LocDlssgSourceMissing"), sourceDll ?? Loc.Get("LocDlssgNotFound"));
                if (customIni == null)
                {
                    message += "\n" + string.Format(Loc.Get("LocDlssgIniLine"), sourceIni ?? Loc.Get("LocDlssgNotFound"));
                }
                PlayniteApi.Dialogs.ShowErrorMessage(message, Loc.Get("LocDlssgTitle"));
                return;
            }

            var confirmation = PlayniteApi.Dialogs.ShowMessage(
                string.Format(Loc.Get("LocDlssgDeployConfirm"), games.Count),
                Loc.Get("LocDlssgTitle"));

            if (confirmation != MessageBoxResult.OK)
            {
                return;
            }

            // Resolve localized strings up front: the progress action runs on a
            // background thread and cannot touch Application resources safely.
            var failNoInstallDir = Loc.Get("LocDlssgFailNoInstallDir");
            var failNoTargetDir = Loc.Get("LocDlssgFailNoTargetDir");

            DeploymentSummary summary = null;
            PlayniteApi.Dialogs.ActivateGlobalProgress(
                progressArgs =>
                {
                    summary = DeployFiles(games, dllFileName, iniFileName, sourceDll, sourceIni, customIni,
                        failNoInstallDir, failNoTargetDir, progressArgs);
                },
                new GlobalProgressOptions(Loc.Get("LocDlssgDeployProgress"))
                {
                    IsIndeterminate = false,
                    Cancelable = true
                });

            if (summary != null)
            {
                PlayniteApi.Dialogs.ShowMessage(summary.ToString(), Loc.Get("LocDlssgDeployResultTitle"));
            }
        }

        private DeploymentSummary DeployFiles(
            IReadOnlyList<Game> games,
            string dllFileName,
            string iniFileName,
            string sourceDll,
            string sourceIni,
            string customIni,
            string failNoInstallDir,
            string failNoTargetDir,
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
                    summary.AddFailure(game.Name, failNoInstallDir);
                    continue;
                }

                var targets = FindTargetDirectories(game.InstallDirectory).ToList();
                if (targets.Count == 0)
                {
                    summary.AddFailure(game.Name, failNoTargetDir);
                    continue;
                }

                foreach (var target in targets)
                {
                    try
                    {
                        var targetDll = Path.Combine(target, dllFileName);
                        var targetIni = Path.Combine(target, iniFileName);

                        // Remove previously deployed files first, then copy the new ones.
                        if (File.Exists(targetDll))
                        {
                            File.Delete(targetDll);
                            summary.DeletedFiles++;
                        }
                        File.Copy(sourceDll, targetDll, true);

                        if (File.Exists(targetIni))
                        {
                            File.Delete(targetIni);
                            summary.DeletedFiles++;
                        }
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

        private void UninstallFromGames(IEnumerable<Game> sourceGames)
        {
            var games = DeduplicateGames(sourceGames);

            if (games.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage(Loc.Get("LocDlssgNoUninstallGames"), Loc.Get("LocDlssgTitle"));
                return;
            }

            var confirmation = PlayniteApi.Dialogs.ShowMessage(
                string.Format(Loc.Get("LocDlssgUninstallConfirm"), games.Count),
                Loc.Get("LocDlssgTitle"));

            if (confirmation != MessageBoxResult.OK)
            {
                return;
            }

            var failNoInstallDir = Loc.Get("LocDlssgFailNoInstallDir");
            var failNoDeployedFiles = Loc.Get("LocDlssgFailNoDeployedFiles");

            DeploymentSummary summary = null;
            PlayniteApi.Dialogs.ActivateGlobalProgress(
                progressArgs =>
                {
                    summary = UninstallFiles(games, failNoInstallDir, failNoDeployedFiles, progressArgs);
                },
                new GlobalProgressOptions(Loc.Get("LocDlssgUninstallProgress"))
                {
                    IsIndeterminate = false,
                    Cancelable = true
                });

            if (summary != null)
            {
                PlayniteApi.Dialogs.ShowMessage(summary.ToString(), Loc.Get("LocDlssgUninstallResultTitle"));
            }
        }

        private DeploymentSummary UninstallFiles(
            IReadOnlyList<Game> games,
            string failNoInstallDir,
            string failNoDeployedFiles,
            GlobalProgressActionArgs progressArgs)
        {
            var dllFileName = settings.Settings.DllFileName;
            var iniFileName = settings.Settings.IniFileName;
            var summary = new DeploymentSummary { UninstallMode = true };

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
                    summary.AddFailure(game.Name, failNoInstallDir);
                    continue;
                }

                var targets = FindUninstallTargetDirectories(game.InstallDirectory, dllFileName, iniFileName).ToList();
                if (targets.Count == 0)
                {
                    summary.AddFailure(game.Name, failNoDeployedFiles);
                    continue;
                }

                foreach (var target in targets)
                {
                    try
                    {
                        var removedAny = false;
                        var targetDll = Path.Combine(target, dllFileName);
                        if (File.Exists(targetDll))
                        {
                            File.Delete(targetDll);
                            summary.DeletedFiles++;
                            removedAny = true;
                        }

                        var targetIni = Path.Combine(target, iniFileName);
                        if (File.Exists(targetIni))
                        {
                            File.Delete(targetIni);
                            summary.DeletedFiles++;
                            removedAny = true;
                        }

                        if (removedAny)
                        {
                            summary.CopiedDirectories++;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Failed to remove files from " + target);
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

        private IEnumerable<string> FindUninstallTargetDirectories(string root, string dllFileName, string iniFileName)
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

                if (files.Any(file =>
                {
                    var fileName = Path.GetFileName(file);
                    return string.Equals(fileName, dllFileName, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(fileName, iniFileName, StringComparison.OrdinalIgnoreCase);
                }))
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
            public int DeletedFiles { get; set; }
            public bool UninstallMode { get; set; }
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
                builder.AppendLine(string.Format(Loc.Get("LocDlssgSummarySucceededGames"), successes.Count));
                builder.AppendLine(string.Format(Loc.Get("LocDlssgSummaryFailedGames"), failures.Count));
                builder.AppendLine(string.Format(
                    Loc.Get(UninstallMode ? "LocDlssgSummaryDirsUninstall" : "LocDlssgSummaryDirsDeploy"),
                    CopiedDirectories));
                if (UninstallMode)
                {
                    builder.AppendLine(string.Format(Loc.Get("LocDlssgSummaryDeletedFiles"), DeletedFiles));
                }
                if (Cancelled)
                {
                    builder.AppendLine(Loc.Get("LocDlssgSummaryCancelled"));
                }

                if (successes.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine(Loc.Get("LocDlssgSummarySuccessHeader"));
                    foreach (var success in successes.Take(30))
                    {
                        builder.AppendLine(" ✓ " + success);
                    }
                    if (successes.Count > 30)
                    {
                        builder.AppendLine(string.Format(Loc.Get("LocDlssgSummaryMore"), successes.Count - 30));
                    }
                }

                if (failures.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine(Loc.Get("LocDlssgSummaryFailHeader"));
                    foreach (var failure in failures.Take(30))
                    {
                        builder.AppendLine(" ✗ " + failure);
                    }

                    if (failures.Count > 30)
                    {
                        builder.AppendLine(string.Format(Loc.Get("LocDlssgSummaryMore"), failures.Count - 30));
                    }
                }

                return builder.ToString();
            }
        }
    }
}
