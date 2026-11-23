using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.IO;

using Avalonia.Collections;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class Launcher : ObservableObject
    {
        public AvaloniaList<LauncherPage> Pages
        {
            get;
            private set;
        }

        public AvaloniaList<RepositoryListRow> VisiblePages
        {
            get;
        } = new();

        public RepositoryListRow SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (!SetProperty(ref _selectedRow, value))
                    return;

                if (value?.Page != null)
                {
                    ActivePage = value.Page;
                }
                else if (value is { IsGroupHeader: true })
                {
                    _selectedRow = VisiblePages.FirstOrDefault(x => x.Page == _activePage);
                    OnPropertyChanged(nameof(SelectedRow));
                }
            }
        }

        public LauncherPage ActivePage
        {
            get => _activePage;
            set
            {
                if (value == null)
                    return;

                if (SetProperty(ref _activePage, value))
                    PostActivePageChanged();
            }
        }

        public string RepositoryFilter
        {
            get => _repositoryFilter;
            set
            {
                if (SetProperty(ref _repositoryFilter, value))
                    RefreshVisiblePages();
            }
        }

        public ICommandPalette CommandPalette
        {
            get => _commandPalette;
            set => SetProperty(ref _commandPalette, value);
        }

        public Models.Version NewVersion
        {
            get => _newVersion;
            set => SetProperty(ref _newVersion, value);
        }

        public Launcher(string startupRepo)
        {
            Models.Notification.Raised += DispatchNotification;
            _ignoreIndexChange = true;

            Pages = new AvaloniaList<LauncherPage>();
            AttachPagesEvents();
            AddNewTab();

            var repos = Preferences.Instance.OpenRepositories.ToArray();
            foreach (var repo in repos)
                OpenRepositoryInTab(repo, null);

            _ignoreIndexChange = false;

            if (!TryOpenRepositoryFromPath(startupRepo))
            {
                var activeRepo = Preferences.Instance.ActiveRepositoryPath;
                var activePage = Pages.FirstOrDefault(x => x.Data is Repository repo && repo.FullPath.Equals(activeRepo, StringComparison.Ordinal));
                ActivePage = activePage ?? Pages[0];
            }

            PostActivePageChanged();
            RefreshVisiblePages();
        }

        public bool TryOpenRepositoryFromPath(string repo)
        {
            if (!string.IsNullOrEmpty(repo) && Directory.Exists(repo))
            {
                var isBare = new Commands.IsBareRepository(repo).GetResult();
                if (isBare)
                {
                    var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(repo, false);
                    Welcome.Instance.Refresh();
                    OpenRepositoryInTab(node, null);
                    return true;
                }

                var test = new Commands.QueryRepositoryRootPath(repo).GetResult();
                if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                {
                    var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(test.StdOut.Trim(), false);
                    Welcome.Instance.Refresh();
                    OpenRepositoryInTab(node, null);
                    return true;
                }
                else
                {
                    if (ActivePage is not { Data: Welcome { }, Popup: null })
                        AddNewTab();

                    if (Models.SafeDirectories.IsUntrustedRepository(test.StdErr) &&
                        Models.SafeDirectories.TryGetSafeDirectoryValue(repo, test.StdErr, out var safeDirectory))
                        ActivePage.Popup = new TrustRepository(ActivePage.Node.Id, repo, test.StdErr, safeDirectory, true, 0);
                    else
                        ActivePage.Popup = new Init(ActivePage.Node.Id, repo, 0, test.StdErr ?? "Unknown error occurred while opening the repository.");

                    return true;
                }
            }

            return false;
        }

        public void CloseAll()
        {
            _ignoreIndexChange = true;

            foreach (var one in Pages)
                CloseRepositoryInTab(one);

            _ignoreIndexChange = false;
        }

        public void AddNewTab()
        {
            var page = new LauncherPage();
            Pages.Add(page);
            ActivePage = page;
        }

        public void MoveTab(LauncherPage from, LauncherPage to)
        {
            _ignoreIndexChange = true;

            var fromIdx = Pages.IndexOf(from);
            var toIdx = Pages.IndexOf(to);
            Pages.Move(fromIdx, toIdx);

            PersistOpenRepositories();

            _ignoreIndexChange = false;
            ActivePage = from;
        }

        public void GotoNextTab()
        {
            if (Pages.Count == 1)
                return;

            var activeIdx = Pages.IndexOf(_activePage);
            var nextIdx = (activeIdx + 1) % Pages.Count;
            ActivePage = Pages[nextIdx];
        }

        public void GotoPrevTab()
        {
            if (Pages.Count == 1)
                return;

            var activeIdx = Pages.IndexOf(_activePage);
            var prevIdx = activeIdx == 0 ? Pages.Count - 1 : activeIdx - 1;
            ActivePage = Pages[prevIdx];
        }

        public void CloseTab(LauncherPage page)
        {
            if (Pages.Count == 1)
            {
                var last = Pages[0];
                if (last.Data is Repository repo)
                {
                    Preferences.Instance.SetOpenRepositories([]);
                    Preferences.Instance.ActiveRepositoryPath = string.Empty;

                    if (last.Node.IsUnmanaged)
                        last.Node.SaveMinimalInfo(repo.GitDir);
                    repo.Close();

                    Welcome.Instance.ClearSearchFilter();
                    last.Node = new RepositoryNode() { Id = Guid.NewGuid().ToString() };
                    last.Data = Welcome.Instance;
                    last.Popup?.Cleanup();
                    last.Popup = null;

                    PostActivePageChanged();
                    GC.Collect();
                }
                else
                {
                    App.Quit(0);
                }

                return;
            }

            page ??= _activePage;

            var removeIdx = Pages.IndexOf(page);
            var activeIdx = Pages.IndexOf(_activePage);
            if (removeIdx == activeIdx)
                ActivePage = Pages[removeIdx > 0 ? removeIdx - 1 : removeIdx + 1];

            CloseRepositoryInTab(page);
            Pages.RemoveAt(removeIdx);
            GC.Collect();
        }

        public void CloseOtherTabs()
        {
            if (Pages.Count == 1)
                return;

            _ignoreIndexChange = true;

            var id = ActivePage.Node.Id;
            foreach (var one in Pages)
            {
                if (one.Node.Id != id)
                    CloseRepositoryInTab(one);
            }

            Pages = new AvaloniaList<LauncherPage> { ActivePage };
            OnPropertyChanged(nameof(Pages));
            AttachPagesEvents();
            RefreshVisiblePages();

            PersistOpenRepositories();
            _ignoreIndexChange = false;
            GC.Collect();
        }

        public void CloseRightTabs()
        {
            _ignoreIndexChange = true;

            var endIdx = Pages.IndexOf(ActivePage);
            for (var i = Pages.Count - 1; i > endIdx; i--)
            {
                CloseRepositoryInTab(Pages[i]);
                Pages.Remove(Pages[i]);
            }

            _ignoreIndexChange = false;
            GC.Collect();
        }

        public void OpenRepositoryInTab(string repo, LauncherPage page)
        {
            var normalizedPath = repo.Replace('\\', '/').TrimEnd('/');
            var node = Preferences.Instance.FindNode(normalizedPath) ?? new RepositoryNode
            {
                Id = normalizedPath,
                Name = Path.GetFileName(normalizedPath),
                Bookmark = 0,
                IsRepository = true,
                IsUnmanaged = true
            };

            OpenRepositoryInTab(node, null);
        }

        public void OpenRepositoryInTab(RepositoryNode node, LauncherPage page)
        {
            foreach (var one in Pages)
            {
                if (one.Node.Id == node.Id)
                {
                    ActivePage = one;
                    return;
                }
            }

            if (!Directory.Exists(node.Id))
            {
                ActivePage.Notifications.Add(new Models.Notification
                {
                    Group = node.Id,
                    Message = "Repository does NOT exist any more. Please remove it.",
                    IsError = true,
                });
                return;
            }

            var isBare = new Commands.IsBareRepository(node.Id).GetResult();
            var gitDir = isBare ? node.Id : GetRepositoryGitDir(node.Id);
            if (string.IsNullOrEmpty(gitDir))
            {
                ActivePage.Notifications.Add(new Models.Notification
                {
                    Group = node.Id,
                    Message = "Given path is not a valid git repository!",
                    IsError = true,
                });
                return;
            }

            if (node.IsUnmanaged)
                node.LoadMinimalInfo(gitDir);

            var repo = new Repository(isBare, node.Id, gitDir);
            repo.Open();

            if (page == null)
            {
                if (_activePage == null || _activePage.Node.IsRepository)
                {
                    page = new LauncherPage(node, repo);
                    Pages.Add(page);
                }
                else
                {
                    page = _activePage;
                    page.Node = node;
                    page.Data = repo;
                }
            }
            else
            {
                page.Node = node;
                page.Data = repo;
            }

            PersistOpenRepositories();

            if (_activePage == page)
                PostActivePageChanged();
            else
                ActivePage = page;
        }

        public void OpenSubRepository(LauncherPage ownerPage, string fullpath)
        {
            var normalizedPath = fullpath.Replace('\\', '/').TrimEnd('/');

            // Check if the sub-repository is already open in any of the tabs
            foreach (var one in Pages)
            {
                if (one.Node.Id.Equals(normalizedPath, StringComparison.Ordinal))
                {
                    ActivePage = one;
                    return;
                }
            }

            // Make sure the target directory exists
            if (!Directory.Exists(normalizedPath))
            {
                ownerPage.Notifications.Add(new Models.Notification
                {
                    Group = ownerPage.Node.Id,
                    Message = "Repository path does NOT exist. Please check the path.",
                    IsError = true,
                });

                return;
            }

            // Check if the target directory is a valid git repository
            var gitDir = GetRepositoryGitDir(normalizedPath);
            if (string.IsNullOrEmpty(gitDir))
            {
                ownerPage.Notifications.Add(new Models.Notification
                {
                    Group = ownerPage.Node.Id,
                    Message = "Given path is not a valid git repository!",
                    IsError = true,
                });
                return;
            }

            // Get the owner repository's name and extract the pure owner name (without any prefix)
            var ownerName = ownerPage.Node.Name;
            var colonIdx = ownerName.LastIndexOf(':');
            var pureOwnerName = (colonIdx >= 0 && colonIdx < ownerName.Length - 1) ? ownerName.Substring(colonIdx + 1).Trim() : ownerName;

            // Find the sub-repository node in the preferences or create a new one if it doesn't exist
            var node = Preferences.Instance.FindNode(normalizedPath) ?? new RepositoryNode
            {
                Id = normalizedPath,
                Name = $"{pureOwnerName}: {Path.GetFileName(normalizedPath)}",
                Bookmark = 0,
                IsRepository = true,
                IsUnmanaged = true
            };
            node.LoadMinimalInfo(gitDir);

            var repo = new Repository(false, node.Id, gitDir);
            repo.Open();

            var page = new LauncherPage(node, repo);
            var idxOfOwner = Pages.IndexOf(ownerPage);
            Pages.Insert(idxOfOwner + 1, page);
            Preferences.Instance.OpenRepositories.Insert(idxOfOwner + 1, normalizedPath);
            ActivePage = page;
        }

        private void AttachPagesEvents()
        {
            Pages.CollectionChanged -= OnPagesCollectionChanged;
            Pages.CollectionChanged += OnPagesCollectionChanged;
        }

        private void OnPagesCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshVisiblePages();
        }

        private void RefreshVisiblePages()
        {
            var filter = _repositoryFilter?.Trim();
            VisiblePages.Clear();

            var nonRepositories = new List<LauncherPage>();
            var repositories = new List<RepositoryNode>();
            var pagesByNode = new Dictionary<string, LauncherPage>(StringComparer.Ordinal);

            foreach (var page in Pages)
            {
                if (page.Data is not Repository)
                {
                    if (string.IsNullOrEmpty(filter) && _tagFilters.Count == 0)
                        nonRepositories.Add(page);
                    continue;
                }

                if (page.Node.MatchesSearch(filter) && page.Node.HasAllTags(_tagFilters))
                {
                    repositories.Add(page.Node);
                    pagesByNode[page.Node.Id] = page;
                }
            }

            foreach (var page in nonRepositories)
                VisiblePages.Add(new RepositoryListRow(page.Node, page));

            foreach (var row in RepositoryListSorter.Build(repositories, node => pagesByNode.TryGetValue(node.Id, out var page) ? page : null))
                VisiblePages.Add(row);

            SyncSelectedRow();
        }

        private void SyncSelectedRow()
        {
            _selectedRow = VisiblePages.FirstOrDefault(x => x.Page == _activePage);
            OnPropertyChanged(nameof(SelectedRow));
        }

        private void DispatchNotification(Models.Notification notification)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Invoke(() => DispatchNotification(notification));
                return;
            }

            if (string.IsNullOrEmpty(notification.Group))
            {
                _activePage?.Notifications.Add(notification);
                return;
            }

            foreach (var page in Pages)
            {
                var id = page.Node.Id.Replace('\\', '/').TrimEnd('/');
                if (id.Equals(notification.Group, StringComparison.OrdinalIgnoreCase))
                {
                    page.Notifications.Add(notification);
                    return;
                }
            }

            _activePage?.Notifications.Add(notification);
        }

        private string GetRepositoryGitDir(string repo)
        {
            var fullpath = Path.Combine(repo, ".git");
            if (Directory.Exists(fullpath))
            {
                if (Directory.Exists(Path.Combine(fullpath, "refs")) &&
                    Directory.Exists(Path.Combine(fullpath, "objects")) &&
                    File.Exists(Path.Combine(fullpath, "HEAD")))
                    return fullpath;

                return null;
            }

            if (File.Exists(fullpath))
            {
                var redirect = File.ReadAllText(fullpath).Trim();
                if (redirect.StartsWith("gitdir: ", StringComparison.Ordinal))
                    redirect = redirect.Substring(8);

                if (!Path.IsPathRooted(redirect))
                    redirect = Path.GetFullPath(Path.Combine(repo, redirect));

                if (Directory.Exists(redirect))
                    return redirect;

                return null;
            }

            return new Commands.QueryGitDir(repo).GetResult();
        }

        private void CloseRepositoryInTab(LauncherPage page)
        {
            if (page.Data is Repository repo)
            {
                Preferences.Instance.OpenRepositories.Remove(repo.FullPath);

                if (page.Node.IsUnmanaged)
                    page.Node.SaveMinimalInfo(repo.GitDir);

                repo.Close();
            }

            page.Popup?.Cleanup();
            page.Popup = null;
            page.Data = null;
        }

        private void PostActivePageChanged()
        {
            if (_ignoreIndexChange)
                return;

            if (_activePage is { Data: Repository repo })
            {
                Preferences.Instance.ActiveRepositoryPath = repo.FullPath;
                Preferences.Instance.Save();
            }

            SyncSelectedRow();
            CommandPalette = null;
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

        public List<string> GetAvailableTags()
        {
            var tags = new List<string>();
            foreach (var page in Pages)
            {
                if (page.Data is not Repository)
                    continue;

                foreach (var tag in page.Node.Tags)
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
            RefreshVisiblePages();
        }

        public void ClearTagFilters()
        {
            if (_tagFilters.Count == 0)
                return;

            _tagFilters.Clear();
            OnPropertyChanged(nameof(TagFilterCount));
            OnPropertyChanged(nameof(HasTagFilters));
            RefreshVisiblePages();
        }

        public void RefreshRepositoryFilters()
        {
            RefreshVisiblePages();
        }

        private void PersistOpenRepositories()
        {
            var repos = new List<string>();
            foreach (var page in Pages)
            {
                if (page.Data is Repository repo && !repos.Contains(repo.FullPath))
                    repos.Add(repo.FullPath);
            }

            Preferences.Instance.SetOpenRepositories(repos);
        }

        private readonly List<string> _tagFilters = [];
        private RepositoryListRow _selectedRow;
        private LauncherPage _activePage;
        private bool _ignoreIndexChange;
        private string _repositoryFilter = string.Empty;
        private ICommandPalette _commandPalette;
        private Models.Version _newVersion = null;
    }
}
