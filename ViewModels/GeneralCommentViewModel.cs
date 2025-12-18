using System.Collections.ObjectModel;

namespace GitWave.ViewModels
{
    public class GeneralCommentViewModel : TimelineItemViewModel
    {
        public long Id { get; set; }
        public string Body { get; set; }
        public string HtmlUrl { get; set; }

        // Quick interaction
        public bool CanEdit { get; set; }
        public bool CanDelete { get; set; }

        // Reaction summary (e.g. "👍 2  🚀 1")
        public ObservableCollection<ReactionViewModel> Reactions { get; set; } = new ObservableCollection<ReactionViewModel>();
    }

    public class ReactionViewModel
    {
        public string Emoji { get; set; } // e.g. 👍
        public int Count { get; set; }
    }
}