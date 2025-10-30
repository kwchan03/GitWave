using GitGUI.Core;
using GitGUI.Models;
using LibGit2Sharp;
using System.IO;

namespace GitGUI.Services
{
    public class GitLibService : IGitService
    {
        private Repository _repo;
        private readonly IGitCredentialProvider _gitCreds;
        private GitHubUser _authenticatedUser;

        public GitLibService(IGitCredentialProvider gitCreds) // NEW
        {
            _gitCreds = gitCreds ?? throw new ArgumentNullException(nameof(gitCreds));
        }

        #region Repository Management
        public bool OpenRepository(string path)
        {
            if (!Repository.IsValid(path))
                throw new InvalidOperationException($"No git repository found at {path}");

            _repo = new Repository(path);

            if (_authenticatedUser != null)
                SetRepoUserFromAuthenticatedUser(_authenticatedUser);

            return true;
        }

        public void CreateRepository(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            if (Repository.IsValid(path))
            {
                _repo = new Repository(path);
            }
            else
            {
                Repository.Init(path);
                _repo = new Repository(path);
                CreateInitialCommit();
            }

            if (_authenticatedUser != null)
                SetRepoUserFromAuthenticatedUser(_authenticatedUser);
        }

        private void CreateInitialCommit()
        {
            var readmePath = Path.Combine(_repo.Info.WorkingDirectory, "README.md");
            if (!File.Exists(readmePath))
                File.WriteAllText(readmePath, "# Initial Commit\nThis is the first commit.");

            Commands.Stage(_repo, readmePath);
            var signature = BuildSignature();
            _repo.Commit("Initial commit", signature, signature);
        }
        #endregion

        #region User Configuration
        public void SetRepoUserFromAuthenticatedUser(GitHubUser user)
        {
            _authenticatedUser = user ?? throw new ArgumentNullException(nameof(user));

            if (_repo == null)
                return;

            _repo.Config.Set("user.name", _authenticatedUser.Login);
            _repo.Config.Set("user.email", _authenticatedUser.Email ?? "user@example.com");
        }

        private Signature BuildSignature()
        {
            if (_authenticatedUser != null)
                return new Signature(_authenticatedUser.Login, _authenticatedUser.Email ?? "user@example.com", DateTimeOffset.Now);

            return _repo.Config.BuildSignature(DateTimeOffset.Now);
        }
        #endregion

        #region Branch & Commit Operations
        public IEnumerable<BranchInfo> GetBranches()
        {
            if (_repo == null) throw new InvalidOperationException("Repository not opened.");
            return _repo.Branches
                .Where(b => !b.FriendlyName.Equals("origin/HEAD", StringComparison.OrdinalIgnoreCase))
                .Select(b => new BranchInfo
                {
                    Name = b.FriendlyName,
                    IsCurrent = b.IsCurrentRepositoryHead,
                    TipSha = b.Tip.Sha.Substring(0, 7) ?? ""
                });
        }

        public void CheckoutBranch(string branchName)
        {
            if (_repo == null)
                throw new InvalidOperationException("Repository not opened.");

            // Block risky switches when there are uncommitted changes
            var status = _repo.RetrieveStatus();
            if (status.IsDirty)
                throw new InvalidOperationException("Uncommitted changes exist. Commit or stash first.");

            // Accept "feature" or "origin/feature"
            bool isRemoteName = branchName.StartsWith("origin/", StringComparison.OrdinalIgnoreCase);
            string localName = isRemoteName ? branchName.Substring("origin/".Length) : branchName;

            // 1) Try local branch
            var local = _repo.Branches[localName];

            // 2) If missing, try to create from remote-tracking branch
            if (local == null)
            {
                var remote = _repo.Branches[$"origin/{localName}"];
                if (remote == null)
                    throw new InvalidOperationException($"Branch '{branchName}' not found (local or remote).");

                // Create local branch at the same tip and set upstream to track the remote
                local = _repo.CreateBranch(localName, remote.Tip);
                _repo.Branches.Update(local, b => b.TrackedBranch = remote.CanonicalName);
            }

            // 3) Checkout the local branch (HEAD attached)
            Commands.Checkout(_repo, local);
        }

