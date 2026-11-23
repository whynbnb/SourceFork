using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;

namespace SourceFork.ViewModels
{
    public class ScanFolderResult
    {
        public int Added { get; set; } = 0;
        public int Untrusted { get; set; } = 0;
        public bool FolderExists { get; set; } = false;
        public bool Queued { get; set; } = false;
    }

    /// <summary>
    /// Discovers repositories in the configured scan folders and keeps real-time watchers alive.
    /// All public methods are expected to be called from the UI thread.
    /// </summary>
    public class RepositoryScanner
    {
        public static RepositoryScanner Instance { get; } = new();

        public event Action StateChanged;

        public bool IsScanning
        {
            get => _activeFolders.Count > 0;
        }

        public string ProgressDescription
        {
            get => _progressDescription;
            private set
            {
                if (_progressDescription == value)
                    return;

                _progressDescription = value;
                StateChanged?.Invoke();
            }
        }

        public void Start()
        {
            if (_started)
                return;

            _started = true;
            Dispatcher.UIThread.Post(async () =>
            {
                RefreshWatchers();
                await ScanAllAsync(folder => folder.Mode is Models.ScanFolderMode.OnStartup or Models.ScanFolderMode.Watch);
            });
        }

        public void Stop()
        {
            foreach (var debounce in _debounces.Values)
            {
                debounce.Cancel();
                debounce.Dispose();
            }

            _debounces.Clear();
            DisposeWatchers();
            _started = false;
        }

        public void RefreshWatchers()
        {
            DisposeWatchers();

            foreach (var folder in Preferences.Instance.ScanFolders)
            {
                if (folder.Mode != Models.ScanFolderMode.Watch)
                    continue;

                if (string.IsNullOrWhiteSpace(folder.Path) || !Directory.Exists(folder.Path))
                    continue;

                try
                {
                    var watcher = new FileSystemWatcher(folder.Path)
                    {
                        IncludeSubdirectories = folder.Recursive,
                        NotifyFilter = NotifyFilters.DirectoryName,
                        InternalBufferSize = 64 * 1024,
                    };

                    watcher.Created += (_, _) => ScheduleScan(folder.Path);
                    watcher.Deleted += (_, _) => ScheduleScan(folder.Path);
                    watcher.Renamed += (_, _) => ScheduleScan(folder.Path);
                    watcher.Error += (_, _) => ScheduleScan(folder.Path);
                    watcher.EnableRaisingEvents = true;

                    _watchers[folder.Path] = watcher;
                }
                catch
                {
                    // Ignore folders we cannot watch (permissions, removed drives, ...).
                }
            }
        }

        public async Task<ScanFolderResult> ScanFolderAsync(Models.ScanFolder folder)
        {
            var result = new ScanFolderResult();
            if (folder == null || string.IsNullOrWhiteSpace(folder.Path))
                return result;

            var key = NormalizePath(folder.Path);
            if (!_activeFolders.Add(key))
            {
                // A scan is already running for this folder. Run it again once it finishes
                // so changes made while scanning are not missed.
                _pendingFolders.Add(key);
                result.FolderExists = Directory.Exists(folder.Path);
                result.Queued = true;
                return result;
            }

            StateChanged?.Invoke();
            try
            {
                result.FolderExists = Directory.Exists(folder.Path);
                if (result.FolderExists)
                {
                    ProgressDescription = App.Text("ScanFolders.Scanning", folder.Path);
                    var found = await FindRepositoriesAsync(folder);
                    result.Untrusted = _untrusted;
                    result.Added = await RegisterAsync(found);
                }
            }
            catch (Exception e)
            {
                Models.Notification.Send(null, e.Message, true);
            }
            finally
            {
                _activeFolders.Remove(key);
                ProgressDescription = string.Empty;
                StateChanged?.Invoke();
            }

            if (_pendingFolders.Remove(key))
            {
                var latest = FindFolder(key);
                if (latest != null)
                    _ = ScanFolderAsync(latest);
            }

            return result;
        }

        public async Task<ScanFolderResult> ScanAllAsync(Func<Models.ScanFolder, bool> predicate = null)
        {
            var result = new ScanFolderResult();
            var folders = Preferences.Instance.ScanFolders.ToList();
            foreach (var folder in folders)
            {
                if (predicate != null && !predicate(folder))
                    continue;

                var one = await ScanFolderAsync(folder);
                result.Added += one.Added;
                result.Untrusted += one.Untrusted;
                result.FolderExists |= one.FolderExists;
            }

            if (result.Untrusted > 0)
                Models.Notification.Send(null, App.Text("TrustRepository.ScanSkipped", result.Untrusted));

            return result;
        }

