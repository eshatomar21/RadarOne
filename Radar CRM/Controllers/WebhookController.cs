using Microsoft.AspNetCore.Mvc;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

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
                    // Matches the exact HTML dropdown: <option value="Google AdWords">
                    leadSource = "Google AdWords";
                }
                else
                {
                    // Matches the exact HTML dropdown: <option value="Website Direct">
                    // We map all CF7 forms to "Website Direct" because specific form names 
                    // like "POP UP Form" are NOT in your HTML Data Source dropdown list.
                    leadSource = "Website Direct";
                }

                // 3. DYNAMIC ASSIGNMENT LOGIC (Replaces Hardcoded ID)
                // Query the database for an active rule that matches this exact Data Source
                var matchedRule = await _context.AssignmentRules.FirstOrDefaultAsync(r =>
                    r.IsActive == true &&
                    r.TargetModule == "Accounts" &&
                    r.ConditionField == "DataSource" &&
                    r.ConditionValue == leadSource
                );

                // If a rule matches, assign that specific user. 
                // If no rule is found, it will safely leave the owner blank.
                string assignedOwnerId = matchedRule != null ? matchedRule.AssignToUserId : null;

                // 4. CREATE ONLY THE ACCOUNT RECORD
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

                    // This now strictly matches your HTML dropdown values ("Google AdWords" or "Website Direct")
                    DataSource = leadSource,

                    CurrentStatus = "Non-User",
                    DateOfEntry = DateTime.Now,

                    // 🚀 THIS links the record to the dynamically found user in the CRM UI
                    AccountOwnerId = assignedOwnerId,

                    CreatedBy = "Webhook System",
                    ModifiedBy = "Webhook System"
                };

                _context.Accounts.Add(newAccount);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "Account created successfully. Lead creation skipped.",
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