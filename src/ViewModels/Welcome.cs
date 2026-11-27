using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class WelcomeTagItem
    {
        public string Key
        {
            get;
        }

        public string DisplayName
        {
            get;
        }

        public int Count
        {
            get;
        }

        public bool IsAll
        {
            get => Key == null;
        }

        public WelcomeTagItem(string key, string displayName, int count)
        {
            Key = key;
            DisplayName = displayName;
            Count = count;
        }
    }

    public class Welcome : ObservableObject
    {
        public static Welcome Instance { get; } = new();

        public AvaloniaList<RepositoryListRow> Rows
        {
            get;
            private set;
        } = [];

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    Refresh();
            }
        }

        public AvaloniaList<WelcomeTagItem> TagItems
        {
            get;
        } = [];

        public WelcomeTagItem SelectedTagItem
        {
            get => _selectedTagItem;
            set
            {
                if (_isRefreshing)
                    return;

                if (SetProperty(ref _selectedTagItem, value))
                    Refresh();
            }
        }

        public Welcome()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (_isRefreshing)
                return;

            _isRefreshing = true;
            try
            {
                RebuildTagItems();

                IEnumerable<RepositoryNode> nodes = Preferences.Instance.RepositoryNodes
                    .Where(x => x.IsRepository && x.MatchesSearch(_searchFilter));

                var key = _selectedTagItem?.Key;
                if (key != null)
                {
                    nodes = nodes.Where(x => string.Equals(x.PrimaryTag ?? string.Empty,
                        key, StringComparison.OrdinalIgnoreCase));
                }

                var desired = RepositoryListSorter.Build(nodes.ToList(), null, true)
                    .Where(x => !x.IsGroupHeader)
                    .ToList();
                RepositoryListSorter.Sync(Rows, desired);
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private void RebuildTagItems()
        {
            var selectedKey = _selectedTagItem?.Key;
            var nodes = Preferences.Instance.RepositoryNodes
                .Where(x => x.IsRepository)
                .ToList();

            var groups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in nodes)
            {
                var key = node.PrimaryTag ?? string.Empty;
                groups[key] = groups.TryGetValue(key, out var count) ? count + 1 : 1;
            }

            var items = new List<WelcomeTagItem>
            {
                new(null, App.Text("Welcome.AllRepositories"), nodes.Count)
            };

            foreach (var key in groups.Keys
                .Where(x => !string.IsNullOrEmpty(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                items.Add(new WelcomeTagItem(key, $"#{key}", groups[key]));
            }

            if (groups.TryGetValue(string.Empty, out var noTagCount))
                items.Add(new WelcomeTagItem(string.Empty, App.Text("RepositoryTags.NoPrimary"), noTagCount));

            var target = items.FirstOrDefault(x => string.Equals(x.Key, selectedKey, StringComparison.Ordinal))
                ?? items[0];

            TagItems.Clear();
            foreach (var item in items)
                TagItems.Add(item);

            if (!ReferenceEquals(target, _selectedTagItem))
            {
                _selectedTagItem = target;
                OnPropertyChanged(nameof(SelectedTagItem));
            }
        }

        public async Task UpdateStatusAsync(bool force, CancellationToken? token)
        {
            if (_isUpdatingStatus)
                return;

            _isUpdatingStatus = true;

            try
            {
                // Rows can be rebuilt by tag changes, sorting or scanning while statuses update.
                var nodes = Rows
                    .Select(x => x.Node)
                    .Where(x => x != null)
                    .ToArray();
                await RepositoryNode.UpdateStatusesAsync(nodes, force, token);
            }
            finally
            {
                _isUpdatingStatus = false;
            }
        }

        public async Task<Commands.Command.Result> GetRepositoryRootAsync(string path)
        {
            if (!Preferences.Instance.IsGitConfigured())
                return Commands.Command.Result.Failed(App.Text("NotConfigured"));

            var isBare = await new Commands.IsBareRepository(path).GetResultAsync();
            if (isBare)
                return new Commands.Command.Result { IsSuccess = true, StdOut = path };

            return await new Commands.QueryRepositoryRootPath(path).GetResultAsync();
        }

        public async Task AddRepositoryAsync(string path, bool open)
        {
            var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(path);
            await node.UpdateStatusAsync(false, null);

            if (open)
                node.Open();
        }

        public async Task ImportDroppedPathsAsync(IReadOnlyList<string> paths, LauncherPage page)
        {
            var refresh = false;
            foreach (var path in paths)
            {
                var root = path;
                if (!Directory.Exists(root))
                {
                    if (File.Exists(root))
                        root = Path.GetDirectoryName(root);
                    else
                        continue;
                }

                if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                    continue;

                var result = await GetRepositoryRootAsync(root);
                if (!result.IsSuccess)
                {
                    if (page != null &&
                        page.CanCreatePopup() &&
                        Models.SafeDirectories.IsUntrustedRepository(result.StdErr) &&
                        Models.SafeDirectories.TryGetSafeDirectoryValue(root, result.StdErr, out var safeDirectory))
                    {
                        page.Popup = new TrustRepository(page.Node.Id, root, result.StdErr, safeDirectory, false);
                        break;
                    }

                    continue;
                }

                await AddRepositoryAsync(result.StdOut.Trim(), false);
                refresh = true;
            }

            if (refresh)
                RepositoryListSorter.NotifyChanged();
        }

        public void Clone()
        {
            if (!Preferences.Instance.IsGitConfigured())
            {
                Models.Notification.Send(null, App.Text("NotConfigured"), true);
                return;
            }

            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new Clone(activePage.Node.Id);
        }

        public void OpenLocalRepository()
        {
            if (!Preferences.Instance.IsGitConfigured())
            {
                Models.Notification.Send(null, App.Text("NotConfigured"), true);
                return;
            }

            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new OpenLocalRepository(activePage.Node.Id);
        }

        public void OpenTerminal()
        {
            if (!Preferences.Instance.IsGitConfigured())
                Models.Notification.Send(null, App.Text("NotConfigured"), true);
            else
                Native.OS.OpenTerminal(null);
        }

        public async void ManageScanFolders()
        {
            if (!Preferences.Instance.IsGitConfigured())
            {
                Models.Notification.Send(null, App.Text("NotConfigured"), true);
                return;
            }

            await App.OpenScanFoldersAsync();
        }

        public void ClearSearchFilter()
        {
            SearchFilter = string.Empty;
        }

        private string _searchFilter = string.Empty;
        private bool _isUpdatingStatus = false;
        private bool _isRefreshing = false;
        private WelcomeTagItem _selectedTagItem;
    }
}
