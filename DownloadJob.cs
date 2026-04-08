using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Threading;

namespace LocalGameDownloader
{
    public enum DownloadJobState
    {
        Queued,
        Downloading,
        Extracting,
        Importing,
        DownloadingMetadata,
        Completed,
        Failed,
        Canceled
    }

    public class DownloadJob : ObservableObject
    {
        private DownloadJobState state;
        private string statusText;
        private double progressValue;
        private string errorMessage;

        public Guid Id { get; } = Guid.NewGuid();

        public RemoteGameEntry Entry { get; }

        public string SevenZipPath { get; }

        public string InstallDirectory { get; }

        public string ArchivePath { get; }

        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        public DownloadJobState State
        {
            get => state;
            set
            {
                if (state != value)
                {
                    SetValue(ref state, value);
                    OnPropertyChanged(nameof(StateLabel));
                }
            }
        }

        public string StateLabel => State.ToString();

        public string StatusText
        {
            get => statusText;
            set => SetValue(ref statusText, value);
        }

        public double ProgressValue
        {
            get => progressValue;
            set => SetValue(ref progressValue, value);
        }

        public string ErrorMessage
        {
            get => errorMessage;
            set => SetValue(ref errorMessage, value);
        }

        public DownloadJob(RemoteGameEntry entry, string sevenZipPath, string installDirectory, string archivePath)
        {
            Entry = entry;
            SevenZipPath = sevenZipPath;
            InstallDirectory = installDirectory;
            ArchivePath = archivePath;
            State = DownloadJobState.Queued;
            StatusText = "Queued";
            ProgressValue = 0;
        }
    }
}
