using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class RepositoryNodeMinimalInfo
    {
        public string FriendlyName { get; set; } = string.Empty;
        public int Bookmark { get; set; } = 0;
        public List<string> Tags { get; set; } = [];
    }

    public class RepositoryNode : ObservableObject
    {
        public string Id
        {
            get => _id;
            set
            {
                var normalized = value.Replace('\\', '/').TrimEnd('/');
                SetProperty(ref _id, normalized);
            }
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public int Bookmark
        {
            get => _bookmark;
            set => SetProperty(ref _bookmark, value);
        }

        public bool IsRepository
        {
            get => _isRepository;
            set => SetProperty(ref _isRepository, value);
        }

        public List<string> Tags
        {
            get => _tags;
            set
            {
                if (SetProperty(ref _tags, NormalizeTags(value)))
                {
                    OnPropertyChanged(nameof(TagsDisplay));
                    OnPropertyChanged(nameof(HasTags));
                }
            }
        }

        [JsonIgnore]
        public string TagsDisplay
        {
            get => string.Join("  ", _tags.Select(t => $"#{t}"));
        }

        [JsonIgnore]
        public bool HasTags
        {
            get => _tags.Count > 0;
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

        public void SetTags(IEnumerable<string> tags)
        {
            Tags = [.. tags];
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

        public bool MatchesSearch(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            if ((_name?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true) ||
                (_id?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true))
                return true;

            return _tags.Any(t => t.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        public void Open()
        {
            if (Directory.Exists(_id))
                App.GetLauncher().OpenRepositoryInTab(this, null);
        }

        public void Edit()
        {
            var activePage = App.GetLauncher().ActivePage;
            if (activePage != null && activePage.CanCreatePopup())
                activePage.Popup = new EditRepositoryNode(this);
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
            Status = await new Commands.QueryRepositoryStatus(_id).GetResultAsync();
        }

        public void LoadMinimalInfo(string gitDir)
        {
            var savedTo = Path.Combine(gitDir, "sourcefork.node");
            if (!File.Exists(savedTo))
                return;

            try
            {
                var minimalInfo = JsonSerializer.Deserialize(File.ReadAllText(savedTo), JsonCodeGen.Default.RepositoryNodeMinimalInfo);
                if (!string.IsNullOrEmpty(minimalInfo.FriendlyName))
                    Name = minimalInfo.FriendlyName;
                Bookmark = minimalInfo.Bookmark;
                Tags = minimalInfo.Tags ?? [];
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
                FriendlyName = Name,
                Bookmark = Bookmark,
                Tags = _tags
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

        private static List<string> NormalizeTags(IEnumerable<string> tags)
        {
            var normalized = new List<string>();
            if (tags == null)
                return normalized;

            foreach (var tag in tags)
            {
                if (string.IsNullOrWhiteSpace(tag))
                    continue;

                var value = tag.Trim().TrimStart('#').Trim();
                if (string.IsNullOrEmpty(value))
                    continue;

                if (!normalized.Any(t => t.Equals(value, StringComparison.OrdinalIgnoreCase)))
                    normalized.Add(value);
            }

            return normalized;
        }

        private string _id = string.Empty;
        private string _name = string.Empty;
        private bool _isRepository = false;
        private int _bookmark = 0;
        private List<string> _tags = [];
        private bool _isVisible = true;
        private Models.RepositoryStatus _status = null;
        private DateTime _lastUpdateStatus = DateTime.UnixEpoch.ToLocalTime();
    }
}
