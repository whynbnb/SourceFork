using System;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace SourceFork.Views
{
    public partial class Launcher : ChromelessWindow
    {
        public static readonly DirectProperty<Launcher, GridLength> CaptionHeightProperty =
            AvaloniaProperty.RegisterDirect<Launcher, GridLength>(
                nameof(CaptionHeight),
                static o => o.CaptionHeight);

        public GridLength CaptionHeight
        {
            get => _captionHeight;
            set => SetAndRaise(CaptionHeightProperty, ref _captionHeight, value);
        }

        public bool ShowMacOSTrafficLights
        {
            get => OperatingSystem.IsMacOS() && !UseSystemWindowFrame;
        }

        public bool HasRightCaptionButton
        {
            get
            {
                if (OperatingSystem.IsLinux() || OperatingSystem.IsWindows())
                    return !Native.OS.UseSystemWindowFrame;

                return false;
            }
        }

        public Launcher()
        {
            if (OperatingSystem.IsMacOS())
                CaptionHeight = new GridLength(UseSystemWindowFrame ? 30 : 34);
            else if (UseSystemWindowFrame)
                CaptionHeight = new GridLength(30);
            else
                CaptionHeight = new GridLength(38);

            InitializeComponent();
            PositionChanged += OnPositionChanged;

            // Drag events are bubbling routed events in Avalonia, so the window only sees them
            // when it listens to handled events as well: children such as the repository list
            // mark DragOver/Drop as handled to accept the drop.
            AddHandler(DragDrop.DragEnterEvent, OnWindowDragOver, RoutingStrategies.Bubble, true);
            AddHandler(DragDrop.DragOverEvent, OnWindowDragOver, RoutingStrategies.Bubble, true);
            AddHandler(DragDrop.DragLeaveEvent, OnWindowDragLeave, RoutingStrategies.Bubble, true);
            AddHandler(DragDrop.DropEvent, OnWindowDrop, RoutingStrategies.Bubble, true);

            // DragLeave also fires when the pointer moves between child controls, so wait a
            // moment and only hide the panel if no DragOver follows.
            _importLeaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _importLeaveTimer.Tick += (_, _) =>
            {
                _importLeaveTimer.Stop();
                if (DataContext is ViewModels.Launcher launcher && !launcher.ImportDrop.IsPinned)
                    launcher.ImportDrop.End();
            };

            // The import preview must never outlive the interaction that opened it.
            Deactivated += (_, _) =>
            {
                if (DataContext is ViewModels.Launcher launcher && !launcher.ImportDrop.IsPinned)
                    launcher.ImportDrop.End();
            };

            var layout = ViewModels.Preferences.Instance.Layout;
            Width = layout.LauncherWidth;
            Height = layout.LauncherHeight;

            var x = layout.LauncherPositionX;
            var y = layout.LauncherPositionY;
            if (x != int.MinValue && y != int.MinValue && Screens is { } screens)
            {
                var position = new PixelPoint(x, y);
                var size = new PixelSize((int)layout.LauncherWidth, (int)layout.LauncherHeight);
                var desiredRect = new PixelRect(position, size);
                for (var i = 0; i < screens.ScreenCount; i++)
                {
                    var screen = screens.All[i];
                    if (screen.WorkingArea.Contains(desiredRect))
                    {
                        Position = position;
                        return;
                    }
                }
            }

            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        public void BringToTop()
        {
            if (WindowState == WindowState.Minimized)
                WindowState = _lastWindowState;
            else
                Activate();
        }

        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var preferences = ViewModels.Preferences.Instance;
            WindowState = preferences.Layout.LauncherWindowState;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == WindowStateProperty)
            {
                var state = (WindowState)change.NewValue!;
                _lastWindowState = (WindowState)change.OldValue!;

                if (!OperatingSystem.IsMacOS() && !UseSystemWindowFrame)
                    CaptionHeight = new GridLength(state == WindowState.Maximized ? 30 : 38);

                ViewModels.Preferences.Instance.Layout.LauncherWindowState = state;
            }
            else if (change.Property == IsActiveProperty)
            {
                if (!IsActive && DataContext is ViewModels.Launcher { CommandPalette: { } } vm)
                    vm.CommandPalette = null;
            }

            if (OperatingSystem.IsMacOS() && !UseSystemWindowFrame && WindowState != WindowState.FullScreen)
            {
                if (change.Property == WindowStateProperty ||
                    change.Property == BoundsProperty ||
                    change.Property == TitleProperty)
                    Native.MacOSUtilities.AdjustTrafficLightsForThickTitleBar(this);
            }
        }

        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);

            if (WindowState == WindowState.Normal)
            {
                var layout = ViewModels.Preferences.Instance.Layout;
                layout.LauncherWidth = Width;
                layout.LauncherHeight = Height;
            }
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            if (DataContext is not ViewModels.Launcher vm)
                return;

            if (e is { Key: Key.Escape, KeyModifiers: KeyModifiers.None } && vm.ImportDrop.IsActive)
            {
                vm.ImportDrop.End();
                e.Handled = true;
                return;
            }

            // Check for AltGr (which is detected as Ctrl+Alt)
            bool isAltGr = e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                           e.KeyModifiers.HasFlag(KeyModifiers.Alt);

            // Skip hotkey processing if AltGr is pressed
            if (isAltGr)
            {
                base.OnKeyDown(e);
                return;
            }

            // Register hotkeys for Windows/Linux (macOS has registered these keys in system menu bar)
            var isMacOS = OperatingSystem.IsMacOS();
            var cmdKey = isMacOS ? KeyModifiers.Meta : KeyModifiers.Control;
            if (!isMacOS)
            {
                if (e is { KeyModifiers: KeyModifiers.Control, Key: Key.OemComma })
                {
                    await this.ShowDialogAsync(new Preferences());
                    e.Handled = true;
                    return;
                }

                if (e is { KeyModifiers: KeyModifiers.None, Key: Key.F1 })
                {
                    await this.ShowDialogAsync(new Hotkeys());
                    e.Handled = true;
                    return;
                }

                if (e is { KeyModifiers: KeyModifiers.Control, Key: Key.Q })
                {
                    App.Quit(0);
                    return;
                }
            }

            // Ctrl+` to open terminal. On macOS, Cmd+` is used to switch between windows
            if (e is { Key: Key.OemTilde, KeyModifiers: KeyModifiers.Control })
            {
                if (vm.ActivePage.Data is ViewModels.Repository repo)
                    Native.OS.OpenTerminal(repo.FullPath);
                else
                    ViewModels.Welcome.Instance.OpenTerminal();

                e.Handled = true;
                return;
            }

            if (vm.CommandPalette != null)
            {
                if (e.Key == Key.Escape)
                {
                    vm.CommandPalette = null;
                    e.Handled = true;
                }
                else if (vm.ActivePage.Data is ViewModels.Repository repo
                    && vm.CommandPalette is ViewModels.LauncherPagesCommandPalette
                    && e.Key == Key.P
                    && e.KeyModifiers == (cmdKey | KeyModifiers.Shift))
                {
                    vm.CommandPalette = new ViewModels.RepositoryCommandPalette(repo);
                    e.Handled = true;
                }

                return;
            }

            if (e.KeyModifiers.HasFlag(cmdKey))
            {
                if (e.Key == Key.R)
                {
                    if (vm.ActivePage.Data is not ViewModels.Welcome)
                        vm.ShowWelcomePage();

                    ViewModels.Welcome.Instance.Clone();
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.L)
                {
                    if (vm.ActivePage.Data is not ViewModels.Welcome)
                        vm.ShowWelcomePage();

                    ViewModels.Welcome.Instance.OpenLocalRepository();
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.T && e.KeyModifiers == cmdKey)
                {
                    vm.ShowWelcomePage();
                    e.Handled = true;
                    return;
                }

                if (e.Key == Key.P && e.KeyModifiers == cmdKey)
                {
                    vm.CommandPalette = new ViewModels.LauncherPagesCommandPalette(vm);
                    e.Handled = true;
                    return;
                }

                if ((isMacOS && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Right) ||
                    (!isMacOS && !e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Tab))
                {
                    vm.GotoNextTab();
                    e.Handled = true;
                    return;
                }

                if ((isMacOS && e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key == Key.Left) ||
                    (!isMacOS && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Tab))
                {
                    vm.GotoPrevTab();
                    e.Handled = true;
                    return;
                }

                if (vm.ActivePage.Data is ViewModels.Repository repo)
                {
                    switch (e.Key)
                    {
                        case Key.D1 or Key.NumPad1:
                            repo.SelectedViewIndex = 0;
                            e.Handled = true;
                            return;
                        case Key.D2 or Key.NumPad2:
                            repo.SelectedViewIndex = 1;
                            e.Handled = true;
                            return;
                        case Key.D3 or Key.NumPad3:
                            repo.SelectedViewIndex = 2;
                            e.Handled = true;
                            return;
                        case Key.F when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                            repo.IsSearchingCommits = !repo.IsSearchingCommits;
                            e.Handled = true;
                            return;
                        case Key.H when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                            repo.IsSearchingCommits = false;
                            e.Handled = true;
                            return;
                        case Key.P when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                            vm.CommandPalette = new ViewModels.RepositoryCommandPalette(repo);
                            e.Handled = true;
                            return;
                        case Key.B when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                            if (repo.CanCreatePopup() && repo.GetSelectedCommitInHistory() is { } bc)
                                repo.ShowPopup(new ViewModels.CreateBranch(repo, bc));
                            e.Handled = true;
                            return;
                        case Key.T when e.KeyModifiers.HasFlag(KeyModifiers.Shift):
                            if (repo.CanCreatePopup() && repo.GetSelectedCommitInHistory() is { } tc)
                                repo.ShowPopup(new ViewModels.CreateTag(repo, tc));
                            e.Handled = true;
                            return;
                        case Key.E:
                            Native.OS.OpenInFileManager(repo.FullPath);
                            e.Handled = true;
                            return;
                    }
                }
            }
            else if (e.Key == Key.Escape)
            {
                vm.ActivePage.CancelPopup();
                vm.ActivePage.Notifications.Clear();
                e.Handled = true;
                return;
            }
            else if (e.Key == Key.F5)
            {
                if (vm.ActivePage.Data is ViewModels.Repository repo)
                {
                    repo.RefreshAll();
                    e.Handled = true;
                    return;
                }
                else if (vm.ActivePage.Data is ViewModels.Welcome welcome)
                {
                    e.Handled = true;
                    await welcome.UpdateStatusAsync(true, null);
                    return;
                }
            }

            base.OnKeyDown(e);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (!e.Handled)
            {
                if (e.Properties.PointerUpdateKind == PointerUpdateKind.XButton1Pressed)
                    (DataContext as ViewModels.Launcher)?.GotoPrevTab();
                else if (e.Properties.PointerUpdateKind == PointerUpdateKind.XButton2Pressed)
                    (DataContext as ViewModels.Launcher)?.GotoNextTab();
            }
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            if (!Design.IsDesignMode && DataContext is ViewModels.Launcher launcher)
                launcher.CloseAll();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            if (!Design.IsDesignMode)
                ViewModels.Preferences.Instance.Save();

            App.Quit(0);
        }

        private DispatcherTimer _importLeaveTimer;

        private void OnWindowDragOver(object sender, DragEventArgs e)
        {
            if (DataContext is not ViewModels.Launcher launcher ||
                !e.DataTransfer.Contains(DataFormat.File))
            {
                return;
            }

            _importLeaveTimer?.Stop();
            e.DragEffects = DragDropEffects.Move;
            e.Handled = true;
            _importLeaveTimer?.Stop();

            var paths = e.DataTransfer.TryGetFiles()?
                .Select(x => x.Path.LocalPath)
                .ToList() ?? [];
            launcher.ImportDrop.Show(paths);
        }

        private void OnWindowDragLeave(object sender, DragEventArgs e)
        {
            if (DataContext is not ViewModels.Launcher launcher || !launcher.ImportDrop.IsActive)
                return;

            _importLeaveTimer?.Stop();
            _importLeaveTimer?.Start();
        }

        private async void OnWindowDrop(object sender, DragEventArgs e)
        {
            if (DataContext is not ViewModels.Launcher launcher ||
                !e.DataTransfer.Contains(DataFormat.File))
            {
                return;
            }

            e.Handled = true;

            var paths = e.DataTransfer.TryGetFiles()?
                .Select(x => x.Path.LocalPath)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList() ?? [];

            if (paths.Count == 0)
            {
                launcher.ImportDrop.End();
                return;
            }

            // Dropping on the small pin area keeps the panel open for picking repositories.
            if (ImportPinArea.IsVisible)
            {
                var position = e.GetPosition(ImportPinArea);
                if (position.X >= 0 &&
                    position.Y >= 0 &&
                    position.X <= ImportPinArea.Bounds.Width &&
                    position.Y <= ImportPinArea.Bounds.Height)
                {
                    launcher.ImportDrop.Pin();
                    return;
                }
            }

            // Hide the panel right away; inspecting a workspace runs git commands and can take
            // a moment, so the overlay should not linger after the mouse is released.
            var session = launcher.ImportDrop;
            session.Hide();
            await session.EnsureInspectedAsync();

            var selected = session.SelectedPaths;
            session.End();

            if (selected.Count > 0)
                await ViewModels.Welcome.Instance.ImportDroppedPathsAsync(selected, launcher.ActivePage);
        }

        private void OnCloseImportOverlay(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher launcher)
                launcher.ImportDrop.End();

            e.Handled = true;
        }

        private void OnSelectAllImport(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher launcher)
                launcher.ImportDrop.SetAllSelected(true);

            e.Handled = true;
        }

        private async void OnImportSelected(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ViewModels.Launcher launcher)
                return;

            e.Handled = true;

            var paths = launcher.ImportDrop.SelectedPaths;
            launcher.ImportDrop.End();

            if (paths.Count > 0)
                await ViewModels.Welcome.Instance.ImportDroppedPathsAsync(paths, launcher.ActivePage);
        }

        private void OnPositionChanged(object sender, PixelPointEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                var layout = ViewModels.Preferences.Instance.Layout;
                layout.LauncherPositionX = Position.X;
                layout.LauncherPositionY = Position.Y;
            }
        }

        private void OnShowWelcome(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher launcher)
                launcher.ShowWelcomePage();

            e.Handled = true;
        }

        private void OnToggleWorkspaceSidebar(object sender, RoutedEventArgs e)
        {
            var layout = ViewModels.Preferences.Instance.Layout;
            layout.WorkspaceSidebarCollapsed = !layout.WorkspaceSidebarCollapsed;
            ViewModels.Preferences.Instance.Save();
            e.Handled = true;
        }

        private void OnWorkspaceSidebarSplitterReleased(object sender, PointerReleasedEventArgs e)
        {
            ViewModels.Preferences.Instance.Save();
        }

        private void OnWorkspaceSidebarSplitterCaptureLost(object sender, PointerCaptureLostEventArgs e)
        {
            ViewModels.Preferences.Instance.Save();
        }

        private void OnCloseCommandPalette(object sender, PointerPressedEventArgs e)
        {
            if (e.Source == sender && DataContext is ViewModels.Launcher vm)
                vm.CommandPalette = null;
            e.Handled = true;
        }

        private async void OnShowNewVersion(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.Launcher { NewVersion: { } ver } vm)
            {
                vm.NewVersion = null;

                var ctx = new ViewModels.SelfUpdate { Data = ver };
                var dialog = new SelfUpdate() { DataContext = ctx };
                await dialog.ShowDialog(this);
            }

            e.Handled = true;
        }

        private GridLength _captionHeight = new(32);
        private WindowState _lastWindowState = WindowState.Normal;
    }
}

