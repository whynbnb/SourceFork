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

        public bool HasSource
        {
            get;
        }

        public string SourceFolder
        {
            get;
        }

        public string SourceText
        {
            get => HasSource
                ? App.Text("RepositoryHistory.Source", SourceFolder)
                : App.Text("RepositoryHistory.UnknownSource");
        }

        public string ActionText
        {
            get => HasSource
                ? App.Text("RepositoryHistory.Exclude")
                : App.Text("RepositoryHistory.Remove");
        }

        public RepositoryHistoryItem(string path, RepositoryNode node, bool isHidden, string sourceFolder)
        {
            Path = path;
            Name = node?.Name ?? System.IO.Path.GetFileName(path);
            Node = node;
            IsHidden = isHidden;
            SourceFolder = sourceFolder ?? string.Empty;
            HasSource = !string.IsNullOrEmpty(SourceFolder);
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

        public int UnknownCount
        {
            get => _unknownCount;
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

        public bool HasUnknown
        {
            get => _unknownCount > 0;
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
            var sources = BuildSourceMap();

            Repositories.Clear();
            HiddenRepositories.Clear();

            _unknownCount =
                Preferences.Instance.RepositoryNodes.Count(x => x.IsRepository && string.IsNullOrEmpty(FindSource(sources, x.Id))) +
                Preferences.Instance.HiddenRepositories.Count(x => string.IsNullOrEmpty(FindSource(sources, x)));

            foreach (var node in Preferences.Instance.RepositoryNodes)
            {
                if (!node.IsRepository || !node.MatchesSearch(filter))
                    continue;

                var source = FindSource(sources, node.Id);
                Repositories.Add(new RepositoryHistoryItem(node.Id, node, false, source));
            }

            foreach (var path in Preferences.Instance.HiddenRepositories)
            {
                if (!string.IsNullOrEmpty(filter) &&
                    !path.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                HiddenRepositories.Add(new RepositoryHistoryItem(path, null, true, FindSource(sources, path)));
            }

            OnPropertyChanged(nameof(RepositoryCount));
            OnPropertyChanged(nameof(HiddenCount));
            OnPropertyChanged(nameof(UnknownCount));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(IsHiddenEmpty));
            OnPropertyChanged(nameof(HasHidden));
            OnPropertyChanged(nameof(HasUnknown));
        }

        public void Exclude(RepositoryHistoryItem item)
        {
            if (item?.Node is not { } node)
                return;

            var launcher = App.GetLauncher();
            if (launcher != null)
            {
                launcher.RemoveRepositoryFromList(node, -1, item.HasSource);
            }
            else
            {
                Preferences.Instance.RemoveNode(node, false);

                if (item.HasSource)
                    Preferences.Instance.HideRepository(node.Id, false);
                else
                    Preferences.Instance.RemoveScannedRepository(node.Id, false);

                Preferences.Instance.Save();
                RepositoryListSorter.NotifyChanged();
            }

            Status = item.HasSource
                ? App.Text("RepositoryHistory.ExcludedOne", node.Name)
                : App.Text("RepositoryHistory.RemovedOne", node.Name);

            Reload();
        }

        public void ExcludeAll()
        {
            var nodes = Repositories
                .Where(x => x.Node != null)
                .Select(x => x.Node)
                .ToList();

            if (nodes.Count == 0)
                return;

            var launcher = App.GetLauncher();
            if (launcher != null)
            {
                launcher.RemoveRepositoriesFromList(nodes);
            }
            else
            {
                foreach (var node in nodes)
                {
                    var hasSource = Preferences.Instance.HasScannedSource(node.Id);
                    Preferences.Instance.RemoveNode(node, false);

                    if (hasSource)
                        Preferences.Instance.HideRepository(node.Id, false);
                    else
                        Preferences.Instance.RemoveScannedRepository(node.Id, false);
                }

                Preferences.Instance.Save();
                RepositoryListSorter.NotifyChanged();
            }

            Status = App.Text("RepositoryHistory.ExcludedAll", nodes.Count);
            Reload();
        }

        public void CleanUnknown()
        {
            var sources = BuildSourceMap();
            var unknownNodes = Preferences.Instance.RepositoryNodes
                .Where(x => x.IsRepository && string.IsNullOrEmpty(FindSource(sources, x.Id)))
                .ToList();

            var hiddenUnknown = Preferences.Instance.HiddenRepositories
                .Where(x => string.IsNullOrEmpty(FindSource(sources, x)))
                .ToList();

            if (unknownNodes.Count == 0 && hiddenUnknown.Count == 0)
                return;

            var launcher = App.GetLauncher();
            if (launcher != null && unknownNodes.Count > 0)
            {
                launcher.RemoveRepositoriesFromList(unknownNodes, false);
            }
            else if (unknownNodes.Count > 0)
            {
                foreach (var node in unknownNodes)
                    Preferences.Instance.RemoveNode(node, false);
            }

            foreach (var path in hiddenUnknown)
                Preferences.Instance.HiddenRepositories.RemoveAll(x => Preferences.IsSamePath(x, path));

            Preferences.Instance.Save();
            RepositoryListSorter.NotifyChanged();

            Status = App.Text("RepositoryHistory.RemovedUnknown", unknownNodes.Count + hiddenUnknown.Count);
            Reload();
        }

        public void RestoreAll()
        {
            var paths = HiddenRepositories.Select(x => x.Path).ToList();
            if (paths.Count == 0)
                return;

            var restored = 0;
            foreach (var path in paths)
            {
                if (!Directory.Exists(path))
                    continue;

                var existed = Preferences.Instance.FindNode(path) != null;
                var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(path, false);
                if (node != null && !existed)
                    restored++;
            }

            Preferences.Instance.HiddenRepositories.Clear();
            Preferences.Instance.Save();
            RepositoryListSorter.NotifyChanged();

            Status = App.Text("RepositoryHistory.RestoredAll", restored);
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

        private static Dictionary<string, string> BuildSourceMap()
        {
            var comparer = OperatingSystem.IsLinux()
                ? StringComparer.Ordinal
                : StringComparer.OrdinalIgnoreCase;

            var sources = new Dictionary<string, string>(comparer);
            foreach (var entry in Preferences.Instance.ScannedRepositories)
            {
                if (!string.IsNullOrEmpty(entry.Path))
                    sources[RepositoryScanner.NormalizePath(entry.Path)] = entry.ScanFolder;
            }

            return sources;
        }

        private static string FindSource(Dictionary<string, string> sources, string path)
        {
            return sources.TryGetValue(RepositoryScanner.NormalizePath(path), out var folder)
                ? folder
                : string.Empty;
        }

        private string _searchFilter = string.Empty;
        private string _status = string.Empty;
        private int _unknownCount = 0;
    }
}
