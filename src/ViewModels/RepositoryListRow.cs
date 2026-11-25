using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class RepositoryListRow : ObservableObject
    {
        public bool IsGroupHeader
        {
            get;
        }

        public string GroupName
        {
            get;
        }

        public RepositoryNode Node
        {
            get;
        }

        public LauncherPage Page
        {
            get;
        }

        public bool IsSelectable
        {
            get => !IsGroupHeader;
        }

        public bool IsRepository
        {
            get => Node is { IsRepository: true };
        }

        public bool IsOpen
        {
            get => Page != null;
        }

        public bool CanClose
        {
            get => IsRepository && Page != null;
        }

        public Models.DirtyState DirtyState
        {
            get => Page?.DirtyState ?? Models.DirtyState.None;
        }

        public bool IsDirty
        {
            get => DirtyState != Models.DirtyState.None;
        }

        public RepositoryListRow(string groupName)
        {
            IsGroupHeader = true;
            GroupName = groupName;
        }

        public RepositoryListRow(RepositoryNode node, LauncherPage page)
        {
            Node = node;
            Page = page;

            if (page != null)
                page.PropertyChanged += OnPagePropertyChanged;
        }

        public void Detach()
        {
            if (Page != null)
                Page.PropertyChanged -= OnPagePropertyChanged;
        }

        private void OnPagePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LauncherPage.DirtyState))
            {
                OnPropertyChanged(nameof(DirtyState));
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public class RepositoryGroupRow : RepositoryListRow
    {
        public string GroupKey
        {
            get;
        }

        public bool IsExpanded
        {
            get;
        }

        public bool IsCollapsed
        {
            get => !IsExpanded;
        }

        public RepositoryGroupRow(string groupKey, string groupName, bool isCollapsed)
            : base(groupName)
        {
            GroupKey = groupKey;
            IsExpanded = !isCollapsed;
        }
    }
}
