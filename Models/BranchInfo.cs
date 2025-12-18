namespace GitWave.Models
{
    public class BranchInfo
    {
        public string Name { get; set; } = "";
        public bool IsCurrent { get; set; }
        public string TipSha { get; set; } = "";
    }
}