        public string CurrentBranchName => _repo?.Head?.FriendlyName ?? "<none>";

        public bool IsRepositoryOpen => _repo != null;

        public void CreateBranch(string branchName)
        {
            if (_repo == null) throw new InvalidOperationException("Repository not opened.");
            var branch = _repo.CreateBranch(branchName);
            Commands.Checkout(_repo, branch);
        }

        public void MergeBranch(string branchName)
        {
            if (_repo == null) throw new InvalidOperationException("Repository not opened.");
            var target = _repo.Branches[branchName];
            if (target == null) throw new InvalidOperationException($"Branch '{branchName}' not found.");
            var signature = BuildSignature();
            _repo.Merge(target, signature);
        }

        public void Commit(string message)
        {
            if (_repo == null) throw new InvalidOperationException("Repository not opened.");
            if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException("Commit message cannot be empty.");

            var signature = BuildSignature();
            _repo.Commit(message, signature, signature);
        }

        public (IEnumerable<ChangeItem> StagedChanges, IEnumerable<ChangeItem> UnstagedChanges) GetChanges()
        {
            if (_repo == null) throw new InvalidOperationException("Repository not opened.");
            var status = _repo.RetrieveStatus();

            var staged = status.Where(s => IsFileStaged(s.State))
                               .Select(s => new ChangeItem { FilePath = s.FilePath, Status = MapStatus(s.State), IsStaged = true });

            var unstaged = status.Where(s => !IsFileStaged(s.State))
                                 .Select(s => new ChangeItem { FilePath = s.FilePath, Status = MapStatus(s.State), IsStaged = false });

            return (staged, unstaged);
        }

        public void StageFile(string path) => Commands.Stage(_repo, path);
        public void UnstageFile(string path) => Commands.Unstage(_repo, path);

        private bool IsFileStaged(FileStatus status) =>
            status.HasFlag(FileStatus.NewInIndex) ||
            status.HasFlag(FileStatus.ModifiedInIndex) ||
            status.HasFlag(FileStatus.DeletedFromIndex) ||
            status.HasFlag(FileStatus.RenamedInIndex);

        private ChangeStatus MapStatus(FileStatus status)
        {
            if (status.HasFlag(FileStatus.NewInIndex) || status.HasFlag(FileStatus.NewInWorkdir)) return ChangeStatus.Added;
            if (status.HasFlag(FileStatus.ModifiedInIndex) || status.HasFlag(FileStatus.ModifiedInWorkdir)) return ChangeStatus.Modified;
            if (status.HasFlag(FileStatus.DeletedFromIndex) || status.HasFlag(FileStatus.DeletedFromWorkdir)) return ChangeStatus.Deleted;
            if (status.HasFlag(FileStatus.RenamedInIndex) || status.HasFlag(FileStatus.RenamedInWorkdir)) return ChangeStatus.Renamed;
            if (status.HasFlag(FileStatus.Conflicted)) return ChangeStatus.Conflicted;
            if (status.HasFlag(FileStatus.Ignored)) return ChangeStatus.Ignored;
            return ChangeStatus.Untracked;
        }

        public IEnumerable<CommitInfo> GetCommitLog(int maxCount = 50)
        {
            if (_repo == null) throw new InvalidOperationException("Repository not opened.");
            return _repo.Commits.Take(maxCount)
                        .Select(c => new CommitInfo
                        {
                            Sha = c.Sha,
                            MessageShort = c.MessageShort,
                            AuthorName = c.Author.Name,
                            AuthorWhen = c.Author.When
                        });
        }

        public IEnumerable<Commit> FetchCommitsForGraph(int maxCount = 2000)
        {
            if (_repo == null) throw new InvalidOperationException("Repository not opened.");

            var filter = new CommitFilter
            {
                IncludeReachableFrom = _repo.Branches,
                SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time
            };

            return _repo.Commits.QueryBy(filter).Take(maxCount).ToList(); // materialize
        }
        #endregion

