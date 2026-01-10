using GitWave.Core;
using GitWave.Services;
using GitWave.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GitGUI.Core
{
    public static class Bootstrapper
    {
        public static IServiceProvider Services { get; private set; }

        public static void Initialize()
        {
            if (Services != null) return; // Run once

            var services = new ServiceCollection();

            // --- 1. Register Services ---
            // Navigation Service (Singleton - shared state for the whole plugin)
            services.AddSingleton<NavigationService>();

            services.AddSingleton<IGitCredentialProvider, GcmCredentialProvider>();
            services.AddSingleton<GitHubAuthService>();
            services.AddSingleton<IGitService>(sp =>
            {
                var credProvider = sp.GetRequiredService<IGitCredentialProvider>();
                return new GitLibService(credProvider);
            });
            services.AddSingleton<RepositoryWatcherService>();
            services.AddSingleton<TapDockContextService>();

            // --- 2. Register ViewModels ---

            // Host ViewModels (The "Shells" for your frames)
            services.AddTransient<SourceControlHostViewModel>();
            services.AddTransient<PullRequestHostViewModel>();

            // Content ViewModels (The actual screens)
            services.AddTransient<LoginViewModel>();
            services.AddSingleton<RepositoryViewModel>();
            services.AddTransient<CommitGraphViewModel>();
            services.AddTransient<OperationViewModel>();     // Transient so it doesn't keep state (tabs, logs)
            services.AddTransient<PullRequestPageViewModel>(); // Transient if you want to keep loaded PRs

            // Note: We DO NOT register "Pages" (LoginPage, etc.) anymore. 
            // WPF DataTemplates in Styles.xaml will handle finding the View for the ViewModel.

            Services = services.BuildServiceProvider();

            // --- 3. Initialize Global Logic ---
            InitializeGlobalNavigationLogic();
        }

        private static void InitializeGlobalNavigationLogic()
        {
            var gitService = Services.GetRequiredService<IGitService>();
            var navigationService = Services.GetRequiredService<NavigationService>();

            // We cast to GitLibService only to access the specific events
            if (gitService is GitLibService gitLib)
            {
                // Logic: When a Repo is opened, the SourceControl frame should switch to the Operation View
                gitLib.OnRepositoryOpened += () =>
                {
                    // Ensure we are on the UI thread if this event comes from a background task
                    // (But usually ViewModels handle dispatching. Here we just update the State in NavigationService)
                    navigationService.Navigate<OperationViewModel>(NavigationRegion.SourceControl);
                    navigationService.Navigate<PullRequestPageViewModel>(NavigationRegion.PullRequest);
                };
            }
        }
    }
}