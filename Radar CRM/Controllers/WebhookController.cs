using Microsoft.AspNetCore.Mvc;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System;
using System.Threading.Tasks;

namespace Radar_CRM.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public WebhookController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("WordpressContact")]
        public async Task<IActionResult> ReceiveWordpressContact([FromBody] WordpressLeadDto formData)
        {
            // 1. SECURITY CHECK
            string mySecretKey = "RADAR_CRM_SECURE_KEY_2026";

            if (!Request.Headers.TryGetValue("X-API-KEY", out var extractedApiKey) || extractedApiKey != mySecretKey)
            {
                return Unauthorized(new { message = "Invalid or missing API Key." });
            }

            if (formData == null || string.IsNullOrEmpty(formData.Name))
            {
                return BadRequest(new { message = "Form data is empty or missing the Name field." });
            }

            try
            {
                // 2. IDENTIFY DATA SOURCE
                string leadSource = "";

                // Check if it came from Google Ads (has a GCLID)
                if (!string.IsNullOrEmpty(formData.Gclid))
                {
                    leadSource = "Google AdWords";
                }
                else
                {
                    // No GCLID means direct website traffic. Use the Form ID.
                    leadSource = formData.FormId switch
                    {
                        "29a1994" => "POP UP Form",
                        "66097b8" => "Contact Page Form",
                        "7966888" => "Untitled Form",
                        _ => string.IsNullOrEmpty(formData.FormId) ? "Website Direct" : $"Website Form (ID: {formData.FormId})"
                    };
                }

                // 3. CREATE ONLY THE ACCOUNT RECORD
                var newAccount = new Account
                {
                    AccountName = formData.Name,
                    ContactPersonName = formData.Name,
                    Email = formData.Email,
                    MobileNumber = formData.Phone,
                    IsHomeopathicDoctor = formData.DoctorRole,

                    Description = $"Message: {formData.Message}\n\n" +
                                  $"--- Tracking Info ---\n" +
                                  $"Form ID: {formData.FormId}\n" +
                                  $"Source: {formData.UtmSource}\n" +
                                  $"Medium: {formData.UtmMedium}\n" +
                                  $"Campaign: {formData.UtmCampaign}\n" +
                                  $"GCLID: {formData.Gclid}",

                    MetaCampaignName = formData.UtmCampaign,

                    // This dynamically assigns "Google AdWords" OR the specific website form name
                    DataSource = leadSource,

                    CurrentStatus = "Non-User",
                    DateOfEntry = DateTime.Now,

                    // Assigning to Sitara for both Google Ads and Website Direct scenarios
                    CreatedBy = "Sitara",
                    ModifiedBy = "Sitara"
                };

                _context.Accounts.Add(newAccount);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Account created successfully. Lead skipped.",
                    accountId = newAccount.Id
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("WEBHOOK ERROR: " + ex.Message);
                return StatusCode(500, new { success = false, message = "Database error: " + ex.Message });
            }
        }
    }
}