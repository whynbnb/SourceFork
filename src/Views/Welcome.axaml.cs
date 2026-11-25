using System;
using System.IO;
using System.Linq;
using System.Threading;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SourceFork.Views
{
    public partial class Welcome : UserControl
    {
        public Welcome()
        {
            InitializeComponent();
        }

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            SearchBox.Focus(NavigationMethod.Directional);
            await ViewModels.Welcome.Instance.UpdateStatusAsync(false, _cancellation.Token);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            _cancellation.Cancel();
            base.OnUnloaded(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (!e.Handled && e.KeyModifiers == KeyModifiers.None)
            {
                if (e.Key == Key.Down && ViewModels.Welcome.Instance.Rows.Count > 0)
                {
                    TreeContainer.SelectedIndex = 0;
                    TreeContainer.Focus(NavigationMethod.Directional);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    var page = this.FindAncestorOfType<LauncherPage>();
                    if (page is { DataContext: ViewModels.LauncherPage ctx } && ctx.Popup == null)
                        OnClearSearchFilter(this, e);
                }
                else if (e.Key == Key.Enter)
                {
                    if (TreeContainer.SelectedItem is ViewModels.RepositoryListRow { Node: { } node })
                    {
                        node.Open();
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Delete || e.Key == Key.Back)
                {
                    if (TreeContainer.SelectedItem is ViewModels.RepositoryListRow { Node: { } node })
                    {
                        node.Delete();
                        e.Handled = true;
                    }
                }
            }
        }

        private void OnSearchHotKey(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }

        private void OnClearSearchFilter(object sender, RoutedEventArgs e)
        {
            ViewModels.Welcome.Instance.ClearSearchFilter();
            SearchBox.Focus(NavigationMethod.Directional);
            e.Handled = true;
        }

        private void OnSortMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
                RepositorySortMenu.Show(this, button);

            e.Handled = true;
        }

        private void OnTagFilterMenu(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            var welcome = ViewModels.Welcome.Instance;
            var menu = new ContextMenu()
            {
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                VerticalOffset = -6,
            };

            menu.Items.Add(new MenuItem()
            {
                Header = App.Text("Sidebar.FilterByTags"),
                IsEnabled = false,
            });

            var tags = welcome.GetAvailableTags();
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
                    if (welcome.IsTagFiltered(tag))
                        item.Icon = this.CreateMenuIcon("Icons.Check");
                    item.Click += (_, ev) =>
                    {
                        welcome.ToggleTagFilter(tag);
                        ev.Handled = true;
                    };
                    menu.Items.Add(item);
                }
            }

            if (welcome.HasTagFilters)
            {
                menu.Items.Add(new MenuItem() { Header = "-" });

                var clear = new MenuItem();
                clear.Header = App.Text("Tags.ClearFilter");
                clear.Icon = this.CreateMenuIcon("Icons.Clear");
                clear.Click += (_, ev) =>
                {
                    welcome.ClearTagFilters();
                    ev.Handled = true;
                };
                menu.Items.Add(clear);
            }

            menu.Open(button);
            e.Handled = true;
        }

        private void OnTreeNodeContextRequested(object sender, ContextRequestedEventArgs ev)
        {
            if (sender is not Grid { DataContext: ViewModels.RepositoryListRow { Node: { } node } } grid)
                return;

            var menu = new ContextMenu();

            var tags = new MenuItem();
            tags.Header = App.Text("RepositoryTags.Edit");
            tags.Icon = this.CreateMenuIcon("Icons.Tags");
            tags.Click += async (_, e) =>
            {
                await this.ShowDialogAsync(node.CreateTagEditor());
                e.Handled = true;
            };

            var edit = new MenuItem();
            edit.Header = App.Text("Welcome.Edit");
            edit.Icon = this.CreateMenuIcon("Icons.Edit");
            edit.Click += (_, e) =>
            {
                node.Edit();
                e.Handled = true;
            };

            var delete = new MenuItem();
            delete.Header = App.Text("Welcome.Delete");
            delete.Icon = this.CreateMenuIcon("Icons.Clear");
            delete.Click += (_, e) =>
            {
                node.Delete();
                e.Handled = true;
            };

            if (Directory.Exists(node.Id))
            {
                var open = new MenuItem();
                open.Header = App.Text("Welcome.OpenOrInit");
                open.Icon = this.CreateMenuIcon("Icons.Folder.Open");
                open.Click += (_, e) =>
                {
                    node.Open();
                    e.Handled = true;
                };

                var explore = new MenuItem();
                explore.Header = App.Text("Repository.Explore");
                explore.Icon = this.CreateMenuIcon("Icons.Explore");
                explore.Click += (_, e) =>
                {
                    node.OpenInFileManager();
                    e.Handled = true;
                };

                var terminal = new MenuItem();
                terminal.Header = App.Text("Repository.Terminal");
                terminal.Icon = this.CreateMenuIcon("Icons.Terminal");
                terminal.Click += (_, e) =>
                {
                    node.OpenTerminal();
                    e.Handled = true;
                };

                menu.Items.Add(open);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(explore);
                menu.Items.Add(terminal);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(tags);
                menu.Items.Add(edit);
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(delete);
            }
            else
            {
                menu.Items.Add(delete);
            }

            menu.Open(grid);
            ev.Handled = true;
        }

        private void OnRowPointerPressed(object sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
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

            if (sender is not Grid { DataContext: ViewModels.RepositoryListRow { Node: { } node } } grid)
                return;

            var delta = e.GetPosition(grid) - _dragStart.GetPosition(grid);
            if (delta.X * delta.X + delta.Y * delta.Y < 64)
                return;

            _dragStarted = true;
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(_dragData, node.Id));
            await DragDrop.DoDragDropAsync(_dragStart, data, DragDropEffects.Move);
        }

        private void OnTreeViewLostFocus(object _1, RoutedEventArgs _2)
        {
            _dragStart = null;
            _dragStarted = false;
        }

        private void DragOverTreeView(object sender, DragEventArgs e)
        {
            if (e.DataTransfer.Contains(_dragData))
            {
                e.DragEffects = DragDropEffects.Move;
                e.Handled = true;
            }
            else if (e.DataTransfer.Contains(DataFormat.File))
            {
                e.DragEffects = DragDropEffects.Copy;
                e.Handled = true;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        private async void DropOnTreeView(object sender, DragEventArgs e)
        {
            if (e.DataTransfer.TryGetValue(_dragData) is { Length: > 0 } sourceId)
            {
                var target = e.Source is Control { DataContext: ViewModels.RepositoryListRow { Node.IsRepository: true } row }
                    ? row.Node.Id
                    : null;

                ViewModels.Preferences.Instance.CaptureRepositoryOrder(ViewModels.Welcome.Instance.Rows
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
                return;
            }

            if (!e.DataTransfer.Contains(DataFormat.File))
                return;

            e.Handled = true;

            var items = e.DataTransfer.TryGetFiles() ?? [];
            var refresh = false;

            foreach (var item in items)
            {
                var root = item.Path.LocalPath;
                if (!Directory.Exists(root))
                {
                    if (File.Exists(root))
                        root = Path.GetDirectoryName(root);
                    else
                        continue;
                }

                var rs = await ViewModels.Welcome.Instance.GetRepositoryRootAsync(root);
                if (!rs.IsSuccess)
                {
                    if (Models.SafeDirectories.IsUntrustedRepository(rs.StdErr) &&
                        Models.SafeDirectories.TryGetSafeDirectoryValue(root, rs.StdErr, out var safeDirectory))
                    {
                        var pageView = this.FindAncestorOfType<LauncherPage>();
                        if (pageView is { DataContext: ViewModels.LauncherPage page })
                        {
                            page.Popup = new ViewModels.TrustRepository(page.Node.Id, root, rs.StdErr, safeDirectory, false, 0);
                            break; // Stop processing further because SourceFork only supports one popup at a time.
                        }
                    }
                }
                else
                {
                    await ViewModels.Welcome.Instance.AddRepositoryAsync(rs.StdOut.Trim(), false);
                    refresh = true;
                }
            }

            if (refresh)
                ViewModels.RepositoryListSorter.NotifyChanged();
        }

        private void OnDoubleTappedTreeNode(object sender, TappedEventArgs e)
        {
            if (sender is Grid { DataContext: ViewModels.RepositoryListRow { Node: { } node } })
            {
                node.Open();
                e.Handled = true;
            }
        }

        private PointerPressedEventArgs _dragStart;
        private bool _dragStarted;
        private readonly DataFormat<string> _dragData = DataFormat.CreateStringApplicationFormat("sourcefork-welcome-repo");
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
    }
}
