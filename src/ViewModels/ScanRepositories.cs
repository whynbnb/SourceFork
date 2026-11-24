using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class ScanFolderItem : ObservableObject
    {
        public Models.ScanFolder Folder
        {
            get;
        }

        public string Path
        {
            get => Folder.Path;
        }

        public int DepthIndex
        {
            get => Folder.Recursive ? 1 : 0;
            set
            {
                var recursive = value == 1;
                if (Folder.Recursive == recursive)
                    return;

                Folder.Recursive = recursive;
                OnPropertyChanged();
                Changed?.Invoke(this);
            }
        }

        public int ModeIndex
        {
            get => (int)Folder.Mode;
            set
            {
                var mode = (Models.ScanFolderMode)Math.Clamp(value, 0, 2);
                if (Folder.Mode == mode)
                    return;

                Folder.Mode = mode;
                OnPropertyChanged();
                Changed?.Invoke(this);
                ModeChanged?.Invoke(this);
            }
        }

        public IReadOnlyList<string> DepthOptions
        {
            get;
        } = [
            App.Text("ScanFolders.Depth.Single"),
            App.Text("ScanFolders.Depth.Recursive"),
        ];

        public IReadOnlyList<string> ModeOptions
        {
            get;
        } = [
            App.Text("ScanFolders.Mode.Never"),
            App.Text("ScanFolders.Mode.OnStartup"),
            App.Text("ScanFolders.Mode.Watch"),
        ];

        public event Action<ScanFolderItem> Changed;
        public event Action<ScanFolderItem> ModeChanged;

        public ScanFolderItem(Models.ScanFolder folder)
        {
            Folder = folder;
        }
    }

    public class ScanRepositories : ObservableObject
    {
        public static ScanRepositories Instance { get; } = new();

        public AvaloniaList<ScanFolderItem> Folders
        {
            get;
        } = [];

        public ScanFolderItem Selected
        {
            get => _selected;
            set => SetProperty(ref _selected, value);
        }

        public bool IsEmpty
        {
            get => Folders.Count == 0;
        }

        public bool IsScanning
        {
            get => RepositoryScanner.Instance.IsScanning;
        }

        public string ProgressDescription
        {
            get => RepositoryScanner.Instance.ProgressDescription;
        }

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public ScanRepositories()
        {
            RepositoryScanner.Instance.StateChanged += OnScannerStateChanged;
            Reload();
        }

        public void Reload()
        {
            foreach (var item in Folders)
            {
                item.Changed -= OnItemChanged;
                item.ModeChanged -= OnItemModeChanged;
            }

            Folders.Clear();
            foreach (var folder in Preferences.Instance.ScanFolders)
                Folders.Add(CreateItem(folder));

            OnPropertyChanged(nameof(IsEmpty));
        }

        public void AddFolders(IEnumerable<string> paths)
        {
            var added = new List<ScanFolderItem>();
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                string normalized;
                try
                {
                    normalized = ViewModels.RepositoryScanner.NormalizePath(Path.GetFullPath(path));
                }
                catch
                {
                    continue;
                }

                if (Folders.Any(x => SamePath(RepositoryScanner.NormalizePath(x.Path), normalized)))
                    continue;

                var folder = new Models.ScanFolder()
                {
                    Path = normalized,
                    Recursive = false,
                    Mode = Models.ScanFolderMode.OnStartup,
                };

                Preferences.Instance.ScanFolders.Add(folder);
                var item = CreateItem(folder);
                Folders.Add(item);
                added.Add(item);
            }

            if (added.Count == 0)
                return;

            Preferences.Instance.Save();
            RepositoryScanner.Instance.RefreshWatchers();
            OnPropertyChanged(nameof(IsEmpty));

            _ = ScanAddedAsync(added);
        }

        public void RemoveFolder(ScanFolderItem item)
        {
            if (item == null)
                return;

            item.Changed -= OnItemChanged;
            item.ModeChanged -= OnItemModeChanged;

            Preferences.Instance.ScanFolders.Remove(item.Folder);
            Preferences.Instance.Save();
            Folders.Remove(item);
            RepositoryScanner.Instance.RefreshWatchers();

            if (Selected == item)
                Selected = null;

            OnPropertyChanged(nameof(IsEmpty));
        }

        public async Task ScanNowAsync(ScanFolderItem item)
        {
            if (item == null)
                return;

            Status = string.Empty;
            var result = await RepositoryScanner.Instance.ScanFolderAsync(item.Folder);
            if (result.Queued)
                return;

            if (result.Canceled)
            {
                Status = App.Text("ScanFolders.Canceled");
                return;
            }

            if (!result.FolderExists)
                Status = App.Text("ScanFolders.FolderMissing", item.Path);
            else
                Status = App.Text("ScanFolders.Result", result.Added);
        }

        public async Task ScanAllNowAsync()
        {
            Status = string.Empty;
            var result = await RepositoryScanner.Instance.ScanAllAsync();
            if (result.Canceled)
            {
                Status = App.Text("ScanFolders.Canceled");
                return;
            }

            Status = App.Text("ScanFolders.Result", result.Added);
        }

        public void CancelScan()
        {
            RepositoryScanner.Instance.CancelAllScans();
            Status = App.Text("ScanFolders.Canceled");
        }

        private ScanFolderItem CreateItem(Models.ScanFolder folder)
        {
            var item = new ScanFolderItem(folder);
            item.Changed += OnItemChanged;
            item.ModeChanged += OnItemModeChanged;
            return item;
        }

        private void OnItemChanged(ScanFolderItem item)
        {
            Preferences.Instance.Save();
            RepositoryScanner.Instance.RefreshWatchers();
        }

        private void OnItemModeChanged(ScanFolderItem item)
        {
            if (item.Folder.Mode == Models.ScanFolderMode.Watch)
                _ = ScanNowAsync(item);
        }

        private async Task ScanAddedAsync(List<ScanFolderItem> items)
        {
            foreach (var item in items)
                await ScanNowAsync(item);
        }

        private static bool SamePath(string left, string right)
        {
            return OperatingSystem.IsLinux()
                ? left == right
                : string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private void OnScannerStateChanged()
        {
            OnPropertyChanged(nameof(IsScanning));
            OnPropertyChanged(nameof(ProgressDescription));
        }

        private ScanFolderItem _selected = null;
        private string _status = string.Empty;
    }
}
