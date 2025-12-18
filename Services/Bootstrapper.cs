using GitWave.Core;
using GitWave.Services;
using GitWave.UI.Pages;
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

            // Register Services
            services.AddSingleton<IGitCredentialProvider, GcmCredentialProvider>();
            services.AddSingleton<GitHubAuthService>();
            services.AddSingleton<IGitService>(sp =>
            {
                var credProvider = sp.GetRequiredService<IGitCredentialProvider>();
                return new GitLibService(credProvider);
            });

            // Register ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddSingleton<OperationViewModel>();
            services.AddTransient<PullRequestPage>(); // Or UserControls
            services.AddTransient<OperationPage>(); // Or UserControls

            Services = services.BuildServiceProvider();
        }
    }
}