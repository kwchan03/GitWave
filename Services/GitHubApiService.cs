using Octokit;

namespace GitWave.Services
{
    public class GitHubApiService
    {
        private readonly GitHubClient _client;

        public GitHubApiService(string token)
        {
            _client = new GitHubClient(new ProductHeaderValue("GitWave-App"));
            _client.Credentials = new Credentials(token);
        }

        public async Task<IReadOnlyList<PullRequest>> GetPullRequests(string owner, string repoName)
        {
            return await _client.PullRequest.GetAllForRepository(owner, repoName, new PullRequestRequest
            {
                State = ItemStateFilter.Open
            });
        }

        public async Task<IReadOnlyList<PullRequestCommit>> GetCommits(string owner, string repo, int number)
        {
            return await _client.PullRequest.Commits(owner, repo, number);
        }

        public async Task<IReadOnlyList<IssueComment>> GetGeneralComments(string owner, string repo, int number)
        {
            // Fetches comments at the bottom of the PR (not code comments)
            return await _client.Issue.Comment.GetAllForIssue(owner, repo, number);
        }

        public async Task<IReadOnlyList<IssueEvent>> GetSystemEvents(string owner, string repo, int number)
        {
            // Fetches "Merged", "Assigned", "Labeled" events
            return await _client.Issue.Events.GetAllForIssue(owner, repo, number);
        }

        public async Task<IReadOnlyList<PullRequestReview>> GetReviews(string owner, string repo, int number)
        {
            // Fetches the "Headers" (e.g. "Alice Approved")
            return await _client.PullRequest.Review.GetAll(owner, repo, number);
        }

        public async Task<IReadOnlyList<PullRequestReviewComment>> GetReviewComments(string owner, string repo, int number)
        {
            // Fetches the actual code comments (e.g. "Fix this bug on line 10")
            return await _client.PullRequest.ReviewComment.GetAll(owner, repo, number);
        }

        public async Task<IReadOnlyList<PullRequestFile>> GetPullRequestFiles(string owner, string repo, int number)
        {
            return await _client.PullRequest.Files(owner, repo, number);
        }

        public async Task<IEnumerable<GitHubCommitFile>> GetCommitFilesAsync(string owner, string repo, string sha)
        {
            try
            {
                var commit = await _client.Repository.Commit.Get(owner, repo, sha);
                return commit.Files ?? new List<GitHubCommitFile>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching commit files for {sha}: {ex.Message}");
                return new List<GitHubCommitFile>();
            }
        }

        public async Task<PullRequest> CreatePullRequest(string owner, string repoName, string title, string head, string baseBranch)
        {
            return await _client.PullRequest.Create(owner, repoName, new NewPullRequest(title, head, baseBranch));
        }

        // --- 2. NEW: MERGING ---

        public async Task<PullRequestMerge> MergePullRequestAsync(string owner, string repo, int number, string commitTitle, PullRequestMergeMethod method)
        {
            var mergeRequest = new MergePullRequest
            {
                CommitTitle = commitTitle,
                MergeMethod = method // Merge, Squash, or Rebase
            };

            return await _client.PullRequest.Merge(owner, repo, number, mergeRequest);
        }

        // --- 3. NEW: SUBMIT REVIEW (Approve / Request Changes) ---

        public async Task<PullRequestReview> SubmitReviewAsync(string owner, string repo, int number, PullRequestReviewEvent eventType, string body)
        {
            var review = new PullRequestReviewCreate
            {
                Body = body,
                Event = eventType
            };

            return await _client.PullRequest.Review.Create(owner, repo, number, review);
        }

        // --- 4. NEW: POST GENERAL COMMENT ---

        public async Task<IssueComment> CreateIssueComment(string owner, string repo, int number, string comment)
        {
            // Note: In GitHub API, PR comments are technically Issue Comments
            return await _client.Issue.Comment.Create(owner, repo, number, comment);
        }

        // --- 5. NEW: CLOSE PULL REQUEST ---

        public async Task<PullRequest> ClosePullRequestAsync(string owner, string repo, int number)
        {
            var update = new PullRequestUpdate
            {
                State = ItemState.Closed
            };

            return await _client.PullRequest.Update(owner, repo, number, update);
        }

        // --- 6. NEW: FILE CONTENT (For Diff Viewer) ---

        public async Task<string> GetFileContentAsync(string owner, string repo, string sha, string path)
        {
            // If there is no SHA (e.g., "Before" version of a new file), return empty
            if (string.IsNullOrEmpty(sha))
                return null;

            try
            {
                // "GetAllContentsByRef" allows us to specify the Commit SHA (ref)
                var contents = await _client.Repository.Content.GetAllContentsByRef(owner, repo, path, sha);

                // GitHub API returns a list (files/directories). We expect a single file.
                // Octokit automatically decodes the Base64 content into a string for us.
                return contents.FirstOrDefault()?.Content ?? null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching file {path} @ {sha}: {ex.Message}");
                return null;
            }
        }

        public async Task<IReadOnlyList<string>> GetBranchesAsync(string owner, string repo)
        {
            var branches = await _client.Repository.Branch.GetAll(owner, repo);
            return branches.Select(b => b.Name).ToList();
        }

        public async Task<PullRequest> CreatePullRequestAsync(string owner, string repo, string title, string body, string head, string baseRef)
        {
            var newPr = new NewPullRequest(title, head, baseRef)
            {
                Body = body
            };

            return await _client.PullRequest.Create(owner, repo, newPr);
        }

        public async Task<PullRequest> UpdatePullRequestState(string owner, string repo, int number, ItemState state)
        {
            var update = new Octokit.PullRequestUpdate
            {
                State = state
            };
            return await _client.PullRequest.Update(owner, repo, number, update);
        }
    }
}