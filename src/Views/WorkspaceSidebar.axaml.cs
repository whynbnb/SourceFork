using System;
using System.IO;
using System.Linq;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceFork.Views
{
    public partial class WorkspaceSidebar : UserControl
    {
        private PointerPressedEventArgs _dragStart;
        private bool _dragStarted;
        private readonly DataFormat<string> _dragData = DataFormat.CreateStringApplicationFormat("sourcefork-sidebar-repo");

        public WorkspaceSidebar()
        {
            InitializeComponent();
        }

        private void OnSortMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
                RepositorySortMenu.Show(this, button);

            e.Handled = true;
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
            if (sender is Button { DataContext: ViewModels.RepositoryListRow row } &&
                row.Node is { IsRepository: true } node &&
                DataContext is ViewModels.Launcher launcher)
            {
                var index = launcher.VisiblePages.IndexOf(row);
                launcher.RemoveRepositoryFromList(node, index);
            }

            e.Handled = true;
        }

        private void OnRowPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as Avalonia.Visual).Properties.IsLeftButtonPressed)
            {
                _dragStart = e;
                _dragStarted = false;
            }
            else
            {
                _dragStart = null;
                _dragStarted = false;
            }
        }

        private void OnRowPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _dragStart = null;
            _dragStarted = false;
        }

        private async void OnRowPointerMoved(object sender, PointerEventArgs e)
        {
            if (_dragStart == null || _dragStarted)
                return;

            if (sender is not Grid { DataContext: ViewModels.RepositoryListRow { Node.IsRepository: true } row } grid)
                return;

            var delta = e.GetPosition(grid) - _dragStart.GetPosition(grid);
            if (delta.X * delta.X + delta.Y * delta.Y < 64)
                return;

            _dragStarted = true;
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(_dragData, row.Node.Id));
            await DragDrop.DoDragDropAsync(_dragStart, data, DragDropEffects.Move);
        }

        private void OnPagesDragOver(object sender, DragEventArgs e)
        {
            if (e.DataTransfer.Contains(_dragData))
            {
                e.DragEffects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void OnPagesDrop(object sender, DragEventArgs e)
        {
            if (e.DataTransfer.TryGetValue(_dragData) is not { Length: > 0 } sourceId)
                return;

            var target = e.Source is Control { DataContext: ViewModels.RepositoryListRow row } &&
                row.Node is { IsRepository: true }
                    ? row.Node.Id
                    : null;

            if (DataContext is ViewModels.Launcher launcher)
                ViewModels.Preferences.Instance.CaptureRepositoryOrder(launcher.VisiblePages
                    .Where(x => x.Node != null)
                    .Select(x => x.Node.Id));

            if (!string.IsNullOrEmpty(target))
                ViewModels.Preferences.Instance.MoveRepositoryNodeBefore(sourceId, target);
            else
                ViewModels.Preferences.Instance.MoveRepositoryNodeToEnd(sourceId);

            ViewModels.Preferences.Instance.RepositorySort = Models.RepositorySortMode.Custom;
            ViewModels.RepositoryListSorter.NotifyChanged();

            _dragStart = null;
            _dragStarted = false;
            e.Handled = true;
        }

        private void OnItemContextRequested(object sender, ContextRequestedEventArgs e)
        {
            if (sender is not Control { DataContext: ViewModels.RepositoryListRow row } control ||
                DataContext is not ViewModels.Launcher launcher ||
                row.Node is not { } node)
                return;

            var menu = new ContextMenu();
            var page = row.Page;

            if (node.IsRepository)
            {
                if (page?.Data is ViewModels.Repository repo)
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
                }
                else
                {
                    var open = new MenuItem();
                    open.Header = App.Text("Open");
                    open.Icon = this.CreateMenuIcon("Icons.Repositories");
                    open.Click += (_, ev) =>
                    {
                        node.Open();
                        ev.Handled = true;
                    };
                    menu.Items.Add(open);
                }

                var tags = new MenuItem();
                tags.Header = App.Text("RepositoryTags.Edit");
                tags.Icon = this.CreateMenuIcon("Icons.Tags");
                tags.Click += (_, ev) =>
                {
                    node.EditTags();
                    ev.Handled = true;
                };
                menu.Items.Add(tags);

                var edit = new MenuItem();
                edit.Header = App.Text("PageTabBar.Tab.Edit");
                edit.Icon = this.CreateMenuIcon("Icons.Edit");
                edit.Click += (_, ev) =>
                {
                    node.Edit();
                    ev.Handled = true;
                };
                menu.Items.Add(edit);

                menu.Items.Add(new MenuItem() { Header = "-" });

                var remove = new MenuItem();
                remove.Header = App.Text("Repository.RemoveFromList");
                remove.Icon = this.CreateMenuIcon("Icons.Minus");
                remove.Click += (_, ev) =>
                {
                    node.Delete();
                    ev.Handled = true;
                };
                menu.Items.Add(remove);
            }

            if (page != null && (node.IsRepository || launcher.Pages.Count > 1))
            {
                if (menu.Items.Count > 0)
                    menu.Items.Add(new MenuItem() { Header = "-" });

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

            if (page != null && launcher.Pages.Count > 1)
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
