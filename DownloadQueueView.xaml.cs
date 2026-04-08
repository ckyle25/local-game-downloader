using System.Windows.Controls;
using System.Windows;

namespace LocalGameDownloader
{
    public partial class DownloadQueueView : UserControl
    {
        public DownloadQueueView()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is DownloadQueueService queue)
            {
                queue.CancelSelectedJob();
            }
        }

        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is DownloadQueueService queue)
            {
                queue.RetrySelectedJob();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is DownloadQueueService queue)
            {
                queue.ClearFinishedJobs();
            }
        }
    }
}
