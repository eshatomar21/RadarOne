using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using Radar_CRM.Data;
using Radar_CRM.Models;

namespace Radar_CRM.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MetaWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory _http;

        public MetaWebhookController(ApplicationDbContext db, IHttpClientFactory http)
        {
            _db = db;
            _http = http;
        }

        [HttpGet]
        public IActionResult VerifyWebhook([FromQuery(Name = "hub.mode")] string mode, [FromQuery(Name = "hub.challenge")] string challenge)
        {
            // Verify webhook with Meta
            return Ok(challenge);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessLead([FromBody] JsonElement payload)
        {
            try
            {
                var changes = payload.GetProperty("entry")[0].GetProperty("changes")[0].GetProperty("value");
                string leadgenId = changes.GetProperty("leadgen_id").GetString();
                string pageId = changes.GetProperty("page_id").GetString();

                var chain = await _db.LeadChains
                    .Include(c => c.FieldMappings)
                    .FirstOrDefaultAsync(c => c.FacebookPageId == pageId && c.IsActive);

                if (chain == null) return Ok();

                var client = _http.CreateClient();
                var response = await client.GetAsync($"https://graph.facebook.com/v19.0/{leadgenId}?access_token={chain.PageAccessToken}");
                var jsonResponse = await response.Content.ReadAsStringAsync();

                var leadData = JsonDocument.Parse(jsonResponse).RootElement.GetProperty("field_data");

                // Map directly to the Account model
                var newAccount = new Account
                {
                    DataSource = "Meta Ads",
                    DateOfEntry = DateTime.Now
                };

                foreach (var mapping in chain.FieldMappings)
                {
                    var metaValue = leadData.EnumerateArray()
                        .FirstOrDefault(f => f.GetProperty("name").GetString() == mapping.MetaField)
                        .GetProperty("values")[0].GetString();

                    if (!string.IsNullOrEmpty(metaValue))
                    {
                        switch (mapping.CrmField)
                        {
                            case "Account Name":
                                newAccount.AccountName = metaValue;
                                break;
                            case "Email":
                                newAccount.Email = metaValue;
                                break;
                            case "Mobile Number":
                                newAccount.MobileNumber = metaValue;
                                break;
                            case "Work Type":
                                newAccount.WorkType = metaValue;
                                break;
                            case "Clinic Type":
                                newAccount.ClinicType = metaValue;
                                break;
                        }
                    }
                }

                // Save directly to Accounts table
                _db.Accounts.Add(newAccount);

                // Update sync stats
                chain.TotalLeadsSynced++;
                chain.LastLeadDate = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Webhook Error: {ex.Message}");
                return Ok();
            }
        }
    }
}