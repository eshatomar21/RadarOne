using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;

namespace Radar_CRM.Controllers
{
    public class MetaAuthController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public MetaAuthController(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        // 1. Shows the Integrations Dashboard UI
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // 2. Fired when the "Connect Facebook" button is clicked
        [HttpGet]
        public IActionResult Connect()
        {
            string appId = _configuration["MetaIntegration:AppId"];
            string redirectUri = _configuration["MetaIntegration:RedirectUri"]; // e.g., https://localhost:5001/MetaAuth/Callback

            string state = Guid.NewGuid().ToString();
            string scopes = "pages_show_list,pages_read_engagement,pages_manage_metadata,leads_retrieval";

            string metaAuthUrl = $"https://www.facebook.com/v18.0/dialog/oauth?client_id={appId}&redirect_uri={redirectUri}&state={state}&scope={scopes}";

            return Redirect(metaAuthUrl);
        }

        // 3. Meta redirects the user back here after approval
        [HttpGet]
        public async Task<IActionResult> Callback(string code, string state, string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                ViewBag.Message = $"Error linking Facebook: {error}";
                return View("CallbackResult");
            }

            if (string.IsNullOrEmpty(code))
            {
                ViewBag.Message = "No authorization code received.";
                return View("CallbackResult");
            }

            string appId = _configuration["MetaIntegration:AppId"];
            string appSecret = _configuration["MetaIntegration:AppSecret"];
            string redirectUri = _configuration["MetaIntegration:RedirectUri"];

            string tokenExchangeUrl = $"https://graph.facebook.com/v18.0/oauth/access_token?client_id={appId}&redirect_uri={redirectUri}&client_secret={appSecret}&code={code}";

            var response = await _httpClient.GetAsync(tokenExchangeUrl);
            var jsonResponse = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                using var document = JsonDocument.Parse(jsonResponse);
                string userAccessToken = document.RootElement.GetProperty("access_token").GetString();

                // TODO for Step 2: Swap this for a Long-Lived Token and save to database
                ViewBag.Message = $"Success! Temporary Access Token Received: {userAccessToken}";
                return View("CallbackResult");
            }

            ViewBag.Message = $"Failed to get token: {jsonResponse}";
            return View("CallbackResult");
        }
    }
}