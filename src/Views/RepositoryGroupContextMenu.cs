using System;

using Avalonia.Controls;

using SourceFork.Models;
using SourceFork.ViewModels;

namespace SourceFork.Views
{
    public static class RepositoryGroupContextMenu
    {
        public static ContextMenu Build(Control owner, RepositoryGroupRow group, Action toggle)
        {
            var menu = new ContextMenu();

            var collapse = new MenuItem()
            {
                Header = App.Text(group.IsCollapsed ? "RepositorySort.ExpandGroup" : "RepositorySort.CollapseGroup"),
            };
            collapse.Click += (_, e) =>
            {
                toggle();
                e.Handled = true;
            };
            menu.Items.Add(collapse);

            menu.Items.Add(new MenuItem() { Header = "-" });
            menu.Items.Add(BuildItemSortMenu(owner, group.GroupKey));

            menu.Items.Add(new MenuItem() { Header = "-" });

            var manage = new MenuItem();
            manage.Header = App.Text("RepositorySort.ManageGroups");
            manage.Icon = owner.CreateMenuIcon("Icons.Tags");
            manage.Click += async (_, e) =>
            {
                await owner.ShowDialogAsync(new ViewModels.ManageRepositoryGroups());
                e.Handled = true;
            };
            menu.Items.Add(manage);

            return menu;
        }

        private static MenuItem BuildItemSortMenu(Control owner, string groupKey)
        {
            var prefs = ViewModels.Preferences.Instance;
            var current = prefs.GetRepositoryGroupItemSort(groupKey);
            var mode = current?.Mode ?? Models.RepositorySortMode.Custom;
            var descending = current?.Descending ?? false;

            var menu = new MenuItem() { Header = App.Text("RepositorySort.GroupItems") };

            AddItemMode(menu, owner, "RepositorySort.AddedTime", Models.RepositorySortMode.AddedTime, mode);
            AddItemMode(menu, owner, "RepositorySort.Name", Models.RepositorySortMode.Name, mode);
            AddItemMode(menu, owner, "RepositorySort.UpdatedTime", Models.RepositorySortMode.UpdatedTime, mode);
            AddItemMode(menu, owner, "RepositorySort.Custom", Models.RepositorySortMode.Custom, mode);

            menu.Items.Add(new MenuItem() { Header = "-" });

            var reverse = new MenuItem();
            reverse.Header = App.Text("RepositorySort.Descending");
            reverse.IsEnabled = mode != Models.RepositorySortMode.Custom;
            if (descending)
                reverse.Icon = owner.CreateMenuIcon("Icons.Check");
            reverse.Click += (_, e) =>
            {
                prefs.SetRepositoryGroupItemSort(groupKey, mode, !descending);
                RepositoryListSorter.NotifyChanged();
                e.Handled = true;
            };
            menu.Items.Add(reverse);

            return menu;

            void AddItemMode(MenuItem parent, Control control, string headerKey, Models.RepositorySortMode value, Models.RepositorySortMode selected)
            {
                var item = new MenuItem();
                item.Header = App.Text(headerKey);
                if (selected == value)
                    item.Icon = control.CreateMenuIcon("Icons.Check");
                item.Click += (_, e) =>
                {
                    prefs.SetRepositoryGroupItemSort(groupKey, value);
                    RepositoryListSorter.NotifyChanged();
                    e.Handled = true;
                };
                parent.Items.Add(item);
            }
        }
    }
}
