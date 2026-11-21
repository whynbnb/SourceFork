using System.Text;
using System.Threading.Tasks;

namespace SourceFork.Commands
{
    public class Push : Command
    {
        public Push(string repo, string local, string remote, string remoteBranch, bool withTags, bool checkSubmodules, bool track, bool force, bool noVerify, bool isRevision = false)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(1024);
            builder.Append("push --progress --verbose ");
            if (withTags)
                builder.Append("--tags ");
            if (checkSubmodules)
                builder.Append("--recurse-submodules=check ");
            if (track)
                builder.Append("-u ");
            if (force)
                builder.Append("--force-with-lease ");
            if (noVerify)
                builder.Append("--no-verify ");

            // Always use full refnames, otherwise a tag with the same name makes the source ambiguous.
            builder.Append(remote).Append(' ');
            if (isRevision)
                builder.Append(local);
            else
                builder.Append("refs/heads/").Append(local);
            builder.Append(":refs/heads/").Append(remoteBranch);
            Args = builder.ToString();
        }

        public Push(string repo, string remote, string refname, bool isDelete)
        {
            _remote = remote;

            WorkingDirectory = repo;
            Context = repo;

            var builder = new StringBuilder(512);
            builder.Append("push ");
            if (isDelete)
                builder.Append("--delete ");
            builder.Append(remote).Append(' ').Append(refname);

            Args = builder.ToString();
        }

        public async Task<bool> RunAsync()
        {
            SSHKey = await new Config(WorkingDirectory).GetAsync($"remote.{_remote}.sshkey").ConfigureAwait(false);
            return await ExecAsync().ConfigureAwait(false);
        }

        private readonly string _remote;
    }
}
