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
                // 2. IDENTIFY THE EXACT SOURCE BASED ON FORM ID
                string leadSource = formData.FormId switch
                {
                    "29a1994" => "POP UP Form",
                    "66097b8" => "Contact Page Form",
                    "7966888" => "Untitled Form", // From the new form you provided
                    _ => string.IsNullOrEmpty(formData.FormId) ? "Website Direct" : $"Website Form (ID: {formData.FormId})"
                };

                // 3. CREATE THE ACCOUNT RECORD
                var newAccount = new Account
                {
                    AccountName = formData.Name,
                    ContactPersonName = formData.Name,
                    Email = formData.Email,
                    MobileNumber = formData.Phone,
                    IsHomeopathicDoctor = formData.DoctorRole, // Mapped from form dropdown

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

                // 4. CREATE THE CONTACT RECORD 
                // Both forms will automatically generate the connected Contact record
                var newContact = new Lead
                {
                    AccountId = newAccount.Id,
                    ContactName = formData.Name,
                    LeadName = formData.Name,
                    EmailID = formData.Email,
                    MobileNumber = formData.Phone,
                    IsHomeopathicDoctor = formData.DoctorRole, // Mapped from form dropdown

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
            catch (Exception ex)
            {
                Console.WriteLine("WEBHOOK ERROR: " + ex.Message);
                return StatusCode(500, new { success = false, message = "Database error: " + ex.Message });
            }
        }
    }
}