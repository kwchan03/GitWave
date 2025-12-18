namespace GitWave.Models
{
    public sealed class StepNode : DiffTreeNode
    {
        public Guid Id { get; }
        public string Name { get; }
        public string Type { get; }
        public StepChangeKind Kind { get; }

        public StepNode(Guid id, string name, string type, StepChangeKind kind, System.Windows.Media.Brush background)
            : base(background)
        {
            Id = id;
            Name = name;
            Type = type;
            Kind = kind;

            Display = string.IsNullOrWhiteSpace(Name) ? Type : $"{Name} ({Type})";
        }
    }
}
