using System.Windows;
using System.Windows.Controls;

namespace LocalGameDownloader
{
    public partial class CatalogView : UserControl
    {
        public CatalogView()
        {
            InitializeComponent();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CatalogViewModel viewModel)
            {
                await viewModel.LoadAsync();
            }
        }

        private async void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CatalogViewModel viewModel)
            {
                await viewModel.InstallSelectedAsync();
            }
        }

        private void BrowseRootButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CatalogViewModel viewModel)
            {
                viewModel.BrowseInstallRoot();
            }
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CatalogViewModel viewModel)
            {
                viewModel.PlaySelected();
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CatalogViewModel viewModel)
            {
                viewModel.OpenSelectedFolder();
            }
        }

        private void QueueButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CatalogViewModel viewModel)
            {
                viewModel.ShowQueue();
            }
        }

        private void OpenIgdbButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CatalogViewModel viewModel &&
                sender is FrameworkElement element &&
                element.DataContext is CatalogGameEntry game)
            {
                viewModel.OpenIgdb(game);
            }
        }

        private void OpenPcGamingWikiButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CatalogViewModel viewModel &&
                sender is FrameworkElement element &&
                element.DataContext is CatalogGameEntry game)
            {
                viewModel.OpenPcGamingWiki(game);
            }
        }
    }
}