        private async Task<int> RegisterAsync(List<string> found)
        {
            var added = 0;
            foreach (var path in found)
            {
                var normalized = NormalizePath(path);
                var existed = Preferences.Instance.FindNode(normalized) != null;
                var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(normalized, false);
                if (!existed)
                {
                    added++;
                    await node.UpdateStatusAsync(false, null);
                }
            }

            if (added > 0)
            {
                Preferences.Instance.Save();
                Welcome.Instance.Refresh();
            }

            return added;
        }

        private async Task<List<string>> FindRepositoriesAsync(Models.ScanFolder folder)
        {
            var found = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            _untrusted = 0;

            var root = new DirectoryInfo(folder.Path);
            var options = new EnumerationOptions()
            {
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                IgnoreInaccessible = true,
                RecurseSubdirectories = false,
            };

            var rootGitDir = Path.Combine(root.FullName, ".git");
            if (Directory.Exists(rootGitDir) || File.Exists(rootGitDir))
            {
                var rootPath = await QueryRepositoryAsync(root.FullName);
                if (!string.IsNullOrEmpty(rootPath))
                {
                    found.Add(rootPath);
                    seen.Add(NormalizePath(rootPath));
                }
            }

            await WalkAsync(root, folder.Recursive, found, seen, options);
            return found;
        }

        private async Task WalkAsync(DirectoryInfo dir,
            bool recursive,
            List<string> found,
            HashSet<string> seen,
            EnumerationOptions options)
        {
            DirectoryInfo[] subdirs;
            try
            {
                subdirs = dir.GetDirectories("*", options);
            }
            catch
            {
                return;
            }

            foreach (var subdir in subdirs)
            {
                if (subdir.Name.StartsWith(".", StringComparison.Ordinal) ||
                    subdir.Name.Equals("node_modules", StringComparison.Ordinal) ||
                    subdir.Name.Equals("Library", StringComparison.Ordinal))
                    continue;

                if ((subdir.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                var normalized = NormalizePath(subdir.FullName);
                if (Preferences.Instance.FindNode(normalized) != null)
                    continue;

                var gitDir = Path.Combine(subdir.FullName, ".git");
                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    var repoPath = await QueryRepositoryAsync(subdir.FullName);
                    if (!string.IsNullOrEmpty(repoPath) && seen.Add(NormalizePath(repoPath)))
                        found.Add(repoPath);

                    continue;
                }

                if (await new Commands.IsBareRepository(subdir.FullName).GetResultAsync())
                {
                    if (seen.Add(normalized))
                        found.Add(normalized);

                    continue;
                }

                if (recursive)
                    await WalkAsync(subdir, true, found, seen, options);
            }
        }

        private async Task<string> QueryRepositoryAsync(string path)
        {
            var test = await new Commands.QueryRepositoryRootPath(path).GetResultAsync();
            if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                return NormalizePath(test.StdOut.Trim());

            if (Models.SafeDirectories.IsUntrustedRepository(test.StdErr))
                _untrusted++;

            return null;
        }

        private void ScheduleScan(string folderPath)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var key = NormalizePath(folderPath);
                if (_debounces.TryGetValue(key, out var old))
                {
                    old.Cancel();
                    old.Dispose();
                }

                var cts = new CancellationTokenSource();
                _debounces[key] = cts;

                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1500, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        _debounces.Remove(key);
                        var folder = FindFolder(key);
                        if (folder?.Mode == Models.ScanFolderMode.Watch)
                            _ = ScanFolderAsync(folder);
                    });
                });
            });
        }

        private Models.ScanFolder FindFolder(string normalizedPath)
        {
            foreach (var folder in Preferences.Instance.ScanFolders)
            {
                if (NormalizePath(folder.Path) == normalizedPath)
                    return folder;
            }

            return null;
        }

        private void DisposeWatchers()
        {
            foreach (var watcher in _watchers.Values)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch
                {
                    // Ignore.
                }
            }

            _watchers.Clear();
        }

        public static string NormalizePath(string path)
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }

        private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CancellationTokenSource> _debounces = new(StringComparer.Ordinal);
        private readonly HashSet<string> _activeFolders = new(StringComparer.Ordinal);
        private readonly HashSet<string> _pendingFolders = new(StringComparer.Ordinal);
        private string _progressDescription = string.Empty;
        private int _untrusted = 0;
        private bool _started = false;
    }
}
