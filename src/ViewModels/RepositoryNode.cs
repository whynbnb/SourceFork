using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class RepositoryNodeMinimalInfo
    {
        public List<string> Tags { get; set; } = [];
        public string PrimaryTag { get; set; } = string.Empty;
        public long CreateTime { get; set; } = 0;
    }

    public class RepositoryTagItem : ObservableObject
    {
        public string Name
        {
            get;
        }

        public string DisplayName
        {
            get => $"#{Name}";
        }

        public bool IsPrimary
        {
            get => _isPrimary;
            set => SetProperty(ref _isPrimary, value);
        }

        public RepositoryTagItem(string name, bool isPrimary)
        {
            Name = name;
            _isPrimary = isPrimary;
        }

        private bool _isPrimary;
    }

    public class RepositoryNode : ObservableObject
    {
        public string Id
        {
            get => _id;
            set
            {
                var normalized = value.Replace('\\', '/').TrimEnd('/');
                if (SetProperty(ref _id, normalized))
                    OnPropertyChanged(nameof(Name));
            }
        }

        [JsonIgnore]
        public string Name
        {
            get
            {
                var name = Path.GetFileName(_id);
                return string.IsNullOrEmpty(name) ? _id : name;
            }
        }

        public bool IsRepository
        {
            get => _isRepository;
            set => SetProperty(ref _isRepository, value);
        }

        public long CreateTime
        {
            get;
            set;
        } = 0;

        public List<string> Tags
        {
            get => _tags;
            set
            {
                if (SetProperty(ref _tags, NormalizeTags(value)))
                {
                    _primaryTag = NormalizePrimary(_primaryTag);
                    RebuildTagItems();
                }
            }
        }

        public string PrimaryTag
        {
            get => _primaryTag;
            set
            {
                if (SetProperty(ref _primaryTag, NormalizePrimary(value)))
                    RebuildTagItems();
            }
        }

        [JsonIgnore]
        public ObservableCollection<RepositoryTagItem> TagItems
        {
            get;
        } = [];

        [JsonIgnore]
        public bool HasTags
        {
            get => _tags.Count > 0;
        }

        [JsonIgnore]
        public bool HasPrimaryTag
        {
            get => !string.IsNullOrEmpty(_primaryTag);
        }

        [JsonIgnore]
        public string PrimaryTagDisplay
        {
            get => string.IsNullOrEmpty(_primaryTag) ? string.Empty : $"  #{_primaryTag}";
        }

        [JsonIgnore]
        public string SecondaryTagsDisplay
        {
            get => string.Concat(_tags
                .Where(x => !x.Equals(_primaryTag, StringComparison.OrdinalIgnoreCase))
                .Select(x => $"  #{x}"));
        }

        [JsonIgnore]
        public bool HasSecondaryTags
        {
            get => _tags.Any(x => !x.Equals(_primaryTag, StringComparison.OrdinalIgnoreCase));
        }

        [JsonIgnore]
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        [JsonIgnore]
        public bool IsInvalid
        {
            get => _isRepository && !Directory.Exists(_id);
        }

        [JsonIgnore]
        public bool IsUnmanaged
        {
            get;
            set;
        } = false;

        /// <summary>
        /// Legacy group hierarchy loaded from older preference files. It is only used once to
        /// flatten the old tree and is cleared afterwards.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<RepositoryNode> SubNodes
        {
            get;
            set;
        } = null;

        public Models.RepositoryStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public void SetTags(IEnumerable<string> tags, string primary = null)
        {
            _tags = NormalizeTags(tags);
            _primaryTag = NormalizePrimary(primary);
            OnPropertyChanged(nameof(Tags));
            RebuildTagItems();
        }

        public bool AddTag(string tag)
        {
            var normalized = NormalizeTag(tag);
            if (string.IsNullOrEmpty(normalized))
                return false;

            if (_tags.Any(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
                return false;

            _tags.Add(normalized);
            _primaryTag = NormalizePrimary(_primaryTag);
            OnPropertyChanged(nameof(Tags));
            RebuildTagItems();
            return true;
        }

        public void RemoveTag(string tag)
        {
            var index = _tags.FindIndex(x => x.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return;

            _tags.RemoveAt(index);
            _primaryTag = NormalizePrimary(_primaryTag);
            OnPropertyChanged(nameof(Tags));
            RebuildTagItems();
        }

        public bool HasAllTags(IReadOnlyCollection<string> filters)
        {
            if (filters == null || filters.Count == 0)
                return true;

            foreach (var filter in filters)
            {
                if (!_tags.Any(t => t.Equals(filter, StringComparison.OrdinalIgnoreCase)))
                    return false;
            }

            return true;
        }

        public DateTime GetLastUpdatedTime()
        {
            try
            {
                var gitDir = Path.Combine(_id, ".git");
                var target = Directory.Exists(gitDir) ? gitDir : _id;
                return Directory.Exists(target) ? Directory.GetLastWriteTimeUtc(target) : DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        public bool MatchesSearch(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            if ((Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true) ||
                (_id?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true))
                return true;

            return _tags.Any(t => t.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        public void Open()
        {
            if (Directory.Exists(_id))
                App.GetLauncher().OpenRepositoryInTab(this, null);
        }

        public EditRepositoryTags CreateTagEditor()
        {
            return new EditRepositoryTags(this);
        }

        public void OpenInFileManager()
        {
            if (_isRepository && Directory.Exists(_id))
                Native.OS.OpenInFileManager(_id);
        }

        public void OpenTerminal()
        {
            if (_isRepository && Directory.Exists(_id))
                Native.OS.OpenTerminal(_id);
        }

        public void Delete()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new DeleteRepositoryNode(this);
        }

        public async Task UpdateStatusAsync(bool force, CancellationToken? token)
        {
            if (token is { IsCancellationRequested: true })
                return;

            if (!_isRepository || !Directory.Exists(_id))
            {
                _lastUpdateStatus = DateTime.Now;
                Status = null;
                return;
            }

            if (!force)
            {
                var passed = DateTime.Now - _lastUpdateStatus;
                if (passed.TotalSeconds < 10.0)
                    return;
            }

            _lastUpdateStatus = DateTime.Now;

            var cached = Status;
            var status = await new Commands.QueryRepositoryStatus(_id).GetResultAsync().ConfigureAwait(false);
            if (status != null)
            {
                var cachedTime = cached?.LastCommitTime ?? 0;
                if (cachedTime > 0 &&
                    !string.IsNullOrEmpty(status.HeadSha) &&
                    string.Equals(status.HeadSha, cached?.HeadSha, StringComparison.Ordinal))
                {
                    // HEAD did not move, so the cached commit time is still valid.
                    status.LastCommitTime = cachedTime;
                }
                else
                {
                    status.LastCommitTime = await new Commands.QueryRepositoryLastCommitTime(_id)
                        .GetResultAsync()
                        .ConfigureAwait(false);
                }
            }

            if (Dispatcher.UIThread.CheckAccess())
                Status = status;
            else
                await Dispatcher.UIThread.InvokeAsync(() => Status = status);
        }

        public static async Task UpdateStatusesAsync(IEnumerable<RepositoryNode> nodes, bool force, CancellationToken? token)
        {
            var pending = nodes?.Where(x => x != null).ToArray();
            if (pending == null || pending.Length == 0)
                return;

            // Keep the git processes off the UI thread; only the final status assignment is
            // marshaled back, so the bounded concurrency actually overlaps process startup.
            using var throttle = new SemaphoreSlim(4);
            var tasks = pending.Select(async node =>
            {
                await throttle.WaitAsync(token ?? CancellationToken.None).ConfigureAwait(false);
                try
                {
                    await node.UpdateStatusAsync(force, token).ConfigureAwait(false);
                }
                finally
                {
                    throttle.Release();
                }
            });

            try
            {
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation is expected when the view is unloaded or a scan is stopped.
            }

            // Persist the last commit time and HEAD SHA so the next launch can skip `git log`.
            if (Dispatcher.UIThread.CheckAccess())
                Preferences.Instance.Save();
            else
                Dispatcher.UIThread.Post(() => Preferences.Instance.Save());
        }

        public void LoadMinimalInfo(string gitDir)
        {
            var savedTo = Path.Combine(gitDir, "sourcefork.node");
            if (!File.Exists(savedTo))
                return;

            try
            {
                var minimalInfo = JsonSerializer.Deserialize(File.ReadAllText(savedTo), JsonCodeGen.Default.RepositoryNodeMinimalInfo);
                if (minimalInfo.CreateTime > 0)
                    CreateTime = minimalInfo.CreateTime;
                SetTags(minimalInfo.Tags ?? [], minimalInfo.PrimaryTag);
            }
            catch
            {
                // Ignore any error and just use default values.
            }
        }

        public void SaveMinimalInfo(string gitDir)
        {
            if (!Directory.Exists(gitDir))
                return;

            var savedTo = Path.Combine(gitDir, "sourcefork.node");
            var minimalInfo = new RepositoryNodeMinimalInfo
            {
                Tags = _tags,
                PrimaryTag = _primaryTag,
                CreateTime = CreateTime
            };

            try
            {
                File.WriteAllText(savedTo, JsonSerializer.Serialize(minimalInfo, JsonCodeGen.Default.RepositoryNodeMinimalInfo));
            }
            catch
            {
                // Ignore any error (e.g. the repository directory was removed while the tab was open).
            }
        }

        private void RebuildTagItems()
        {
            TagItems.Clear();
            foreach (var tag in _tags)
                TagItems.Add(new RepositoryTagItem(tag, tag.Equals(_primaryTag, StringComparison.OrdinalIgnoreCase)));

            OnPropertyChanged(nameof(HasTags));
            OnPropertyChanged(nameof(HasPrimaryTag));
            OnPropertyChanged(nameof(PrimaryTagDisplay));
            OnPropertyChanged(nameof(SecondaryTagsDisplay));
            OnPropertyChanged(nameof(HasSecondaryTags));
        }

        private string NormalizePrimary(string primary)
        {
            if (_tags.Count == 0)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(primary))
            {
                var found = _tags.Find(x => x.Equals(primary, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(found))
                    return found;
            }

            return _tags[0];
        }

        private static string NormalizeTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return string.Empty;

            return tag.Trim().TrimStart('#').Trim();
        }

        private static List<string> NormalizeTags(IEnumerable<string> tags)
        {
            var normalized = new List<string>();
            if (tags == null)
                return normalized;

            foreach (var tag in tags)
            {
                var value = NormalizeTag(tag);
                if (string.IsNullOrEmpty(value))
                    continue;

                if (!normalized.Any(t => t.Equals(value, StringComparison.OrdinalIgnoreCase)))
                    normalized.Add(value);
            }

            return normalized;
        }

        private string _id = string.Empty;
        private bool _isRepository = false;
        private List<string> _tags = [];
        private string _primaryTag = string.Empty;
        private bool _isVisible = true;
        private Models.RepositoryStatus _status = null;
        private DateTime _lastUpdateStatus = DateTime.UnixEpoch.ToLocalTime();
    }
}
