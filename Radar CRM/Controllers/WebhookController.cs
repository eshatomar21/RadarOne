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
                // Set source name based on the Form ID
                string leadSource = formData.FormId == "29a1994" ? "POP UP Form" : "Website Direct";

                // 1. CREATE THE ACCOUNT RECORD
                var newAccount = new Account
                {
                    AccountName = formData.Name,
                    ContactPersonName = formData.Name,
                    Email = formData.Email,
                    MobileNumber = formData.Phone,

                    Description = $"Message: {formData.Message}\n\n" +
                                  $"--- Tracking Info ---\n" +
                                  $"Form ID: {formData.FormId}\n" +
                                  $"Source: {formData.UtmSource}\n" +
                                  $"Medium: {formData.UtmMedium}\n" +
                                  $"Campaign: {formData.UtmCampaign}\n" +
                                  $"GCLID: {formData.Gclid}",

                    MetaCampaignName = formData.UtmCampaign,
                    DataSource = leadSource,
                    CurrentStatus = "Non-User",
                    DateOfEntry = DateTime.Now,
                };

                _context.Accounts.Add(newAccount);
                await _context.SaveChangesAsync();

                // 2. CONVERT TO CONTACT RECORD IF IT'S THE POPUP FORM
                if (formData.FormId == "29a1994")
                {
                    var newContact = new Lead
                    {
                        AccountId = newAccount.Id,
                        ContactName = formData.Name,
                        LeadName = formData.Name,
                        EmailID = formData.Email,
                        MobileNumber = formData.Phone,

                        DataSources = leadSource,
                        CampaignSource = leadSource,
                        Description = formData.Message,

                        Stage = "Open",
                        CurrentStatus = "Non-User",
                        CreatedDateAndTime = DateTime.Now,
                        LeadCreatedTime = DateTime.Now
                    };

                    _context.Leads.Add(newContact);
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        success = true,
                        message = "Account and Contact created successfully",
                        accountId = newAccount.Id,
                        contactId = newContact.Id
                    });
                }

                return Ok(new { success = true, message = "Account created successfully", accountId = newAccount.Id });
            }
            catch (Exception ex)
            {
                Console.WriteLine("WEBHOOK ERROR: " + ex.Message);
                return StatusCode(500, new { success = false, message = "Database error: " + ex.Message });
            }
        }
    }
}