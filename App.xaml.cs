using GitGUI.Core;
using GitGUI.Pages;
using GitGUI.Services;
using GitGUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace GitGUI
{
    public partial class App : System.Windows.Application
    {
        public static IServiceProvider Services { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            var services = new ServiceCollection();

            // 1) Register the GCM credential provider
            services.AddSingleton<IGitCredentialProvider, GcmCredentialProvider>();

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
            var loginPage = Services.GetRequiredService<LoginPage>();
            mainWindow.NavigateTo(loginPage);

            base.OnStartup(e);
        }
    }
}
