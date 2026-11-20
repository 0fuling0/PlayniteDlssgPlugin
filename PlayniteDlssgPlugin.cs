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

        /// <summary>Folder the mod itself creates inside a game directory.</summary>
        private const string PayloadFolderName = "dlssg_sm86";

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

        /// <summary>One file of the payload: where it comes from and where it goes.</summary>
        private sealed class FileMapping
        {
            /// <summary>Path inside the upstream bundle, for example dlssg_sm86\kernels\fg_kernels.dll.</summary>
            public string SourceRelative;

            /// <summary>Path inside the game directory.</summary>
            public string TargetRelative;

            /// <summary>Not bundled with the plugin: comes from the component cache.</summary>
            public bool Optional;

            /// <summary>Literal text written instead of copying a file (the generated INI).</summary>
            public string Content;
        }

        private static string BundledDirectory
        {
            get
            {
                var pluginDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                return Path.Combine(pluginDirectory, "dlssg_for_sm86");
            }
        }

        /// <summary>
        /// Builds the file list for the current settings. The proxy and the INI go to
        /// the game root, everything else mirrors the upstream dlssg_sm86 folder.
        /// </summary>
        private List<FileMapping> BuildFileMappings()
        {
            var current = settings.Settings;
            var mappings = new List<FileMapping>();

            var proxyIsStandard = string.Equals(current.DllFileName, "version.dll", StringComparison.OrdinalIgnoreCase);
            mappings.Add(new FileMapping
            {
                SourceRelative = proxyIsStandard ? "version.dll" : @"alternatives\" + current.DllFileName,
                TargetRelative = current.DllFileName,
                Optional = !proxyIsStandard
            });

            mappings.Add(new FileMapping
            {
                SourceRelative = "dlssg_sm86.ini",
                TargetRelative = current.IniFileName,
                Content = current.UseCustomIni ? current.BuildCustomIniContent() : null
            });

            // Everything below is mandatory for frame generation.
            mappings.Add(new FileMapping { SourceRelative = @"dlssg_sm86\backend\sm86_backend.dll" });
            mappings.Add(new FileMapping { SourceRelative = @"dlssg_sm86\kernels\fg_kernels.dll" });
            mappings.Add(new FileMapping { SourceRelative = @"dlssg_sm86\runtime\nvngx_dlssg.dll" });

            if (current.IsXeSSEngine)
            {
                mappings.Add(new FileMapping
                {
                    SourceRelative = @"dlssg_sm86\xefg\xefg_kernels.dll",
                    Optional = true
                });
            }

            if (current.IsSuperResolutionEnabled)
            {
                mappings.Add(new FileMapping
                {
                    SourceRelative = @"dlssg_sm86\sr\nvngx_dlss.dll",
                    Optional = true
                });
                mappings.Add(new FileMapping
                {
                    SourceRelative = @"dlssg_sm86\sr\sr_kernels.dll",
                    Optional = true
                });
            }

            foreach (var mapping in mappings)
            {
                if (mapping.TargetRelative == null)
                {
                    mapping.TargetRelative = mapping.SourceRelative;
                }
            }

            return mappings;
        }

        /// <summary>
        /// Resolves a payload file from the user supplied source directory first, the
        /// files bundled with the plugin second and the component cache last.
        /// </summary>
        private static string ResolveSourcePath(string relativePath, string sourceDirectory)
        {
            var roots = new List<string>();
            if (!string.IsNullOrWhiteSpace(sourceDirectory))
            {
                roots.Add(sourceDirectory);
            }
            roots.Add(BundledDirectory);
            roots.Add(ComponentCache.CacheRoot);

            foreach (var root in roots)
            {
                var candidate = Path.Combine(root, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private void DeployToGames(IEnumerable<Game> sourceGames)
        {
            var games = DeduplicateGames(sourceGames);

            if (games.Count == 0)
            {
                PlayniteApi.Dialogs.ShowMessage(Loc.Get("LocDlssgNoDeployGames"), Loc.Get("LocDlssgTitle"));
                return;
            }

            var current = settings.Settings;
            var mappings = BuildFileMappings();

            // Core files must be present before anything is written to a game directory.
            var missingCore = mappings
                .Where(mapping => !mapping.Optional && mapping.Content == null)
                .Select(mapping => mapping.SourceRelative)
                .Where(relativePath => ResolveSourcePath(relativePath, current.SourceDirectory) == null)
                .ToList();

            if (missingCore.Count > 0)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    string.Format(Loc.Get("LocDlssgSourceMissing"), string.Join(Environment.NewLine, missingCore)),
                    Loc.Get("LocDlssgTitle"));
                return;
            }

            // Ask about the deploy itself first: downloading a few hundred megabytes
            // only to see the deploy cancelled afterwards would be wasteful.
            var confirmationDeploy = PlayniteApi.Dialogs.ShowMessage(
                string.Format(Loc.Get("LocDlssgDeployConfirm"), games.Count),
                Loc.Get("LocDlssgTitle"));

            if (confirmationDeploy != MessageBoxResult.OK)
            {
                return;
            }

            // Optional payload (XeSS pack, super-resolution pair, alternative proxy) is
            // fetched once into %LOCALAPPDATA% instead of being shipped in the .pext.
            var missingOptional = mappings
                .Where(mapping => mapping.Optional)
                .Select(mapping => mapping.SourceRelative)
                .Where(relativePath => ResolveSourcePath(relativePath, current.SourceDirectory) == null)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missingOptional.Count > 0)
            {
                var megabytes = (ComponentCache.EstimateDownloadSize(missingOptional) / (1024.0 * 1024.0))
                    .ToString("0", System.Globalization.CultureInfo.CurrentCulture);

                var confirmation = PlayniteApi.Dialogs.ShowMessage(
                    string.Format(
                        Loc.Get("LocDlssgDownloadConfirm"),
                        megabytes,
                        string.Join(Environment.NewLine, missingOptional.Select(Path.GetFileName))),
                    Loc.Get("LocDlssgTitle"));

                if (confirmation != MessageBoxResult.OK)
                {
                    return;
                }

                var downloadError = DownloadComponents(missingOptional);
                if (downloadError != null)
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        string.Format(Loc.Get("LocDlssgDownloadFailed"), downloadError),
                        Loc.Get("LocDlssgTitle"));
                    return;
                }
            }

            var resolved = new List<Tuple<FileMapping, string>>();
            foreach (var mapping in mappings)
            {
                var sourcePath = mapping.Content != null
                    ? null
                    : ResolveSourcePath(mapping.SourceRelative, current.SourceDirectory);

                if (sourcePath == null && mapping.Content == null)
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(
                        string.Format(Loc.Get("LocDlssgSourceMissing"), mapping.SourceRelative),
                        Loc.Get("LocDlssgTitle"));
                    return;
                }

                resolved.Add(Tuple.Create(mapping, sourcePath));
            }

            // Resolve localized strings up front: the progress action runs on a
            // background thread and cannot touch Application resources safely.
            var failNoInstallDir = Loc.Get("LocDlssgFailNoInstallDir");
            var failNoTargetDir = Loc.Get("LocDlssgFailNoTargetDir");
            var deployXeSS = current.IsXeSSEngine;
            var deploySuperResolution = current.IsSuperResolutionEnabled;

            DeploymentSummary summary = null;
            PlayniteApi.Dialogs.ActivateGlobalProgress(
                progressArgs =>
                {
                    summary = DeployFiles(games, resolved, deployXeSS, deploySuperResolution,
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

        /// <summary>
        /// Downloads the optional payload into the component cache. Returns null on
        /// success and an error description otherwise.
        /// </summary>
        private string DownloadComponents(IReadOnlyList<string> relativePaths)
        {
            var cancelled = false;
            string error = null;

            PlayniteApi.Dialogs.ActivateGlobalProgress(
                progressArgs =>
                {
                    try
                    {
                        ComponentCache.Ensure(
                            relativePaths,
                            (percent, text) =>
                            {
                                progressArgs.CurrentProgressValue = percent;
                                if (!string.IsNullOrEmpty(text))
                                {
                                    progressArgs.Text = text;
                                }
                            },
                            progressArgs.CancelToken);
                    }
                    catch (OperationCanceledException)
                    {
                        cancelled = true;
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Failed to download optional DLSSG components");
                        error = ex.Message;
                    }
                },
                new GlobalProgressOptions(Loc.Get("LocDlssgDownloadProgress"))
                {
                    IsIndeterminate = false,
                    Cancelable = true
                });

            if (cancelled)
            {
                return Loc.Get("LocDlssgDownloadCancelled");
            }

            settings.RefreshCacheStatus();
            return error;
        }

        private DeploymentSummary DeployFiles(
            IReadOnlyList<Game> games,
            IReadOnlyList<Tuple<FileMapping, string>> resolved,
            bool deployXeSS,
            bool deploySuperResolution,
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
                        foreach (var entry in resolved)
                        {
                            var mapping = entry.Item1;
                            var destination = Path.Combine(target, mapping.TargetRelative);
                            var destinationDirectory = Path.GetDirectoryName(destination);

                            if (!string.IsNullOrEmpty(destinationDirectory))
                            {
                                Directory.CreateDirectory(destinationDirectory);
                            }

                            if (File.Exists(destination))
                            {
                                File.Delete(destination);
                                summary.DeletedFiles++;
                            }

                            if (mapping.Content != null)
                            {
                                File.WriteAllText(destination, mapping.Content, new UTF8Encoding(false));
                            }
                            else
                            {
                                File.Copy(entry.Item2, destination, true);
                            }

                            summary.CopiedFiles++;
                        }

                        // Drop payload folders the current settings no longer use, so a
                        // game directory never carries a stale XeSS pack or SR pair.
                        if (!deployXeSS)
                        {
                            summary.DeletedFiles += DeleteDirectoryIfPresent(
                                Path.Combine(target, PayloadFolderName, "xefg"));
                        }

                        if (!deploySuperResolution)
                        {
                            summary.DeletedFiles += DeleteDirectoryIfPresent(
                                Path.Combine(target, PayloadFolderName, "sr"));
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

        private static int DeleteDirectoryIfPresent(string path)
        {
            if (!Directory.Exists(path))
            {
                return 0;
            }

            var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
            Directory.Delete(path, true);
            return files;
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
            var dllFileName = settings.Settings.DllFileName;
            var iniFileName = settings.Settings.IniFileName;

            DeploymentSummary summary = null;
            PlayniteApi.Dialogs.ActivateGlobalProgress(
                progressArgs =>
                {
                    summary = UninstallFiles(games, dllFileName, iniFileName,
                        failNoInstallDir, failNoDeployedFiles, progressArgs);
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
            string dllFileName,
            string iniFileName,
            string failNoInstallDir,
            string failNoDeployedFiles,
            GlobalProgressActionArgs progressArgs)
        {
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

                        var payloadDirectory = Path.Combine(target, PayloadFolderName);
                        if (IsOurPayload(payloadDirectory))
                        {
                            summary.DeletedFiles += DeleteDirectoryIfPresent(payloadDirectory);
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

        /// <summary>
        /// True only when the folder carries this mod's payload. Guards against deleting
        /// a directory that merely shares the name.
        /// </summary>
        private static bool IsOurPayload(string payloadDirectory)
        {
            if (!Directory.Exists(payloadDirectory))
            {
                return false;
            }

            return File.Exists(Path.Combine(payloadDirectory, @"backend\sm86_backend.dll")) ||
                   File.Exists(Path.Combine(payloadDirectory, @"runtime\nvngx_dlssg.dll"));
        }

        private IEnumerable<string> FindTargetDirectories(string root)
        {
            foreach (var entry in WalkDirectories(root))
            {
                if (entry.Value.Any(file => string.Equals(
                    Path.GetFileName(file), "nvngx_dlssg.dll", StringComparison.OrdinalIgnoreCase)))
                {
                    yield return entry.Key;
                }
            }
        }

        private IEnumerable<string> FindUninstallTargetDirectories(string root, string dllFileName, string iniFileName)
        {
            foreach (var entry in WalkDirectories(root))
            {
                var deployed = entry.Value.Any(file =>
                {
                    var fileName = Path.GetFileName(file);
                    return string.Equals(fileName, dllFileName, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(fileName, iniFileName, StringComparison.OrdinalIgnoreCase);
                });

                if (deployed || IsOurPayload(Path.Combine(entry.Key, PayloadFolderName)))
                {
                    yield return entry.Key;
                }
            }
        }

        /// <summary>
        /// Depth-first walk that yields every readable directory together with its
        /// files. Unreadable directories are logged and skipped.
        /// </summary>
        private IEnumerable<KeyValuePair<string, string[]>> WalkDirectories(string root)
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

                yield return new KeyValuePair<string, string[]>(directory, files);
            }
        }

        private sealed class DeploymentSummary
        {
            private readonly List<string> failures = new List<string>();
            private readonly List<string> successes = new List<string>();

            public int CopiedDirectories { get; set; }
            public int CopiedFiles { get; set; }
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
                if (!UninstallMode)
                {
                    builder.AppendLine(string.Format(Loc.Get("LocDlssgSummaryFilesCopied"), CopiedFiles));
                }
                if (UninstallMode || DeletedFiles > 0)
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
