namespace GitGUI.Models
{
    public class BranchPath
    {
        public int ColorIndex { get; set; }
        public List<(int Row, int Lane)> Points { get; set; }

        public override string ToString()
        {
            return $"BranchPath(color={ColorIndex}, points={Points.Count})";
        }
    }
}
