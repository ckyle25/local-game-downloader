using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;
using System.IO;

namespace LocalGameDownloader
{
    public class LocalGameDownloaderSettings : ObservableObject
    {
        private string manifestLocation = string.Empty;
        private bool hasConfiguredManifestLocation;
        private string defaultInstallRoot = string.Empty;
        private string sevenZipPath = string.Empty;
        private bool deleteArchiveOnSuccess = true;

        public string ManifestLocation
        {
            get => manifestLocation;
            set => SetValue(ref manifestLocation, value);
        }

        public bool HasConfiguredManifestLocation
        {
            get => hasConfiguredManifestLocation;
            set => SetValue(ref hasConfiguredManifestLocation, value);
        }

        public string DefaultInstallRoot
        {
            get => defaultInstallRoot;
            set => SetValue(ref defaultInstallRoot, value);
        }

        public string SevenZipPath
        {
            get => sevenZipPath;
            set => SetValue(ref sevenZipPath, value);
        }

        public bool DeleteArchiveOnSuccess
        {
            get => deleteArchiveOnSuccess;
            set => SetValue(ref deleteArchiveOnSuccess, value);
        }
    }

    public class LocalGameDownloaderSettingsViewModel : ObservableObject, ISettings
    {
        private readonly LocalGameDownloader plugin;
        private LocalGameDownloaderSettings editingClone;
        private LocalGameDownloaderSettings settings;

        public LocalGameDownloaderSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public LocalGameDownloaderSettingsViewModel(LocalGameDownloader plugin)
        {
            this.plugin = plugin;
            Settings = plugin.LoadPluginSettings<LocalGameDownloaderSettings>() ?? new LocalGameDownloaderSettings();
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            Settings.ManifestLocation = Settings.ManifestLocation?.Trim() ?? string.Empty;
            Settings.HasConfiguredManifestLocation = !string.IsNullOrWhiteSpace(Settings.ManifestLocation);
            plugin.PersistSettings();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();

            if (!string.IsNullOrWhiteSpace(Settings.SevenZipPath) && !File.Exists(Settings.SevenZipPath))
            {
                errors.Add("Configured 7-Zip path does not exist.");
            }

            if (!string.IsNullOrWhiteSpace(Settings.DefaultInstallRoot) && !Directory.Exists(Settings.DefaultInstallRoot))
            {
                errors.Add("Default install root does not exist.");
            }

            return errors.Count == 0;
        }

        public void BrowseInstallRoot()
        {
            var selected = plugin.Api.Dialogs.SelectFolder(Settings.DefaultInstallRoot);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                Settings.DefaultInstallRoot = selected;
            }
        }

        public void BrowseManifestFile()
        {
            var selected = plugin.Api.Dialogs.SelectFile("Manifest JSON|*.json|All files|*.*", Settings.ManifestLocation);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                Settings.ManifestLocation = selected.Trim();
                Settings.HasConfiguredManifestLocation = true;
            }
        }

        public void ClearManifestLocation()
        {
            Settings.ManifestLocation = string.Empty;
            Settings.HasConfiguredManifestLocation = false;
        }

        public void ClearInstallRoot()
        {
            Settings.DefaultInstallRoot = string.Empty;
        }

        public bool AutoDetectSevenZip()
        {
            var resolved = new SevenZipResolver().Resolve(Settings.SevenZipPath);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return false;
            }

            Settings.SevenZipPath = resolved;
            return true;
        }

        public void BrowseSevenZip()
        {
            var selected = plugin.Api.Dialogs.SelectFile("7-Zip executable|7z.exe", Settings.SevenZipPath);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                Settings.SevenZipPath = selected;
            }
        }
    }
}
