using Playnite.SDK.Models;
using System.Collections.Generic;

namespace LocalGameDownloader
{
    public class CatalogGameEntry : ObservableObject
    {
        private string stateLabel = "Available";
        private string statusDetail = "Ready to install.";
        private bool canInstall = true;
        private bool canRetry;
        private bool canPlay;
        private bool canOpenFolder;
        private Game existingGame;
        private DownloadJob latestJob;

        public RemoteGameEntry Entry { get; }

        public string Name => Entry.Name;

        public string DisplayFileSize => Entry.DisplayFileSize;

        public string UploadDateDisplay => Entry.UploadDateDisplay;

        public string DownloadHost => Entry.DownloadHost;

        public string DownloadUri => Entry.DownloadUri?.ToString();

        public string DisplaySummary => Entry.DisplaySummary;

        public string StateLabel
        {
            get => stateLabel;
            set => SetValue(ref stateLabel, value);
        }

        public string StatusDetail
        {
            get => statusDetail;
            set => SetValue(ref statusDetail, value);
        }

        public bool CanInstall
        {
            get => canInstall;
            set => SetValue(ref canInstall, value);
        }

        public bool CanRetry
        {
            get => canRetry;
            set => SetValue(ref canRetry, value);
        }

        public bool CanPlay
        {
            get => canPlay;
            set => SetValue(ref canPlay, value);
        }

        public bool CanOpenFolder
        {
            get => canOpenFolder;
            set => SetValue(ref canOpenFolder, value);
        }

        public string InstallActionLabel => CanRetry ? "Retry" : "Install";

        public Game ExistingGame
        {
            get => existingGame;
            private set => SetValue(ref existingGame, value);
        }

        public DownloadJob LatestJob
        {
            get => latestJob;
            private set => SetValue(ref latestJob, value);
        }

        public CatalogGameEntry(RemoteGameEntry entry)
        {
            Entry = entry;
        }

        public void UpdateState(Game game, DownloadJob job)
        {
            ExistingGame = game;
            LatestJob = job;

            if (game != null)
            {
                StateLabel = "Installed";
                StatusDetail = string.IsNullOrWhiteSpace(game.InstallDirectory) ? "Installed in Playnite." : game.InstallDirectory;
                CanInstall = false;
                CanRetry = false;
                CanPlay = game.IsInstalled;
                CanOpenFolder = !string.IsNullOrWhiteSpace(game.InstallDirectory);
            }
            else if (job != null)
            {
                ApplyJobState(job);
            }
            else
            {
                StateLabel = "Available";
                StatusDetail = "Ready to install.";
                CanInstall = true;
                CanRetry = false;
                CanPlay = false;
                CanOpenFolder = false;
            }

            OnPropertyChanged(nameof(InstallActionLabel));
        }

        private void ApplyJobState(DownloadJob job)
        {
            StateLabel = job.StateLabel;
            StatusDetail = string.IsNullOrWhiteSpace(job.StatusText) ? job.StateLabel : job.StatusText;
            CanPlay = false;
            CanOpenFolder = job.State == DownloadJobState.Completed && !string.IsNullOrWhiteSpace(job.InstallDirectory);

            switch (job.State)
            {
                case DownloadJobState.Failed:
                case DownloadJobState.Canceled:
                    CanInstall = false;
                    CanRetry = true;
                    break;
                case DownloadJobState.Completed:
                    CanInstall = false;
                    CanRetry = false;
                    break;
                default:
                    CanInstall = false;
                    CanRetry = false;
                    break;
            }
        }
    }
}
