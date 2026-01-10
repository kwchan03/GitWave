using GitWave.Core;
using GitWave.Services;

namespace GitWave.ViewModels
{
    public class PullRequestHostViewModel
    {
        public NavigationService NavService { get; }
        private readonly IGitService _gitService;

        public PullRequestHostViewModel(NavigationService navService, IGitService gitService)
        {
            NavService = navService;
            _gitService = gitService;
            Initialize();
        }

        private void Initialize()
        {
            if (_gitService.AuthenticatedUser == null)
                NavService.Navigate<LoginViewModel>(NavigationRegion.PullRequest);
            else if (_gitService.IsRepositoryOpen)
                NavService.Navigate<PullRequestPageViewModel>(NavigationRegion.PullRequest);
            else
                NavService.Navigate<RepositoryViewModel>(NavigationRegion.PullRequest);
        }
    }
}
