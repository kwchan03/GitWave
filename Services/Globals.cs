using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;

namespace GitWave.Services
{
    internal static class Globals
    {
        internal static bool Authorised;
        internal static HttpClient GClient = new HttpClient();

        internal static Action<string> GithubCallback;

        static IWebHost _host;

        internal static void Clear()
        {
            Authorised = false;
            GithubCallback = null;
            GClient = new HttpClient();
        }

        public static void MakeCall(string code)
        {
            GithubCallback?.Invoke(code);
        }

        public static void InitiateListener()
        {
            _host = new WebHostBuilder()
                .UseKestrel()
                .UseStartup<StartupArgs>()
                .UseUrls("http://localhost:8080")
                .Build();

            Task.Run(() => _host.Run());
        }
    }

    internal class StartupArgs
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
