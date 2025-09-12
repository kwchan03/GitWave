using GitGUI.Models;

namespace GitGUI.Core
{
    public interface IAuthState
    {
        GitHubUser CurrentUser { get; set; }
    }

    public sealed class AuthState : IAuthState
    {
        public GitHubUser? CurrentUser { get; set; }
    }
}
