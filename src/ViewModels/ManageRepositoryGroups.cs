using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class RepositoryGroupSetting : ObservableObject
    {
        private bool _isVisible;
        private readonly Action<RepositoryGroupSetting> _onVisibilityChanged;

        public string Key
        {
            get;
        }

        public string DisplayName
        {
            get => $"#{Key}";
        }

        public string CountText
        {
            get;
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (SetProperty(ref _isVisible, value))
                    _onVisibilityChanged?.Invoke(this);
            }
        }

        public RepositoryGroupSetting(string key, int count, bool isVisible, Action<RepositoryGroupSetting> onVisibilityChanged)
        {
            Key = key;
            CountText = App.Text("RepositoryGroups.RepositoryCount", count);
            _isVisible = isVisible;
            _onVisibilityChanged = onVisibilityChanged;
        }
    }

    public class ManageRepositoryGroups : ObservableObject
    {
        public ObservableCollection<RepositoryGroupSetting> Groups
        {
            get;
        } = [];

        public bool HasGroups
        {
            get => Groups.Count > 0;
        }

        public ManageRepositoryGroups()
        {
            var prefs = Preferences.Instance;
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in prefs.RepositoryNodes)
            {
                if (!node.IsRepository)
                    continue;

                var tag = node.PrimaryTag?.Trim();
                if (string.IsNullOrEmpty(tag))
                    continue;

                counts.TryGetValue(tag, out var count);
                counts[tag] = count + 1;
            }

            foreach (var key in prefs.GetRepositoryGroupKeys())
            {
                counts.TryGetValue(key, out var count);
                Groups.Add(new RepositoryGroupSetting(key, count, !prefs.IsRepositoryGroupHidden(key), OnVisibilityChanged));
            }
        }

        public void MoveUp(RepositoryGroupSetting group)
        {
            Move(group, -1);
        }

        public void MoveDown(RepositoryGroupSetting group)
        {
            Move(group, 1);
        }

        private void Move(RepositoryGroupSetting group, int offset)
        {
            var index = Groups.IndexOf(group);
            var target = index + offset;
            if (index < 0 || target < 0 || target >= Groups.Count)
                return;

            Groups.Move(index, target);
            Preferences.Instance.SetRepositoryGroupOrder(Groups.Select(x => x.Key));
            RepositoryListSorter.NotifyChanged();
        }

        private void OnVisibilityChanged(RepositoryGroupSetting group)
        {
            Preferences.Instance.SetRepositoryGroupHidden(group.Key, !group.IsVisible);
            RepositoryListSorter.NotifyChanged();
        }
    }
}
