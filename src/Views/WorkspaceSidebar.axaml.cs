using System;
using System.IO;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceFork.Views
{
    public partial class WorkspaceSidebar : UserControl
    {
        public WorkspaceSidebar()
        {
            InitializeComponent();
        }

        private void OnTagFilterMenu(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || DataContext is not ViewModels.Launcher launcher)
                return;

            var menu = new ContextMenu()
            {
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                VerticalOffset = -6,
            };

            var header = new MenuItem()
            {
                Header = App.Text("Sidebar.FilterByTags"),
                IsEnabled = false,
            };
            menu.Items.Add(header);

            var tags = launcher.GetAvailableTags();
            if (tags.Count == 0)
            {
                menu.Items.Add(new MenuItem()
                {
                    Header = App.Text("Sidebar.NoTags"),
                    IsEnabled = false,
                });
            }
            else
            {
                foreach (var tag in tags)
                {
                    var item = new MenuItem();
                    item.Header = $"#{tag}";
                    if (launcher.IsTagFiltered(tag))
                        item.Icon = this.CreateMenuIcon("Icons.Check");
                    item.Click += (_, ev) =>
                    {
                        launcher.ToggleTagFilter(tag);
                        ev.Handled = true;
                    };
                    menu.Items.Add(item);
                }
            }

            if (launcher.HasTagFilters)
            {
                menu.Items.Add(new MenuItem() { Header = "-" });

                var clear = new MenuItem();
                clear.Header = App.Text("Tags.ClearFilter");
                clear.Icon = this.CreateMenuIcon("Icons.Clear");
                clear.Click += (_, ev) =>
                {
                    launcher.ClearTagFilters();
                    ev.Handled = true;
                };
                menu.Items.Add(clear);
            }

            menu.Open(button);
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

                var tags = new MenuItem();
                tags.Header = App.Text("RepositoryTags.Edit");
                tags.Icon = this.CreateMenuIcon("Icons.Tags");
                tags.Click += (_, ev) =>
                {
                    page.Node.EditTags();
                    ev.Handled = true;
                };
                menu.Items.Add(tags);

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
