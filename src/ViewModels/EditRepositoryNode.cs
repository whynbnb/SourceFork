using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace SourceFork.ViewModels
{
    public class EditRepositoryNode : Popup
    {
        public string Target
        {
            get;
        }

        [Required(ErrorMessage = "Name is required!")]
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, true);
        }

        public int Bookmark
        {
            get => _bookmark;
            set => SetProperty(ref _bookmark, value);
        }

        public EditRepositoryNode(RepositoryNode node)
        {
            _node = node;
            _name = node.Name;
            _bookmark = node.Bookmark;
            Target = node.Id;
        }

        public override Task<bool> Sure()
        {
            bool needSort = _node.Name != _name;
            _node.Name = _name;
            _node.Bookmark = _bookmark;

            if (needSort)
                Preferences.Instance.SortByRenamedNode(_node);
            else
                Preferences.Instance.Save();

            Welcome.Instance.Refresh();
            App.GetLauncher()?.RefreshRepositoryFilters();
            return Task.FromResult(true);
        }

        private readonly RepositoryNode _node = null;
        private string _name = null;
        private int _bookmark = 0;
    }
}
