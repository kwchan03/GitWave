namespace GitWave.ViewModels
{
    public abstract class TimelineItemViewModel : BaseViewModel
    {
        private DateTimeOffset _createdAt;
        public DateTimeOffset CreatedAt
        {
            get => _createdAt;
            set
            {
                if (SetProperty(ref _createdAt, value))
                {
                    // Notify that the display version changed when the raw date changes
                    OnPropertyChanged(nameof(CreatedAtDisplay));
                }
            }
        }

        // 2. Add Display Property (e.g., "Dec 18, 2025 2:30 PM")
        public string CreatedAtDisplay => CreatedAt.ToString("MMM d, yyyy h:mm tt");
        public UserViewModel Actor { get; set; }
    }

    public class UserViewModel
    {
        public string Login { get; set; }
        public string AvatarUrl { get; set; }
        public string HtmlUrl { get; set; }
    }
}