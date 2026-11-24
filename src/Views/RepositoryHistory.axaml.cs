using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SourceFork.Views
{
    public partial class RepositoryHistory : ChromelessWindow
    {
        public RepositoryHistory()
        {
            CloseOnESC = true;
            DataContext = new ViewModels.RepositoryHistory();
            InitializeComponent();
        }

        private void OnExclude(object sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.RepositoryHistoryItem item } &&
                DataContext is ViewModels.RepositoryHistory vm)
                vm.Exclude(item);

            e.Handled = true;
        }

        private async void OnRestore(object sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.RepositoryHistoryItem item } &&
                DataContext is ViewModels.RepositoryHistory vm)
                await vm.RestoreAsync(item);

            e.Handled = true;
        }

        private void OnClose(object _, RoutedEventArgs e)
        {
            Close();
            e.Handled = true;
        }
    }
}
