using GitGUI.Core;
using GitGUI.Pages;
using GitGUI.Services;
using GitGUI.ViewModels;
using Keysight.Ccl.Wsl.UI;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace GitGUI
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider Services { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 0) Initialize WSL UX (themes/skins/fonts) before any window is shown
            //TestPlanGrid testPlanGrid = new TestPlanGrid();
            //testPlanGrid.InitializeComponent();
            UXManager.Initialize("System");
            UXManager.ColorScheme = "CaranuLight";
            Keysight.Ccl.Wsl.UI.Managers.SkinManager.Instance.SkinFragment("CaranuDark", false);

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

            // Register Pages
            services.AddTransient<LoginPage>();
            services.AddTransient<PullRequestPage>();
            services.AddTransient<OperationPage>();

            // Register MainWindow
            services.AddSingleton<MainWindow>();

            Services = services.BuildServiceProvider();

            // Start Kestrel listener for OAuth
            Globals.InitiateListener();

            // Resolve MainWindow and show
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            // Navigate to LoginPage first
            //var loginPage = Services.GetRequiredService<LoginPage>();
            var loginPage = Services.GetRequiredService<LoginPage>();
            mainWindow.NavigateTo(loginPage);

            //var window = new GitGUI.Pages.TestPlanView();
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
