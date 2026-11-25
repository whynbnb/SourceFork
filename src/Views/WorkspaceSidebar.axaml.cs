using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace SourceFork.Views
{
    public partial class WorkspaceSidebar : UserControl
    {
        private PointerPressedEventArgs _dragStart;
        private bool _dragStarted;
        private string _dragSourceId;
        private string _dropTargetId;
        private bool _dropAfterTarget;
        private bool _canDrop;
        private readonly DataFormat<string> _dragData = DataFormat.CreateStringApplicationFormat("sourcefork-sidebar-repo");

        public WorkspaceSidebar()
        {
            InitializeComponent();
            PagesList.AddHandler(PointerPressedEvent, OnPagesPointerPressed, RoutingStrategies.Tunnel, true);
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
                launcher.ShowWelcomePage();

            e.Handled = true;
        }

        private async Task CloseRowAsync(ViewModels.RepositoryListRow row)
        {
            if (row.Node is not { IsRepository: true } node ||
                DataContext is not ViewModels.Launcher launcher)
                return;

            if (node.HasTags && TopLevel.GetTopLevel(this) is Window owner)
            {
                var confirm = new Confirm();
                confirm.SetData(App.Text("RepositoryHistory.ExcludeTaggedConfirm", node.Name), Models.ConfirmButtonType.OkCancel);
                if (!await confirm.ShowDialog<bool>(owner))
                    return;
            }

            var index = launcher.VisiblePages.IndexOf(row);
            launcher.RemoveRepositoryFromList(node, index);
        }

        private void OnPagesPointerPressed(object sender, PointerPressedEventArgs e)
        {
            var item = FindListBoxItem(e.Source);
            if (item?.DataContext is not ViewModels.RepositoryListRow row)
                return;

            var properties = e.GetCurrentPoint(PagesList).Properties;
            if (properties.IsRightButtonPressed)
            {
                // Right click opens the row menu without changing the current selection.
                e.Handled = true;
                return;
            }

            if (!properties.IsLeftButtonPressed)
                return;

            if (FindCloseButton(e.Source) != null)
            {
                // Handle the close button before the list item can select or open the row.
                e.Handled = true;
                _ = CloseRowAsync(row);
                return;
            }

            if (row is ViewModels.RepositoryGroupRow group)
            {
                e.Handled = true;
                Dispatcher.UIThread.Post(() => ToggleGroup(group), DispatcherPriority.Background);
            }
        }

        private void OnRowPointerPressed(object sender, PointerPressedEventArgs e)
        {
            ResetDrag();

            if (sender is not Grid { DataContext: ViewModels.RepositoryListRow { Node.IsRepository: true } })
                return;

            if (!e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
                return;

            _dragStart = e;
        }

        private void OnRowPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            ResetDrag();
        }

        private void OnRowPointerCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            ResetDrag();
        }

        private async void OnRowPointerMoved(object sender, PointerEventArgs e)
        {
            if (_dragStart == null || _dragStarted)
                return;

            if (sender is not Grid { DataContext: ViewModels.RepositoryListRow { Node.IsRepository: true } row } grid)
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
            _dragSourceId = row.Node.Id;

            try
            {
                var data = new DataTransfer();
                data.Add(DataTransferItem.Create(_dragData, row.Node.Id));
                await DragDrop.DoDragDropAsync(_dragStart, data, DragDropEffects.Move);
            }
            catch (Exception ex)
            {
                // The source row can be rebuilt while the pointer is still pressed.
                // Do not let a stale drag event crash the application.
                Console.Error.WriteLine($"Sidebar drag start failed: {ex}");
            }
            finally
            {
                ResetDrag();
            }
        }

        private void OnPagesDragOver(object sender, DragEventArgs e)
        {
            if (!e.DataTransfer.Contains(_dragData) || !UpdateDropIndicator(e))
            {
                HideDropIndicator();
                e.DragEffects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void OnPagesDragLeave(object sender, DragEventArgs e)
        {
            HideDropIndicator();
            e.Handled = true;
        }

        private void OnPagesDrop(object sender, DragEventArgs e)
        {
            if (e.DataTransfer.TryGetValue(_dragData) is not { Length: > 0 } sourceId || !_canDrop)
            {
                ResetDrag();
                return;
            }

            var prefs = ViewModels.Preferences.Instance;
            var targetId = _dropTargetId;
            var afterTarget = _dropAfterTarget;

            if (string.IsNullOrEmpty(targetId))
                prefs.MoveRepositoryNodeToEnd(sourceId);
            else if (afterTarget)
                prefs.MoveRepositoryNodeAfter(sourceId, targetId);
            else
                prefs.MoveRepositoryNodeBefore(sourceId, targetId);

            prefs.RepositorySort = Models.RepositorySortMode.Custom;
            ViewModels.RepositoryListSorter.NotifyChanged();

            ResetDrag();
            e.Handled = true;
        }

        private bool UpdateDropIndicator(DragEventArgs e)
        {
            if (DataContext is not ViewModels.Launcher launcher)
                return false;

            var prefs = ViewModels.Preferences.Instance;
            var item = FindListBoxItem(e.Source);
            var pointer = e.GetPosition(DropIndicatorLayer);
            var source = string.IsNullOrEmpty(_dragSourceId) ? null : prefs.FindNode(_dragSourceId);

            if (item?.DataContext is ViewModels.RepositoryGroupRow group)
            {
                if (prefs.RepositoryGroupByPrimaryTag && !IsSameGroup(source, group.GroupKey))
                {
                    HideDropIndicator();
                    return false;
                }

                var top = item.TranslatePoint(new Point(0, 0), DropIndicatorLayer)?.Y;
                var target = FindFirstRepositoryInGroup(launcher.VisiblePages, group);
                if (top == null || target == null)
                {
                    HideDropIndicator();
                    return false;
                }

                ShowDropIndicator(top.Value + item.Bounds.Height, target, false);
                return true;
            }

            if (item?.DataContext is ViewModels.RepositoryListRow row && row.Node is { IsRepository: true } node)
            {
                if (prefs.RepositoryGroupByPrimaryTag && !IsSameGroup(source, node))
                {
                    HideDropIndicator();
                    return false;
                }

                var top = item.TranslatePoint(new Point(0, 0), DropIndicatorLayer)?.Y;
                if (top == null)
                {
                    HideDropIndicator();
                    return false;
                }

                var after = pointer.Y > top.Value + item.Bounds.Height / 2;
                ShowDropIndicator(after ? top.Value + item.Bounds.Height : top.Value, node.Id, after);
                return true;
            }

            if (prefs.RepositoryGroupByPrimaryTag)
            {
                var last = launcher.VisiblePages.LastOrDefault(x => x.Node is { IsRepository: true });
                if (last?.Node == null || !IsSameGroup(source, last.Node))
                {
                    HideDropIndicator();
                    return false;
                }
            }

            ShowDropIndicator(DropIndicatorLayer.Bounds.Height, null, false);
            return true;
        }

        private static string FindFirstRepositoryInGroup(System.Collections.Generic.IList<ViewModels.RepositoryListRow> rows, ViewModels.RepositoryGroupRow group)
        {
            var index = rows.IndexOf(group);
            for (var i = index + 1; i < rows.Count; i++)
            {
                if (rows[i] is ViewModels.RepositoryGroupRow)
                    break;

                if (rows[i].Node is { IsRepository: true } node)
                    return node.Id;
            }

            return null;
        }

        private static bool IsSameGroup(ViewModels.RepositoryNode source, string groupKey)
        {
            if (source == null)
                return true;

            return string.Equals(source.PrimaryTag ?? string.Empty, groupKey ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameGroup(ViewModels.RepositoryNode source, ViewModels.RepositoryNode target)
        {
            if (source == null || target == null)
                return true;

            return string.Equals(source.PrimaryTag ?? string.Empty, target.PrimaryTag ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private void ShowDropIndicator(double y, string targetId, bool afterTarget)
        {
            var height = DropIndicatorLayer.Bounds.Height;
            var top = Math.Clamp(y - 1, 0, Math.Max(0, height - 2));

            DropIndicator.Width = DropIndicatorLayer.Bounds.Width;
            Canvas.SetLeft(DropIndicator, 0);
            Canvas.SetTop(DropIndicator, top);
            DropIndicator.IsVisible = true;

            _dropTargetId = targetId;
            _dropAfterTarget = afterTarget;
            _canDrop = true;
        }

        private void HideDropIndicator()
        {
            DropIndicator.IsVisible = false;
            _dropTargetId = null;
            _dropAfterTarget = false;
            _canDrop = false;
        }

        private void ResetDrag()
        {
            _dragStart = null;
            _dragStarted = false;
            _dragSourceId = null;
            HideDropIndicator();
        }

        private void ToggleGroup(ViewModels.RepositoryGroupRow group)
        {
            ViewModels.Preferences.Instance.ToggleRepositoryGroupCollapsed(group.GroupKey);
            ViewModels.RepositoryListSorter.NotifyChanged();
        }

        private static Button FindCloseButton(object source)
        {
            if (source is Button self && self.Classes.Contains("page_close"))
                return self;

            var button = (source as Visual)?.FindAncestorOfType<Button>();
            return button != null && button.Classes.Contains("page_close") ? button : null;
        }

        private static ListBoxItem FindListBoxItem(object source)
        {
            if (source is ListBoxItem item)
                return item;

            return (source as Visual)?.FindAncestorOfType<ListBoxItem>();
        }

        private void OnPagesContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var item = FindListBoxItem(e.Source);
            if (item?.DataContext is not ViewModels.RepositoryListRow row)
                return;

            if (row is ViewModels.RepositoryGroupRow group)
            {
                var groupMenu = new ContextMenu();
                var toggle = new MenuItem();
                toggle.Header = App.Text(group.IsCollapsed ? "RepositorySort.ExpandGroup" : "RepositorySort.CollapseGroup");
                toggle.Click += (_, ev) =>
                {
                    ToggleGroup(group);
                    ev.Handled = true;
                };
                groupMenu.Items.Add(toggle);
                groupMenu.Open(item);
                e.Handled = true;
                return;
            }

            if (row.Node is not { } node || DataContext is not ViewModels.Launcher launcher)
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
                tags.Click += async (_, ev) =>
                {
                    await this.ShowDialogAsync(node.CreateTagEditor());
                    ev.Handled = true;
                };
                menu.Items.Add(tags);

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

            menu.Open(item);
            e.Handled = true;
        }
    }
}
