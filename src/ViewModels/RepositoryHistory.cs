using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia.Collections;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class RepositoryHistoryItem
    {
        public string Path
        {
            get;
        }

        public string Name
        {
            get;
        }

        public RepositoryNode Node
        {
            get;
        }

        public bool IsHidden
        {
            get;
        }

        public RepositoryHistoryItem(string path, RepositoryNode node, bool isHidden)
        {
            Path = path;
            Name = node?.Name ?? System.IO.Path.GetFileName(path);
            Node = node;
            IsHidden = isHidden;
        }
    }

    public class RepositoryHistory : ObservableObject
    {
        public AvaloniaList<RepositoryHistoryItem> Repositories
        {
            get;
        } = [];

        public AvaloniaList<RepositoryHistoryItem> HiddenRepositories
        {
            get;
        } = [];

        public string SearchFilter
        {
            get => _searchFilter;
            set
            {
                if (SetProperty(ref _searchFilter, value))
                    Reload();
            }
        }

        public int RepositoryCount
        {
            get => Repositories.Count;
        }

        public int HiddenCount
        {
            get => HiddenRepositories.Count;
        }

        public bool IsEmpty
        {
            get => Repositories.Count == 0;
        }

        public bool IsHiddenEmpty
        {
            get => HiddenRepositories.Count == 0;
        }

        public bool HasHidden
        {
            get => HiddenRepositories.Count > 0;
        }

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public RepositoryHistory()
        {
            Reload();
        }

        public void Reload()
        {
            var filter = _searchFilter?.Trim();
            Repositories.Clear();
            HiddenRepositories.Clear();

            foreach (var node in Preferences.Instance.RepositoryNodes)
            {
                if (!node.IsRepository || !node.MatchesSearch(filter))
                    continue;

                Repositories.Add(new RepositoryHistoryItem(node.Id, node, false));
            }

            foreach (var path in Preferences.Instance.HiddenRepositories)
            {
                if (!string.IsNullOrEmpty(filter) &&
                    !path.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                HiddenRepositories.Add(new RepositoryHistoryItem(path, null, true));
            }

            OnPropertyChanged(nameof(RepositoryCount));
            OnPropertyChanged(nameof(HiddenCount));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsHiddenEmpty));
            OnPropertyChanged(nameof(HasHidden));
        }

        public void Exclude(RepositoryHistoryItem item)
        {
            if (item?.Node is not { } node)
                return;

            var launcher = App.GetLauncher();
            var page = launcher?.Pages.FirstOrDefault(x => x.Node.Id == node.Id && x.Data is Repository);

            Preferences.Instance.RemoveNode(node, false);
            Preferences.Instance.HideRepository(node.Id, false);
            Preferences.Instance.Save();

            if (page != null)
                launcher.CloseTab(page);

            Status = App.Text("RepositoryHistory.ExcludedOne", node.Name);
            RepositoryListSorter.NotifyChanged();
            Reload();
        }

        public async Task RestoreAsync(RepositoryHistoryItem item)
        {
            if (item == null)
                return;

            Preferences.Instance.ShowRepository(item.Path, false);

            RepositoryNode node = null;
            if (Directory.Exists(item.Path) && Preferences.Instance.FindNode(item.Path) == null)
            {
                node = Preferences.Instance.FindOrAddNodeByRepositoryPath(item.Path, false);
                if (node != null)
                    await node.UpdateStatusAsync(false, null);
            }

            Preferences.Instance.Save();
            RepositoryListSorter.NotifyChanged();

            Status = node != null
                ? App.Text("RepositoryHistory.RestoredOne", node.Name)
                : App.Text("RepositoryHistory.RestoredMissing", item.Path);

            Reload();
        }

        private string _searchFilter = string.Empty;
        private string _status = string.Empty;
    }
}
