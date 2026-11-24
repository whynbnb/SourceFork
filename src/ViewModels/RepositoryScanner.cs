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
        public bool Canceled { get; set; } = false;
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
            CancelAllScans();

            foreach (var debounce in _debounces.Values)
            {
                debounce.Cancel();
                debounce.Dispose();
            }

            _debounces.Clear();
            DisposeWatchers();
            _started = false;
        }

        public void CancelAllScans()
        {
            try
            {
                _scanSession?.Cancel();
            }
            catch
            {
                // Cancellation callbacks may fail; the scans still stop.
            }

            foreach (var cts in _scanCancellations.Values.ToArray())
            {
                try
                {
                    cts.Cancel();
                }
                catch
                {
                    // Already finished.
                }
            }

            _pendingFolders.Clear();
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

        public async Task<ScanFolderResult> ScanFolderAsync(Models.ScanFolder folder, CancellationToken sessionToken = default)
        {
            var result = new ScanFolderResult();
            if (folder == null || string.IsNullOrWhiteSpace(folder.Path))
                return result;

            if (sessionToken.IsCancellationRequested)
            {
                result.Canceled = true;
                return result;
            }

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

            var cancellation = sessionToken.CanBeCanceled
                ? CancellationTokenSource.CreateLinkedTokenSource(sessionToken)
                : new CancellationTokenSource();
            var token = cancellation.Token;
            _scanCancellations[key] = cancellation;

            StateChanged?.Invoke();
            try
            {
                result.FolderExists = Directory.Exists(folder.Path);
                if (result.FolderExists)
                {
                    ProgressDescription = App.Text("ScanFolders.Scanning", folder.Path);
                    var discovery = await FindRepositoriesAsync(folder, token);
                    result.Untrusted = discovery.Untrusted;
                    token.ThrowIfCancellationRequested();
                    result.Added = await RegisterAsync(discovery.Paths, token);
                }
            }
            catch (OperationCanceledException)
            {
                result.Canceled = true;
            }
            catch (Exception e)
            {
                if (!token.IsCancellationRequested)
                    Models.Notification.Send(null, e.Message, true);
            }
            finally
            {
                _activeFolders.Remove(key);
                _scanCancellations.Remove(key);
                cancellation.Dispose();

                if (_activeFolders.Count == 0)
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
            var session = new CancellationTokenSource();
            var token = session.Token;
            _scanSession = session;

            try
            {
                var folders = Preferences.Instance.ScanFolders.ToList();
                foreach (var folder in folders)
                {
                    if (token.IsCancellationRequested)
                    {
                        result.Canceled = true;
                        break;
                    }

                    if (predicate != null && !predicate(folder))
                        continue;

                    var one = await ScanFolderAsync(folder, token);
                    result.Added += one.Added;
                    result.Untrusted += one.Untrusted;
                    result.FolderExists |= one.FolderExists;
                    result.Canceled |= one.Canceled;

                    if (token.IsCancellationRequested)
                    {
                        result.Canceled = true;
                        break;
                    }
                }
            }
            finally
            {
                if (_scanSession == session)
                    _scanSession = null;

                session.Dispose();
            }

            if (result.Untrusted > 0)
                Models.Notification.Send(null, App.Text("TrustRepository.ScanSkipped", result.Untrusted));

            return result;
        }

        private async Task<int> RegisterAsync(List<string> found, CancellationToken token)
        {
            var added = 0;
            try
            {
                foreach (var path in found)
                {
                    token.ThrowIfCancellationRequested();

                    var normalized = NormalizePath(path);
                    if (Preferences.Instance.IsRepositoryHidden(normalized))
                        continue;

                    var existed = Preferences.Instance.FindNode(normalized) != null;
                    var node = Preferences.Instance.FindOrAddNodeByRepositoryPath(normalized, false);
                    if (!existed)
                    {
                        added++;
                        await node.UpdateStatusAsync(false, token);
                    }
                }
            }
            finally
            {
                if (added > 0)
                    RepositoryListSorter.NotifyChanged();
            }

            return added;
        }

        private async Task<DiscoveryResult> FindRepositoriesAsync(Models.ScanFolder folder, CancellationToken token)
        {
            // Snapshot registered repositories and hidden paths before leaving the UI thread.
            var comparer = OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;
            var managed = new HashSet<string>(comparer);
            foreach (var node in Preferences.Instance.RepositoryNodes)
                managed.Add(NormalizePath(node.Id));

            var hidden = new HashSet<string>(comparer);
            foreach (var path in Preferences.Instance.HiddenRepositories)
                hidden.Add(NormalizePath(path));

            return await Task.Run(async () =>
            {
                var discovery = new DiscoveryResult();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var root = new DirectoryInfo(folder.Path);
                var options = new EnumerationOptions()
                {
                    AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = false,
                };

                token.ThrowIfCancellationRequested();

                var rootNormalized = NormalizePath(root.FullName);
                if (hidden.Contains(rootNormalized))
                    return discovery;

                var rootGitDir = Path.Combine(root.FullName, ".git");
                if (Directory.Exists(rootGitDir) || File.Exists(rootGitDir))
                {
                    var rootPath = await QueryRepositoryAsync(root.FullName, discovery, token);
                    if (!string.IsNullOrEmpty(rootPath))
                    {
                        discovery.Paths.Add(rootPath);
                        seen.Add(NormalizePath(rootPath));
                    }
                }

                await WalkAsync(root, folder.Recursive, discovery, seen, managed, hidden, options, token);
                return discovery;
            }, token);
        }

        private async Task WalkAsync(DirectoryInfo dir,
            bool recursive,
            DiscoveryResult discovery,
            HashSet<string> seen,
            HashSet<string> managed,
            HashSet<string> hidden,
            EnumerationOptions options,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            DirectoryInfo[] subdirs;
            try
            {
                subdirs = dir.GetDirectories("*", options);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return;
            }

            foreach (var subdir in subdirs)
            {
                token.ThrowIfCancellationRequested();

                if (subdir.Name.StartsWith(".", StringComparison.Ordinal) ||
                    subdir.Name.Equals("node_modules", StringComparison.Ordinal))
                    continue;

                if ((subdir.Attributes & FileAttributes.ReparsePoint) != 0)
                    continue;

                var normalized = NormalizePath(subdir.FullName);
                if (managed.Contains(normalized) || hidden.Contains(normalized))
                    continue;

                var gitDir = Path.Combine(subdir.FullName, ".git");
                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    var repoPath = await QueryRepositoryAsync(subdir.FullName, discovery, token);
                    if (!string.IsNullOrEmpty(repoPath) && seen.Add(NormalizePath(repoPath)))
                        discovery.Paths.Add(repoPath);

                    continue;
                }

                var bare = new Commands.IsBareRepository(subdir.FullName) { CancellationToken = token };
                if (await bare.GetResultAsync())
                {
                    if (seen.Add(normalized))
                        discovery.Paths.Add(normalized);

                    continue;
                }

                if (recursive)
                    await WalkAsync(subdir, true, discovery, seen, managed, hidden, options, token);
            }
        }

        private async Task<string> QueryRepositoryAsync(string path, DiscoveryResult discovery, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var query = new Commands.QueryRepositoryRootPath(path) { CancellationToken = token };
            var test = await query.GetResultAsync();
            if (test.IsSuccess && !string.IsNullOrEmpty(test.StdOut))
                return NormalizePath(test.StdOut.Trim());

            if (Models.SafeDirectories.IsUntrustedRepository(test.StdErr))
                discovery.Untrusted++;

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

        private class DiscoveryResult
        {
            public List<string> Paths { get; } = [];
            public int Untrusted { get; set; } = 0;
        }

        private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CancellationTokenSource> _debounces = new(StringComparer.Ordinal);
        private readonly Dictionary<string, CancellationTokenSource> _scanCancellations = new(StringComparer.Ordinal);
        private readonly HashSet<string> _activeFolders = new(StringComparer.Ordinal);
        private readonly HashSet<string> _pendingFolders = new(StringComparer.Ordinal);
        private string _progressDescription = string.Empty;
        private CancellationTokenSource _scanSession = null;
        private bool _started = false;
    }
}
