using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace LocalGameDownloader
{
    public class LocalGameDownloader : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string LegacyDefaultManifestLocation = "https://pcgames.cosmere.cc/manifest.json";

        private readonly DownloadService downloadService = new DownloadService();
        private readonly ExtractionService extractionService = new ExtractionService();
        private readonly GameImportService gameImportService = new GameImportService();
        private readonly MetadataEnrichmentService metadataEnrichmentService = new MetadataEnrichmentService();
        private readonly SevenZipResolver sevenZipResolver = new SevenZipResolver();
        private Window catalogWindow;
        private Window queueWindow;
        private readonly DownloadQueueService downloadQueueService;

        public LocalGameDownloaderSettingsViewModel SettingsViewModel { get; }

        public IPlayniteAPI Api => PlayniteApi;

        public DownloadQueueService QueueService => downloadQueueService;

        public override Guid Id { get; } = Guid.Parse("9925d475-baad-480c-b219-e7d500e382a1");

        public LocalGameDownloader(IPlayniteAPI api) : base(api)
        {
            SettingsViewModel = new LocalGameDownloaderSettingsViewModel(this);
            downloadQueueService = new DownloadQueueService(api, ExecuteInstallJobAsync);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = "Browse downloadable portable games from the configured manifest.",
                MenuSection = "Local Game Downloader",
                Action = _ => ShowCatalogWindow()
            };

            yield return new MainMenuItem
            {
                Description = "Show queued and active downloads.",
                MenuSection = "Local Game Downloader",
                Action = _ => ShowQueueWindow()
            };
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            yield return new SidebarItem
            {
                Type = SiderbarItemType.Button,
                Title = "Local Game Downloader",
                Icon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "icon.png"),
                Activated = ShowCatalogWindow
            };
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return SettingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new LocalGameDownloaderSettingsView();
        }

        public void PersistSettings()
        {
            SavePluginSettings(SettingsViewModel.Settings);
        }

        public bool TryAutoDetectSevenZip()
        {
            var resolved = sevenZipResolver.Resolve(SettingsViewModel.Settings.SevenZipPath);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return false;
            }

            SettingsViewModel.Settings.SevenZipPath = resolved;
            PersistSettings();
            return true;
        }

        public void BrowseForInstallRoot()
        {
            var selected = PlayniteApi.Dialogs.SelectFolder(SettingsViewModel.Settings.DefaultInstallRoot);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                SettingsViewModel.Settings.DefaultInstallRoot = selected;
                PersistSettings();
            }
        }

        public void BrowseForSevenZip()
        {
            var selected = PlayniteApi.Dialogs.SelectFile("7-Zip executable|7z.exe", SettingsViewModel.Settings.SevenZipPath);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                SettingsViewModel.Settings.SevenZipPath = selected;
                PersistSettings();
            }
        }

        public bool EnsureManifestLocation(bool promptIfMissing = true, bool showRequiredMessage = false)
        {
            MigrateManifestConfiguration();

            var manifestLocation = SettingsViewModel.Settings.ManifestLocation?.Trim();
            if (!string.Equals(manifestLocation, SettingsViewModel.Settings.ManifestLocation, StringComparison.Ordinal))
            {
                SettingsViewModel.Settings.ManifestLocation = manifestLocation ?? string.Empty;
            }

            if (SettingsViewModel.Settings.HasConfiguredManifestLocation &&
                !string.IsNullOrWhiteSpace(manifestLocation))
            {
                return true;
            }

            if (!promptIfMissing)
            {
                return false;
            }

            var selected = PlayniteApi.Dialogs.SelectString(
                "Enter the manifest URL or local manifest file path for Local Game Downloader.",
                "Configure Manifest Source",
                manifestLocation ?? string.Empty);

            if (selected == null || !selected.Result || string.IsNullOrWhiteSpace(selected.SelectedString))
            {
                if (showRequiredMessage)
                {
                    PlayniteApi.Dialogs.ShowMessage(
                        "Set a manifest source when you're ready to browse downloadable games.",
                        "Local Game Downloader");
                }
                return false;
            }

            SettingsViewModel.Settings.ManifestLocation = selected.SelectedString.Trim();
            SettingsViewModel.Settings.HasConfiguredManifestLocation = true;
            PersistSettings();
            return true;
        }

        public bool QueueGameInstall(RemoteGameEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            var existingJob = downloadQueueService.GetLatestJobForEntry(entry);
            if (existingJob != null &&
                existingJob.State != DownloadJobState.Completed &&
                existingJob.State != DownloadJobState.Failed &&
                existingJob.State != DownloadJobState.Canceled)
            {
                PlayniteApi.Dialogs.ShowMessage(
                    $"'{entry.Name}' is already queued or installing.",
                    "Local Game Downloader");
                ShowQueueWindow();
                return false;
            }

            var installRoot = EnsureInstallRoot();
            if (string.IsNullOrWhiteSpace(installRoot))
            {
                return false;
            }

            var sevenZipPath = EnsureSevenZipPath();
            if (string.IsNullOrWhiteSpace(sevenZipPath))
            {
                return false;
            }

            var installDirectory = Path.Combine(installRoot, PathUtilities.SanitizeFileName(entry.Name));
            if (Directory.Exists(installDirectory) && Directory.EnumerateFileSystemEntries(installDirectory).Any())
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"The install directory already exists and is not empty:\n{installDirectory}",
                    "Local Game Downloader");
                return false;
            }

            if (PlayniteApi.Database.Games.Any(game =>
                string.Equals(game.Name, entry.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(game.InstallDirectory, installDirectory, StringComparison.OrdinalIgnoreCase)))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    $"A game named '{entry.Name}' is already in your Playnite library or uses the same install path.",
                    "Local Game Downloader");
                return false;
            }

            var archiveDirectory = Path.Combine(installRoot, "_downloads");
            var archivePath = Path.Combine(archiveDirectory, PathUtilities.SanitizeFileName(entry.ArchiveFileName));
            downloadQueueService.Enqueue(entry, sevenZipPath, installDirectory, archivePath);
            ShowQueueWindow();
            return true;
        }

        public bool RetryJob(DownloadJob job)
        {
            return downloadQueueService.RetryJob(job) != null;
        }

        public Game FindInstalledGame(RemoteGameEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            return PlayniteApi.Database.Games.FirstOrDefault(game =>
                string.Equals(game.GameId, entry.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(game.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
        }

        public void OpenInstallFolder(Game game)
        {
            if (game == null || string.IsNullOrWhiteSpace(game.InstallDirectory) || !Directory.Exists(game.InstallDirectory))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "The install folder could not be found for this game.",
                    "Local Game Downloader");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = game.InstallDirectory,
                UseShellExecute = true
            });
        }

        public void StartGame(Game game)
        {
            if (game == null)
            {
                return;
            }

            PlayniteApi.StartGame(game.Id);
        }

        public void OpenExternalUri(Uri uri)
        {
            if (uri == null)
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            });
        }

        public void ShowQueue()
        {
            ShowQueueWindow();
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            TryAutoDetectSevenZip();
            EnsureManifestLocation(showRequiredMessage: false);
        }

        private void MigrateManifestConfiguration()
        {
            if (SettingsViewModel.Settings.HasConfiguredManifestLocation)
            {
                return;
            }

            var manifestLocation = SettingsViewModel.Settings.ManifestLocation?.Trim();
            if (string.IsNullOrWhiteSpace(manifestLocation) ||
                string.Equals(manifestLocation, LegacyDefaultManifestLocation, StringComparison.OrdinalIgnoreCase))
            {
                SettingsViewModel.Settings.ManifestLocation = string.Empty;
                return;
            }

            SettingsViewModel.Settings.HasConfiguredManifestLocation = true;
            PersistSettings();
        }

        private void ShowCatalogWindow()
        {
            if (!EnsureManifestLocation(showRequiredMessage: true))
            {
                return;
            }

            if (catalogWindow != null)
            {
                catalogWindow.Activate();
                return;
            }

            var viewModel = new CatalogViewModel(this);
            var view = new CatalogView
            {
                DataContext = viewModel
            };

            catalogWindow = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = true,
                ShowCloseButton = true
            });

            catalogWindow.Title = "Local Game Downloader";
            catalogWindow.Width = 1050;
            catalogWindow.Height = 720;
            catalogWindow.Content = view;
            catalogWindow.Closed += (_, __) => catalogWindow = null;
            catalogWindow.Loaded += async (_, __) => await viewModel.LoadAsync();
            catalogWindow.Show();
        }

        private void ShowQueueWindow()
        {
            if (queueWindow != null)
            {
                queueWindow.Activate();
                return;
            }

            var view = new DownloadQueueView
            {
                DataContext = downloadQueueService
            };

            queueWindow = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowMaximizeButton = false,
                ShowCloseButton = true
            });

            queueWindow.Title = "Local Game Downloader Queue";
            queueWindow.Width = 780;
            queueWindow.Height = 420;
            queueWindow.Content = view;
            queueWindow.Closed += (_, __) => queueWindow = null;
            queueWindow.Show();
        }

        private string EnsureInstallRoot()
        {
            var installRoot = SettingsViewModel.Settings.DefaultInstallRoot;
            if (!string.IsNullOrWhiteSpace(installRoot) && Directory.Exists(installRoot))
            {
                return installRoot;
            }

            installRoot = PlayniteApi.Dialogs.SelectFolder(installRoot);
            if (string.IsNullOrWhiteSpace(installRoot))
            {
                return null;
            }

            SettingsViewModel.Settings.DefaultInstallRoot = installRoot;
            PersistSettings();
            return installRoot;
        }

        private string EnsureSevenZipPath()
        {
            var resolved = sevenZipResolver.Resolve(SettingsViewModel.Settings.SevenZipPath);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                if (!string.Equals(resolved, SettingsViewModel.Settings.SevenZipPath, StringComparison.OrdinalIgnoreCase))
                {
                    SettingsViewModel.Settings.SevenZipPath = resolved;
                    PersistSettings();
                }

                return resolved;
            }

            var manualSelection = PlayniteApi.Dialogs.SelectFile("7-Zip executable|7z.exe", SettingsViewModel.Settings.SevenZipPath);
            if (string.IsNullOrWhiteSpace(manualSelection))
            {
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "7-Zip could not be auto-detected. Install 7-Zip or browse to 7z.exe manually.",
                    "Local Game Downloader");
                return null;
            }

            SettingsViewModel.Settings.SevenZipPath = manualSelection;
            PersistSettings();
            return manualSelection;
        }

        private async Task<Game> ExecuteInstallJobAsync(DownloadJob job)
        {
            var entry = job.Entry;
            Directory.CreateDirectory(Path.GetDirectoryName(job.ArchivePath));

            try
            {
                RunOnUi(() =>
                {
                    job.State = DownloadJobState.Downloading;
                    job.ProgressValue = 0;
                    job.StatusText = "Starting download";
                });
                downloadQueueService.RefreshSummary();

                await downloadService.DownloadFileAsync(
                    entry.DownloadUri,
                    job.ArchivePath,
                    report => UpdateDownloadProgress(job, entry, report),
                    job.CancellationTokenSource.Token).ConfigureAwait(false);

                job.CancellationTokenSource.Token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(job.InstallDirectory);

                RunOnUi(() =>
                {
                    job.State = DownloadJobState.Extracting;
                    job.StatusText = "Extracting archive";
                    job.ProgressValue = 50;
                });
                downloadQueueService.RefreshSummary();
                await extractionService.ExtractArchiveAsync(
                    job.SevenZipPath,
                    job.ArchivePath,
                    job.InstallDirectory,
                    report => UpdateExtractionProgress(job, entry, report),
                    job.CancellationTokenSource.Token).ConfigureAwait(false);

                RunOnUi(() =>
                {
                    job.State = DownloadJobState.Importing;
                    job.StatusText = "Preparing import and metadata";
                    job.ProgressValue = 97;
                });
                downloadQueueService.RefreshSummary();

                if (SettingsViewModel.Settings.DeleteArchiveOnSuccess && File.Exists(job.ArchivePath))
                {
                    File.Delete(job.ArchivePath);
                }

                var importedGame = gameImportService.ImportInstalledGame(
                    PlayniteApi,
                    entry,
                    job.InstallDirectory,
                    metadataEnrichmentService);

                RunOnUi(() => job.ProgressValue = 100);
                return importedGame;
            }
            catch (OperationCanceledException)
            {
                CleanupFailedInstall(job.ArchivePath, job.InstallDirectory);
                throw;
            }
            catch
            {
                CleanupFailedInstall(job.ArchivePath, job.InstallDirectory);
                throw;
            }
        }

        private void CleanupFailedInstall(string archivePath, string installDirectory)
        {
            try
            {
                if (File.Exists(archivePath))
                {
                    File.Delete(archivePath);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Failed to remove archive '{archivePath}'.");
            }

            try
            {
                if (Directory.Exists(installDirectory) && !Directory.EnumerateFileSystemEntries(installDirectory).Any())
                {
                    Directory.Delete(installDirectory, false);
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Failed to clean install directory '{installDirectory}'.");
            }
        }

        private static void UpdateDownloadProgress(DownloadJob job, RemoteGameEntry entry, TransferProgress report)
        {
            RunOnUi(() =>
            {
                if (report.TotalBytes.HasValue && report.TotalBytes.Value > 0)
                {
                    var percent = (double)report.BytesTransferred / report.TotalBytes.Value;
                    job.ProgressValue = Math.Min(50, Math.Round(percent * 50d));
                    job.StatusText = $"Downloading ({PathUtilities.FormatBytes(report.BytesTransferred)} / {PathUtilities.FormatBytes(report.TotalBytes.Value)})";
                }
                else
                {
                    job.StatusText = $"Downloading ({PathUtilities.FormatBytes(report.BytesTransferred)})";
                }
            });
        }

        private static void UpdateExtractionProgress(DownloadJob job, RemoteGameEntry entry, ExtractionProgress report)
        {
            RunOnUi(() =>
            {
                if (report.PercentComplete.HasValue)
                {
                    job.ProgressValue = 50 + Math.Round(report.PercentComplete.Value * 0.45d);
                    job.StatusText = $"Extracting ({report.PercentComplete.Value}%)";
                }
                else
                {
                    job.StatusText = "Extracting";
                }
            });
        }

        private static void RunOnUi(Action action)
        {
            if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(action, DispatcherPriority.Background);
            }
        }
    }
}
