using System.Windows;
using System.Windows.Controls;

namespace LocalGameDownloader
{
    public partial class LocalGameDownloaderSettingsView : UserControl
    {
        public LocalGameDownloaderSettingsView()
        {
            InitializeComponent();
        }

        private void BrowseInstallRootButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LocalGameDownloaderSettingsViewModel viewModel)
            {
                viewModel.BrowseInstallRoot();
            }
        }

        private void BrowseManifestButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LocalGameDownloaderSettingsViewModel viewModel)
            {
                viewModel.BrowseManifestFile();
            }
        }

        private void ClearManifestButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LocalGameDownloaderSettingsViewModel viewModel)
            {
                viewModel.ClearManifestLocation();
            }
        }

        private void ClearInstallRootButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LocalGameDownloaderSettingsViewModel viewModel)
            {
                viewModel.ClearInstallRoot();
            }
        }

        private void AutoDetectSevenZipButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LocalGameDownloaderSettingsViewModel viewModel && !viewModel.AutoDetectSevenZip())
            {
                MessageBox.Show("7-Zip could not be auto-detected. Use Browse to pick 7z.exe manually.", "Local Game Downloader");
            }
        }

        private void BrowseSevenZipButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is LocalGameDownloaderSettingsViewModel viewModel)
            {
                viewModel.BrowseSevenZip();
            }
        }
    }
}
