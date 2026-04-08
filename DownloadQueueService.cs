using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LocalGameDownloader
{
    public class DownloadQueueService : ObservableObject
    {
        private readonly IPlayniteAPI api;
        private readonly Func<DownloadJob, Task<Game>> processor;
        private readonly ConcurrentQueue<DownloadJob> pendingJobs = new ConcurrentQueue<DownloadJob>();
        private readonly SemaphoreSlim pendingSignal = new SemaphoreSlim(0);
        private int processorRunning;
        private string summaryText = "No queued downloads.";
        private DownloadJob selectedJob;

        public event EventHandler QueueChanged;

        public ObservableCollection<DownloadJob> Jobs { get; } = new ObservableCollection<DownloadJob>();

        public string SummaryText
        {
            get => summaryText;
            set => SetValue(ref summaryText, value);
        }

        public DownloadJob SelectedJob
        {
            get => selectedJob;
            set
            {
                SetValue(ref selectedJob, value);
                OnPropertyChanged(nameof(CanCancelSelected));
                OnPropertyChanged(nameof(CanRetrySelected));
            }
        }

        public bool CanCancelSelected => SelectedJob != null &&
            (SelectedJob.State == DownloadJobState.Queued ||
             SelectedJob.State == DownloadJobState.Downloading ||
             SelectedJob.State == DownloadJobState.Extracting ||
             SelectedJob.State == DownloadJobState.Importing ||
             SelectedJob.State == DownloadJobState.DownloadingMetadata);

        public bool CanRetrySelected => SelectedJob != null &&
            (SelectedJob.State == DownloadJobState.Failed || SelectedJob.State == DownloadJobState.Canceled);

        public DownloadQueueService(IPlayniteAPI api, Func<DownloadJob, Task<Game>> processor)
        {
            this.api = api;
            this.processor = processor;
        }

        public DownloadJob Enqueue(RemoteGameEntry entry, string sevenZipPath, string installDirectory, string archivePath)
        {
            var job = new DownloadJob(entry, sevenZipPath, installDirectory, archivePath);
            RunOnUi(() =>
            {
                Jobs.Insert(0, job);
                SubscribeToJob(job);
                UpdateSummary();
            });

            pendingJobs.Enqueue(job);
            pendingSignal.Release();
            EnsureProcessor();
            return job;
        }

        public void RefreshSummary()
        {
            RunOnUi(() =>
            {
                UpdateSummary();
                NotifyQueueChanged();
            });
        }

        public DownloadJob GetLatestJobForEntry(RemoteGameEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            return Jobs.FirstOrDefault(job => IsSameEntry(job.Entry, entry));
        }

        public bool CancelSelectedJob()
        {
            return CancelJob(SelectedJob);
        }

        public bool CancelJob(DownloadJob job)
        {
            if (job == null)
            {
                return false;
            }

            if (job.State != DownloadJobState.Queued &&
                job.State != DownloadJobState.Downloading &&
                job.State != DownloadJobState.Extracting &&
                job.State != DownloadJobState.Importing &&
                job.State != DownloadJobState.DownloadingMetadata)
            {
                return false;
            }

            job.CancellationTokenSource.Cancel();
            RunOnUi(() =>
            {
                job.State = DownloadJobState.Canceled;
                job.StatusText = "Canceled";
                UpdateSummary();
                NotifyQueueChanged();
            });
            return true;
        }

        public DownloadJob RetrySelectedJob()
        {
            return RetryJob(SelectedJob);
        }

        public DownloadJob RetryJob(DownloadJob job)
        {
            if (job == null || (job.State != DownloadJobState.Failed && job.State != DownloadJobState.Canceled))
            {
                return null;
            }

            return Enqueue(job.Entry, job.SevenZipPath, job.InstallDirectory, job.ArchivePath);
        }

        public void ClearFinishedJobs()
        {
            RunOnUi(() =>
            {
                var removable = Jobs
                    .Where(job => job.State == DownloadJobState.Completed ||
                                  job.State == DownloadJobState.Failed ||
                                  job.State == DownloadJobState.Canceled)
                    .ToList();

                foreach (var job in removable)
                {
                    UnsubscribeFromJob(job);
                    Jobs.Remove(job);
                }

                if (SelectedJob != null && !Jobs.Contains(SelectedJob))
                {
                    SelectedJob = Jobs.FirstOrDefault();
                }

                UpdateSummary();
                NotifyQueueChanged();
            });
        }

        private void EnsureProcessor()
        {
            if (Interlocked.CompareExchange(ref processorRunning, 1, 0) == 0)
            {
                _ = Task.Run(ProcessQueueAsync);
            }
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                while (true)
                {
                    await pendingSignal.WaitAsync().ConfigureAwait(false);
                    while (pendingJobs.TryDequeue(out var job))
                    {
                        if (job.CancellationTokenSource.IsCancellationRequested || job.State == DownloadJobState.Canceled)
                        {
                            RunOnUi(() =>
                            {
                                job.State = DownloadJobState.Canceled;
                                job.StatusText = "Canceled";
                                UpdateSummary();
                                NotifyQueueChanged();
                            });
                            continue;
                        }

                        try
                        {
                            await processor(job).ConfigureAwait(false);
                            RunOnUi(() =>
                            {
                                job.State = DownloadJobState.Completed;
                                job.StatusText = "Completed";
                                job.ProgressValue = 100;
                                UpdateSummary();
                                NotifyQueueChanged();
                            });

                            api.Notifications.Add(new NotificationMessage(
                                $"localgamedownloader-job-{job.Id}",
                                $"Installed {job.Entry.Name}",
                                NotificationType.Info));
                        }
                        catch (OperationCanceledException)
                        {
                            RunOnUi(() =>
                            {
                                job.State = DownloadJobState.Canceled;
                                job.StatusText = "Canceled";
                                UpdateSummary();
                                NotifyQueueChanged();
                            });
                        }
                        catch (Exception ex)
                        {
                            RunOnUi(() =>
                            {
                                job.State = DownloadJobState.Failed;
                                job.StatusText = ex.Message;
                                job.ErrorMessage = ex.ToString();
                                UpdateSummary();
                                NotifyQueueChanged();
                            });

                            api.Notifications.Add(new NotificationMessage(
                                $"localgamedownloader-job-{job.Id}",
                                $"Install failed for {job.Entry.Name}: {ex.Message}",
                                NotificationType.Error));
                        }
                    }

                    if (pendingJobs.IsEmpty)
                    {
                        break;
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref processorRunning, 0);
                if (!pendingJobs.IsEmpty)
                {
                    EnsureProcessor();
                }
            }
        }

        private void UpdateSummary()
        {
            var queued = Jobs.Count(job => job.State == DownloadJobState.Queued);
            var active = Jobs.Count(job =>
                job.State == DownloadJobState.Downloading ||
                job.State == DownloadJobState.Extracting ||
                job.State == DownloadJobState.Importing ||
                job.State == DownloadJobState.DownloadingMetadata);

            SummaryText = queued == 0 && active == 0
                ? "No queued downloads."
                : $"{active} active, {queued} queued.";

            OnPropertyChanged(nameof(CanCancelSelected));
            OnPropertyChanged(nameof(CanRetrySelected));
        }

        private void SubscribeToJob(DownloadJob job)
        {
            job.PropertyChanged += Job_PropertyChanged;
            NotifyQueueChanged();
        }

        private void UnsubscribeFromJob(DownloadJob job)
        {
            job.PropertyChanged -= Job_PropertyChanged;
        }

        private void Job_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DownloadJob.State) ||
                e.PropertyName == nameof(DownloadJob.StatusText) ||
                e.PropertyName == nameof(DownloadJob.ProgressValue))
            {
                RunOnUi(() =>
                {
                    UpdateSummary();
                    NotifyQueueChanged();
                });
            }
        }

        private static bool IsSameEntry(RemoteGameEntry left, RemoteGameEntry right)
        {
            return left != null && right != null &&
                   string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        private void NotifyQueueChanged()
        {
            QueueChanged?.Invoke(this, EventArgs.Empty);
        }

        private static void RunOnUi(Action action)
        {
            if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                Application.Current.Dispatcher.Invoke(action);
            }
        }
    }
}
