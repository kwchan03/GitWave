using GitWave.Core;
using GitWave.Models;
using LibGit2Sharp;
using System.Diagnostics;
using System.IO;
using Commit = LibGit2Sharp.Commit;
using Repository = LibGit2Sharp.Repository;
using Signature = LibGit2Sharp.Signature;

namespace GitWave.Services
{
    public class GitLibService : IGitService
    {
        private Repository _repo;
        private string _repoPath;
        private readonly IGitCredentialProvider _gitCreds;
        private GitHubUser _authenticatedUser;
        public event Action? OnAuthenticationChanged;
        public event Action? OnRepositoryOpened;
        public GitHubUser AuthenticatedUser { get; set; }
        public string GetRepositoryPath()
        {
            return _repoPath;
        }

        public GitLibService(IGitCredentialProvider gitCreds)
        {
            _gitCreds = gitCreds ?? throw new ArgumentNullException(nameof(gitCreds));
        }

        public void SetAuthenticatedUser(GitHubUser user)
        {
            AuthenticatedUser = user;
            OnAuthenticationChanged?.Invoke();
        }

        #region Repository Management
        public bool OpenRepository(string path)
        {
            if (!Repository.IsValid(path))
                throw new InvalidOperationException($"No git repository found at {path}");

            _repoPath = path;
            _repo = new Repository(path);

            if (_authenticatedUser != null)
                SetRepoUserFromAuthenticatedUser(_authenticatedUser);
            IsRepositoryOpen = true;
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                OnRepositoryOpened?.Invoke();
            });
            return true;
        }

        public void CreateRepository(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            if (!Repository.IsValid(path))
            {
                Repository.Init(path);

                using (var tempRepo = new Repository(path))
                {
                    // Create README
                    var readmePath = Path.Combine(tempRepo.Info.WorkingDirectory, "README.md");
                    if (!File.Exists(readmePath))
                        File.WriteAllText(readmePath, "# Initial Commit\nThis is the first commit.");

                    // Stage
                    Commands.Stage(tempRepo, readmePath);

                    Signature signature;
                    if (_authenticatedUser != null)
                    {
                        string name = _authenticatedUser.Login;
                        string email = !string.IsNullOrWhiteSpace(_authenticatedUser.Email)
                            ? _authenticatedUser.Email
                            : $"{name}@users.noreply.github.com";
                        signature = new Signature(name, email, DateTimeOffset.Now);
                    }
                    else
                    {
                        signature = tempRepo.Config.BuildSignature(DateTimeOffset.Now);
                    }

                    tempRepo.Commit("Initial commit", signature, signature);
                }
            }
            OpenRepository(path);
        }

        public Repository GetRepository()
        {
            if (_repo == null)
                throw new InvalidOperationException("Repository not opened.");
            return _repo;
        }

        public string? FindGitRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                // LibGit2Sharp.Repository.Discover searches up the directory tree for .git
                var gitPath = Repository.Discover(path);
                if (!string.IsNullOrEmpty(gitPath))
                {
                    return gitPath;
                }
            }
            catch
            { }
            return null;
        }

        public bool TryGetRepository(out Repository repository)
        {
            repository = _repo;
            return _repo != null;
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
            {
                string name = _authenticatedUser.Login;

                // FIX: Construct the specific GitHub no-reply email if the public one is missing.
                // Format: {username}@users.noreply.github.com
                string email = !string.IsNullOrWhiteSpace(_authenticatedUser.Email)
                    ? _authenticatedUser.Email
                    : $"{name}@users.noreply.github.com";

                return new Signature(name, email, DateTimeOffset.Now);
            }

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

        public bool IsRepositoryOpen { get; private set; }

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

            if (_repo.RetrieveStatus().IsDirty)
            {
                throw new InvalidOperationException("You have uncommitted changes. Please commit or stash them before merging.");
            }

            var signature = BuildSignature();

            // 2. Capture the result
            var result = _repo.Merge(target, signature);

            // 3. FIX: specific check for conflicts
            if (result.Status == MergeStatus.Conflicts)
            {
                // Throwing here allows your ViewModel to catch it and show a "Merge Failed" message box
                throw new InvalidOperationException("Merge resulted in conflicts. Please resolve them in the 'Changes' list before committing.");
            }
            else if (result.Status == MergeStatus.UpToDate)
            {
                // Optional: You can choose to throw or just let it finish silently
                // throw new InvalidOperationException("Branch is already up to date.");
            }
        }

        public void DeleteBranch(string branchName)
        {
            if (_repo == null)
                throw new InvalidOperationException("Repository not opened.");

            var branch = _repo.Branches[branchName];
            if (branch == null)
                throw new InvalidOperationException($"Branch '{branchName}' not found");

            if (branch.IsCurrentRepositoryHead)
                throw new InvalidOperationException($"Cannot delete the currently checked out branch '{branchName}'");

            _repo.Branches.Remove(branchName);
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

            var unstaged = status.Where(s => IsFileUnstaged(s.State))
                                 .Select(s => new ChangeItem { FilePath = s.FilePath, Status = MapStatus(s.State), IsStaged = false });

            return (staged, unstaged);
        }

        public void StageFile(string path)
        {
            var cleanPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Commands.Stage(_repo, cleanPath);
        }
        public void UnstageFile(string path)
        {
            var cleanPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Commands.Unstage(_repo, cleanPath);
        }

        private bool IsFileStaged(FileStatus status) =>
            status.HasFlag(FileStatus.NewInIndex) ||
            status.HasFlag(FileStatus.ModifiedInIndex) ||
            status.HasFlag(FileStatus.DeletedFromIndex) ||
            status.HasFlag(FileStatus.RenamedInIndex);

        private bool IsFileUnstaged(FileStatus status) =>
            status.HasFlag(FileStatus.NewInWorkdir) ||
            status.HasFlag(FileStatus.ModifiedInWorkdir) ||
            status.HasFlag(FileStatus.DeletedFromWorkdir) ||
            status.HasFlag(FileStatus.RenamedInWorkdir) ||
            status.HasFlag(FileStatus.TypeChangeInWorkdir);

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

        public IEnumerable<Commit> FetchCommitsForGraph()
        {
            if (_repo == null)
                throw new InvalidOperationException("Repository not opened.");

            var filter = new CommitFilter
            {
                IncludeReachableFrom = _repo.Branches,
                SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time // Only this
            };

            var commits = _repo.Commits.QueryBy(filter).ToList();

            DebugPrintCommits(commits);

            return commits;
        }

        private void DebugPrintCommits(List<Commit> commits)
        {
            Debug.WriteLine("\n╔" + new string('═', 98) + "╗");
            Debug.WriteLine("║ COMMIT ORDER DEBUG" + new string(' ', 79) + "║");
            Debug.WriteLine("╚" + new string('═', 98) + "╝");
            Debug.WriteLine($"Total commits: {commits.Count}\n");

            for (int i = 0; i < commits.Count; i++)
            {
                var c = commits[i];
                var msg = c.Message.Split('\n')[0];
                if (msg.Length > 40) msg = msg.Substring(0, 37) + "...";

                Debug.WriteLine($"[{i,2}] {c.Sha.Substring(0, 7)} {c.Author.When:yyyy-MM-dd HH:mm}  {msg,-40}  Parents: {c.Parents.Count()}");
            }

            Debug.WriteLine("");

            if (commits.Count >= 2)
            {
                var first = commits[0].Author.When;
                var last = commits[commits.Count - 1].Author.When;

                Debug.WriteLine($"First: {commits[0].Sha.Substring(0, 7)} ({first:yyyy-MM-dd})");
                Debug.WriteLine($"Last:  {commits[commits.Count - 1].Sha.Substring(0, 7)} ({last:yyyy-MM-dd})");
                Debug.WriteLine("");

                if (first > last)
                    Debug.WriteLine("✓ NEWEST → OLDEST");
                else if (first < last)
                    Debug.WriteLine("✗ OLDEST → NEWEST (Layout expects this!)");
                else
                    Debug.WriteLine("⚠️  SAME DATE");
            }

            Debug.WriteLine("╔" + new string('═', 98) + "╗\n");
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
                OpenRepository(finalPath);
            }
            catch (Exception ex)
            {
                if (Directory.Exists(finalPath))
                {
                    try { Directory.Delete(finalPath, true); } catch { /* Ignore cleanup errors */ }
                }
                throw;
            }


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

        // Add this inside GitLibService class
        public (string Owner, string Name) GetRemoteRepoInfo()
        {
            if (_repo == null) return (null, null);

            // 1. Try to get 'origin', otherwise grab the first remote found
            var remote = _repo.Network.Remotes["origin"] ?? _repo.Network.Remotes.FirstOrDefault();

            if (remote == null) return (null, null);

            // 2. Clean the URL (remove .git extension and trailing slashes)
            // Supports: 
            // - https://github.com/Owner/Repo.git
            // - git@github.com:Owner/Repo.git
            var url = remote.Url.TrimEnd('/');
            if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(0, url.Length - 4);
            }

            // 3. Parse the URL to find the last two segments
            var parts = url.Split(new[] { '/', ':' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
            {
                // Return (Owner, RepoName)
                return (parts[parts.Length - 2], parts[parts.Length - 1]);
            }

            return (null, null);
        }
    }
}