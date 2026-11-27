namespace SourceFork.Models
{
    public enum RepositorySortMode
    {
        Custom = 0,
        AddedTime,
        Name,
        UpdatedTime,
    }

    public enum RepositoryGroupSortMode
    {
        Name = 0,
        Count,
        Custom,
    }

    public class RepositoryGroupItemSort
    {
        public RepositorySortMode Mode { get; set; } = RepositorySortMode.Custom;
        public bool Descending { get; set; } = false;
    }
}
