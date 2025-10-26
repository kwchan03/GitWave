using GitGUI.Models;
using GitGUI.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GitGUI.ViewModels
{
    public sealed class CommitGraphViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<CommitRow> Items { get; } = new();

        private double _graphWidth = 120;
        public double GraphWidth
        {
            get => _graphWidth;
            set { _graphWidth = value; OnPropertyChanged(); }
        }

        private double _rowHeight = 20;
        public double RowHeight
        {
            get => _rowHeight;
            set { _rowHeight = value; OnPropertyChanged(); }
        }

        public void LoadGraph(string repoPath)
        {
            Items.Clear();

            // 1️⃣ Fetch commits
            var commits = GitCommitFetcher.FetchCommits(repoPath);

            // 2️⃣ Build commit graph
            var rows = CommitGraphBuilder.Build(commits);

            // 3️⃣ Add to observable collection
            foreach (var row in rows)
                Items.Add(row);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
