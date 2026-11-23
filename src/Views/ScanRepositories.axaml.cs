using System;
using System.Collections.Generic;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace SourceFork.Views
{
    public partial class ScanRepositories : UserControl
    {
        public ScanRepositories()
        {
            InitializeComponent();
            DataContext = ViewModels.ScanRepositories.Instance;
        }

        private async void OnAddFolder(object _, RoutedEventArgs e)
        {
            var provider = TopLevel.GetTopLevel(this)?.StorageProvider;
            if (provider == null)
                return;

            if (DataContext is not ViewModels.ScanRepositories vm)
                return;

            var options = new FolderPickerOpenOptions() { AllowMultiple = true };
            try
            {
                var selected = await provider.OpenFolderPickerAsync(options);
                var paths = new List<string>();
                foreach (var folder in selected)
                {
                    var folderPath = folder is { Path: { IsAbsoluteUri: true } path } ? path.LocalPath : folder?.Path.ToString();
                    if (!string.IsNullOrEmpty(folderPath))
                        paths.Add(folderPath);
                }

                vm.AddFolders(paths);
            }
            catch (Exception ex)
            {
                Models.Notification.Send(null, $"Failed to select scanning directory: {ex.Message}", true);
            }

            e.Handled = true;
        }

        private void OnRemoveFolder(object sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.ScanFolderItem item } &&
                DataContext is ViewModels.ScanRepositories vm)
                vm.RemoveFolder(item);

            e.Handled = true;
        }

        private async void OnScanFolder(object sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.ScanFolderItem item } &&
                DataContext is ViewModels.ScanRepositories vm)
                await vm.ScanNowAsync(item);

            e.Handled = true;
        }

        private async void OnScanAll(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.ScanRepositories vm)
                await vm.ScanAllNowAsync();

            e.Handled = true;
        }
    }
}
