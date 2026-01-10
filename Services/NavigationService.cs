// Services/NavigationService.cs
using GitGUI.Core;
using GitWave.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace GitWave.Services
{
    public enum NavigationRegion { SourceControl, PullRequest, CommitGraph }

    public class NavigationService : INotifyPropertyChanged
    {
        private BaseViewModel? _currentSourceControlViewModel;
        private BaseViewModel? _currentPullRequestViewModel;
        private BaseViewModel? _currentCommitGraphViewModel;

        public BaseViewModel? CurrentSourceControlViewModel
        {
            get => _currentSourceControlViewModel;
            set { _currentSourceControlViewModel = value; OnPropertyChanged(nameof(CurrentSourceControlViewModel)); }
        }

        public BaseViewModel? CurrentPullRequestViewModel
        {
            get => _currentPullRequestViewModel;
            set { _currentPullRequestViewModel = value; OnPropertyChanged(nameof(CurrentPullRequestViewModel)); }
        }

        public BaseViewModel? CurrentCommitGraphViewModel
        {
            get => _currentCommitGraphViewModel;
            set { _currentCommitGraphViewModel = value; OnPropertyChanged(nameof(CurrentCommitGraphViewModel)); }
        }

        public async Task<TViewModel> Navigate<TViewModel>(NavigationRegion region) where TViewModel : BaseViewModel
        {
            // 1. Resolve the ViewModel
            var viewModel = Bootstrapper.Services.GetRequiredService<TViewModel>();

            // 2. Ensure the update happens on the UI Thread automatically
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateRegion(region, viewModel);
                });
            }
            else
            {
                // Fallback for Unit Tests / CLI where no UI thread exists
                UpdateRegion(region, viewModel);
            }

            return viewModel;
        }

        private void UpdateRegion(NavigationRegion region, BaseViewModel viewModel)
        {
            if (region == NavigationRegion.SourceControl)
                CurrentSourceControlViewModel = viewModel;
            else if (region == NavigationRegion.PullRequest)
                CurrentPullRequestViewModel = viewModel;
            else
                CurrentCommitGraphViewModel = viewModel;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}