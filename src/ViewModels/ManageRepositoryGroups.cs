using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class RepositorySortOption
    {
        public Models.RepositorySortMode Mode
        {
            get;
        }

        public string Label
        {
            get;
        }

        public RepositorySortOption(Models.RepositorySortMode mode, string label)
        {
            Mode = mode;
            Label = label;
        }
    }

    public class RepositoryGroupSortOption
    {
        public Models.RepositoryGroupSortMode Mode
        {
            get;
        }

        public string Label
        {
            get;
        }

        public RepositoryGroupSortOption(Models.RepositoryGroupSortMode mode, string label)
        {
            Mode = mode;
            Label = label;
        }
    }

    public class RepositoryGroupSetting : ObservableObject
    {
        private bool _isVisible;
        private RepositorySortOption _selectedSortOption;
        private bool _sortDescending;
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

        public IReadOnlyList<RepositorySortOption> SortOptions
        {
            get;
        } =
        [
            new(Models.RepositorySortMode.AddedTime, App.Text("RepositorySort.AddedTime")),
            new(Models.RepositorySortMode.Name, App.Text("RepositorySort.Name")),
            new(Models.RepositorySortMode.UpdatedTime, App.Text("RepositorySort.UpdatedTime")),
            new(Models.RepositorySortMode.Custom, App.Text("RepositorySort.GroupSort.Custom")),
        ];

        public RepositorySortOption SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (value == null || ReferenceEquals(_selectedSortOption, value))
                    return;

                _selectedSortOption = value;
                OnPropertyChanged();

                if (value.Mode == Models.RepositorySortMode.Custom && _sortDescending)
                {
                    _sortDescending = false;
                    OnPropertyChanged(nameof(SortDescending));
                }

                Preferences.Instance.SetRepositoryGroupItemSort(Key, value.Mode, _sortDescending);
                RepositoryListSorter.NotifyChanged();
                OnPropertyChanged(nameof(CanReverseSort));
            }
        }

        public bool CanReverseSort
        {
            get => _selectedSortOption?.Mode != Models.RepositorySortMode.Custom;
        }

        public bool SortDescending
        {
            get => _sortDescending;
            set
            {
                if (!SetProperty(ref _sortDescending, value) || _selectedSortOption == null)
                    return;

                Preferences.Instance.SetRepositoryGroupItemSort(Key, _selectedSortOption.Mode, value);
                RepositoryListSorter.NotifyChanged();
            }
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

            var sort = Preferences.Instance.GetRepositoryGroupItemSort(key);
            var mode = sort?.Mode ?? Models.RepositorySortMode.Custom;
            _selectedSortOption = SortOptions.First(x => x.Mode == mode);
            _sortDescending = mode == Models.RepositorySortMode.Custom ? false : (sort?.Descending ?? false);
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

        public IReadOnlyList<RepositoryGroupSortOption> GroupSortOptions
        {
            get;
        } =
        [
            new(Models.RepositoryGroupSortMode.Name, App.Text("RepositorySort.GroupSort.Name")),
            new(Models.RepositoryGroupSortMode.Count, App.Text("RepositorySort.GroupSort.Count")),
            new(Models.RepositoryGroupSortMode.Custom, App.Text("RepositorySort.GroupSort.Custom")),
        ];

        public RepositoryGroupSortOption SelectedGroupSortOption
        {
            get => _selectedGroupSortOption;
            set
            {
                if (value == null || ReferenceEquals(_selectedGroupSortOption, value))
                    return;

                _selectedGroupSortOption = value;
                OnPropertyChanged();

                Preferences.Instance.RepositoryGroupSort = value.Mode;
                RepositoryListSorter.NotifyChanged();
                OnPropertyChanged(nameof(CanReverseGroupSort));
                ScheduleReload();
            }
        }

        public bool CanReverseGroupSort
        {
            get => _selectedGroupSortOption?.Mode != Models.RepositoryGroupSortMode.Custom;
        }

        public bool GroupSortDescending
        {
            get => Preferences.Instance.RepositoryGroupSortDescending;
            set
            {
                if (value == Preferences.Instance.RepositoryGroupSortDescending)
                    return;

                Preferences.Instance.RepositoryGroupSortDescending = value;
                RepositoryListSorter.NotifyChanged();
                OnPropertyChanged();
                ScheduleReload();
            }
        }

        public ManageRepositoryGroups()
        {
            _selectedGroupSortOption = GroupSortOptions.First(x => x.Mode == Preferences.Instance.RepositoryGroupSort);
            Reload();
        }

        public void Reload()
        {
            var prefs = Preferences.Instance;

            _selectedGroupSortOption = GroupSortOptions.First(x => x.Mode == prefs.RepositoryGroupSort);
            OnPropertyChanged(nameof(SelectedGroupSortOption));
            OnPropertyChanged(nameof(CanReverseGroupSort));
            OnPropertyChanged(nameof(GroupSortDescending));

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

            Groups.Clear();
            foreach (var key in prefs.GetRepositoryGroupKeys())
            {
                counts.TryGetValue(key, out var count);
                Groups.Add(new RepositoryGroupSetting(key, count, !prefs.IsRepositoryGroupHidden(key), OnVisibilityChanged));
            }

            OnPropertyChanged(nameof(HasGroups));
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
            SyncGroupSort();
        }

        private void OnVisibilityChanged(RepositoryGroupSetting group)
        {
            Preferences.Instance.SetRepositoryGroupHidden(group.Key, !group.IsVisible);
            RepositoryListSorter.NotifyChanged();
        }

        private void SyncGroupSort()
        {
            _selectedGroupSortOption = GroupSortOptions.First(x => x.Mode == Preferences.Instance.RepositoryGroupSort);
            OnPropertyChanged(nameof(SelectedGroupSortOption));
            OnPropertyChanged(nameof(CanReverseGroupSort));
            OnPropertyChanged(nameof(GroupSortDescending));
        }

        private void ScheduleReload()
        {
            Dispatcher.UIThread.Post(Reload, DispatcherPriority.Background);
        }

        private RepositoryGroupSortOption _selectedGroupSortOption;
    }
}
