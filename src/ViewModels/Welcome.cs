using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
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

        public int TagFilterCount
        {
            get => _tagFilters.Count;
        }

        public bool HasTagFilters
        {
            get => _tagFilters.Count > 0;
        }

        public IReadOnlyList<string> ActiveTagFilters
        {
            get => _tagFilters;
        }

        public Welcome()
        {
            Refresh();
        }

        public void Refresh()
        {
            Rows.Clear();

            var nodes = Preferences.Instance.RepositoryNodes
                .Where(x => x.IsRepository && x.MatchesSearch(_searchFilter) && x.HasAllTags(_tagFilters));
            foreach (var row in RepositoryListSorter.Build(nodes))
                Rows.Add(row);
        }

        public async Task UpdateStatusAsync(bool force, CancellationToken? token)
        {
            if (_isUpdatingStatus)
                return;

            _isUpdatingStatus = true;

            foreach (var row in Rows)
            {
                if (row.Node != null)
                    await row.Node.UpdateStatusAsync(force, token);
            }

            _isUpdatingStatus = false;
        }

        public List<string> GetAvailableTags()
        {
            var tags = new List<string>();
            foreach (var node in Preferences.Instance.RepositoryNodes)
            {
                foreach (var tag in node.Tags)
                {
                    if (!tags.Any(x => x.Equals(tag, StringComparison.OrdinalIgnoreCase)))
                        tags.Add(tag);
                }
            }

            tags.Sort((l, r) => string.Compare(l, r, StringComparison.OrdinalIgnoreCase));
            return tags;
        }

        public bool IsTagFiltered(string tag)
        {
            return _tagFilters.Any(x => x.Equals(tag, StringComparison.OrdinalIgnoreCase));
        }

        public void ToggleTagFilter(string tag)
        {
            var exists = _tagFilters.FindIndex(x => x.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (exists >= 0)
                _tagFilters.RemoveAt(exists);
            else
                _tagFilters.Add(tag);

            OnPropertyChanged(nameof(TagFilterCount));
            OnPropertyChanged(nameof(HasTagFilters));
            Refresh();
        }

        public void ClearTagFilters()
        {
            if (_tagFilters.Count == 0)
                return;

            _tagFilters.Clear();
            OnPropertyChanged(nameof(TagFilterCount));
            OnPropertyChanged(nameof(HasTagFilters));
            Refresh();
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
        private readonly List<string> _tagFilters = [];
    }
}
