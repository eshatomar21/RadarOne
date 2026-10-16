using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Radar_CRM.Data; // Adjust to your ApplicationDbContext namespace
using Radar_CRM.Models;
using Radar_CRM.Services;

namespace Radar_CRM.Workers
{
    public class CallBatchWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public CallBatchWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Initial delay to let the web server start up cleanly
            await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessNextBatchAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BATCH WORKER ERROR] {ex.Message}");
                }

                // Wait 2 minutes before checking for new pending records
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }

        private async System.Threading.Tasks.Task ProcessNextBatchAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var aiService = scope.ServiceProvider.GetRequiredService<AiCallService>();

            // Pick calls needing analysis that have an audio source
            // Priority: longest duration first, taking 3 per cycle
            var pendingCalls = await dbContext.CallRecords
                .Where(c => (!string.IsNullOrEmpty(c.RecordingUrl) || !string.IsNullOrEmpty(c.LocalFilePath)) &&
                            (c.SalespersonTone == null ||
                             c.SalespersonTone == "Pending" ||
                             c.SalespersonTone == "N/A" ||
                             c.SalespersonTone == "Processing..."))
                .OrderByDescending(c => c.DurationSeconds)
                .Take(3)
                .ToListAsync(stoppingToken);

            if (!pendingCalls.Any()) return;

            foreach (var record in pendingCalls)
            {
                if (stoppingToken.IsCancellationRequested) break;

                record.SalespersonTone = "Processing...";
                await dbContext.SaveChangesAsync(stoppingToken);

                try
                {
                    byte[] audioBytes = null;

                    if (!string.IsNullOrEmpty(record.LocalFilePath) && System.IO.File.Exists(record.LocalFilePath))
                    {
                        audioBytes = await System.IO.File.ReadAllBytesAsync(record.LocalFilePath, stoppingToken);
                    }
                    else if (!string.IsNullOrEmpty(record.RecordingUrl))
                    {
                        audioBytes = await aiService.DownloadAudioAsync(record.RecordingUrl);
                    }

                    if (audioBytes == null || audioBytes.Length == 0)
                    {
                        record.SalespersonTone = "ERROR";
                        record.AiSummary = "Audio file missing or download failed.";
                        await dbContext.SaveChangesAsync(stoppingToken);
                        continue;
                    }

                    // 1. Groq Whisper Transcription
                    string rawTranscription = await aiService.TranscribeAudioAsync(audioBytes);

                    if (string.IsNullOrWhiteSpace(rawTranscription) || rawTranscription.Contains("[GROQ WHISPER ERROR]"))
                    {
                        record.SalespersonTone = "ERROR";
                        record.AiSummary = rawTranscription ?? "Transcription returned empty.";
                        await dbContext.SaveChangesAsync(stoppingToken);
                        continue;
                    }

                    // 2. Groq LLM Analysis (openai/gpt-oss-20b)
                    var aiResult = await aiService.AnalyzeCallAsync(rawTranscription);

                    if (aiResult != null && aiResult.SalespersonTone != "ERROR")
                    {
                        record.TranscriptionText = aiResult.FormattedChatJson;
                        record.AiSummary = aiResult.AiSummary ?? "N/A";
                        record.SalespersonConfidence = aiResult.SalespersonConfidence ?? "N/A";
                        record.SalespersonEmpathy = aiResult.SalespersonEmpathy ?? "N/A";
                        record.SalespersonTone = aiResult.SalespersonTone ?? "N/A";
                        record.UserNeeds = aiResult.UserNeeds ?? "N/A";
                        record.RecommendedProducts = aiResult.RecommendedProducts ?? "N/A";
                        record.GapAnalysis = aiResult.GapAnalysis ?? "N/A";

                        // ==========================================
                        // NEW PROFILE & SOFTWARE FIELDS
                        // ==========================================
                        record.IsHomeopathicDoctor = aiResult.IsHomeopathicDoctor ?? "N/A";
                        record.ClinicType = aiResult.ClinicType ?? "N/A";
                        record.WorkType = aiResult.WorkType ?? "N/A";
                        record.HasComputer = aiResult.HasComputer ?? "N/A";
                        record.DateOfBirth = aiResult.DateOfBirth ?? "N/A";
                        record.Age = aiResult.Age ?? "N/A";
                        record.YearsOfPractice = aiResult.YearsOfPractice ?? "N/A";
                        record.TotalExperience = aiResult.TotalExperience ?? "N/A";
                        record.NumberOfClinics = aiResult.NumberOfClinics ?? "N/A";
                        record.PatientsPerDay = aiResult.PatientsPerDay ?? "N/A";
                        record.Qualification = aiResult.Qualification ?? "N/A";
                        record.YearOfPassing = aiResult.YearOfPassing ?? "N/A";
                        record.CollegeName = aiResult.CollegeName ?? "N/A";
                        record.AveragePatientFee = aiResult.AveragePatientFee ?? "N/A";
                        record.CurrentlyUsingSoftware = aiResult.CurrentlyUsingSoftware ?? "N/A";
                        record.CurrentSoftwareName = aiResult.CurrentSoftwareName ?? "N/A";
                    }
                    else
                    {
                        record.SalespersonTone = "ERROR";
                        record.AiSummary = aiResult?.AiSummary ?? "LLM returned invalid output.";
                        record.GapAnalysis = aiResult?.GapAnalysis;
                    }
                }
                catch (Exception ex)
                {
                    record.SalespersonTone = "ERROR";
                    record.AiSummary = $"Worker exception: {ex.Message}";
                }

                await dbContext.SaveChangesAsync(stoppingToken);

                // Anti-rate-limit breather: 20 seconds between Groq requests
                await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }
        }
    }
}