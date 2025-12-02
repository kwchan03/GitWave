// GitGUI.Models/CommitRowFactory.cs
using GitGUI.Core;
using GitGUI.ViewModels;
using LibGit2Sharp;

namespace GitGUI.Models
{
    public static class CommitRowFactory
    {
        // Build from LibGit2Sharp.Commit objects (use this for your graph pipeline)
        public static List<CommitRowViewModel> FromCommits(IEnumerable<Commit> commits, IGitService gitService)
        {
            return commits.Select((commit, index) => new CommitRowViewModel(gitService)
            {
                Sha = commit.Sha,
                ShortSha = commit.Sha.Substring(0, 7),
                Message = commit.MessageShort,
                Author = commit.Author.Name,
                AuthorEmail = commit.Author.Email,
                Committer = commit.Committer.Name,
                CommitterEmail = commit.Committer.Email,
                CommitDate = commit.Author.When.DateTime,
                Parents = commit.Parents.Select(p => p.Sha).ToList(),
                Row = index,
            }).ToList();
        }
    }
}
