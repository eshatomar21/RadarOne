using System;
using System.Collections.Generic;
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

                        // 🚀 STRICT NULL CHECK FOR LIVE CALLS
                        string empNameRaw = evt.emp_name;
                        if (string.IsNullOrWhiteSpace(empNameRaw)) empNameRaw = "Unknown Agent";
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
                            TranscriptionText = "Pending AI Transcription...",
                            AiSummary = "Pending AI Summary..."
                        };

                        _context.CallRecords.Add(callRecord);
                        await _context.SaveChangesAsync();

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
                                    byte[] audioBytes = await DownloadAudioBytesAsync(recordingUrl);
                                    if (audioBytes != null && audioBytes.Length > 0)
                                    {
                                        string folderPath = @"C:\CRM_files\Call_recordings";
                                        if (!System.IO.Directory.Exists(folderPath))
                                        {
                                            System.IO.Directory.CreateDirectory(folderPath);
                                        }

                                        string fileName = $"call_{savedCallId}_{DateTime.Now:yyyyMMdd_HHmmss}.mp3";
                                        string fullLocalPath = System.IO.Path.Combine(folderPath, fileName);

                                        await System.IO.File.WriteAllBytesAsync(fullLocalPath, audioBytes);

                                        string rawTranscription = await TranscribeAudioWithWhisperAsync(audioBytes, apiKey);
                                        string finalTranscript = "No speech detected.";
                                        string finalSummary = "No speech detected.";

                                        if (!string.IsNullOrWhiteSpace(rawTranscription) && !rawTranscription.Contains("ERROR]"))
                                        {
                                            var aiResult = await AnalyzeCallWithGptAsync(rawTranscription, apiKey);
                                            finalTranscript = aiResult.FormattedChat;
                                            finalSummary = aiResult.Summary;
                                        }
                                        else if (rawTranscription.Contains("ERROR]"))
                                        {
                                            finalTranscript = rawTranscription;
                                            finalSummary = "Whisper Translation Failed.";
                                        }

                                        var recordToUpdate = await bgContext.CallRecords.FindAsync(savedCallId);
                                        if (recordToUpdate != null)
                                        {
                                            recordToUpdate.LocalFilePath = fullLocalPath;
                                            recordToUpdate.TranscriptionText = finalTranscript;
                                            recordToUpdate.AiSummary = finalSummary;
                                            await bgContext.SaveChangesAsync();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogError($"[Audio Processing Error] Call ID {savedCallId}: {ex.Message}");
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

        [HttpGet("play-audio/{id}")]
        public async System.Threading.Tasks.Task<IActionResult> PlayAudio(int id)
        {
            var record = await _context.CallRecords.FindAsync(id);
            if (record == null || string.IsNullOrEmpty(record.LocalFilePath) || !System.IO.File.Exists(record.LocalFilePath))
            {
                return NotFound("Audio file not downloaded or missing from server.");
            }

            var fileStream = new FileStream(record.LocalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return File(fileStream, "audio/mpeg", enableRangeProcessing: true);
        }

        [HttpPost("import-historical-callyzer")]
        public async System.Threading.Tasks.Task<IActionResult> ImportHistoricalData([FromServices] IConfiguration config)
        {
            string callyzerApiToken = config["Callyzer:ApiToken"];
            if (string.IsNullOrEmpty(callyzerApiToken))
            {
                return BadRequest(new { success = false, message = "Missing Callyzer:ApiToken in appsettings.json." });
            }

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", callyzerApiToken);

                long syncedTo = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long syncedFrom = DateTimeOffset.UtcNow.AddDays(-175).ToUnixTimeSeconds();

                var payload = new
                {
                    synced_from = syncedFrom,
                    synced_to = syncedTo,
                    call_method = "PhoneCall",
                    call_mode = "Voice"
                };

                var jsonContent = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://api1.callyzer.co/api/v2.2/call-log/history", jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    string err = await response.Content.ReadAsStringAsync();
                    return StatusCode(500, new { success = false, message = "Callyzer API error: " + err });
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(jsonResponse);

                var root = doc.RootElement;
                System.Text.Json.JsonElement logsArray = default;
                bool arrayFound = false;

                if (root.TryGetProperty("result", out var resultNode))
                {
                    if (resultNode.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        logsArray = resultNode;
                        arrayFound = true;
                    }
                    else if (resultNode.TryGetProperty("data", out var dataNode) && dataNode.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        logsArray = dataNode;
                        arrayFound = true;
                    }
                    else if (resultNode.TryGetProperty("call_logs", out var clNode) && clNode.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        logsArray = clNode;
                        arrayFound = true;
                    }
                }

                if (!arrayFound)
                {
                    return Ok(new { success = true, message = "No valid call logs array found in Callyzer response." });
                }

                int processedCount = 0;
                string folderPath = @"C:\CRM_files\Call_recordings";
                if (!System.IO.Directory.Exists(folderPath)) System.IO.Directory.CreateDirectory(folderPath);

                foreach (var log in logsArray.EnumerateArray())
                {
                    string clientNumber = log.TryGetProperty("client_number", out var cn) ? cn.GetString() : "";
                    string recordingUrl = log.TryGetProperty("call_recording_url", out var ru) ? ru.GetString() : "";

                    if (string.IsNullOrWhiteSpace(clientNumber)) continue;

                    string phoneToMatch = clientNumber.Replace("+", "").Trim();

                    var existingRecord = await _context.CallRecords.FirstOrDefaultAsync(c => c.RecordingUrl == recordingUrl && c.CustomerPhone.Contains(phoneToMatch));

                    int savedCallId = 0;

                    if (existingRecord != null)
                    {
                        bool hasError = string.IsNullOrEmpty(existingRecord.TranscriptionText) ||
                                        existingRecord.TranscriptionText.Contains("Pending") ||
                                        existingRecord.TranscriptionText.Contains("ERROR]");

                        if (!hasError) continue;

                        savedCallId = existingRecord.Id;
                    }
                    else
                    {
                        var matchedLead = await _context.Leads.FirstOrDefaultAsync(l => l.Phone != null && l.Phone.Contains(phoneToMatch));
                        var matchedAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Phone != null && a.Phone.Contains(phoneToMatch));

                        int parsedDuration = 0;
                        if (log.TryGetProperty("duration", out var durNode))
                        {
                            if (durNode.ValueKind == System.Text.Json.JsonValueKind.Number) parsedDuration = durNode.GetInt32();
                            else if (durNode.ValueKind == System.Text.Json.JsonValueKind.String) int.TryParse(durNode.GetString(), out parsedDuration);
                        }

                        string empNo = "";
                        if (log.TryGetProperty("emp_number", out var eNum)) empNo = eNum.GetString();
                        else if (log.TryGetProperty("emp_no", out var eNo)) empNo = eNo.GetString();
                        else if (log.TryGetProperty("employee_number", out var eFull)) empNo = eFull.GetString();

                        // 🚀 FIX: Check every possible variation Callyzer uses for the Agent's Name
                        string empName = "";
                        if (log.TryGetProperty("employee_name", out var eFullName) && eFullName.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            empName = eFullName.GetString();
                        }
                        else if (log.TryGetProperty("emp_name", out var eName) && eName.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            empName = eName.GetString();
                        }
                        else if (log.TryGetProperty("name", out var justName) && justName.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            empName = justName.GetString();
                        }

                        // Force it to "Unknown Agent" if Callyzer sent null or empty
                        if (string.IsNullOrWhiteSpace(empName))
                        {
                            empName = "Unknown Agent";
                        }

                        string safeCustomerPhone = clientNumber.Length > 20 ? clientNumber.Substring(0, 20) : clientNumber;
                        string safeSalesPhone = empNo?.Length > 20 ? empNo.Substring(0, 20) : (empNo ?? "");
                        string safeUrl = recordingUrl?.Length > 500 ? recordingUrl.Substring(0, 500) : (recordingUrl ?? "");
                        string safeSalesName = empName.Length > 100 ? empName.Substring(0, 100) : empName;

                        var callRecord = new CallRecord
                        {
                            CustomerPhone = safeCustomerPhone,
                            SalespersonPhone = safeSalesPhone,
                            SalespersonName = safeSalesName,
                            RecordingUrl = safeUrl,
                            DurationSeconds = parsedDuration,
                            CallDate = DateTime.UtcNow,
                            LeadId = matchedLead?.Id,
                            AccountId = matchedAccount?.Id,
                            TranscriptionText = "Processing AI Retry...",
                            AiSummary = "Processing AI Retry..."
                        };

                        _context.CallRecords.Add(callRecord);
                        await _context.SaveChangesAsync();

                        savedCallId = callRecord.Id;
                        processedCount++;
                    }

                    if (savedCallId > 0 && !string.IsNullOrEmpty(recordingUrl))
                    {
                        string currentUrl = recordingUrl;
                        string apiKey = _openAiApiKey;

                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var bgContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                            try
                            {
                                byte[] audioBytes = await DownloadAudioBytesAsync(currentUrl);
                                if (audioBytes != null && audioBytes.Length > 0)
                                {
                                    string fileName = $"historical_call_{savedCallId}_{DateTime.Now:yyyyMMdd_HHmmss}.mp3";
                                    string fullLocalPath = System.IO.Path.Combine(folderPath, fileName);
                                    await System.IO.File.WriteAllBytesAsync(fullLocalPath, audioBytes);

                                    string rawTranscription = await TranscribeAudioWithWhisperAsync(audioBytes, apiKey);
                                    string finalTranscript = "No speech detected.";
                                    string finalSummary = "No speech detected.";

                                    if (!string.IsNullOrWhiteSpace(rawTranscription) && !rawTranscription.Contains("ERROR]"))
                                    {
                                        var aiResult = await AnalyzeCallWithGptAsync(rawTranscription, apiKey);
                                        finalTranscript = aiResult.FormattedChat;
                                        finalSummary = aiResult.Summary;
                                    }
                                    else if (rawTranscription.Contains("ERROR]"))
                                    {
                                        finalTranscript = rawTranscription;
                                        finalSummary = "Whisper Translation Failed.";
                                    }

                                    var recordToUpdate = await bgContext.CallRecords.FindAsync(savedCallId);
                                    if (recordToUpdate != null)
                                    {
                                        recordToUpdate.LocalFilePath = fullLocalPath;
                                        recordToUpdate.TranscriptionText = finalTranscript;
                                        recordToUpdate.AiSummary = finalSummary;
                                        await bgContext.SaveChangesAsync();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogError($"[Historical Download Error] {ex.Message}");
                            }
                        });
                    }
                }

                return Ok(new { success = true, message = $"Successfully pulled {processedCount} historical calls." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Historical Import Error: " + (ex.InnerException?.Message ?? ex.Message) });
            }
        }

        // =========================================================================
        // HELPER METHODS
        // =========================================================================

        private static void LogError(string message)
        {
            try
            {
                string folder = @"C:\CRM_files";
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);

                string path = System.IO.Path.Combine(folder, "OpenAI_Logs.txt");
                System.IO.File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
            }
            catch { }
        }

        private static async System.Threading.Tasks.Task<byte[]> DownloadAudioBytesAsync(string fileUrl)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(3);
            return await client.GetByteArrayAsync(fileUrl);
        }

        private static async System.Threading.Tasks.Task<string> TranscribeAudioWithWhisperAsync(byte[] audioBytes, string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                LogError("Whisper API Error: OpenAI API Key is missing or empty in appsettings.json.");
                return "[AI ERROR] Missing API Key";
            }

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(3);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var content = new MultipartFormDataContent();
            var audioContent = new ByteArrayContent(audioBytes);
            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/mpeg");

            content.Add(audioContent, "file", "recording.mp3");
            content.Add(new StringContent("whisper-1"), "model");

            var response = await client.PostAsync("https://api.openai.com/v1/audio/translations", content);
            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                LogError($"Whisper API Error [{response.StatusCode}]: {error}");
                return $"[OPENAI WHISPER ERROR] {response.StatusCode}. Check C:\\CRM_files\\OpenAI_Logs.txt";
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);
            return document.RootElement.GetProperty("text").GetString();
        }

        private static async System.Threading.Tasks.Task<(string FormattedChat, string Summary)> AnalyzeCallWithGptAsync(string rawTranscription, string apiKey)
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            string systemPrompt = @"You are an expert CRM Sales Call Analyst.
You will receive a raw audio translation. You MUST return ONLY a valid JSON object matching this exact structure:
{
  ""formatted_chat"": [
    { ""Speaker"": ""Salesperson"", ""Text"": ""Hello, how can I help?"" },
    { ""Speaker"": ""Customer"", ""Text"": ""I need info."" }
  ],
  ""summary"": ""A brief 2-sentence summary of the intent and outcome.""
}
If it is a voicemail, assign the speaker appropriately. Use exactly the keys 'formatted_chat' and 'summary'.";

            var payload = new
            {
                model = "gpt-4o-mini",
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = $"Raw Transcript:\n{rawTranscription}" }
                },
                temperature = 0.2
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", jsonContent);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                LogError($"GPT-4o-mini Error [{response.StatusCode}]: {error}");
                return (rawTranscription, "OpenAI API Error. Check Logs.");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);
            string jsonResult = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            try
            {
                using var resultDoc = JsonDocument.Parse(jsonResult);

                string chatJsonArray = "[]";
                if (resultDoc.RootElement.TryGetProperty("formatted_chat", out var chatNode))
                {
                    chatJsonArray = chatNode.GetRawText();
                }

                string summary = "No summary generated.";
                if (resultDoc.RootElement.TryGetProperty("summary", out var summaryNode))
                {
                    summary = summaryNode.GetString();
                }

                return (chatJsonArray, summary);
            }
            catch (Exception ex)
            {
                LogError($"JSON Parsing Error: {ex.Message}\nRaw JSON from GPT: {jsonResult}");
                return (rawTranscription, "Audio was too short or unclear to summarize.");
            }
        }
    }
}