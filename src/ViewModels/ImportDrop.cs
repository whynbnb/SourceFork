using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public enum ImportDropState
    {
        Checking,
        Ready,
        Managed,
        NotGit,
        Missing,
    }

    public class ImportDropItem : ObservableObject
    {
        public string SourcePath
        {
            get;
        }

        public string Name
        {
            get;
        }

        public string DisplayPath
        {
            get;
        }

        public ImportDropState State
        {
            get => _state;
            set
            {
                if (!SetProperty(ref _state, value))
                    return;

                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(CanSelect));
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool IsPinned
        {
            get => _isPinned;
            set => SetProperty(ref _isPinned, value);
        }

        public bool IsReady
        {
            get => _state == ImportDropState.Ready;
        }

        public bool CanSelect
        {
            get => _state == ImportDropState.Ready;
        }

        public string StatusText
        {
            get => _state switch
            {
                ImportDropState.Ready => App.Text("Import.Ready"),
                ImportDropState.Managed => App.Text("Import.Managed"),
                ImportDropState.NotGit => App.Text("Import.NotGit"),
                ImportDropState.Missing => App.Text("Import.Missing"),
                _ => App.Text("Import.Checking"),
            };
        }

        public ImportDropItem(string path)
        {
            SourcePath = path;

            var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Name = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(Name))
                Name = path;

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.Ordinal))
                DisplayPath = "~" + path[home.Length..];
            else
                DisplayPath = path;
        }

        public void SetState(ImportDropState state, bool selected)
        {
            State = state;
            IsSelected = selected;
        }

        private ImportDropState _state = ImportDropState.Checking;
        private bool _isSelected = true;
        private bool _isPinned;
    }

    public class ImportDropSession : ObservableObject
    {
        public AvaloniaList<ImportDropItem> Items
        {
            get;
        } = [];

        public bool IsActive
        {
            get => _isActive;
            private set => SetProperty(ref _isActive, value);
        }

        public bool IsPinned
        {
            get => _isPinned;
            private set => SetProperty(ref _isPinned, value);
        }

        public bool HasItems
        {
            get => Items.Count > 0;
        }

        public bool CanImport
        {
            get => Items.Any(x => x.CanSelect && x.IsSelected);
        }

        public string Summary
        {
            get
            {
                if (Items.Count == 0)
                    return App.Text("Import.Checking");

                var ready = Items.Count(x => x.State == ImportDropState.Ready);
                var managed = Items.Count(x => x.State == ImportDropState.Managed);
                return App.Text("Import.Summary", ready, managed);
            }
        }

        public IReadOnlyList<string> SelectedPaths
        {
            get => Items
                .Where(x => x.CanSelect && x.IsSelected)
                .Select(x => x.SourcePath)
                .ToList();
        }

        public void Show(IEnumerable<string> paths)
        {
            var list = paths?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];

            if (list.Count == 0)
            {
                End();
                return;
            }

            var key = string.Join("\n", list.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            if (IsActive && !IsPinned && string.Equals(key, _key, StringComparison.Ordinal))
                return;

            _key = key;
            var version = ++_version;

            foreach (var item in Items)
                item.PropertyChanged -= OnItemPropertyChanged;

            Items.Clear();
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(CanImport));

            IsPinned = false;
            IsActive = true;
            _inspectTask = BuildAsync(list, version);
        }

        public void Pin()
        {
            if (!IsActive)
                return;

            IsPinned = true;
            foreach (var item in Items)
                item.IsPinned = true;
        }

        public Task EnsureInspectedAsync()
        {
            return _inspectTask ?? Task.CompletedTask;
        }

        public void SetAllSelected(bool selected)
        {
            foreach (var item in Items.Where(x => x.CanSelect))
                item.IsSelected = selected;
        }

        public void Hide()
        {
            _key = string.Empty;
            IsPinned = false;
            IsActive = false;
        }

        public void End()
        {
            _version++;
            _key = string.Empty;
            IsPinned = false;

            foreach (var item in Items)
                item.PropertyChanged -= OnItemPropertyChanged;

            if (Items.Count > 0)
            {
                Items.Clear();
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(Summary));
                OnPropertyChanged(nameof(CanImport));
            }

            _inspectTask = null;
            IsActive = false;
        }

        private async Task BuildAsync(IReadOnlyList<string> sources, int version)
        {
            foreach (var source in sources)
            {
                if (version != _version)
                    return;

                await AddSourceAsync(source, version);
            }
        }

        private async Task AddSourceAsync(string source, int version)
        {
            try
            {
                var dir = source;
                if (!Directory.Exists(dir))
                {
                    if (!File.Exists(dir))
                    {
                        AddItem(source, ImportDropState.Missing, false);
                        return;
                    }

                    dir = Path.GetDirectoryName(dir);
                }

                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    AddItem(source, ImportDropState.Missing, false);
                    return;
                }

                var result = await Welcome.Instance.GetRepositoryRootAsync(dir);
                if (version != _version)
                    return;

                if (result.IsSuccess)
                {
                    AddRepo(result.StdOut.Trim());
                    return;
                }

                // A folder that contains several repositories (a workspace) is expanded so
                // each repository can be imported separately.
                var children = ScanChildren(dir);
                if (version != _version)
                    return;

                if (children.Count == 0)
                {
                    AddItem(dir, ImportDropState.NotGit, false);
                    return;
                }

                foreach (var child in children)
                {
                    if (version != _version)
                        return;

                    AddRepo(child);
                }
            }
            catch
            {
                AddItem(source, ImportDropState.Missing, false);
            }
        }

        private void AddRepo(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var normalized = Normalize(path);
            if (Items.Any(x => string.Equals(Normalize(x.SourcePath), normalized, StringComparison.OrdinalIgnoreCase)))
                return;

            var managed = IsManaged(normalized);
            AddItem(path, managed ? ImportDropState.Managed : ImportDropState.Ready, !managed);
        }

        private void AddItem(string path, ImportDropState state, bool selected)
        {
            var item = new ImportDropItem(path);
            item.PropertyChanged += OnItemPropertyChanged;
            item.SetState(state, selected);
            item.IsPinned = IsPinned;

            Items.Add(item);
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(CanImport));
        }

        private void OnItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ImportDropItem.IsSelected))
                OnPropertyChanged(nameof(CanImport));
        }

        private static List<string> ScanChildren(string dir)
        {
            var results = new List<string>();
            try
            {
                foreach (var child in Directory.EnumerateDirectories(dir))
                {
                    var name = Path.GetFileName(child);
                    if (name.StartsWith('.'))
                        continue;

                    var git = Path.Combine(child, ".git");
                    if (!Directory.Exists(git) && !File.Exists(git))
                        continue;

                    results.Add(child);
                    if (results.Count >= 100)
                        break;
                }
            }
            catch
            {
                // Ignore unreadable folders.
            }

            results.Sort((l, r) => string.Compare(l, r, StringComparison.OrdinalIgnoreCase));
            return results;
        }

        private static bool IsManaged(string normalizedPath)
        {
            return Preferences.Instance.RepositoryNodes.Any(x =>
                x.IsRepository &&
                !string.IsNullOrEmpty(x.Id) &&
                string.Equals(Normalize(x.Id), normalizedPath, StringComparison.OrdinalIgnoreCase));
        }

        private static string Normalize(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private string _key = string.Empty;
        private int _version;
        private bool _isActive;
        private bool _isPinned;
        private Task _inspectTask;
    }
}
