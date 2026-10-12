using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceGit.Commands
{
    public class QueryMergedBranches : Command
    {
        public QueryMergedBranches(string repo, string target)
        {
            WorkingDirectory = repo;
            Context = repo;
            Args = $"branch --merged {target} --format=\"%(refname)\"";
        }

        public async Task<HashSet<string>> GetResultAsync()
        {
            var outs = new HashSet<string>();
            var rs = await ReadToEndAsync().ConfigureAwait(false);
            if (!rs.IsSuccess)
                return outs;

            var lines = rs.StdOut.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
                outs.Add(line.Trim());

            return outs;
        }
    }
}
