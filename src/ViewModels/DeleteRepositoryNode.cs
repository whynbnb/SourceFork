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
            Dispatcher.UIThread.Post(() => launcher?.RemoveRepositoryFromList(Node), DispatcherPriority.Background);
            return Task.FromResult(true);
        }
    }
}
