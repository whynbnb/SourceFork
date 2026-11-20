using System.Text.Json.Serialization;

using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class LayoutInfo : ObservableObject
    {
        public double LauncherWidth
        {
            get;
            set;
        } = 1280;

        public double LauncherHeight
        {
            get;
            set;
        } = 720;

        public int LauncherPositionX
        {
            get;
            set;
        } = int.MinValue;

        public int LauncherPositionY
        {
            get;
            set;
        } = int.MinValue;

        public WindowState LauncherWindowState
        {
            get;
            set;
        } = WindowState.Normal;

        public GridLength WorkspaceSidebarWidth
        {
            get => _workspaceSidebarWidth;
            set
            {
                if (SetProperty(ref _workspaceSidebarWidth, value))
                    OnPropertyChanged(nameof(VisibleWorkspaceSidebarWidth));
            }
        }

        public bool WorkspaceSidebarCollapsed
        {
            get => _workspaceSidebarCollapsed;
            set
            {
                if (SetProperty(ref _workspaceSidebarCollapsed, value))
                {
                    OnPropertyChanged(nameof(VisibleWorkspaceSidebarWidth));
                    OnPropertyChanged(nameof(WorkspaceSidebarSplitterWidth));
                    OnPropertyChanged(nameof(WorkspaceSidebarMinWidth));
                    OnPropertyChanged(nameof(WorkspaceSidebarMaxWidth));
                }
            }
        }

        [JsonIgnore]
        public GridLength VisibleWorkspaceSidebarWidth
        {
            get => WorkspaceSidebarCollapsed ? new GridLength(0) : _workspaceSidebarWidth;
            set
            {
                if (!WorkspaceSidebarCollapsed)
                    WorkspaceSidebarWidth = value;
            }
        }

        [JsonIgnore]
        public GridLength WorkspaceSidebarSplitterWidth
        {
            get => WorkspaceSidebarCollapsed ? new GridLength(0) : new GridLength(3);
        }

        [JsonIgnore]
        public double WorkspaceSidebarMinWidth
        {
            get => WorkspaceSidebarCollapsed ? 0 : 180;
        }

        [JsonIgnore]
        public double WorkspaceSidebarMaxWidth
        {
            get => WorkspaceSidebarCollapsed ? 0 : 420;
        }

        public GridLength RepositorySidebarWidth
        {
            get => _repositorySidebarWidth;
            set => SetProperty(ref _repositorySidebarWidth, value);
        }

        public GridLength WorkingCopyLeftWidth
        {
            get => _workingCopyLeftWidth;
            set => SetProperty(ref _workingCopyLeftWidth, value);
        }

        public GridLength StashesLeftWidth
        {
            get => _stashesLeftWidth;
            set => SetProperty(ref _stashesLeftWidth, value);
        }

        public GridLength CommitDetailChangesLeftWidth
        {
            get => _commitDetailChangesLeftWidth;
            set => SetProperty(ref _commitDetailChangesLeftWidth, value);
        }

        public GridLength CommitDetailFilesLeftWidth
        {
            get => _commitDetailFilesLeftWidth;
            set => SetProperty(ref _commitDetailFilesLeftWidth, value);
        }

        private GridLength _workspaceSidebarWidth = new GridLength(230, GridUnitType.Pixel);
        private bool _workspaceSidebarCollapsed = false;
        private GridLength _repositorySidebarWidth = new GridLength(250, GridUnitType.Pixel);
        private GridLength _workingCopyLeftWidth = new GridLength(300, GridUnitType.Pixel);
        private GridLength _stashesLeftWidth = new GridLength(300, GridUnitType.Pixel);
        private GridLength _commitDetailChangesLeftWidth = new GridLength(256, GridUnitType.Pixel);
        private GridLength _commitDetailFilesLeftWidth = new GridLength(256, GridUnitType.Pixel);
    }
}
