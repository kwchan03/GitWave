namespace GitGUI.Models
{
    /// <summary>
    /// Describes the type of line segment in the commit graph.
    /// </summary>
    public enum SegmentKind
    {
        /// <summary>
        /// A straight vertical line continuing the same branch.
        /// </summary>
        Vertical,

        /// <summary>
        /// A diagonal or curved line connecting to a merge parent.
        /// </summary>
        Merge,

        /// <summary>
        /// A diagonal or curved line indicating a branch fork.
        /// </summary>
        Branch
    }
}
