namespace GitWave.Services
{
    public sealed class GitCredentials
    {
        public string Username { get; }
        public string Secret { get; }  // PAT/App/GCM token (NOT human password)
        public GitCredentials(string username, string secret)
        {
            Username = username; Secret = secret;
        }
    }

    public interface IGitCredentialProvider
    {
        /// Return username+token suitable for Git over HTTPS (not API OAuth token).
        GitCredentials GetForUrl(string httpsRemoteUrl);
        void StoreForHost(string username, string token);
        void Revoke(string url);
    }
}
