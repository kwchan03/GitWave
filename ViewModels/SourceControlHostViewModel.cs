using GitWave.Core;
using GitWave.Services;

namespace GitWave.ViewModels
{
    // ViewModels/SourceControlHostViewModel.cs
    public class SourceControlHostViewModel : BaseViewModel
    {
        public NavigationService NavService { get; }
        private readonly IGitService _gitService;

        public SourceControlHostViewModel(NavigationService navService, IGitService gitService)
        {
            NavService = navService;
            _gitService = gitService;
            Initialize();
        }

        private void Initialize()
        {
            if (_gitService.AuthenticatedUser == null)
                NavService.Navigate<LoginViewModel>(NavigationRegion.SourceControl);
            else if (_gitService.IsRepositoryOpen)
                NavService.Navigate<OperationViewModel>(NavigationRegion.SourceControl);
            else
                NavService.Navigate<RepositoryViewModel>(NavigationRegion.SourceControl);
        }
    }
}
