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
                string leadSource = !string.IsNullOrEmpty(formData.Gclid) ? "Google AdWords" : "Website Direct";

                // 3. DYNAMIC ASSIGNMENT LOGIC
                var matchedRule = await _context.AssignmentRules.FirstOrDefaultAsync(r =>
                    r.IsActive == true &&
                    r.TargetModule == "Accounts" &&
                    r.ConditionField == "DataSource" &&
                    r.ConditionValue == leadSource
                );

                string assignedOwnerId = "zcrm_1092392000000518001"; // Default Fallback Owner
                if (matchedRule != null && !string.IsNullOrWhiteSpace(matchedRule.AssignToUserId))
                {
                    bool userExists = await _context.Users.AnyAsync(u => u.Id == matchedRule.AssignToUserId);
                    if (userExists) assignedOwnerId = matchedRule.AssignToUserId;
                }

                // ====================================================================
                // 4. DUPLICATE CHECK & RECORD CREATION (ZOHO SCRIPT REPLICATION)
                // ====================================================================

                // Search for existing account by Email OR Phone (ignoring empty values)
                var existingAccount = await _context.Accounts.FirstOrDefaultAsync(a =>
                    (!string.IsNullOrEmpty(formData.Email) && a.Email == formData.Email) ||
                    (!string.IsNullOrEmpty(formData.Phone) && a.MobileNumber == formData.Phone)
                );

                if (existingAccount != null)
                {
                    // 🔴 DUPLICATE FOUND: Update existing record instead of creating a new one
                    existingAccount.IsDuplicated = true;

                    // Append the new message to the existing description so history isn't lost
                    string newMessage = $"--- New Webhook Inquiry ({DateTime.Now.ToString("g")}) ---\nMessage: {formData.Message}";
                    existingAccount.Description = string.IsNullOrEmpty(existingAccount.Description)
                        ? newMessage
                        : existingAccount.Description + $"\n\n{newMessage}";

                    // Update Tracking Info if the new form submission has it
                    if (!string.IsNullOrEmpty(formData.Gclid)) existingAccount.Gclid = formData.Gclid;
                    if (!string.IsNullOrEmpty(formData.UtmCampaign)) existingAccount.MetaCampaignName = formData.UtmCampaign;

                    existingAccount.ModifiedBy = "Webhook System";

                    _context.Accounts.Update(existingAccount);
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        success = true,
                        message = "Duplicate lead found. Record updated successfully.",
                        accountId = existingAccount.Id
                    });
                }
                else
                {
                    // 🟢 NO DUPLICATE: Create a fresh new Account

                    // Format UTMs cleanly for the description if they exist
                    string trackingFooter = "";
                    if (!string.IsNullOrEmpty(formData.UtmSource) || !string.IsNullOrEmpty(formData.UtmMedium))
                    {
                        trackingFooter = $"\n\n[Tracking: Source={formData.UtmSource} | Medium={formData.UtmMedium} | FormID={formData.FormId}]";
                    }

                    var newAccount = new Account
                    {
                        AccountName = formData.Name,
                        ContactPersonName = formData.Name,
                        Email = formData.Email,
                        MobileNumber = formData.Phone,
                        IsHomeopathicDoctor = formData.DoctorRole,

                        // 🚀 FIX: Message goes to Description cleanly
                        Description = formData.Message + trackingFooter,

                        // 🚀 FIX: GCLID goes directly to its dedicated database column
                        Gclid = formData.Gclid,
                        MetaCampaignName = formData.UtmCampaign,

                        DataSource = leadSource,
                        CurrentStatus = "Non User",
                        DateOfEntry = DateTime.Now,
                        CreatedDateAndTime = DateTime.Now,
                        ModifiedDateAndTime = DateTime.Now,
                        AccountOwnerId = assignedOwnerId,
                        CreatedBy = "Webhook System",
                        ModifiedBy = "Webhook System"
                    };

                    _context.Accounts.Add(newAccount);
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        success = true,
                        message = "Record added successfully",
                        accountId = newAccount.Id
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("WEBHOOK ERROR: " + ex.Message);
                return StatusCode(500, new { success = false, message = "Database error: " + ex.Message });
            }
        }
    }
}