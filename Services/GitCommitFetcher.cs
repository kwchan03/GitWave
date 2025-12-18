using GitWave.Models;
using LibGit2Sharp;

namespace GitWave.Services
{
    public static class GitCommitFetcher
    {
        public static List<CommitInfo> FetchCommits(string repoPath, int max = 2000)
        {
            try
            {
                using var repo = new Repository(repoPath);

                // ✅ Map branch refs (local + remote optional)
                var refsBySha = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                foreach (var br in repo.Branches.Where(b => b.Tip is not null))
                {
                    var sha = br.Tip!.Sha;
                    if (!refsBySha.TryGetValue(sha, out var list))
                        refsBySha[sha] = list = new();

                    list.Add(br.FriendlyName);
                }

                // ✅ Include all branches in traversal (like `git log --all`)
                var filter = new CommitFilter
                {
                    IncludeReachableFrom = repo.Branches,
                    SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time
                };

                var result = new List<CommitInfo>(Math.Min(max, 4096));

                foreach (var commit in repo.Commits.QueryBy(filter))
                {
                    result.Add(new CommitInfo
                    {
                        Sha = commit.Sha,
                        MessageShort = commit.MessageShort ?? "",
                        AuthorName = commit.Author?.Name ?? "",
                        AuthorWhen = commit.Author?.When ?? default,
                        ParentShas = commit.Parents.Select(p => p.Sha).ToArray(),
                        Refs = refsBySha.TryGetValue(commit.Sha, out var names)
                            ? names.ToArray()
                            : Array.Empty<string>()
                    });

                    if (result.Count >= max)
                        break;
                }

                return result;
            }
            catch (RepositoryNotFoundException)
            {
                // Repo path invalid or not found
                return new List<CommitInfo>();
            }
        }
    }
}
