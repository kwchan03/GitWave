using System.Diagnostics;

namespace GitGUI.Services
{
    /// Resolves GitHub credentials via Git Credential Manager (device flow + secure cache).
    /// Requires GCM installed and configured (e.g., `git-credential-manager configure`).
    public sealed class GcmCredentialProvider : IGitCredentialProvider
    {
        public GitCredentials GetForUrl(string httpsRemoteUrl)
        {
            if (string.IsNullOrWhiteSpace(httpsRemoteUrl))
                throw new ArgumentException("Remote URL is required.", nameof(httpsRemoteUrl));

            var uri = new Uri(httpsRemoteUrl);
            if (!string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only https://github.com/* is supported by this provider.");

            // Standard git-credential protocol input
            var input = $"protocol=https\nhost=github.com\npath={uri.AbsolutePath.TrimStart('/')}\n\n";

            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "credential-manager get",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi) ?? throw new InvalidOperationException(
                "Failed to start 'git credential-manager'. Is Git Credential Manager installed?");
            p.StandardInput.Write(input);
            p.StandardInput.Close();

            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0)
                throw new InvalidOperationException($"GCM error (exit {p.ExitCode}). Details: {stderr}");

            string? user = null, token = null;
            foreach (var line in stdout.Split('\n'))
            {
                var i = line.IndexOf('=');
                if (i <= 0) continue;
                var key = line[..i].Trim();
                var val = line[(i + 1)..].Trim();
                if (key.Equals("username", StringComparison.OrdinalIgnoreCase)) user = val;
                if (key.Equals("password", StringComparison.OrdinalIgnoreCase)) token = val; // token (PAT-like)
            }

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(token))
                throw new InvalidOperationException("GCM returned no username/token. Sign in when prompted, then retry.");

            return new GitCredentials(user!, token!);
        }
    }
}
