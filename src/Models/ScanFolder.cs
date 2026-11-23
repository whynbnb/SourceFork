namespace SourceFork.Models
{
    public enum ScanFolderMode
    {
        Never = 0,
        OnStartup = 1,
        Watch = 2,
    }

    public class ScanFolder
    {
        public string Path { get; set; } = string.Empty;
        public bool Recursive { get; set; } = true;
        public ScanFolderMode Mode { get; set; } = ScanFolderMode.OnStartup;
    }
}