        #region GitHub Operations
        public void CloneRepository(string sourceUrl, string parentDirectory, GitHubUser AuthenticatedUser)
        {
            if (string.IsNullOrWhiteSpace(sourceUrl))
                throw new ArgumentException("Source URL is required.", nameof(sourceUrl));
            if (string.IsNullOrWhiteSpace(parentDirectory))
                throw new ArgumentException("Parent directory is required.", nameof(parentDirectory));

            // 1) Ensure parent directory exists
            Directory.CreateDirectory(parentDirectory);

            // 2) Always create a brand-new subfolder (unique) under the parent directory
            var baseName = SuggestRepoFolderName(sourceUrl);
            var folderName = MakeUniqueFolderName(parentDirectory, baseName);
            var finalPath = Path.Combine(parentDirectory, folderName);
            Directory.CreateDirectory(finalPath);

            // 3) Clone with OAuth token
            // 1) Try anonymous (works for public repos)
            try
            {
                Repository.Clone(sourceUrl, finalPath, new CloneOptions());
            }
            catch (LibGit2SharpException)
            {
                // 2) Needs auth → use GCM (or fallback PAT)
                var creds = _gitCreds.GetForUrl(sourceUrl);
                var co = new CloneOptions();
                co.FetchOptions.CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials
                {
                    Username = creds.Username,
                    Password = creds.Secret
                };
                Repository.Clone(sourceUrl, finalPath, co);
            }

            _repo = new Repository(finalPath);

            _authenticatedUser = AuthenticatedUser;
            if (_authenticatedUser != null)
                SetRepoUserFromAuthenticatedUser(_authenticatedUser);
        }

        /// e.g. "https://github.com/owner/repo.git" -> "repo"
        private static string SuggestRepoFolderName(string url)
        {
            try
            {
                var last = url.TrimEnd('/').Split('/').LastOrDefault() ?? "repository";
                if (last.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                    last = last.Substring(0, last.Length - 4);
                return string.IsNullOrWhiteSpace(last) ? "repository" : last;
            }
            catch { return "repository"; }
        }

        /// Always generate a unique child folder name under 'parent' (repo, repo (2), repo (3), ...)
        private static string MakeUniqueFolderName(string parent, string baseName)
        {
            int i = 1;
            while (true)
            {
                string candidate = i == 1 ? baseName : $"{baseName} ({i})";
                string path = Path.Combine(parent, candidate);
                if (!Directory.Exists(path) && !File.Exists(path))
                    return candidate;
                i++;
            }
        }

        public void PullCurrentBranch(string repoPath, GitHubUser AuthenticatedUser)
        {
            using var repo = new Repository(repoPath);
            var signature = BuildSignature();

            // Try anonymous fetch first (public repos)
            try
            {
                var optsAnon = new PullOptions { FetchOptions = new FetchOptions() };
                Commands.Pull(repo, signature, optsAnon);
                return;
            }
            catch (LibGit2SharpException) { /* fall through to auth */ }

            var remote = repo.Network.Remotes["origin"] ?? throw new InvalidOperationException("Remote 'origin' not found.");
            var creds = _gitCreds.GetForUrl(remote.Url);

            var options = new PullOptions();
            options.FetchOptions = new FetchOptions();
            options.FetchOptions.CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials
            {
                Username = creds.Username,
                Password = creds.Secret
            };
            Commands.Pull(repo, signature, options);
        }

        public void PushCurrentBranch(string repoPath, GitHubUser AuthenticatedUser)
        {
            using var repo = new Repository(repoPath);
            var currentBranch = repo.Head ?? throw new InvalidOperationException("No branch is checked out.");
            var remote = repo.Network.Remotes["origin"] ?? throw new InvalidOperationException("Remote 'origin' not found.");

            var refSpec = $"refs/heads/{currentBranch.FriendlyName}:refs/heads/{currentBranch.FriendlyName}";

            var options = new PushOptions();
            options.CredentialsProvider = (_url, _user, _cred) => new UsernamePasswordCredentials
            {
                Username = AuthenticatedUser.Login,
                Password = AuthenticatedUser.AccessToken
            };

            repo.Network.Push(remote, refSpec, options);
        }
        #endregion
    }
}