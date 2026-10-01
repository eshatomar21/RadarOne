using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radar_CRM.Data;
using Radar_CRM.DTOs;
using Radar_CRM.Models;

namespace Radar_CRM.Controllers
{
    [Route("api/webhook")]
    [ApiController]
    public class WebhookcallyzerController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly string _openAiApiKey;

        // IConfiguration is added to the constructor to read appsettings.json
        public WebhookcallyzerController(ApplicationDbContext context, IServiceScopeFactory scopeFactory, IConfiguration configuration)
        {
            _context = context;
            _scopeFactory = scopeFactory;
            _openAiApiKey = configuration["OpenAI:ApiKey"];
        }

        [HttpPost("callyzer")]
        public async System.Threading.Tasks.Task<IActionResult> ReceiveCallyzerData([FromBody] List<CallyzerWebhookEvent> events)
        {
            try
            {
                if (events == null || !events.Any())
                {
                    return BadRequest(new { success = false, message = "Invalid data received." });
                }

                foreach (var evt in events)
                {
                    if (evt.call_logs == null || !evt.call_logs.Any()) continue;

                    foreach (var payload in evt.call_logs)
                    {
                        if (string.IsNullOrWhiteSpace(payload.client_number)) continue;

                        string phoneToMatch = payload.client_number.Replace("+", "").Trim();

                        var matchedLead = await _context.Leads
                            .FirstOrDefaultAsync(l => l.Phone != null && l.Phone.Contains(phoneToMatch));

                        var matchedAccount = await _context.Accounts
                            .FirstOrDefaultAsync(a => a.Phone != null && a.Phone.Contains(phoneToMatch));

                        int parsedDuration = 0;
                        if (!string.IsNullOrWhiteSpace(payload.duration))
                        {
                            int.TryParse(payload.duration, out parsedDuration);
                        }

                        string customerPhoneSafe = payload.client_number ?? "";
                        if (customerPhoneSafe.Length > 20) customerPhoneSafe = customerPhoneSafe.Substring(0, 20);

                        string salesPhoneSafe = !string.IsNullOrWhiteSpace(payload.emp_no) ? payload.emp_no : evt.emp_number;
                        salesPhoneSafe ??= "";
                        if (salesPhoneSafe.Length > 20) salesPhoneSafe = salesPhoneSafe.Substring(0, 20);

                        string urlSafe = payload.call_recording_url ?? "";
                        if (urlSafe.Length > 500) urlSafe = urlSafe.Substring(0, 500);

                        var callRecord = new CallRecord
                        {
                            CustomerPhone = customerPhoneSafe,
                            SalespersonPhone = salesPhoneSafe,
                            RecordingUrl = urlSafe,
                            DurationSeconds = parsedDuration,
                            CallDate = DateTime.UtcNow,
                            LeadId = matchedLead?.Id,
                            AccountId = matchedAccount?.Id,
                            // 🚀 FIX: Explicitly assign placeholders so SQL Server doesn't reject the row
                            TranscriptionText = "Pending AI Transcription...",
                            AiSummary = "Pending AI Summary..."
                        };

                        _context.CallRecords.Add(callRecord);
                        await _context.SaveChangesAsync();

                        // --- Background Audio Processing & File Saving ---
                        if (!string.IsNullOrEmpty(callRecord.RecordingUrl))
                        {
                            int savedCallId = callRecord.Id;
                            string recordingUrl = callRecord.RecordingUrl;
                            string apiKey = _openAiApiKey;

                            _ = System.Threading.Tasks.Task.Run(async () =>
                            {
                                using var scope = _scopeFactory.CreateScope();
                                var bgContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                                try
                                {
                                    // 1. Download the audio file
                                    byte[] audioBytes = await DownloadAudioBytesAsync(recordingUrl);
                                    if (audioBytes == null || audioBytes.Length == 0) return;

                                    // 2. Define the exact local folder path
                                    string folderPath = @"C:\CRM_files\Call_recordings";

                                    // Ensure the directory exists, if not, create it automatically
                                    if (!System.IO.Directory.Exists(folderPath))
                                    {
                                        System.IO.Directory.CreateDirectory(folderPath);
                                    }

                                    // 3. Create a unique filename and save to the C: drive
                                    string fileName = $"call_{savedCallId}_{DateTime.Now:yyyyMMdd_HHmmss}.mp3";
                                    string fullLocalPath = System.IO.Path.Combine(folderPath, fileName);

                                    await System.IO.File.WriteAllBytesAsync(fullLocalPath, audioBytes);

                                    // 4. Send to AI for transcription and summary
                                    string transcription = await TranscribeAudioWithWhisperAsync(audioBytes, apiKey);
                                    string summary = string.IsNullOrWhiteSpace(transcription) ? "No speech detected." : await GenerateCallSummaryAsync(transcription, apiKey);

                                    // 5. Update the database record with the new Local File Path and AI results
                                    var recordToUpdate = await bgContext.CallRecords.FindAsync(savedCallId);
                                    if (recordToUpdate != null)
                                    {
                                        recordToUpdate.LocalFilePath = fullLocalPath; // Save path to DB
                                        recordToUpdate.TranscriptionText = transcription;
                                        recordToUpdate.AiSummary = summary;
                                        await bgContext.SaveChangesAsync();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[Audio Processing Error] Call ID {savedCallId}: {ex.Message}");
                                }
                            });
                        }
                    }
                }

                return Ok(new { success = true, message = "Call logged and AI processing initiated." });
            }
            catch (Exception ex)
            {
                string actualDbError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { success = false, message = "Database Error: " + actualDbError });
            }
        }
        // =========================================================================
        // HELPER METHODS
        // =========================================================================

        private static async System.Threading.Tasks.Task<byte[]> DownloadAudioBytesAsync(string fileUrl)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(3);
            return await client.GetByteArrayAsync(fileUrl);
        }

        // Added apiKey parameter
        private static async System.Threading.Tasks.Task<string> TranscribeAudioWithWhisperAsync(byte[] audioBytes, string apiKey)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(3);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var content = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(audioBytes);
            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/mpeg");

            content.Add(audioContent, "file", "recording.mp3");
            content.Add(new StringContent("whisper-1"), "model");

            var response = await client.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Whisper API Error: {error}");
                return string.Empty;
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);
            return document.RootElement.GetProperty("text").GetString();
        }

        // Added apiKey parameter
        private static async System.Threading.Tasks.Task<string> GenerateCallSummaryAsync(string rawTranscription, string apiKey)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            string systemPrompt = @"You are an expert CRM Sales Call Analyst.
Analyze the provided phone call transcript between a Salesperson and a Customer.

Provide a response formatted cleanly in plain text:
1. DIALOGUE / KEY EXCHANGE:
Identify speaker intentions and key topics discussed.

2. CALL SUMMARY:
A concise 2-3 sentence overview of the conversation intent, customer needs, and outcome.

3. ACTION ITEMS & FOLLOW-UPS:
Provide bullet points for any commitments, requested quotes, callbacks, or next steps.";

            var payload = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = $"Call Transcript:\n{rawTranscription}" }
                },
                temperature = 0.5
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"GPT-4o-mini API Error: {error}");
                return "Summary could not be generated.";
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);
            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()
                ?.Trim();
        }
    }
}