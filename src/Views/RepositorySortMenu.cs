using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace SourceFork.Views
{
    public static class RepositorySortMenu
    {
        public static void Show(Control owner, Control anchor)
        {
            var prefs = ViewModels.Preferences.Instance;
            var menu = new ContextMenu()
            {
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                VerticalOffset = -6,
            };

            menu.Items.Add(new MenuItem()
            {
                Header = App.Text("RepositorySort.Title"),
                IsEnabled = false,
            });

            AddMode(owner, menu, App.Text("RepositorySort.AddedTime"), Models.RepositorySortMode.AddedTime);
            AddMode(owner, menu, App.Text("RepositorySort.Name"), Models.RepositorySortMode.Name);
            AddMode(owner, menu, App.Text("RepositorySort.UpdatedTime"), Models.RepositorySortMode.UpdatedTime);
            AddMode(owner, menu, App.Text("RepositorySort.Custom"), Models.RepositorySortMode.Custom);

            menu.Items.Add(new MenuItem() { Header = "-" });

            var descending = new MenuItem();
            descending.Header = App.Text("RepositorySort.Descending");
            descending.IsEnabled = prefs.RepositorySort != Models.RepositorySortMode.Custom;
            if (prefs.RepositorySortDescending)
                descending.Icon = owner.CreateMenuIcon("Icons.Check");
            descending.Click += (_, e) =>
            {
                prefs.RepositorySortDescending = !prefs.RepositorySortDescending;
                ViewModels.RepositoryListSorter.NotifyChanged();
                e.Handled = true;
            };
            menu.Items.Add(descending);

            var group = new MenuItem();
            group.Header = App.Text("RepositorySort.GroupByPrimaryTag");
            if (prefs.RepositoryGroupByPrimaryTag)
                group.Icon = owner.CreateMenuIcon("Icons.Check");
            group.Click += (_, e) =>
            {
                prefs.RepositoryGroupByPrimaryTag = !prefs.RepositoryGroupByPrimaryTag;
                ViewModels.RepositoryListSorter.NotifyChanged();
                e.Handled = true;
            };
            menu.Items.Add(group);

            menu.Open(anchor);

            void AddMode(Control control, ContextMenu target, string header, Models.RepositorySortMode mode)
            {
                var item = new MenuItem();
                item.Header = header;
                if (prefs.RepositorySort == mode)
                    item.Icon = control.CreateMenuIcon("Icons.Check");
                item.Click += (_, e) =>
                {
                    prefs.RepositorySort = mode;
                    ViewModels.RepositoryListSorter.NotifyChanged();
                    e.Handled = true;
                };
                target.Items.Add(item);
            }
        }
    }
}
