using System.Diagnostics;

namespace GitWave.Services
{
    public sealed class GcmCredentialProvider : IGitCredentialProvider
    {
        private const int ProcessTimeoutMs = 120000;

        public GitCredentials GetForUrl(string httpsRemoteUrl)
        {
            var uri = new Uri(httpsRemoteUrl);
            if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
                !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only https://github.com/* is supported.");

            // Host-wide request (no path) so one login covers all repos
            var input = "protocol=https\nhost=github.com\n\n";

            try
            {
                var (exit, stdout, stderr) = Run("git", "credential-manager get", input);
                if (exit != 0)
                    throw new InvalidOperationException($"GCM failed: {stderr}");

                string? user = null, token = null;
                foreach (var line in stdout.Split('\n'))
                {
                    var i = line.IndexOf('=');
                    if (i <= 0) continue;
                    var k = line[..i].Trim();
                    var v = line[(i + 1)..].Trim();
                    if (k.Equals("username", StringComparison.OrdinalIgnoreCase)) user = v;
                    if (k.Equals("password", StringComparison.OrdinalIgnoreCase)) token = v;
                }

                if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(token))
                    throw new InvalidOperationException("Authentication was not completed. Please try again.");

                return new GitCredentials(user!, token!);
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException("Login timed out. Please try again without closing the browser.");
            }
        }

        public void StoreForHost(string username, string token)
        {
            var input = $"protocol=https\nhost=github.com\nusername={username}\npassword={token}\n\n";
            try
            {
                var (exit, _, stderr) = Run("git", "credential-manager store", input);
                if (exit != 0)
                    throw new InvalidOperationException($"Failed to store credentials: {stderr}");
            }
            catch (TimeoutException)
            {
                throw new InvalidOperationException("Credential storage timed out.");
            }
        }

        // ✅ KEY CHANGE: Add timeout to WaitForExit
        private static (int exit, string stdout, string stderr) Run(string file, string args, string? stdin = null)
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {file}");

            try
            {
                if (stdin != null)
                {
                    p.StandardInput.Write(stdin);
                    p.StandardInput.Close();
                }

                // ✅ Wait with timeout (30 seconds max)
                bool exited = p.WaitForExit(ProcessTimeoutMs);

                if (!exited)
                {
                    // ✅ Kill the process if it timed out
                    try
                    {
                        p.Kill(entireProcessTree: true);
                    }
                    catch { /* ignore */ }

                    throw new TimeoutException($"GCM process did not complete within {ProcessTimeoutMs}ms");
                }

                var outText = p.StandardOutput.ReadToEnd();
                var errText = p.StandardError.ReadToEnd();

                return (p.ExitCode, outText, errText);
            }
            finally
            {
                // ✅ Ensure process is cleaned up
                if (!p.HasExited)
                {
                    try { p.Kill(entireProcessTree: true); }
                    catch { }
                }
            }
        }

        public void Revoke(string url)
        {
            // 1. Parse the URL
            if (!url.StartsWith("http")) url = "https://" + url;
            var uri = new Uri(url);

            // 2. Prepare input for git credential reject
            var input = $"protocol={uri.Scheme}\nhost={uri.Host}\n\n";

            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "credential reject",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (var proc = Process.Start(psi))
                {
                    // ✅ Timeout for revoke too
                    if (!proc.WaitForExit(5000))
                    {
                        try { proc.Kill(entireProcessTree: true); }
                        catch { }
                    }

                    proc.StandardInput.Write(input);
                    proc.StandardInput.Flush();
                    proc.StandardInput.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to revoke: {ex.Message}");
            }
        }
    }
}