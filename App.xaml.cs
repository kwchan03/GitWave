using GitWave.Core;
using GitWave.Services;
using GitWave.UI.Pages;
using GitWave.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace GitWave
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider Services { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 0) Initialize WSL UX (themes/skins/fonts) before any window is shown
            //TestPlanGrid testPlanGrid = new TestPlanGrid();
            //testPlanGrid.InitializeComponent();
            var services = new ServiceCollection();

            // 1) Register the GCM credential provider
            // Auth & Git credentials via GCM
            services.AddSingleton<IGitCredentialProvider, GcmCredentialProvider>();
            services.AddSingleton<GitHubAuthService>();

            // 2) Register GitLibService with the provider (factory so DI passes the provider)
            services.AddSingleton<IGitService>(sp =>
            {
                var credProvider = sp.GetRequiredService<IGitCredentialProvider>();
                return new GitLibService(credProvider);
            });

            // Register ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddSingleton<OperationViewModel>();
            services.AddSingleton<RepositoryViewModel>();

            // Register Pages
            services.AddTransient<LoginPage>();
            services.AddTransient<RepositoryPage>();
            services.AddTransient<PullRequestPage>();
            services.AddTransient<OperationPage>();
            services.AddTransient<PullRequestPage>();

            // Register MainWindow
            services.AddSingleton<MainWindow>();

            Services = services.BuildServiceProvider();

            // Resolve MainWindow and show
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            // Navigate to LoginPage first
            var loginPage = Services.GetRequiredService<LoginPage>();
            mainWindow.NavigateTo(loginPage);

            //var window = new GitWave.Pages.TestPlanView();
            //MainWindow = window;
            //window.Show();

            //string testPlanPath = @"C:\Users\chank\Documents\UM\Y3S2\WIA3002 ACADEMIC PROJECT I\TestGit\TestPlan.TapPlan";
            //string logPath = @"C:\Users\chank\Documents\UM\Y3S2\WIA3002 ACADEMIC PROJECT I\TestGit\TestPlanDump.txt";

            //TestPlanInspector.Run(testPlanPath);
            //Shutdown();

            base.OnStartup(e);
        }
    }
}
