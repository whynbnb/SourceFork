using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia.Collections;
using Avalonia.Threading;

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
                .Where(x => !string.IsNullOrEmpty(x) && !prefs.IsRepositoryGroupHidden(x))
                .ToList();

            if (prefs.RepositoryGroupSort == Models.RepositoryGroupSortMode.Custom)
            {
                keys = prefs.GetRepositoryGroupOrder(keys);
            }
            else
            {
                if (prefs.RepositoryGroupSort == Models.RepositoryGroupSortMode.Count)
                {
                    keys.Sort((l, r) =>
                    {
                        var result = groups[l].Count.CompareTo(groups[r].Count);
                        return result != 0 ? result : string.Compare(l, r, StringComparison.OrdinalIgnoreCase);
                    });
                }
                else
                {
                    keys.Sort((l, r) => string.Compare(l, r, StringComparison.OrdinalIgnoreCase));
                }

                if (prefs.RepositoryGroupSortDescending)
                    keys.Reverse();
            }

            if (groups.ContainsKey(string.Empty))
                keys.Add(string.Empty);

            foreach (var key in keys)
            {
                var name = string.IsNullOrEmpty(key)
                    ? App.Text("RepositoryTags.NoPrimary")
                    : $"#{displayNames[key]}";
                var collapsed = prefs.IsRepositoryGroupCollapsed(key);
                rows.Add(new RepositoryGroupRow(key, name, collapsed));

                if (collapsed)
                    continue;

                var group = groups[key];
                Sort(group);
                foreach (var node in group)
                    rows.Add(new RepositoryListRow(node, pageSelector?.Invoke(node)));
            }

            return rows;
        }

        public static void Sync(AvaloniaList<RepositoryListRow> target, IReadOnlyList<RepositoryListRow> desired)
        {
            var oldByKey = new Dictionary<string, RepositoryListRow>(StringComparer.Ordinal);
            foreach (var row in target)
            {
                var key = GetRowKey(row);
                if (key != null)
                    oldByKey.TryAdd(key, row);
            }

            var reused = new List<RepositoryListRow>(desired.Count);
            var used = new HashSet<RepositoryListRow>();
            foreach (var want in desired)
            {
                var key = GetRowKey(want);
                if (key != null &&
                    oldByKey.TryGetValue(key, out var existing) &&
                    !used.Contains(existing) &&
                    CanReuse(existing, want))
                {
                    used.Add(existing);
                    reused.Add(existing);
                }
                else
                {
                    reused.Add(null);
                }
            }

            for (var i = target.Count - 1; i >= 0; i--)
            {
                if (used.Contains(target[i]))
                    continue;

                target[i].Detach();
                target.RemoveAt(i);
            }

            for (var i = 0; i < desired.Count; i++)
            {
                var existing = reused[i];
                if (existing == null)
                {
                    target.Insert(i, desired[i]);
                    continue;
                }

                var current = target.IndexOf(existing);
                if (current != i)
                    target.Move(current, i);

                if (existing is RepositoryGroupRow oldGroup && desired[i] is RepositoryGroupRow newGroup)
                    oldGroup.SetExpanded(newGroup.IsExpanded);

                // The freshly built duplicate row attached to its page in the constructor.
                desired[i].Detach();
            }
        }

        public static void NotifyChanged()
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(NotifyChanged);
                return;
            }

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

        private static string GetRowKey(RepositoryListRow row)
        {
            if (row is RepositoryGroupRow group)
                return "G:" + group.GroupKey;

            return row.Node is { } node ? "R:" + node.Id : null;
        }

        private static bool CanReuse(RepositoryListRow existing, RepositoryListRow desired)
        {
            if (existing is RepositoryGroupRow left && desired is RepositoryGroupRow right)
                return string.Equals(left.GroupName, right.GroupName, StringComparison.Ordinal);

            if (existing is RepositoryGroupRow || desired is RepositoryGroupRow)
                return false;

            return ReferenceEquals(existing.Node, desired.Node) && ReferenceEquals(existing.Page, desired.Page);
        }
    }
}
