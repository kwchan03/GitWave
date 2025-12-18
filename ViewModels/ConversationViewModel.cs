using GitWave.Services;
using Octokit;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace GitWave.ViewModels
{
    public class ConversationViewModel : BaseViewModel
    {
        private readonly GitHubApiService _apiService;
        private readonly string _owner;
        private readonly string _repo;
        private readonly int _prNumber;

        // The master list bound to the UI
        public ObservableCollection<TimelineItemViewModel> TimelineItems { get; }
            = new ObservableCollection<TimelineItemViewModel>();

        public ICommand RefreshCommand { get; }

        public ConversationViewModel(GitHubApiService apiService, string owner, string repo, int prNumber)
        {
            _apiService = apiService;
            _owner = owner;
            _repo = repo;
            _prNumber = prNumber;

            RefreshCommand = new AsyncRelayCommand(_ => LoadDataAsync());

            // Start loading immediately
            if (_prNumber > 0) LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            TimelineItems.Clear();

            try
            {
                // 1. Fetch all data in parallel
                var tCommits = _apiService.GetCommits(_owner, _repo, _prNumber);
                var tComments = _apiService.GetGeneralComments(_owner, _repo, _prNumber);
                var tEvents = _apiService.GetSystemEvents(_owner, _repo, _prNumber);
                var tReviews = _apiService.GetReviews(_owner, _repo, _prNumber);
                var tReviewComments = _apiService.GetReviewComments(_owner, _repo, _prNumber);

                await Task.WhenAll(tCommits, tComments, tEvents, tReviews, tReviewComments);

                var allItems = new List<TimelineItemViewModel>();

                // 2. Process Commits
                foreach (var c in await tCommits)
                {
                    allItems.Add(new CommitViewModel
                    {
                        CreatedAt = c.Commit.Committer.Date,
                        Actor = new UserViewModel { Login = c.Author?.Login ?? c.Commit.Author.Name, AvatarUrl = c.Author?.AvatarUrl },
                        Sha = c.Sha,
                        Message = c.Commit.Message,
                        MessageHeadline = c.Commit.Message.Split('\n')[0]
                    });
                }

                // 3. Process General Comments
                foreach (var c in await tComments)
                {
                    allItems.Add(new GeneralCommentViewModel
                    {
                        CreatedAt = c.CreatedAt,
                        Actor = new UserViewModel { Login = c.User.Login, AvatarUrl = c.User.AvatarUrl },
                        Body = c.Body,
                        HtmlUrl = c.HtmlUrl,
                        Id = c.Id
                    });
                }

                // 4. Process System Events
                foreach (var e in await tEvents)
                {
                    if (IsRelevantEvent(e.Event))
                    {
                        allItems.Add(MapSystemEvent(e));
                    }
                }

                // 5. Process Reviews & Threads (GLOBAL THREADING FIX)
                var reviews = await tReviews;
                var allComments = await tReviewComments;

                // A. Create the Review Containers first (Dictionary for fast lookup)
                var reviewVmMap = new Dictionary<long, ReviewViewModel>();
                foreach (var r in reviews)
                {
                    var vm = new ReviewViewModel
                    {
                        Id = r.Id,
                        CreatedAt = r.SubmittedAt,
                        Actor = new UserViewModel { Login = r.User.Login, AvatarUrl = r.User.AvatarUrl },
                        State = r.State.StringValue,
                        Body = r.Body,
                        AuthorAssociation = r.AuthorAssociation.StringValue
                    };

                    // Store in map so we can find it later by ID
                    reviewVmMap[r.Id] = vm;
                }

                // B. Group ALL comments by their Thread (Root ID)
                //    This connects replies (Review B) back to the original thread (Review A)
                var globalThreads = allComments.GroupBy(c => c.InReplyToId ?? c.Id);

                foreach (var threadGroup in globalThreads)
                {
                    // 1. Identify the "Root" comment (the one that started the thread)
                    var root = threadGroup.OrderBy(c => c.CreatedAt).First();

                    // 2. Find which Review this thread belongs to (based on the ROOT's Review ID)
                    if (root.PullRequestReviewId.HasValue &&
                        reviewVmMap.TryGetValue(root.PullRequestReviewId.Value, out var parentReview))
                    {
                        // 3. Create the Thread VM
                        var threadVm = new ReviewThreadViewModel
                        {
                            FilePath = root.Path,
                            OriginalPosition = root.OriginalPosition ?? root.Position,
                            DiffHunk = root.DiffHunk
                        };

                        // 4. Add ALL comments (Root + Replies) to this thread
                        //    This ensures replies from later reviews are visually glued to the original thread
                        foreach (var c in threadGroup.OrderBy(x => x.CreatedAt))
                        {
                            threadVm.Comments.Add(new ReviewCommentViewModel
                            {
                                Actor = new UserViewModel { Login = c.User.Login, AvatarUrl = c.User.AvatarUrl },
                                Body = c.Body,
                                CreatedAt = c.CreatedAt
                            });
                        }

                        // 5. Place the populated thread into the PARENT Review
                        parentReview.Threads.Add(threadVm);
                    }
                }

                // C. Add the populated Reviews to the main list
                foreach (var review in reviewVmMap.Values)
                {
                    bool isEmptyShell = string.IsNullOrWhiteSpace(review.Body) &&
                                        review.Threads.Count == 0 &&
                                        review.State == "COMMENTED"; // Only hide if it's a neutral comment

                    if (!isEmptyShell)
                    {
                        allItems.Add(review);
                    }
                }

                // 6. Final Sort & Update UI
                var sorted = allItems.OrderBy(x => x.CreatedAt).ToList();
                foreach (var item in sorted)
                {
                    TimelineItems.Add(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private bool IsRelevantEvent(StringEnum<EventInfoState> eventType)
        {
            return eventType.Value == EventInfoState.Merged ||
                   eventType.Value == EventInfoState.Closed ||
                   eventType.Value == EventInfoState.Reopened ||
                   eventType.Value == EventInfoState.Assigned;
        }

        private SystemEventViewModel MapSystemEvent(IssueEvent e)
        {
            var vm = new SystemEventViewModel
            {
                CreatedAt = e.CreatedAt,
                Actor = new UserViewModel { Login = e.Actor.Login, AvatarUrl = e.Actor.AvatarUrl },
                EventType = e.Event.StringValue
            };

            switch (e.Event.Value)
            {
                case EventInfoState.Merged:
                    vm.Icon = "⭮"; vm.IconColor = "#6f42c1"; vm.Description = "merged commit"; break;
                case EventInfoState.Closed:
                    vm.Icon = "⊘"; vm.IconColor = "#cb2431"; vm.Description = "closed this"; break;
                case EventInfoState.Reopened:
                    vm.Icon = "🔓"; vm.IconColor = "#28a745"; vm.Description = "reopened this"; break;
                case EventInfoState.Assigned:
                    vm.Icon = "👤"; vm.IconColor = "#28a745"; vm.Description = $"assigned {e.Assignee?.Login}"; break;
                default:
                    vm.Icon = "•"; vm.IconColor = "Gray"; vm.Description = e.Event.StringValue; break;
            }
            return vm;
        }
    }
}