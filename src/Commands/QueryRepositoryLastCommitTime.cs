using System;
using System.Threading.Tasks;

namespace SourceFork.Commands
{
    public partial class QueryRepositoryLastCommitTime : Command
    {
        public QueryRepositoryLastCommitTime(string repo)
        {
            WorkingDirectory = repo;
            RaiseError = false;
        }

        public async Task<long> GetResultAsync()
        {
            Args = "log -1 --format=%ct";
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return 0;

            return long.TryParse(rs.StdOut.Trim(), out var timestamp) ? timestamp : 0;
        }
    }
}
