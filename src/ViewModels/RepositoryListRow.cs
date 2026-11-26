using System;
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

        public string PrimaryTagText
        {
            get
            {
                if (Preferences.Instance.RepositoryGroupByPrimaryTag)
                    return string.Empty;

                return Node is { HasPrimaryTag: true } node ? $"#{node.PrimaryTag}" : string.Empty;
            }
        }

        public bool HasUpdatedText
        {
            get => UpdatedText.Length > 0;
        }

        public string UpdatedText
        {
            get => _updatedText;
            private set => SetProperty(ref _updatedText, value);
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
            _updatedText = DescribeUpdatedTime(node);

            node.PropertyChanged += OnNodePropertyChanged;

            if (page != null)
                page.PropertyChanged += OnPagePropertyChanged;
        }

        public void RefreshDerived()
        {
            OnPropertyChanged(nameof(PrimaryTagText));
            RefreshUpdated();
        }

        public void Detach()
        {
            if (Node != null)
                Node.PropertyChanged -= OnNodePropertyChanged;

            if (Page != null)
                Page.PropertyChanged -= OnPagePropertyChanged;
        }

        private void OnNodePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RepositoryNode.Status))
                RefreshUpdated();
        }

        private void OnPagePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LauncherPage.DirtyState))
            {
                OnPropertyChanged(nameof(DirtyState));
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        private void RefreshUpdated()
        {
            UpdatedText = DescribeUpdatedTime(Node);
            OnPropertyChanged(nameof(HasUpdatedText));
        }

        private static string DescribeUpdatedTime(RepositoryNode node)
        {
            var timestamp = node?.Status?.LastCommitTime ?? 0;
            if (timestamp <= 0)
                return string.Empty;

            var days = (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp) / 86400.0;
            if (days <= 1)
                return App.Text("Sidebar.UpdatedWithinDay");

            if (days <= 7)
                return App.Text("Sidebar.UpdatedWithinWeek");

            if (days <= 30)
                return App.Text("Sidebar.UpdatedWithinMonth");

            return App.Text("Sidebar.UpdatedLongAgo");
        }

        private string _updatedText = string.Empty;
    }

    public class RepositoryGroupRow : RepositoryListRow
    {
        public string GroupKey
        {
            get;
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            private set
            {
                if (SetProperty(ref _isExpanded, value))
                    OnPropertyChanged(nameof(IsCollapsed));
            }
        }

        public bool IsCollapsed
        {
            get => !_isExpanded;
        }

        public RepositoryGroupRow(string groupKey, string groupName, bool isCollapsed)
            : base(groupName)
        {
            GroupKey = groupKey;
            _isExpanded = !isCollapsed;
        }

        public void SetExpanded(bool expanded)
        {
            IsExpanded = expanded;
        }

        private bool _isExpanded;
    }
}
