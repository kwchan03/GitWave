using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace GitWave.ViewModels
{
    public class ReviewViewModel : TimelineItemViewModel
    {
        public long Id { get; set; }
        private string _state;
        public string State // "APPROVED", "CHANGES_REQUESTED", "COMMENTED"
        {
            get => _state;
            set
            {
                if (SetProperty(ref _state, value))
                {
                    // Notify that the display value has also changed
                    OnPropertyChanged(nameof(StateDisplay));
                }
            }
        }

        // 2. COMPUTE DISPLAY STATE (e.g., "Requested Changes")
        public string StateDisplay { get => ParseReviewState(State); }
        public string Body { get; set; } // The summary comment (optional)
        private string _authorAssociation;
        public string AuthorAssociation // "OWNER", "MEMBER", "CONTRIBUTOR"
        {
            get => _authorAssociation;
            set
            {
                if (SetProperty(ref _authorAssociation, value))
                {
                    OnPropertyChanged(nameof(AuthorAssociationDisplay));
                }
            }
        }

        public string AuthorAssociationDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(AuthorAssociation) || AuthorAssociation.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                return AuthorAssociation.ToLower().Replace('_', ' ');
            }
        }

        public bool HasBody => !string.IsNullOrEmpty(Body);
        public bool HasThreads => Threads.Count > 0;

        // The file-level discussions nested inside this review
        public ObservableCollection<ReviewThreadViewModel> Threads { get; set; }
            = new ObservableCollection<ReviewThreadViewModel>();

        private string ParseReviewState(string rawState)
        {
            if (string.IsNullOrEmpty(rawState)) return string.Empty;

            return rawState.ToUpper() switch
            {
                "APPROVED" => "approved",
                "CHANGES_REQUESTED" => "requested changes",
                "COMMENTED" => "reviewed",
                "DISMISSED" => "dismissed",
                "PENDING" => "pending",
                // Fallback: "SOME_OTHER_STATE" -> "Some Other State"
                _ => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(rawState.ToLower().Replace('_', ' '))
            };
        }
    }

    // A specific conversation on a file (File A, Line 10)
    public class ReviewThreadViewModel
    {
        public string FilePath { get; set; }
        public int? Position { get; set; }
        public int? OriginalPosition { get; set; }
        public string DiffHunk { get; set; } // The code snippet context
        public bool IsResolved { get; set; } // You might need to track this manually or via GraphQL

        // The actual bubbles in this thread
        public ObservableCollection<ReviewCommentViewModel> Comments { get; set; }
            = new ObservableCollection<ReviewCommentViewModel>();

        // Reply logic
        public string NewReplyText { get; set; }
        public ICommand ReplyCommand { get; set; }
        public ICommand ResolveCommand { get; set; }
    }

    // Individual comments inside a thread
    public class ReviewCommentViewModel : GeneralCommentViewModel
    {
        public string DiffHunk { get; set; }
        public long? InReplyToId { get; set; } // Used to reconstruct the tree structure
    }
}