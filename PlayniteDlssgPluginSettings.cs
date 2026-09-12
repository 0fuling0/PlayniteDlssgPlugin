using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace PlayniteDlssgPlugin
{
    public partial class PlayniteDlssgPluginSettings : ObservableObject
    {
        private string sourceDirectory = "";
        private string dllFileName = "version.dll";
        private string iniFileName = "dlssg_sm86.ini";
        private bool useCustomIni = true;
        private string router = "SM86";
        private string kernelImage = "PTX";
        private int hardwareBilinear;
        private int maxGeneratedFrames = 3;
        private string releaseScope = "Filtered";
        private int logLevel = 1;
        private string customIniContent =
            "[Compatibility]\r\n" +
            "; SM86 for Ampere; SM75 for Turing or SM75 forward-JIT testing.\r\n" +
            "Router=SM86\r\n" +
            "; PTX uses driver JIT. Cubin requires an exact GPU/Router match.\r\n" +
            "; Auto selects Cubin on an exact match, otherwise PTX.\r\n" +
            "KernelImage=PTX\r\n" +
            "; 0 = exact output (default); 1 = optional approximate sampling, SM86 only.\r\n" +
            "HardwareBilinear=0\r\n\r\n" +
            "[FrameGeneration]\r\n" +
            "; Capability limit: 1=2X, 2=3X, 3=4X. The game requests the actual multiplier.\r\n" +
            "MaxGeneratedFrames=3\r\n\r\n" +
            "[Logging]\r\n" +
            "; 0=off, 1=errors, 2=diagnostics, 3=verbose.\r\n" +
            "Level=1\r\n";

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

        public string Router
        {
            get { return router; }
            set { SetValue(ref router, value); }
        }

        public string KernelImage
        {
            get { return kernelImage; }
            set { SetValue(ref kernelImage, value); }
        }

        public int HardwareBilinear
        {
            get { return hardwareBilinear; }
            set { SetValue(ref hardwareBilinear, value); }
        }

        public int MaxGeneratedFrames
        {
            get { return maxGeneratedFrames; }
            set { SetValue(ref maxGeneratedFrames, value); }
        }

        public string ReleaseScope
        {
            get { return releaseScope; }
            set { SetValue(ref releaseScope, value); }
        }

        public int LogLevel
        {
            get { return logLevel; }
            set { SetValue(ref logLevel, value); }
        }

        public string CustomIniContent
        {
            get { return customIniContent; }
            set { SetValue(ref customIniContent, value); }
        }

        public string BuildCustomIniContent()
        {
            return "[Compatibility]\r\n" +
                "Router=" + Router + "\r\n" +
                "KernelImage=" + KernelImage + "\r\n" +
                "HardwareBilinear=" + HardwareBilinear + "\r\n\r\n" +
                "[FrameGeneration]\r\n" +
                "MaxGeneratedFrames=" + MaxGeneratedFrames + "\r\n\r\n" +
                "[Logging]\r\n" +
                "Level=" + LogLevel + "\r\n";
        }
    }

    public class PlayniteDlssgPluginSettingsViewModel : ObservableObject, ISettings
    {
        private readonly PlayniteDlssgPlugin plugin;
        private PlayniteDlssgPluginSettings editingClone;
        private PlayniteDlssgPluginSettings settings;

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

            // Migrate deploy scopes saved by versions that stored localized values.
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

            ReleaseByConfigCommand = new RelayCommand(() => plugin.DeployUsingConfiguredScope());
            UninstallByConfigCommand = new RelayCommand(() => plugin.UninstallUsingConfiguredScope());
            BrowseSourceDirectoryCommand = new RelayCommand(BrowseSourceDirectory);
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
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Settings.SourceDirectory) || !Directory.Exists(Settings.SourceDirectory))
            {
                errors.Add(Loc.Get("LocDlssgErrSourceMissing"));
            }

            ValidateFileName(Settings.DllFileName, "LocDlssgErrDllInvalid", errors);
            ValidateFileName(Settings.IniFileName, "LocDlssgErrIniInvalid", errors);
            if (string.IsNullOrWhiteSpace(Settings.Router) ||
                (Settings.Router != "SM86" && Settings.Router != "SM75"))
            {
                errors.Add(Loc.Get("LocDlssgErrRouter"));
            }

            if (string.IsNullOrWhiteSpace(Settings.KernelImage) ||
                (Settings.KernelImage != "PTX" && Settings.KernelImage != "Cubin" &&
                 Settings.KernelImage != "Auto"))
            {
                errors.Add(Loc.Get("LocDlssgErrKernel"));
            }

            if (Settings.HardwareBilinear < 0 || Settings.HardwareBilinear > 1)
            {
                errors.Add(Loc.Get("LocDlssgErrBilinear"));
            }

            if (Settings.MaxGeneratedFrames < 1 || Settings.MaxGeneratedFrames > 3)
            {
                errors.Add(Loc.Get("LocDlssgErrMaxFrames"));
            }

            if (Settings.LogLevel < 0 || Settings.LogLevel > 3)
            {
                errors.Add(Loc.Get("LocDlssgErrLogLevel"));
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
