using System;
using System.IO;
using System.Linq;
using System.Threading;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SourceFork.Views
{
    public partial class Welcome : UserControl
    {
        public Welcome()
        {
            InitializeComponent();
            TreeContainer.AddHandler(PointerPressedEvent, OnTreePointerPressed, RoutingStrategies.Tunnel, true);
        }

        private void OnTreePointerPressed(object sender, PointerPressedEventArgs e)
        {
            var item = FindListBoxItem(e.Source);
            if (item?.DataContext is not ViewModels.RepositoryListRow row)
                return;

            var properties = e.GetCurrentPoint(TreeContainer).Properties;
            if (properties.IsRightButtonPressed)
            {
                // Right click opens the item menu without changing the current selection.
                e.Handled = true;
                return;
            }


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

        private void OnTreeContextRequested(object sender, ContextRequestedEventArgs ev)
        {
            var item = FindListBoxItem(ev.Source);
            if (item?.DataContext is not ViewModels.RepositoryListRow row)
                return;

            if (row.Node is not { } node)
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
                menu.Items.Add(new MenuItem() { Header = "-" });
                menu.Items.Add(delete);
            }
            else
            {
                menu.Items.Add(delete);
            }

            menu.Open(item);
            ev.Handled = true;
        }

        private void OnRowPointerPressed(object sender, PointerPressedEventArgs e)
        {
            _dragStart = null;
            _dragStarted = false;

            if (sender is not Grid { DataContext: ViewModels.RepositoryListRow { Node: { } } })
                return;

            if (e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
                _dragStart = e;
        }

        private void OnRowPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _dragStart = null;
            _dragStarted = false;
        }

        private void OnRowPointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            _dragStart = null;
            _dragStarted = false;
        }

        private async void OnRowPointerMoved(object sender, PointerEventArgs e)
        {
            if (_dragStart == null || _dragStarted)
                return;

            if (sender is not Grid { DataContext: ViewModels.RepositoryListRow { Node: { } node } } grid)
            {
                _dragStart = null;
                return;
            }

            if (TopLevel.GetTopLevel(grid) == null ||
                _dragStart.Source is not Visual pressSource ||
                TopLevel.GetTopLevel(pressSource) == null)
            {
                _dragStart = null;
                return;
            }

            if (!e.GetCurrentPoint(grid).Properties.IsLeftButtonPressed)
            {
                _dragStart = null;
                return;
            }

            var delta = e.GetPosition(grid) - _dragStart.GetPosition(grid);
            if (delta.X * delta.X + delta.Y * delta.Y < 64)
                return;

            _dragStarted = true;

            try
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.Create(_dragData, node.Id));
                await DragDrop.DoDragDropAsync(_dragStart, data, DragDropEffects.Move);
            }
            catch (Exception ex)
            {
                // The source row can be rebuilt while the pointer is still pressed.
                // Do not let a stale drag event crash the application.
                Console.Error.WriteLine($"Welcome drag start failed: {ex}");
            }
            finally
            {
                _dragStart = null;
                _dragStarted = false;
            }
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
                var item = FindListBoxItem(e.Source);
                var target = item?.DataContext is ViewModels.RepositoryListRow { Node.IsRepository: true } row
                    ? row.Node.Id
                    : null;
                var after = false;

                if (target != null &&
                    item != null &&
                    item.TranslatePoint(new Point(0, 0), TreeContainer) is { } top)
                {
                    after = e.GetPosition(TreeContainer).Y > top.Y + item.Bounds.Height / 2;
                }

                var prefs = ViewModels.Preferences.Instance;
                var source = prefs.FindNode(sourceId);
                if (!string.IsNullOrEmpty(target))
                {
                    var targetNode = prefs.FindNode(target);
                    var sourceGroup = source?.PrimaryTag ?? string.Empty;
                    var targetGroup = targetNode?.PrimaryTag ?? string.Empty;
                    if (!string.Equals(sourceGroup, targetGroup, StringComparison.OrdinalIgnoreCase))
                    {
                        _dragStart = null;
                        _dragStarted = false;
                        e.Handled = true;
                        return;
                    }

                    if (after)
                        prefs.MoveRepositoryNodeAfter(sourceId, target);
                    else
                        prefs.MoveRepositoryNodeBefore(sourceId, target);
                }
                else
                {
                    prefs.MoveRepositoryNodeToEnd(sourceId);
                }

                prefs.SetRepositoryGroupItemSort(source?.PrimaryTag, Models.RepositorySortMode.Custom);
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
                            page.Popup = new ViewModels.TrustRepository(page.Node.Id, root, rs.StdErr, safeDirectory, false);
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

        private static ListBoxItem FindListBoxItem(object source)
        {
            if (source is ListBoxItem item)
                return item;

            return (source as Visual)?.FindAncestorOfType<ListBoxItem>();
        }

        private PointerPressedEventArgs _dragStart;
        private bool _dragStarted;
        private readonly DataFormat<string> _dragData = DataFormat.CreateStringApplicationFormat("sourcefork-welcome-repo");
        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
    }
}
