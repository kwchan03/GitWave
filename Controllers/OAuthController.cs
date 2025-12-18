using GitWave.Services;
using Microsoft.AspNetCore.Mvc;

namespace GitWave.Controllers
{
    [Route("/callback")]
    [ApiController]
    public class OAuthController : ControllerBase
    {
        // GET: api/<OAuthController>
        [HttpGet]
        public string GetGithub([FromQuery] string code)
        {
            // Fire-and-forget callback to your WPF app
            Task.Run(() => { Globals.MakeCall(code); });

            // Response shown to user in the browser after successful login
            return "GitHub login successful! You can now close this window and return to your application.";
        }
    }
}