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
        public async Task<IActionResult> ReceiveWordpressContact([FromBody] WordpressLeadDto formData, [FromHeader(Name = "X-API-KEY")] string apiKey)
        {
            // 1. SECURITY CHECK: Ensures only your WordPress site can create records
            string mySecretKey = "RADAR_CRM_SECURE_KEY_2026";

            if (string.IsNullOrEmpty(apiKey) || apiKey != mySecretKey)
            {
                return Unauthorized(new { message = "Invalid or missing API Key." });
            }

            if (formData == null || string.IsNullOrEmpty(formData.Name))
            {
                return BadRequest(new { message = "Form data is empty or missing the Name field." });
            }

            try
            {
                // 2. MAP TO ACCOUNT: Convert the incoming web data into a CRM Account record
                var newAccount = new Account
                {
                    AccountName = formData.Name,
                    ContactPersonName = formData.Name,
                    Email = formData.Email,
                    MobileNumber = formData.Phone,

                    // Combine the user's message and tracking details into the description
                    Description = $"Message: {formData.Message}\n\n" +
                                  $"--- Tracking Info ---\n" +
                                  $"Source: {formData.UtmSource}\n" +
                                  $"Medium: {formData.UtmMedium}\n" +
                                  $"Campaign: {formData.UtmCampaign}\n" +
                                  $"Term: {formData.UtmTerm}\n" +
                                  $"Content: {formData.UtmContent}\n" +
                                  $"GCLID: {formData.Gclid}",

                    // Mapping UTMs to specific fields if they exist in your model
                    
                    MetaCampaignName = formData.UtmCampaign,

                    // Default system values for website leads
                    DataSource = "Website Direct",
                    CurrentStatus = "Non-User",
                  
                    DateOfEntry = DateTime.Now,
                   
                };

                // 3. SAVE TO DATABASE
                _context.Accounts.Add(newAccount);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Record created successfully", accountId = newAccount.Id });
            }
            catch (Exception ex)
            {
                // Log the exact error to the console for debugging
                Console.WriteLine("WEBHOOK ERROR: " + ex.Message);
                return StatusCode(500, new { success = false, message = "Database error: " + ex.Message });
            }
        }
    }
}