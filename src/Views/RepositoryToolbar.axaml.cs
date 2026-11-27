using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace SourceFork.Views
{
    public partial class RepositoryToolbar : UserControl
    {
        private readonly List<ViewModels.RepositoryToolbarAction> _allActions = [];
        private readonly List<ViewModels.RepositoryToolbarAction> _leftMoreActions = [];
        private readonly List<ViewModels.RepositoryToolbarAction> _middleMoreActions = [];
        private readonly List<ViewModels.RepositoryToolbarAction> _rightMoreActions = [];
        private ViewModels.Repository _repo;

        public RepositoryToolbar()
        {
            InitializeComponent();
        }

        protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            ViewModels.Preferences.Instance.RepositoryToolbarChanged += OnToolbarSettingsChanged;
            BuildActions();
        }

        protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
        {
            ViewModels.Preferences.Instance.RepositoryToolbarChanged -= OnToolbarSettingsChanged;

            if (_repo != null)
            {
                _repo.PropertyChanged -= OnRepositoryPropertyChanged;
                _repo = null;
            }

            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            if (_repo != null)
                _repo.PropertyChanged -= OnRepositoryPropertyChanged;

            _repo = DataContext as ViewModels.Repository;
            if (_repo != null)
                _repo.PropertyChanged += OnRepositoryPropertyChanged;

            BuildActions();
        }

        private void OnToolbarSettingsChanged(object sender, EventArgs e)
        {
            BuildActions();
        }

        private void OnRepositoryPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(ViewModels.Repository.CurrentBranch) or
                nameof(ViewModels.Repository.FocusOnCurrentBranch) or
                nameof(ViewModels.Repository.IsBare))
            {
                UpdateActionStates();
            }
        }

        private void BuildActions()
        {
            var left = new List<ViewModels.RepositoryToolbarAction>();
            var middle = new List<ViewModels.RepositoryToolbarAction>();
            var right = new List<ViewModels.RepositoryToolbarAction>();
            _allActions.Clear();
            _leftMoreActions.Clear();
            _middleMoreActions.Clear();
            _rightMoreActions.Clear();

            if (DataContext is ViewModels.Repository)
            {
                foreach (var info in Models.RepositoryToolbarCatalog.Actions)
                {
                    var zone = ViewModels.Preferences.Instance.GetRepositoryToolbarZone(info.Id);
                    if (zone == Models.RepositoryToolbarZones.Hidden)
                        continue;

                    var action = new ViewModels.RepositoryToolbarAction(info);
                    _allActions.Add(action);

                    switch (zone)
                    {
                        case Models.RepositoryToolbarZones.Left:
                            left.Add(action);
                            break;
                        case Models.RepositoryToolbarZones.Middle:
                            middle.Add(action);
                            break;
                        case Models.RepositoryToolbarZones.Right:
                            right.Add(action);
                            break;
                        case Models.RepositoryToolbarZones.LeftMore:
                            _leftMoreActions.Add(action);
                            break;
                        case Models.RepositoryToolbarZones.RightMore:
                            _rightMoreActions.Add(action);
                            break;
                        default:
                            _middleMoreActions.Add(action);
                            break;
                    }
                }
            }

            LeftActions.ItemsSource = left;
            MiddleActions.ItemsSource = middle;
            RightActions.ItemsSource = right;
            LeftMoreButton.IsVisible = _leftMoreActions.Count > 0;
            MoreButton.IsVisible = _middleMoreActions.Count > 0;
            RightMoreButton.IsVisible = _rightMoreActions.Count > 0;

            UpdateActionStates();
        }

        private void UpdateActionStates()
        {
            var repo = DataContext as ViewModels.Repository;
            foreach (var action in _allActions)
            {
                action.IsVisible = repo is { IsBare: true } &&
                    action.Id is "pull" or "stash" or "applyPatch" or "gitflow" or "lfs" or "bisect"
                        ? false
                        : true;

                action.ShowBadge = action.Id switch
                {
                    "pull" => repo?.CurrentBranch?.Behind.Count > 0,
                    "push" => repo?.CurrentBranch?.Ahead.Count > 0,
                    _ => false,
                };

                action.IsToggled = repo?.FocusOnCurrentBranch == true;
            }
        }

        private async void OnToolbarActionTapped(object sender, TappedEventArgs e)
        {
            if (sender is not Button { DataContext: ViewModels.RepositoryToolbarAction action } button)
                return;

            e.Handled = true;
            await ExecuteActionAsync(action, button, e.KeyModifiers);
        }

        private void OnToggleToolbarAction(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton { DataContext: ViewModels.RepositoryToolbarAction action } toggle &&
                DataContext is ViewModels.Repository repo)
            {
                repo.FocusOnCurrentBranch = toggle.IsChecked == true;
                action.IsToggled = repo.FocusOnCurrentBranch;
            }

            e.Handled = true;
        }

        private void OnOpenMoreActions(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            var actions = (button.Tag as string) switch
            {
                "LeftMore" => _leftMoreActions,
                "RightMore" => _rightMoreActions,
                _ => _middleMoreActions,
            };

            var menu = new ContextMenu();
            menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

            foreach (var action in actions)
            {
                var item = new MenuItem();
                item.Header = action.Tooltip;
                item.Icon = this.CreateMenuIcon(action.IconKey);
                item.Click += async (_, ev) =>
                {
                    await ExecuteActionAsync(action, button, KeyModifiers.None);
                    ev.Handled = true;
                };
                menu.Items.Add(item);
            }

            if (menu.Items.Count > 0)
                menu.Open(button);

            e.Handled = true;
        }

        private async Task ExecuteActionAsync(ViewModels.RepositoryToolbarAction action,
            Control anchor, KeyModifiers modifiers)
        {
            if (DataContext is not ViewModels.Repository repo)
                return;

            var showOptions = modifiers.HasFlag(KeyModifiers.Control);
            switch (action.Id)
            {
                case "explore":
                    Native.OS.OpenInFileManager(repo.FullPath);
                    break;
                case "terminal":
                    Native.OS.OpenTerminal(repo.FullPath);
                    break;
                case "fetch":
                    await FetchCoreAsync(showOptions);
                    break;
                case "pull":
                    await PullCoreAsync(showOptions);
                    break;
                case "push":
                    await PushCoreAsync(showOptions);
                    break;
                case "stash":
                    await StashCoreAsync(showOptions);
                    break;
                case "applyPatch":
                    repo.ApplyPatch();
                    break;
                case "gitflow":
                    OpenGitFlowMenu(anchor, new RoutedEventArgs());
                    break;
                case "lfs":
                    OpenGitLFSMenu(anchor, new RoutedEventArgs());
                    break;
                case "bisect":
                    StartBisect(anchor, new RoutedEventArgs());
                    break;
                case "customAction":
                    OpenCustomActionMenu(anchor, new RoutedEventArgs());
                    break;
                case "clean":
                    Cleanup(anchor, new RoutedEventArgs());
                    break;
                case "openWith":
                    OpenWithExternalTools(anchor, new RoutedEventArgs());
                    break;
                case "logs":
                    OpenGitLogs(anchor, new RoutedEventArgs());
                    break;
                case "statistics":
                    OpenStatistics(anchor, new RoutedEventArgs());
                    break;
                case "settings":
                    OpenConfigure(anchor, new RoutedEventArgs());
                    break;
                case "magic":
                    repo.FocusOnCurrentBranch = !repo.FocusOnCurrentBranch;
                    break;
            }
        }

        private void OpenWithExternalTools(object sender, RoutedEventArgs ev)
        {
            if (sender is Button button && DataContext is ViewModels.Repository repo)
            {
                var fullpath = repo.FullPath;
                if (!Directory.Exists(fullpath))
                    return;

                var isMacOS = OperatingSystem.IsMacOS();
                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

                RenderOptions.SetBitmapInterpolationMode(menu, BitmapInterpolationMode.HighQuality);
                RenderOptions.SetEdgeMode(menu, EdgeMode.Antialias);
                TextOptions.SetTextRenderingMode(menu, TextRenderingMode.SubpixelAntialias);

                var explore = new MenuItem();
                explore.Header = App.Text("Repository.Explore");
                explore.Icon = this.CreateMenuIcon("Icons.Explore");
                explore.Tag = isMacOS ? "⌘+E" : "Ctrl+E";
                explore.Click += (_, e) =>
                {
                    Native.OS.OpenInFileManager(fullpath);
                    e.Handled = true;
                };

                var terminal = new MenuItem();
                terminal.Header = App.Text("Repository.Terminal");
                terminal.Icon = this.CreateMenuIcon("Icons.Terminal");
                terminal.Tag = isMacOS ? "Λ+`" : "Ctrl+`";
                terminal.Click += (_, e) =>
                {
                    Native.OS.OpenTerminal(fullpath);
                    e.Handled = true;
                };

                menu.Items.Add(explore);
                menu.Items.Add(terminal);

                var tools = Native.OS.ExternalTools;
                if (tools.Count > 0)
                {
                    menu.Items.Add(new MenuItem() { Header = "-" });

                    foreach (var tool in tools)
                    {
                        var dupTool = tool;

                        var item = new MenuItem();
                        item.Header = App.Text("Repository.OpenIn", dupTool.Name);
                        item.Icon = new Image { Width = 16, Height = 16, Source = dupTool.IconImage };

                        var options = dupTool.MakeLaunchOptions(fullpath);
                        var count = (dupTool.SupportOpenFolder ? 1 : 0) + (options?.Count ?? 0);
                        if (count == 0)
                            continue;

                        if (count == 1)
                        {
                            var args = fullpath.Quoted();
                            if (options is { Count: 1 })
                                args = options[0].Args;

                            item.Click += (_, e) =>
                            {
                                dupTool.Launch(args);
                                e.Handled = true;
                            };
                        }
                        else
                        {
                            foreach (var opt in options)
                            {
                                var subItem = new MenuItem();
                                subItem.Header = opt.Title;
                                subItem.Click += (_, e) =>
                                {
                                    dupTool.Launch(opt.Args);
                                    e.Handled = true;
                                };

                                item.Items.Add(subItem);
                            }

                            if (dupTool.SupportOpenFolder)
                            {
                                var open = new MenuItem();
                                open.Header = App.Text("Repository.OpenAsFolder");
                                open.Click += (_, e) =>
                                {
                                    dupTool.Launch(fullpath.Quoted());
                                    e.Handled = true;
                                };
                                item.Items.Add(new MenuItem() { Header = "-" });
                                item.Items.Add(open);
                            }
                        }

                        menu.Items.Add(item);
                    }
                }

                var urls = new Dictionary<string, string>();
                foreach (var r in repo.Remotes)
                {
                    if (r.TryGetVisitURL(out var visit))
                        urls.Add(r.Name, visit);
                }

                if (urls.Count > 0)
                {
                    menu.Items.Add(new MenuItem() { Header = "-" });

                    foreach (var (name, addr) in urls)
                    {
                        var dupUrl = addr;

                        var item = new MenuItem();
                        item.Header = App.Text("Repository.Visit", name);
                        item.Icon = this.CreateMenuIcon("Icons.Remotes");
                        item.Click += (_, e) =>
                        {
                            Native.OS.OpenBrowser(dupUrl);
                            e.Handled = true;
                        };

                        menu.Items.Add(item);
                    }
                }

                menu.Open(button);
                ev.Handled = true;
            }
        }

        private async void OpenStatistics(object _, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await this.ShowDialogAsync(new ViewModels.Statistics(repo.FullPath));
                e.Handled = true;
            }
        }

        private async void OpenConfigure(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await this.ShowDialogAsync(new ViewModels.RepositoryConfigure(repo));
                e.Handled = true;
            }
        }

        private async void Fetch(object sender, TappedEventArgs e)
        {
            await FetchCoreAsync(e.KeyModifiers is KeyModifiers.Control);
            e.Handled = true;
        }

        private async Task FetchCoreAsync(bool showOptions)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.FetchAsync(showOptions);
        }

        private async void FetchDirectlyByHotKey(object sender, RoutedEventArgs e)
        {
            if (App.GetLauncher() is { CommandPalette: { } } launcher)
                return;

            if (DataContext is ViewModels.Repository repo)
            {
                await repo.FetchAsync(true);
                e.Handled = true;
            }
        }

        private async void Pull(object sender, TappedEventArgs e)
        {
            await PullCoreAsync(e.KeyModifiers is KeyModifiers.Control);
            e.Handled = true;
        }

        private async Task PullCoreAsync(bool showOptions)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.PullAsync(showOptions);
        }

        private async void PullDirectlyByHotKey(object sender, RoutedEventArgs e)
        {
            if (App.GetLauncher() is { CommandPalette: { } } launcher)
                return;

            if (DataContext is ViewModels.Repository repo)
            {
                await repo.PullAsync(true);
                e.Handled = true;
            }
        }

        private async void Push(object sender, TappedEventArgs e)
        {
            await PushCoreAsync(e.KeyModifiers is KeyModifiers.Control);
            e.Handled = true;
        }

        private async Task PushCoreAsync(bool showOptions)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.PushAsync(showOptions);
        }

        private async void PushDirectlyByHotKey(object sender, RoutedEventArgs e)
        {
            if (App.GetLauncher() is { CommandPalette: { } } launcher)
                return;

            if (DataContext is ViewModels.Repository repo)
            {
                await repo.PushAsync(true);
                e.Handled = true;
            }
        }

        private async void StashAll(object _, TappedEventArgs e)
        {
            await StashCoreAsync(e.KeyModifiers is KeyModifiers.Control);
            e.Handled = true;
        }

        private async Task StashCoreAsync(bool showOptions)
        {
            if (DataContext is ViewModels.Repository repo)
                await repo.StashAllAsync(showOptions);
        }

        private void OpenGitFlowMenu(object sender, RoutedEventArgs ev)
        {
            if (DataContext is ViewModels.Repository repo && sender is Control control)
            {
                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

                if (repo.IsGitFlowEnabled())
                {
                    var startFeature = new MenuItem();
                    startFeature.Header = App.Text("GitFlow.StartFeature");
                    startFeature.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.GitFlowStart(repo, Models.GitFlowBranchType.Feature));
                        e.Handled = true;
                    };

                    var startRelease = new MenuItem();
                    startRelease.Header = App.Text("GitFlow.StartRelease");
                    startRelease.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.GitFlowStart(repo, Models.GitFlowBranchType.Release));
                        e.Handled = true;
                    };

                    var startHotfix = new MenuItem();
                    startHotfix.Header = App.Text("GitFlow.StartHotfix");
                    startHotfix.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.GitFlowStart(repo, Models.GitFlowBranchType.Hotfix));
                        e.Handled = true;
                    };

                    menu.Items.Add(startFeature);
                    menu.Items.Add(startRelease);
                    menu.Items.Add(startHotfix);

                    var type = repo.CurrentBranch != null ? repo.GetGitFlowType(repo.CurrentBranch) : Models.GitFlowBranchType.None;
                    if (type != Models.GitFlowBranchType.None)
                    {
                        var finish = new MenuItem();
                        finish.Header = App.Text("GitFlow.Finish", repo.CurrentBranch.Name);
                        finish.Icon = this.CreateMenuIcon("Icons.GitFlow.Finish");
                        finish.Click += (_, e) =>
                        {
                            if (repo.CanCreatePopup())
                                repo.ShowPopup(new ViewModels.GitFlowFinish(repo, repo.CurrentBranch, type));
                            e.Handled = true;
                        };
                        menu.Items.Add(new MenuItem() { Header = "-" });
                        menu.Items.Add(finish);
                    }
                }
                else
                {
                    var init = new MenuItem();
                    init.Header = App.Text("GitFlow.Init");
                    init.Icon = this.CreateMenuIcon("Icons.Init");
                    init.Click += (_, e) =>
                    {
                        if (repo.CurrentBranch == null)
                            repo.SendNotification("Git flow init failed: No branch found!!!", true);
                        else if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.InitGitFlow(repo));

                        e.Handled = true;
                    };
                    menu.Items.Add(init);
                }

                menu.Open(control);
            }

            ev.Handled = true;
        }

        private void OpenGitLFSMenu(object sender, RoutedEventArgs ev)
        {
            if (DataContext is ViewModels.Repository repo && sender is Control control)
            {
                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

                if (repo.IsLFSEnabled())
                {
                    var addPattern = new MenuItem();
                    addPattern.Header = App.Text("GitLFS.AddTrackPattern");
                    addPattern.Icon = this.CreateMenuIcon("Icons.File.Add");
                    addPattern.Click += (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            repo.ShowPopup(new ViewModels.LFSTrackCustomPattern(repo));

                        e.Handled = true;
                    };
                    menu.Items.Add(addPattern);
                    menu.Items.Add(new MenuItem() { Header = "-" });

                    var fetch = new MenuItem();
                    fetch.Header = App.Text("GitLFS.Fetch");
                    fetch.Icon = this.CreateMenuIcon("Icons.Fetch");
                    fetch.IsEnabled = repo.Remotes.Count > 0;
                    fetch.Click += async (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                        {
                            if (repo.Remotes.Count == 1)
                                await repo.ShowAndStartPopupAsync(new ViewModels.LFSFetch(repo));
                            else
                                repo.ShowPopup(new ViewModels.LFSFetch(repo));
                        }

                        e.Handled = true;
                    };
                    menu.Items.Add(fetch);

                    var pull = new MenuItem();
                    pull.Header = App.Text("GitLFS.Pull");
                    pull.Icon = this.CreateMenuIcon("Icons.Pull");
                    pull.IsEnabled = repo.Remotes.Count > 0;
                    pull.Click += async (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                        {
                            if (repo.Remotes.Count == 1)
                                await repo.ShowAndStartPopupAsync(new ViewModels.LFSPull(repo));
                            else
                                repo.ShowPopup(new ViewModels.LFSPull(repo));
                        }

                        e.Handled = true;
                    };
                    menu.Items.Add(pull);

                    var push = new MenuItem();
                    push.Header = App.Text("GitLFS.Push");
                    push.Icon = this.CreateMenuIcon("Icons.Push");
                    push.IsEnabled = repo.Remotes.Count > 0;
                    push.Click += async (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                        {
                            if (repo.Remotes.Count == 1)
                                await repo.ShowAndStartPopupAsync(new ViewModels.LFSPush(repo));
                            else
                                repo.ShowPopup(new ViewModels.LFSPush(repo));
                        }

                        e.Handled = true;
                    };
                    menu.Items.Add(push);

                    var prune = new MenuItem();
                    prune.Header = App.Text("GitLFS.Prune");
                    prune.Icon = this.CreateMenuIcon("Icons.Clean");
                    prune.Click += async (_, e) =>
                    {
                        if (repo.CanCreatePopup())
                            await repo.ShowAndStartPopupAsync(new ViewModels.LFSPrune(repo));

                        e.Handled = true;
                    };
                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(prune);

                    var locks = new MenuItem();
                    locks.Header = App.Text("GitLFS.Locks");
                    locks.Icon = this.CreateMenuIcon("Icons.Lock");
                    locks.IsEnabled = repo.Remotes.Count > 0;
                    if (repo.Remotes.Count == 1)
                    {
                        locks.Click += async (_, e) =>
                        {
                            await this.ShowDialogAsync(new ViewModels.LFSLocks(repo, repo.Remotes[0].Name));
                            e.Handled = true;
                        };
                    }
                    else
                    {
                        foreach (var remote in repo.Remotes)
                        {
                            var remoteName = remote.Name;
                            var lockRemote = new MenuItem();
                            lockRemote.Header = remoteName;
                            lockRemote.Click += async (_, e) =>
                            {
                                await this.ShowDialogAsync(new ViewModels.LFSLocks(repo, remoteName));
                                e.Handled = true;
                            };
                            locks.Items.Add(lockRemote);
                        }
                    }

                    menu.Items.Add(new MenuItem() { Header = "-" });
                    menu.Items.Add(locks);
                }
                else
                {
                    var install = new MenuItem();
                    install.Header = App.Text("GitLFS.Install");
                    install.Icon = this.CreateMenuIcon("Icons.Init");
                    install.Click += async (_, e) =>
                    {
                        await repo.InstallLFSAsync();
                        e.Handled = true;
                    };
                    menu.Items.Add(install);
                }

                menu.Open(control);
            }

            ev.Handled = true;
        }

        private async void StartBisect(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository { IsBisectCommandRunning: false, InProgressContext: null } repo &&
                repo.CanCreatePopup())
            {
                if (repo.LocalChangesCount > 0)
                    repo.SendNotification("You have un-committed local changes. Please discard or stash them first.", true);
                else if (repo.IsBisectCommandRunning || repo.BisectState != Models.BisectState.None)
                    repo.SendNotification("Bisect is running! Please abort it before starting a new one.", true);
                else
                    await repo.ExecBisectCommandAsync("start");
            }

            e.Handled = true;
        }

        private async void Cleanup(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await repo.CleanupAsync();
                e.Handled = true;
            }
        }

        private void OpenCustomActionMenu(object sender, RoutedEventArgs ev)
        {
            if (DataContext is ViewModels.Repository repo && sender is Control control)
            {
                var menu = new ContextMenu();
                menu.Placement = PlacementMode.BottomEdgeAlignedLeft;

                var actions = repo.GetCustomActions(Models.CustomActionScope.Repository);
                if (actions.Count > 0)
                {
                    foreach (var action in actions)
                    {
                        var (dup, label) = action;
                        var item = new MenuItem();
                        item.Icon = this.CreateMenuIcon("Icons.Action");
                        item.Header = label;
                        item.Click += async (_, e) =>
                        {
                            await repo.ExecCustomActionAsync(dup, null);
                            e.Handled = true;
                        };

                        menu.Items.Add(item);
                    }
                }
                else
                {
                    menu.Items.Add(new MenuItem() { Header = App.Text("Repository.CustomActions.Empty") });
                }

                menu.Open(control);
            }

            ev.Handled = true;
        }

        private async void OpenGitLogs(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository repo)
            {
                await this.ShowDialogAsync(new ViewModels.ViewLogs(repo));
                e.Handled = true;
            }
        }

        private void NavigateToHead(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Repository { CurrentBranch: not null } repo)
            {
                var repoView = TopLevel.GetTopLevel(this)?.FindDescendantOfType<Repository>();
                repoView?.LocalBranchTree?.Select(repo.CurrentBranch);

                repo.NavigateToCommit(repo.CurrentBranch.Head);
                e.Handled = true;
            }
        }
    }
}
