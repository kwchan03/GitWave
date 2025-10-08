namespace GitGUI.Models
{
    public sealed class PropertyNode : DiffTreeNode
    {
        public string PropertyName { get; }
        public string? Value { get; }

        public PropertyNode(string propertyName, string? value, System.Windows.Media.Brush background)
            : base(background)
        {
            PropertyName = propertyName;
            Value = value;
            Display = string.IsNullOrWhiteSpace(Value) ? PropertyName : $"{PropertyName}: {Value}";
        }
    }
}
