using System.Threading.Tasks;

namespace SourceFork.ViewModels
{
    public class PruneRemote : Popup
    {
        public Models.Remote Remote
        {
            get;
        }

        public PruneRemote(Repository repo, Models.Remote remote)
        {
            _repo = repo;
            Remote = remote;
        }

        public override async Task<bool> Sure()
        {
            using var lockWatcher = _repo.LockWatcher();
            ProgressDescription = "Run `prune` on remote ...";

            var log = _repo.CreateLog($"Prune Remote '{Remote.Name}'");
            Use(log);

            // `remote prune` also needs the configured SSH key; Models.Remote in this fork does not
            // cache it yet, so read it from the repository config here.
            var sshKey = await new Commands.Config(_repo.FullPath)
                .GetAsync($"remote.{Remote.Name}.sshkey")
                .ConfigureAwait(false);

            var succ = await new Commands.Remote(_repo.FullPath)
                .Use(log)
                .PruneAsync(Remote.Name, sshKey);

            log.Complete();
            return succ;
        }

        private readonly Repository _repo = null;
    }
}
