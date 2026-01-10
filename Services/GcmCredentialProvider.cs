using System.Diagnostics;

namespace GitWave.Services
{
    /// Resolves GitHub credentials via Git Credential Manager (device flow + secure cache).
    /// Requires GCM installed and configured (e.g., `git-credential-manager configure`).
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
            var (exit, stdout, stderr) = Run("git", "credential-manager get", input);
            if (exit != 0) throw new InvalidOperationException($"GCM get failed: {stderr}");

            string? user = null, token = null;
            foreach (var line in stdout.Split('\n'))
            {
                var i = line.IndexOf('=');
                if (i <= 0) continue;
                var k = line[..i].Trim(); var v = line[(i + 1)..].Trim();
                if (k.Equals("username", StringComparison.OrdinalIgnoreCase)) user = v;
                if (k.Equals("password", StringComparison.OrdinalIgnoreCase)) token = v;
            }
            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(token))
                throw new InvalidOperationException("GCM returned no username/token. Complete the sign-in when prompted, then retry.");

            return new GitCredentials(user!, token!);
        }

        public void StoreForHost(string username, string token)
        {
            var input = $"protocol=https\nhost=github.com\nusername={username}\npassword={token}\n\n";
            var (exit, _, stderr) = Run("git", "credential-manager store", input);
            if (exit != 0) throw new InvalidOperationException($"GCM store failed: {stderr}");
        }

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
            using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start {file} {args}");
            if (stdin != null) { p.StandardInput.Write(stdin); p.StandardInput.Close(); }
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

        public void Revoke(string url)
        {
            // 1. Parse the URL (e.g., https://github.com/)
            if (!url.StartsWith("http")) url = "https://" + url;
            var uri = new System.Uri(url);

            // 2. Prepare the input for git credential reject
            var input = $"protocol={uri.Scheme}\nhost={uri.Host}\n\n";

            // 3. Call git credential reject
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "credential reject",
                RedirectStandardInput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi))
            {
                proc.StandardInput.Write(input);
                proc.StandardInput.Flush();
                proc.StandardInput.Close();
                proc.WaitForExit();
            }
        }
    }
}
