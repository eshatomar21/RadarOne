using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Radar_CRM.Data;
using Radar_CRM.DTOs;
using Radar_CRM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Radar_CRM.Controllers
{
    [Route("api/webhook")]
    [ApiController]
    public class WebhookcallyzerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public WebhookcallyzerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==============================================================
        // 1. REAL-TIME WEBHOOK (Lightning Fast - No AI processing here)
        // ==============================================================
        [HttpPost("callyzer")]
        public async Task<IActionResult> ReceiveCallyzerData([FromBody] List<CallyzerWebhookEvent> events)
        {
            try
            {
                if (events == null || !events.Any()) return BadRequest(new { success = false, message = "Invalid data received." });

                foreach (var evt in events)
                {
                    if (evt.call_logs == null || !evt.call_logs.Any()) continue;

                    foreach (var payload in evt.call_logs)
                    {
                        if (string.IsNullOrWhiteSpace(payload.client_number)) continue;
                        string phoneToMatch = payload.client_number.Replace("+", "").Trim();

                        // Extract exactly the last 10 digits to ignore country codes (+91, 0, etc.)
                        string last10Digits = phoneToMatch.Length >= 10 ? phoneToMatch.Substring(phoneToMatch.Length - 10) : phoneToMatch;

                        // Search Leads (assumes Lead has a 'Phone' property)
                        var matchedLead = await _context.Leads.FirstOrDefaultAsync(l =>
                            l.Phone != null && l.Phone.Contains(last10Digits));

                        // Search Accounts across MobileNumber OR AlternateMobile
                        var matchedAccount = await _context.Accounts.FirstOrDefaultAsync(a =>
                            (a.MobileNumber != null && a.MobileNumber.Contains(last10Digits)) ||
                            (a.AlternateMobile != null && a.AlternateMobile.Contains(last10Digits)));

                        int parsedDuration = 0;
                        if (!string.IsNullOrWhiteSpace(payload.duration)) int.TryParse(payload.duration, out parsedDuration);

                        string customerPhoneSafe = payload.client_number ?? "";
                        if (customerPhoneSafe.Length > 20) customerPhoneSafe = customerPhoneSafe.Substring(0, 20);

                        string salesPhoneSafe = !string.IsNullOrWhiteSpace(payload.emp_no) ? payload.emp_no : evt.emp_number;
                        salesPhoneSafe ??= "";
                        if (salesPhoneSafe.Length > 20) salesPhoneSafe = salesPhoneSafe.Substring(0, 20);

                        string urlSafe = payload.call_recording_url ?? "";
                        if (urlSafe.Length > 500) urlSafe = urlSafe.Substring(0, 500);

                        string empNameRaw = string.IsNullOrWhiteSpace(evt.emp_name) ? "Unknown Agent" : evt.emp_name;
                        string safeSalesName = empNameRaw.Length > 100 ? empNameRaw.Substring(0, 100) : empNameRaw;

                        var callRecord = new CallRecord
                        {
                            CustomerPhone = customerPhoneSafe,
                            SalespersonPhone = salesPhoneSafe,
                            SalespersonName = safeSalesName,
                            RecordingUrl = urlSafe,
                            DurationSeconds = parsedDuration,
                            CallDate = DateTime.UtcNow,
                            LeadId = matchedLead?.Id,
                            AccountId = matchedAccount?.Id,
                            // Set to Pending so the 24/7 Background Worker picks it up automatically
                            TranscriptionText = "Pending AI Transcription...",
                            AiSummary = "Pending AI Summary...",
                            SalespersonConfidence = "Pending",
                            SalespersonEmpathy = "Pending",
                            SalespersonTone = "Pending",
                            UserNeeds = "Pending",
                            RecommendedProducts = "Pending",
                            GapAnalysis = "Pending"
                        };

                        _context.CallRecords.Add(callRecord);
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Call logged successfully. AI worker will process it shortly." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Database Error: " + ex.Message });
            }
        }

        // ==============================================================
        // 2. PLAY AUDIO (Streams local file if downloaded by worker)
        // ==============================================================
        [HttpGet("play-audio/{id}")]
        public async Task<IActionResult> PlayAudio(int id)
        {
            var record = await _context.CallRecords.FindAsync(id);
            if (record == null || string.IsNullOrEmpty(record.LocalFilePath) || !System.IO.File.Exists(record.LocalFilePath))
                return NotFound("Audio file missing from server.");

            var fileStream = new FileStream(record.LocalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(fileStream, "audio/mpeg", enableRangeProcessing: true);
        }

        // ==============================================================
        // 3. HISTORICAL IMPORT (Queues old calls for the worker)
        // ==============================================================
        [HttpPost("import-historical-callyzer")]
        public async Task<IActionResult> ImportHistoricalData([FromServices] IConfiguration config)
        {
            string callyzerApiToken = config["Callyzer:ApiToken"];
            if (string.IsNullOrEmpty(callyzerApiToken)) return BadRequest(new { success = false, message = "Missing API Token." });

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", callyzerApiToken);

                var payload = new
                {
                    synced_from = DateTimeOffset.UtcNow.AddDays(-175).ToUnixTimeSeconds(),
                    synced_to = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    call_method = "PhoneCall",
                    call_mode = "Voice"
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://api1.callyzer.co/api/v2.2/call-log/history", jsonContent);

                if (!response.IsSuccessStatusCode) return StatusCode(500, new { success = false, message = "Callyzer API error." });

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = doc.RootElement;
                JsonElement logsArray = default;

                if (root.TryGetProperty("result", out var resNode) && resNode.ValueKind == JsonValueKind.Array) logsArray = resNode;
                else if (root.TryGetProperty("data", out var datNode) && datNode.ValueKind == JsonValueKind.Array) logsArray = datNode;
                else if (root.TryGetProperty("call_logs", out var callNode) && callNode.ValueKind == JsonValueKind.Array) logsArray = callNode;
                else return Ok(new { success = true, message = "No valid call logs array found." });

                int processedCount = 0;

                foreach (var log in logsArray.EnumerateArray())
                {
                    string clientNumber = log.TryGetProperty("client_number", out var cn) ? cn.GetString() : "";
                    string recordingUrl = log.TryGetProperty("call_recording_url", out var ru) ? ru.GetString() : "";

                    if (string.IsNullOrWhiteSpace(clientNumber)) continue;
                    string phoneToMatch = clientNumber.Replace("+", "").Trim();

                    var existingRecord = await _context.CallRecords.FirstOrDefaultAsync(c => c.RecordingUrl == recordingUrl && c.CustomerPhone.Contains(phoneToMatch));
                    if (existingRecord != null) continue; // Skip if already imported

                    var matchedLead = await _context.Leads.FirstOrDefaultAsync(l => l.Phone != null && l.Phone.Contains(phoneToMatch));
                    var matchedAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Phone != null && a.Phone.Contains(phoneToMatch));

                    int parsedDuration = 0;
                    if (log.TryGetProperty("duration", out var durNode) && durNode.ValueKind == JsonValueKind.String)
                        int.TryParse(durNode.GetString(), out parsedDuration);

                    string empNo = log.TryGetProperty("emp_number", out var eNum) ? eNum.GetString() : "";
                    string empName = "Unknown Agent";
                    if (log.TryGetProperty("emp_name", out var eName1) && eName1.ValueKind == JsonValueKind.String)
                    {
                        empName = eName1.GetString();
                    }
                    else if (log.TryGetProperty("employee_name", out var eName2) && eName2.ValueKind == JsonValueKind.String)
                    {
                        empName = eName2.GetString();
                    }
                    if (string.IsNullOrWhiteSpace(empName)) empName = "Unknown Agent";

                    var callRecord = new CallRecord
                    {
                        CustomerPhone = clientNumber.Length > 20 ? clientNumber.Substring(0, 20) : clientNumber,
                        SalespersonPhone = empNo?.Length > 20 ? empNo.Substring(0, 20) : (empNo ?? ""),
                        SalespersonName = empName.Length > 100 ? empName.Substring(0, 100) : empName,
                        RecordingUrl = recordingUrl?.Length > 500 ? recordingUrl.Substring(0, 500) : (recordingUrl ?? ""),
                        DurationSeconds = parsedDuration,
                        CallDate = DateTime.UtcNow,
                        LeadId = matchedLead?.Id,
                        AccountId = matchedAccount?.Id,
                        // Set to Pending for Background Worker
                        TranscriptionText = "Pending AI Retry...",
                        AiSummary = "Pending AI Retry...",
                        SalespersonTone = "Pending"
                    };

                    _context.CallRecords.Add(callRecord);
                    processedCount++;
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = $"Successfully queued {processedCount} historical calls for the AI worker." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Historical Import Error: " + ex.Message });
            }
        }

        // ==============================================================
        // 4. SMART REPAIR: RE-QUEUES FAILED OR STUCK RECORDS
        // ==============================================================
        [HttpPost("fix-processing-records")]
        public async Task<IActionResult> FixProcessingRecords()
        {
            // Find records that got stuck or threw an error during AI generation
            var brokenRecords = await _context.CallRecords
                .Where(c => c.SalespersonTone == null ||
                            c.SalespersonTone == "N/A" ||
                            c.SalespersonTone.Contains("ERROR") ||
                            c.SalespersonTone.Contains("Processing") ||
                            (c.AiSummary != null && c.AiSummary.Contains("ERROR")))
                .ToListAsync();

            if (!brokenRecords.Any())
                return Ok(new { success = true, message = "All records are healthy. Nothing to repair!" });

            // Reset them to 'Pending' so the 24/7 background worker picks them up on its next cycle
            foreach (var record in brokenRecords)
            {
                record.SalespersonTone = "Pending";
                record.AiSummary = "Re-queued for AI Processing...";
                record.TranscriptionText = "Re-queued for AI Processing...";
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = $"Successfully re-queued {brokenRecords.Count} failed/stuck records. The background worker will process them shortly." });
        }
    }
}