using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class EditRepositoryTags : ObservableObject
    {
        public string Target
        {
            get;
        }

        public ObservableCollection<RepositoryTagItem> Tags
        {
            get;
        } = [];

        public string NewTag
        {
            get => _newTag;
            set
            {
                if (SetProperty(ref _newTag, value))
                    RefreshSuggestions();
            }
        }

        public bool HasTags
        {
            get => Tags.Count > 0;
        }

        public ObservableCollection<string> Suggestions
        {
            get;
        } = [];

        public bool HasSuggestions
        {
            get => Suggestions.Count > 0;
        }

        public EditRepositoryTags(RepositoryNode node)
        {
            _node = node;
            Target = node.Id;

            foreach (var tag in node.Tags)
                Tags.Add(new RepositoryTagItem(tag, tag.Equals(node.PrimaryTag, System.StringComparison.OrdinalIgnoreCase)));

            RefreshSuggestions();
        }

        public void RefreshSuggestions()
        {
            var keyword = NewTag?.Trim().TrimStart('#').Trim() ?? string.Empty;
            var assigned = new HashSet<string>(Tags.Select(x => x.Name), StringComparer.OrdinalIgnoreCase);

            var launcher = App.GetLauncher();
            var existing = launcher != null
                ? launcher.GetAvailableTags()
                : Preferences.Instance.RepositoryNodes
                    .Where(x => x.IsRepository)
                    .SelectMany(x => x.Tags)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();

            Suggestions.Clear();
            foreach (var tag in existing)
            {
                if (assigned.Contains(tag))
                    continue;

                if (keyword.Length > 0 && !tag.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    continue;

                Suggestions.Add(tag);
            }

            OnPropertyChanged(nameof(HasSuggestions));
        }

        public void AcceptSuggestion(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;

            NewTag = tag;
            AddTag();
        }

        public void AddTag()
        {
            var name = NewTag?.Trim().TrimStart('#').Trim();
            if (string.IsNullOrEmpty(name))
                return;

            if (Tags.Any(x => x.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase)))
                return;

            Tags.Add(new RepositoryTagItem(name, Tags.Count == 0));
            NewTag = string.Empty;
            OnPropertyChanged(nameof(HasTags));
            ApplyChanges();
        }

        public void RemoveTag(RepositoryTagItem tag)
        {
            if (tag == null || !Tags.Remove(tag))
                return;

            if (tag.IsPrimary && Tags.Count > 0)
                Tags[0].IsPrimary = true;

            OnPropertyChanged(nameof(HasTags));
            RefreshSuggestions();
            ApplyChanges();
        }

        public void MoveUp(RepositoryTagItem tag)
        {
            var index = Tags.IndexOf(tag);
            if (index > 0)
            {
                Tags.Move(index, index - 1);
                ApplyChanges();
            }
        }

        public void MoveDown(RepositoryTagItem tag)
        {
            var index = Tags.IndexOf(tag);
            if (index >= 0 && index < Tags.Count - 1)
            {
                Tags.Move(index, index + 1);
                ApplyChanges();
            }
        }

        public void SetPrimary(RepositoryTagItem tag)
        {
            if (tag == null || tag.IsPrimary)
                return;

            foreach (var item in Tags)
                item.IsPrimary = ReferenceEquals(item, tag);

            ApplyChanges();
        }

        public void ApplyChanges()
        {
            var primary = Tags.FirstOrDefault(x => x.IsPrimary)?.Name ?? Tags.FirstOrDefault()?.Name;
            _node.SetTags(Tags.Select(x => x.Name), primary);
            RepositoryListSorter.NotifyChanged();
        }

        private readonly RepositoryNode _node;
        private string _newTag = string.Empty;
    }
}
