namespace GitWave.ViewModels
{
    public class SystemEventViewModel : TimelineItemViewModel
    {
        public string EventType { get; set; } // "merged", "closed", "assigned"

        // Display Logic
        public string Icon { get; set; }      // Unicode or Icon Key (e.g. "⭮", "🔒")
        public string IconColor { get; set; } // Hex code (e.g. "#6f42c1")
        public string Description { get; set; } // e.g. "merged commit a1b2c into main"

        // Context specific data (optional, depends on event type)
        public string TargetBranch { get; set; } // For merged events
        public string LabelName { get; set; }    // For labeled events
        public string AssigneeName { get; set; } // For assigned events
    }
}