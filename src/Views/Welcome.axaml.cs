using System;
using System.IO;
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
                    if (TreeContainer.SelectedItem is ViewModels.RepositoryNode node)
                    {
                        node.Open();
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.Delete || e.Key == Key.Back)
                {
                    if (TreeContainer.SelectedItem is ViewModels.RepositoryNode node)
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
            if (sender is not Grid { DataContext: ViewModels.RepositoryNode node } grid)
                return;

            var menu = new ContextMenu();

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

        private void OnTreeViewLostFocus(object _1, RoutedEventArgs _2)
        {
        }

        private void DragOverTreeView(object sender, DragEventArgs e)
        {
            if (e.DataTransfer.Contains(DataFormat.File))
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
                ViewModels.Welcome.Instance.Refresh();
        }

        private void OnDoubleTappedTreeNode(object sender, TappedEventArgs e)
        {
            if (sender is Grid { DataContext: ViewModels.RepositoryNode node })
            {
                node.Open();
                e.Handled = true;
            }
        }

        private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
    }
}
