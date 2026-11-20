using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace PlayniteDlssgPlugin
{
    /// <summary>
    /// Plugin settings. Option names and value domains follow dlssg_for_sm86 0.4.2;
    /// every value is stored as text so the generated INI is a direct echo of what
    /// the user picked and no XAML type conversion sits in between.
    /// </summary>
    public partial class PlayniteDlssgPluginSettings : ObservableObject
    {
        public const string EngineDlss = "DLSS";
        public const string EngineXeSS = "XeSS";
        public const string SuperResolutionOff = "Off";

        private string sourceDirectory = "";
        private string dllFileName = "version.dll";
        private string iniFileName = "dlssg_sm86.ini";
        private bool useCustomIni = true;
        private string releaseScope = "Filtered";

        // [FrameGeneration]
        private string engine = EngineDlss;
        private string maxGeneratedFrames = "3";
        private string catmullRomWarp = "0";

        // [DLSSG]
        private string optimized = "1";
        private string preset = "A";

        // [XeSSFG]
        private string flowResolution = "Middle";
        private string useHudless = "Auto";

        // [SuperResolution]
        private string superResolutionMode = SuperResolutionOff;

        // [Logging]
        private string logLevel = "1";
        private string frameTiming = "0";

        private string customIniContent = string.Empty;

        public string SourceDirectory
        {
            get { return sourceDirectory; }
            set { SetValue(ref sourceDirectory, value); }
        }

        public string DllFileName
        {
            get { return dllFileName; }
            set { SetValue(ref dllFileName, value); }
        }

        public string IniFileName
        {
            get { return iniFileName; }
            set { SetValue(ref iniFileName, value); }
        }

        public bool UseCustomIni
        {
            get { return useCustomIni; }
            set { SetValue(ref useCustomIni, value); }
        }

        public string ReleaseScope
        {
            get { return releaseScope; }
            set { SetValue(ref releaseScope, value); }
        }

        public string Engine
        {
            get { return engine; }
            set
            {
                var changed = !string.Equals(engine, value, StringComparison.Ordinal);
                SetValue(ref engine, value);

                if (changed)
                {
                    // Engine=XeSS tops out at 4X; keep the stored value valid.
                    if (IsXeSSEngine && ParseInt(MaxGeneratedFrames, 3) > 3)
                    {
                        MaxGeneratedFrames = "3";
                    }
                    OnPropertyChanged("IsXeSSEngine");
                    OnPropertyChanged("IsDlssEngine");
                }
            }
        }

        public bool IsXeSSEngine
        {
            get { return string.Equals(Engine, EngineXeSS, StringComparison.OrdinalIgnoreCase); }
        }

        public bool IsDlssEngine
        {
            get { return !IsXeSSEngine; }
        }

        public string MaxGeneratedFrames
        {
            get { return maxGeneratedFrames; }
            set { SetValue(ref maxGeneratedFrames, value); }
        }

        public string CatmullRomWarp
        {
            get { return catmullRomWarp; }
            set { SetValue(ref catmullRomWarp, value); }
        }

        public string Optimized
        {
            get { return optimized; }
            set { SetValue(ref optimized, value); }
        }

        public string Preset
        {
            get { return preset; }
            set { SetValue(ref preset, value); }
        }

        public string FlowResolution
        {
            get { return flowResolution; }
            set { SetValue(ref flowResolution, value); }
        }

        public string UseHudless
        {
            get { return useHudless; }
            set { SetValue(ref useHudless, value); }
        }

        public string SuperResolutionMode
        {
            get { return superResolutionMode; }
            set
            {
                var changed = !string.Equals(superResolutionMode, value, StringComparison.Ordinal);
                SetValue(ref superResolutionMode, value);
                if (changed)
                {
                    OnPropertyChanged("IsSuperResolutionEnabled");
                }
            }
        }

        public bool IsSuperResolutionEnabled
        {
            get { return !string.Equals(SuperResolutionMode, SuperResolutionOff, StringComparison.OrdinalIgnoreCase); }
        }

        public string LogLevel
        {
            get { return logLevel; }
            set { SetValue(ref logLevel, value); }
        }

        public string FrameTiming
        {
            get { return frameTiming; }
            set { SetValue(ref frameTiming, value); }
        }

        /// <summary>Last generated INI text, kept so the view can show a preview.</summary>
        public string CustomIniContent
        {
            get { return customIniContent; }
            set { SetValue(ref customIniContent, value); }
        }

        public static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        /// <summary>
        /// Builds a dlssg_sm86.ini in the 0.4.2 layout. Sections and keys that the
        /// selected engine does not read are still written with their factory value,
        /// so switching engines later only means changing one line.
        /// </summary>
        public string BuildCustomIniContent()
        {
            var builder = new StringBuilder();
            builder.AppendLine("; Generated by Playnite DLSSG Plugin for dlssg_for_sm86 " + ComponentCache.UpstreamVersion + ".");
            builder.AppendLine("; Change the values in the plugin settings instead of editing this file -");
            builder.AppendLine("; the next deploy overwrites it. Key list: docs\\INSTALL.md upstream.");
            builder.AppendLine();
            builder.AppendLine("[General]");
            builder.AppendLine("; 1 = frame generation on, 0 = off (the game's own DLSS-G loads unchanged).");
            builder.AppendLine("Enabled=1");
            builder.AppendLine();
            builder.AppendLine("[FrameGeneration]");
            builder.AppendLine("; DLSS = NVIDIA DLSS-G model on this project's kernels (default).");
            builder.AppendLine("; XeSS = Intel XeSS-FG model 15 on this project's own runner (needs the xefg pack).");
            builder.AppendLine("Engine=" + Engine);
            builder.AppendLine("; Generated frames per real frame: 1 = 2X, 2 = 3X, 3 = 4X, 5 = 6X.");
            builder.AppendLine("; Engine=XeSS accepts 1-3 only. A requirement, not a ceiling.");
            builder.AppendLine("MaxGeneratedFrames=" + MaxGeneratedFrames);
            builder.AppendLine("; 1 = Catmull-Rom motion-compensation sampling (+3-4 % per generated frame,");
            builder.AppendLine("; no longer bit-identical). 0 = bilinear (default).");
            builder.AppendLine("CatmullRomWarp=" + CatmullRomWarp);
            builder.AppendLine();
            builder.AppendLine("[DLSSG]");
            builder.AppendLine("; Read only by Engine=DLSS.");
            builder.AppendLine("; 0 = stock kernels, 1 = all accelerations, bit-identical output (default),");
            builder.AppendLine("; 2 = tier 1 plus lossy image kernels, 3 = everything lossy (fastest).");
            builder.AppendLine("Optimized=" + Optimized);
            builder.AppendLine("; A = UI recomposition off (default), B = on (needs a HUD-less image and a UI");
            builder.AppendLine("; plane from the game), C = FG Lite (faster, slightly lower quality).");
            builder.AppendLine("Preset=" + Preset);
            builder.AppendLine();
            builder.AppendLine("[XeSSFG]");
            builder.AppendLine("; Read only by Engine=XeSS.");
            builder.AppendLine("; Optical flow resolution: Coarse / Middle / Fine, finer is slower.");
            builder.AppendLine("; CoarseAt4K = Coarse at 3840x2160 and above, Middle otherwise.");
            builder.AppendLine("FlowResolution=" + FlowResolution);
            builder.AppendLine("; Auto = same as 0 (whole image, UI included, is interpolated).");
            builder.AppendLine("; 1 = strict: always composite the game's HUD-less image, error when missing.");
            builder.AppendLine("UseHudless=" + UseHudless);
            builder.AppendLine();
            builder.AppendLine("[SuperResolution]");
            builder.AppendLine("; RTX 30 / 20 only. Off = not touched (default). M-INT8 / L-INT8 = preset M / L");
            builder.AppendLine("; on this project's INT8 kernels, M-FP16 / L-FP16 = the same preset on NVIDIA's.");
            builder.AppendLine("Mode=" + SuperResolutionMode);
            builder.AppendLine();
            builder.AppendLine("[Logging]");
            builder.AppendLine("; 0 = off, 1 = errors (default), 2 = configuration and capability, 3 = traces.");
            builder.AppendLine("Level=" + LogLevel);
            builder.AppendLine("Directory=dlssg_sm86\\logs");
            builder.AppendLine("; 1 = write one line per real frame to frame_timing.txt.");
            builder.AppendLine("FrameTiming=" + FrameTiming);
            builder.AppendLine();
            builder.AppendLine("[Runtime]");
            builder.AppendLine("; Bundled = the packaged runtime in dlssg_sm86\\runtime (normal use).");
            builder.AppendLine("Mode=Bundled");

            return builder.ToString();
        }
    }

    public class PlayniteDlssgPluginSettingsViewModel : ObservableObject, ISettings
    {
        private readonly PlayniteDlssgPlugin plugin;
        private PlayniteDlssgPluginSettings editingClone;
        private PlayniteDlssgPluginSettings settings;
        private string cacheStatus = string.Empty;

        public PlayniteDlssgPluginSettings Settings
        {
            get { return settings; }
            set { settings = value; }
        }

        public PlayniteDlssgPluginSettingsViewModel(PlayniteDlssgPlugin plugin)
        {
            this.plugin = plugin;
            Settings = plugin.LoadPluginSettings<PlayniteDlssgPluginSettings>()
                ?? new PlayniteDlssgPluginSettings();

            if (Settings.SourceDirectory == @"D:\Download\dlssg_for_sm86-main")
            {
                Settings.SourceDirectory = "";
            }

            // dlssg_for_sm86 0.3.1 renamed altnative/ to alternatives/, dropped
            // winhttp.dll and the preset INIs, and picks the kernel family itself.
            if (Settings.DllFileName == "winhttp.dll")
            {
                Settings.DllFileName = "version.dll";
            }
            if (Settings.IniFileName != "dlssg_sm86.ini")
            {
                Settings.IniFileName = "dlssg_sm86.ini";
            }

            // 0.4.x dropped KernelImage / HardwareBilinear / [General] Enabled and
            // moved Optimized / Preset into [DLSSG]; the stored values are simply
            // ignored and the new defaults apply. Only the deploy scope is migrated.
            if (Settings.ReleaseScope == "当前筛选结果")
            {
                Settings.ReleaseScope = "Filtered";
            }
            else if (Settings.ReleaseScope == "当前选中游戏")
            {
                Settings.ReleaseScope = "Selected";
            }
            else if (Settings.ReleaseScope == "全部游戏")
            {
                Settings.ReleaseScope = "All";
            }

            if (!Settings.IsDlssEngine && !Settings.IsXeSSEngine)
            {
                Settings.Engine = PlayniteDlssgPluginSettings.EngineDlss;
            }

            // Regenerate once on load: a settings file written by 1.3 still holds an
            // INI built from keys that 0.4.x no longer reads.
            Settings.CustomIniContent = Settings.BuildCustomIniContent();

            ReleaseByConfigCommand = new RelayCommand(() => plugin.DeployUsingConfiguredScope());
            UninstallByConfigCommand = new RelayCommand(() => plugin.UninstallUsingConfiguredScope());
            BrowseSourceDirectoryCommand = new RelayCommand(BrowseSourceDirectory);
            ClearCacheCommand = new RelayCommand(ClearCache);
            OpenCacheFolderCommand = new RelayCommand(OpenCacheFolder);

            RefreshCacheStatus();
        }

        public string CacheStatus
        {
            get { return cacheStatus; }
            private set { SetValue(ref cacheStatus, value); }
        }

        public void RefreshCacheStatus()
        {
            var cached = new List<string>();
            if (ComponentCache.IsCached(@"dlssg_sm86\xefg\xefg_kernels.dll"))
            {
                cached.Add(Loc.Get("LocDlssgCacheItemXeSS"));
            }
            if (ComponentCache.IsCached(@"dlssg_sm86\sr\nvngx_dlss.dll") &&
                ComponentCache.IsCached(@"dlssg_sm86\sr\sr_kernels.dll"))
            {
                cached.Add(Loc.Get("LocDlssgCacheItemSr"));
            }
            if (ComponentCache.RawFiles.Any(file => ComponentCache.IsCached(file)))
            {
                cached.Add(Loc.Get("LocDlssgCacheItemProxy"));
            }

            var size = ComponentCache.GetCacheSize();
            var sizeText = size >= 1024L * 1024L
                ? (size / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.CurrentCulture) + " MB"
                : (size / 1024.0).ToString("0", CultureInfo.CurrentCulture) + " KB";

            var lines = new List<string>
            {
                string.Format(Loc.Get("LocDlssgCacheComponents"),
                    cached.Count == 0 ? Loc.Get("LocDlssgCacheEmpty") : string.Join(", ", cached)),
                string.Format(Loc.Get("LocDlssgCacheSize"), sizeText),
                string.Format(Loc.Get("LocDlssgCacheLocation"), ComponentCache.CacheRoot)
            };

            CacheStatus = string.Join(Environment.NewLine, lines);
        }

        private void ClearCache()
        {
            var confirmation = plugin.PlayniteApi.Dialogs.ShowMessage(
                Loc.Get("LocDlssgCacheClearConfirm"),
                Loc.Get("LocDlssgTitle"));
            if (confirmation != MessageBoxResult.OK)
            {
                return;
            }

            try
            {
                ComponentCache.Clear();
                RefreshCacheStatus();
            }
            catch (Exception ex)
            {
                plugin.PlayniteApi.Dialogs.ShowErrorMessage(
                    string.Format(Loc.Get("LocDlssgCacheClearFailed"), ex.Message),
                    Loc.Get("LocDlssgTitle"));
            }
        }

        private void OpenCacheFolder()
        {
            try
            {
                var root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PlayniteDlssgPlugin");
                Directory.CreateDirectory(root);
                System.Diagnostics.Process.Start("explorer.exe", "\"" + root + "\"");
            }
            catch (Exception ex)
            {
                plugin.PlayniteApi.Dialogs.ShowErrorMessage(ex.Message, Loc.Get("LocDlssgTitle"));
            }
        }

        private void BrowseSourceDirectory()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = Loc.Get("LocDlssgBrowseDialog"),
                SelectedPath = Settings.SourceDirectory,
                ShowNewFolderButton = false
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Settings.SourceDirectory = dialog.SelectedPath;
            }
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
            OnPropertyChanged("Settings");
        }

        public void EndEdit()
        {
            Settings.CustomIniContent = Settings.BuildCustomIniContent();
            plugin.SavePluginSettings(Settings);
            RefreshCacheStatus();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            // An empty source directory means "use the payload bundled with the plugin",
            // which is the normal case.
            if (!string.IsNullOrWhiteSpace(Settings.SourceDirectory) &&
                !Directory.Exists(Settings.SourceDirectory))
            {
                errors.Add(Loc.Get("LocDlssgErrSourceMissing"));
            }

            ValidateFileName(Settings.DllFileName, "LocDlssgErrDllInvalid", errors);
            ValidateFileName(Settings.IniFileName, "LocDlssgErrIniInvalid", errors);

            if (!IsOneOf(Settings.Engine, "DLSS", "XeSS"))
            {
                errors.Add(Loc.Get("LocDlssgErrEngine"));
            }

            var maxFrames = PlayniteDlssgPluginSettings.ParseInt(Settings.MaxGeneratedFrames, -1);
            if (maxFrames < 1 || maxFrames > 5)
            {
                errors.Add(Loc.Get("LocDlssgErrMaxFrames"));
            }
            else if (Settings.IsXeSSEngine && maxFrames > 3)
            {
                errors.Add(Loc.Get("LocDlssgErrMaxFramesXeSS"));
            }

            var optimized = PlayniteDlssgPluginSettings.ParseInt(Settings.Optimized, -1);
            if (optimized < 0 || optimized > 3)
            {
                errors.Add(Loc.Get("LocDlssgErrOptimized"));
            }

            if (!IsOneOf(Settings.Preset, "A", "B", "C"))
            {
                errors.Add(Loc.Get("LocDlssgErrPreset"));
            }

            if (!IsOneOf(Settings.CatmullRomWarp, "0", "1"))
            {
                errors.Add(Loc.Get("LocDlssgErrCatmullRom"));
            }

            if (!IsOneOf(Settings.FlowResolution, "Coarse", "Middle", "Fine", "CoarseAt4K"))
            {
                errors.Add(Loc.Get("LocDlssgErrFlowResolution"));
            }

            if (!IsOneOf(Settings.UseHudless, "Auto", "0", "1"))
            {
                errors.Add(Loc.Get("LocDlssgErrUseHudless"));
            }

            if (!IsOneOf(Settings.SuperResolutionMode, "Off", "M-INT8", "L-INT8", "M-FP16", "L-FP16"))
            {
                errors.Add(Loc.Get("LocDlssgErrSuperResolution"));
            }

            var logLevel = PlayniteDlssgPluginSettings.ParseInt(Settings.LogLevel, -1);
            if (logLevel < 0 || logLevel > 3)
            {
                errors.Add(Loc.Get("LocDlssgErrLogLevel"));
            }

            if (!IsOneOf(Settings.FrameTiming, "0", "1"))
            {
                errors.Add(Loc.Get("LocDlssgErrFrameTiming"));
            }

            if (Settings.ReleaseScope != "Filtered" &&
                Settings.ReleaseScope != "Selected" &&
                Settings.ReleaseScope != "All")
            {
                errors.Add(Loc.Get("LocDlssgErrScope"));
            }

            if (Settings.UseCustomIni && string.IsNullOrWhiteSpace(Settings.CustomIniContent))
            {
                errors.Add(Loc.Get("LocDlssgErrCustomIni"));
            }

            return errors.Count == 0;
        }

        public ICommand ReleaseByConfigCommand { get; private set; }
        public ICommand UninstallByConfigCommand { get; private set; }
        public ICommand BrowseSourceDirectoryCommand { get; private set; }
        public ICommand ClearCacheCommand { get; private set; }
        public ICommand OpenCacheFolderCommand { get; private set; }

        private static bool IsOneOf(string value, params string[] allowed)
        {
            return allowed.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase));
        }

        private static void ValidateFileName(string fileName, string errorKey, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                Path.GetFileName(fileName) != fileName)
            {
                errors.Add(Loc.Get(errorKey));
            }
        }
    }
}
