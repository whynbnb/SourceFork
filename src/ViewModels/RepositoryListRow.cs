namespace SourceFork.ViewModels
{
    public class RepositoryListRow
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

        public RepositoryListRow(string groupName)
        {
            IsGroupHeader = true;
            GroupName = groupName;
        }

        public RepositoryListRow(RepositoryNode node, LauncherPage page)
        {
            Node = node;
            Page = page;
        }
    }
}
