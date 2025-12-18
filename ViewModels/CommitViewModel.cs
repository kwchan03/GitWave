namespace GitWave.ViewModels
{
    public class CommitViewModel : TimelineItemViewModel
    {
        public string Sha { get; set; }
        public string ShortSha => Sha?.Substring(0, 7);
        public string Message { get; set; }
        public string MessageHeadline { get; set; } // First line of message
        public string VerificationStatus { get; set; } // "verified", "unsigned"
        public bool IsVerified => VerificationStatus == "verified";
    }
}