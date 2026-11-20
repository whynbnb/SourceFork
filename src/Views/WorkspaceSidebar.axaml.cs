using System;
using System.IO;

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceGit.Views
{
    public partial class WorkspaceSidebar : UserControl
    {
        public WorkspaceSidebar()
        {
            InitializeComponent();
        }

        private void OnWorkspaceMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
                this.FindAncestorOfType<Launcher>()?.ShowWorkspaceMenu(button);

            e.Handled = true;
        }

        private void OnAddPage(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher launcher)
                launcher.AddNewTab();

            e.Handled = true;
        }

        private void OnClosePage(object sender, RoutedEventArgs e)
        {
            if (sender is Button { DataContext: ViewModels.LauncherPage page } &&
                DataContext is ViewModels.Launcher launcher)
                launcher.CloseTab(page);

            e.Handled = true;
        }

        private void OnItemContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is not Control { DataContext: ViewModels.LauncherPage page } control ||
                DataContext is not ViewModels.Launcher launcher)
                return;

            var menu = new ContextMenu();

            if (page.Data is ViewModels.Repository repo)
            {
                var refresh = new MenuItem();
                refresh.Header = App.Text("PageTabBar.Tab.Refresh");
                refresh.Icon = this.CreateMenuIcon("Icons.Loading");
                refresh.Click += (_, ev) =>
                {
                    repo.RefreshAll();
                    ev.Handled = true;
                };
                menu.Items.Add(refresh);

                var copyPath = new MenuItem();
                copyPath.Header = App.Text("PageTabBar.Tab.CopyPath");
                copyPath.Icon = this.CreateMenuIcon("Icons.Copy");
                copyPath.Click += async (_, ev) =>
                {
                    var dir = new DirectoryInfo(repo.FullPath);
                    await this.CopyTextAsync(dir.FullName);
                    ev.Handled = true;
                };
                menu.Items.Add(copyPath);

                var edit = new MenuItem();
                edit.Header = App.Text("PageTabBar.Tab.Edit");
                edit.Icon = this.CreateMenuIcon("Icons.Edit");
                edit.Click += (_, ev) =>
                {
                    page.Node.Edit();
                    ev.Handled = true;
                };
                menu.Items.Add(edit);

                menu.Items.Add(new MenuItem() { Header = "-" });
            }

            if (page.Node.IsRepository || launcher.Pages.Count > 1)
            {
                var close = new MenuItem();
                close.Header = App.Text("PageTabBar.Tab.Close");
                close.Tag = OperatingSystem.IsMacOS() ? "⌘+W" : "Ctrl+W";
                close.Click += (_, ev) =>
                {
                    launcher.CloseTab(page);
                    ev.Handled = true;
                };
                menu.Items.Add(close);
            }

            if (launcher.Pages.Count > 1)
            {
                var closeOthers = new MenuItem();
                closeOthers.Header = App.Text("PageTabBar.Tab.CloseOther");
                closeOthers.Click += (_, ev) =>
                {
                    if (launcher.ActivePage != page)
                        launcher.ActivePage = page;

                    launcher.CloseOtherTabs();
                    ev.Handled = true;
                };
                menu.Items.Add(closeOthers);
            }

            menu.Open(control);
            e.Handled = true;
        }
    }
}
