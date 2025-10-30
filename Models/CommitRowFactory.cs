// GitGUI.Models/CommitRowFactory.cs
using LibGit2Sharp;

namespace GitGUI.Models
{
    public static class CommitRowFactory
    {
        // Build from LibGit2Sharp.Commit objects (use this for your graph pipeline)
        public static List<CommitRow> FromCommits(IEnumerable<Commit> commits)
        {
            var list = commits.ToList();
            var rows = new List<CommitRow>(list.Count);
            foreach (var c in list)
            {
                rows.Add(new CommitRow { Commit = c });
            }
            return rows;
        }
    }
}
