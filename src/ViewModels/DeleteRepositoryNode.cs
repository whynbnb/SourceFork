using System.Linq;
using System.Threading.Tasks;

using Avalonia.Threading;

namespace SourceFork.ViewModels
{
    public class DeleteRepositoryNode : Popup
    {
        public RepositoryNode Node
        {
            get;
        }

        public DeleteRepositoryNode(RepositoryNode node)
        {
            Node = node;
        }

        public override Task<bool> Sure()
        {
            var launcher = App.GetLauncher();
            var page = launcher?.Pages.FirstOrDefault(x => x.Node.Id == Node.Id && x.Data is Repository);

            Preferences.Instance.RemoveNode(Node, true);
            RepositoryListSorter.NotifyChanged();

            if (page != null)
                Dispatcher.UIThread.Post(() => launcher.CloseTab(page), DispatcherPriority.Background);

            return Task.FromResult(true);
        }
    }
}
