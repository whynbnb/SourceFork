using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SourceFork.ViewModels
{
    public class EditRepositoryTags : Popup
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
            set => SetProperty(ref _newTag, value);
        }

        public bool HasTags
        {
            get => Tags.Count > 0;
        }

        public EditRepositoryTags(RepositoryNode node)
        {
            _node = node;
            Target = node.Id;

            foreach (var tag in node.Tags)
                Tags.Add(new RepositoryTagItem(tag, tag.Equals(node.PrimaryTag, System.StringComparison.OrdinalIgnoreCase)));
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
        }

        public void RemoveTag(RepositoryTagItem tag)
        {
            if (tag == null || !Tags.Remove(tag))
                return;

            if (tag.IsPrimary && Tags.Count > 0)
                Tags[0].IsPrimary = true;

            OnPropertyChanged(nameof(HasTags));
        }

        public void MoveUp(RepositoryTagItem tag)
        {
            var index = Tags.IndexOf(tag);
            if (index > 0)
                Tags.Move(index, index - 1);
        }

        public void MoveDown(RepositoryTagItem tag)
        {
            var index = Tags.IndexOf(tag);
            if (index >= 0 && index < Tags.Count - 1)
                Tags.Move(index, index + 1);
        }

        public void SetPrimary(RepositoryTagItem tag)
        {
            if (tag == null)
                return;

            foreach (var item in Tags)
                item.IsPrimary = ReferenceEquals(item, tag);
        }

        public override Task<bool> Sure()
        {
            var primary = Tags.FirstOrDefault(x => x.IsPrimary)?.Name ?? Tags.FirstOrDefault()?.Name;
            _node.SetTags(Tags.Select(x => x.Name), primary);
            Preferences.Instance.Save();
            RepositoryListSorter.NotifyChanged();
            return Task.FromResult(true);
        }

        private readonly RepositoryNode _node;
        private string _newTag = string.Empty;
    }
}
