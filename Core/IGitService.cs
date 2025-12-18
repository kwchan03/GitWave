using GitWave.Models;
using LibGit2Sharp;

namespace GitWave.Core
{
    public interface IGitService
    {
        GitHubUser AuthenticatedUser { get; set; }
        void CreateRepository(string path);
        IEnumerable<CommitInfo> GetCommitLog(int maxCount = 50);
        IEnumerable<Commit> FetchCommitsForGraph();
        bool OpenRepository(string repositoryPath);
        Repository GetRepository();
        bool TryGetRepository(out Repository repository);
        bool IsRepositoryOpen { get; }

        IEnumerable<BranchInfo> GetBranches();
        void CheckoutBranch(string branchName);
        void CreateBranch(string newBranchName);
        void MergeBranch(string branchToMerge);
        (IEnumerable<ChangeItem> StagedChanges, IEnumerable<ChangeItem> UnstagedChanges) GetChanges();
        void StageFile(string relativePath);
        void UnstageFile(string relativePath);
        void SetRepoUserFromAuthenticatedUser(GitHubUser user);
        void Commit(string commitMessage);
        void CloneRepository(string sourceUrl, string parentDirectory, GitHubUser AuthenticatedUser);
        void PullCurrentBranch(string path, GitHubUser AuthenticatedUser);
        void PushCurrentBranch(string path, GitHubUser AuthenticatedUser);
        (string Owner, string Name) GetRemoteRepoInfo();
    }
}
