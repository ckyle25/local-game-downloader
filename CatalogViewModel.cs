using Playnite.SDK;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK.Models;

namespace LocalGameDownloader
{
    public class CatalogViewModel : ObservableObject
    {
        private readonly LocalGameDownloader plugin;
        private readonly ManifestService manifestService = new ManifestService();
        private readonly ObservableCollection<CatalogGameEntry> visibleGames = new ObservableCollection<CatalogGameEntry>();
        private readonly List<CatalogGameEntry> allGames = new List<CatalogGameEntry>();

        private string searchText = string.Empty;
        private string statusText = "Loading manifest...";
        private CatalogGameEntry selectedGame;

        public ObservableCollection<CatalogGameEntry> VisibleGames => visibleGames;

        public string SearchText
        {
            get => searchText;
            set
            {
                if (searchText != value)
                {
                    SetValue(ref searchText, value);
                    ApplyFilter();
                }
            }
        }

        public string StatusText
        {
            get => statusText;
            set => SetValue(ref statusText, value);
        }

        public CatalogGameEntry SelectedGame
        {
            get => selectedGame;
            set
            {
                SetValue(ref selectedGame, value);
                OnPropertyChanged(nameof(CanInstallSelected));
                OnPropertyChanged(nameof(CanRetrySelected));
                OnPropertyChanged(nameof(CanInstallActionSelected));
                OnPropertyChanged(nameof(CanPlaySelected));
                OnPropertyChanged(nameof(CanOpenFolderSelected));
                OnPropertyChanged(nameof(InstallActionLabel));
            }
        }

        public string InstallRootDisplay =>
            string.IsNullOrWhiteSpace(plugin.SettingsViewModel.Settings.DefaultInstallRoot)
                ? "No default install root selected yet."
                : plugin.SettingsViewModel.Settings.DefaultInstallRoot;

        public CatalogViewModel(LocalGameDownloader plugin)
        {
            this.plugin = plugin;
            plugin.QueueService.QueueChanged += (_, __) => RefreshEntryStates();
        }

        public bool CanInstallSelected => SelectedGame?.CanInstall == true;

        public bool CanRetrySelected => SelectedGame?.CanRetry == true;

        public bool CanInstallActionSelected => CanInstallSelected || CanRetrySelected;

        public bool CanPlaySelected => SelectedGame?.CanPlay == true;

        public bool CanOpenFolderSelected => SelectedGame?.CanOpenFolder == true;

        public string InstallActionLabel => SelectedGame?.InstallActionLabel ?? "Install";

        public async Task LoadAsync()
        {
            try
            {
                StatusText = "Loading manifest...";
                var loadedGames = await manifestService.LoadEntriesAsync(
                    plugin.SettingsViewModel.Settings.ManifestLocation,
                    CancellationToken.None);

                allGames.Clear();
                allGames.AddRange(loadedGames
                    .OrderBy(game => game.Name)
                    .Select(game => new CatalogGameEntry(game)));
                RefreshEntryStates();
                ApplyFilter();
                StatusText = $"Loaded {allGames.Count} downloadable games.";
            }
            catch (System.Exception ex)
            {
                StatusText = $"Failed to load manifest: {ex.Message}";
                plugin.Api.Dialogs.ShowErrorMessage(ex.Message, "Local Game Downloader");
            }
        }

        public Task InstallSelectedAsync()
        {
            if (SelectedGame == null)
            {
                plugin.Api.Dialogs.ShowMessage("Select a game to install.", "Local Game Downloader");
                return Task.CompletedTask;
            }

            if (SelectedGame.CanRetry && SelectedGame.LatestJob != null)
            {
                var retried = plugin.RetryJob(SelectedGame.LatestJob);
                if (retried)
                {
                    StatusText = $"Re-queued {SelectedGame.Name}.";
                }
            }
            else
            {
                var queued = plugin.QueueGameInstall(SelectedGame.Entry);
                if (queued)
                {
                    StatusText = $"Queued {SelectedGame.Name} for download.";
                }
            }

            return Task.CompletedTask;
        }

        public void PlaySelected()
        {
            if (SelectedGame?.ExistingGame != null)
            {
                plugin.StartGame(SelectedGame.ExistingGame);
            }
        }

        public void OpenSelectedFolder()
        {
            if (SelectedGame?.ExistingGame != null)
            {
                plugin.OpenInstallFolder(SelectedGame.ExistingGame);
            }
        }

        public void OpenIgdb(CatalogGameEntry game)
        {
            if (game != null)
            {
                plugin.OpenExternalUri(ExternalMetadataLinks.BuildIgdbSearchUri(game.Name));
            }
        }

        public void OpenPcGamingWiki(CatalogGameEntry game)
        {
            if (game != null)
            {
                plugin.OpenExternalUri(ExternalMetadataLinks.BuildPcGamingWikiSearchUri(game.Name));
            }
        }

        public void ShowQueue()
        {
            plugin.ShowQueue();
        }

        public void BrowseInstallRoot()
        {
            plugin.BrowseForInstallRoot();
            OnPropertyChanged(nameof(InstallRootDisplay));
        }

        private void ApplyFilter()
        {
            var filtered = allGames
                .Where(game =>
                    string.IsNullOrWhiteSpace(SearchText) ||
                    game.Name.IndexOf(SearchText, System.StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            VisibleGames.Clear();
            foreach (var game in filtered)
            {
                VisibleGames.Add(game);
            }

            SelectedGame = VisibleGames.FirstOrDefault();
        }

        private void RefreshEntryStates()
        {
            foreach (var game in allGames)
            {
                var installedGame = plugin.FindInstalledGame(game.Entry);
                var latestJob = plugin.QueueService.GetLatestJobForEntry(game.Entry);
                game.UpdateState(installedGame, latestJob);
            }

            OnPropertyChanged(nameof(CanInstallSelected));
            OnPropertyChanged(nameof(CanRetrySelected));
            OnPropertyChanged(nameof(CanInstallActionSelected));
            OnPropertyChanged(nameof(CanPlaySelected));
            OnPropertyChanged(nameof(CanOpenFolderSelected));
            OnPropertyChanged(nameof(InstallActionLabel));
        }
    }
}
