using System;

using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

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
            {
                // Defer the collection rebuild until after this click has been fully handled.
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        vm.Exclude(item);
                    }
                    catch (Exception ex)
                    {
                        Models.Notification.Send(null, ex.Message, true);
                    }
                }, DispatcherPriority.Background);
            }

            e.Handled = true;
        }

        private void OnRestore(object sender, RoutedEventArgs e)
        {
            if (sender is Control { DataContext: ViewModels.RepositoryHistoryItem item } &&
                DataContext is ViewModels.RepositoryHistory vm)
            {
                Dispatcher.UIThread.Post(async () =>
                {
                    try
                    {
                        await vm.RestoreAsync(item);
                    }
                    catch (Exception ex)
                    {
                        Models.Notification.Send(null, ex.Message, true);
                    }
                }, DispatcherPriority.Background);
            }

            e.Handled = true;
        }

        private async void OnExcludeAll(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.RepositoryHistory vm && vm.Repositories.Count > 0)
            {
                var confirm = new Confirm();
                confirm.SetData(App.Text("RepositoryHistory.ConfirmExcludeAll", vm.Repositories.Count), Models.ConfirmButtonType.OkCancel);
                if (await confirm.ShowDialog<bool>(this))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        try
                        {
                            vm.ExcludeAll();
                        }
                        catch (Exception ex)
                        {
                            Models.Notification.Send(null, ex.Message, true);
                        }
                    }, DispatcherPriority.Background);
                }
            }

            e.Handled = true;
        }

        private void OnClose(object _, RoutedEventArgs e)
        {
            Close();
            e.Handled = true;
        }
    }
}
