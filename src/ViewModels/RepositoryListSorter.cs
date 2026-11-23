using System;
using System.Collections.Generic;
using System.Linq;

namespace SourceFork.ViewModels
{
    public static class RepositoryListSorter
    {
        public static List<RepositoryListRow> Build(IEnumerable<RepositoryNode> nodes, Func<RepositoryNode, LauncherPage> pageSelector = null)
        {
            var list = nodes.Where(x => x != null).ToList();
            Sort(list);

            var rows = new List<RepositoryListRow>();
            var prefs = Preferences.Instance;
            if (!prefs.RepositoryGroupByPrimaryTag)
            {
                foreach (var node in list)
                    rows.Add(new RepositoryListRow(node, pageSelector?.Invoke(node)));

                return rows;
            }

            var groups = new Dictionary<string, List<RepositoryNode>>(StringComparer.OrdinalIgnoreCase);
            var displayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in list)
            {
                var tag = node.PrimaryTag;
                var key = string.IsNullOrEmpty(tag) ? string.Empty : tag;
                if (!groups.TryGetValue(key, out var group))
                {
                    group = [];
                    groups.Add(key, group);
                }

                if (!string.IsNullOrEmpty(tag) && !displayNames.ContainsKey(key))
                    displayNames[key] = tag;

                group.Add(node);
            }

            var keys = groups.Keys
                .Where(x => !string.IsNullOrEmpty(x))
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (groups.ContainsKey(string.Empty))
                keys.Add(string.Empty);

            foreach (var key in keys)
            {
                var name = string.IsNullOrEmpty(key)
                    ? App.Text("RepositoryTags.NoPrimary")
                    : $"#{displayNames[key]}";
                rows.Add(new RepositoryListRow(name));

                var group = groups[key];
                Sort(group);
                foreach (var node in group)
                    rows.Add(new RepositoryListRow(node, pageSelector?.Invoke(node)));
            }

            return rows;
        }

        public static void NotifyChanged()
        {
            Preferences.Instance.Save();
            App.GetLauncher()?.RefreshRepositoryFilters();
            Welcome.Instance.Refresh();
        }

        private static void Sort(List<RepositoryNode> nodes)
        {
            var prefs = Preferences.Instance;
            var descending = prefs.RepositorySortDescending;

            switch (prefs.RepositorySort)
            {
                case Models.RepositorySortMode.AddedTime:
                    nodes.Sort((l, r) => Compare(l.CreateTime, r.CreateTime, l, r, descending));
                    break;
                case Models.RepositorySortMode.Name:
                    nodes.Sort((l, r) => CompareName(l, r, descending));
                    break;
                case Models.RepositorySortMode.UpdatedTime:
                    nodes.Sort((l, r) => Compare(l.GetLastUpdatedTime(), r.GetLastUpdatedTime(), l, r, descending));
                    break;
                default:
                    var order = new Dictionary<string, int>(StringComparer.Ordinal);
                    var all = prefs.RepositoryNodes;
                    for (var i = 0; i < all.Count; i++)
                        order[all[i].Id] = i;

                    nodes.Sort((l, r) =>
                    {
                        var li = order.TryGetValue(l.Id, out var lv) ? lv : int.MaxValue;
                        var ri = order.TryGetValue(r.Id, out var rv) ? rv : int.MaxValue;
                        return li.CompareTo(ri);
                    });
                    break;
            }
        }

        private static int Compare(long left, long right, RepositoryNode l, RepositoryNode r, bool descending)
        {
            var result = left.CompareTo(right);
            if (result == 0)
                result = CompareName(l, r, false);

            return descending ? -result : result;
        }

        private static int Compare(DateTime left, DateTime right, RepositoryNode l, RepositoryNode r, bool descending)
        {
            var result = left.CompareTo(right);
            if (result == 0)
                result = CompareName(l, r, false);

            return descending ? -result : result;
        }

        private static int CompareName(RepositoryNode left, RepositoryNode right, bool descending)
        {
            var result = Models.NumericSort.Compare(left.Name, right.Name);
            return descending ? -result : result;
        }
    }
}
