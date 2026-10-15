using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Radar_CRM.Services
{
    // 🚀 NEW: Class to hold the parsed JSON data from OpenAI
    public class AiAnalysisResult
    {
        public string AiSummary { get; set; }
        public string SalespersonConfidence { get; set; }
        public string SalespersonEmpathy { get; set; }
        public string SalespersonTone { get; set; }
        public string UserNeeds { get; set; }
        public string RecommendedProducts { get; set; }
        public string GapAnalysis { get; set; }
    }

    public class AiCallService
    {
        // Replace with your actual OpenAI API Key
        private readonly string _apiKey = "sk-proj-YOUR_API_KEY_HERE";

        // 1. Download the audio file from Callyzer
        public async Task<byte[]> DownloadAudioAsync(string url)
        {
            using var client = new HttpClient();
            return await client.GetByteArrayAsync(url);
        }

        // 2. Transcribe Audio using OpenAI Whisper
        public async Task<string> TranscribeAudioAsync(byte[] audioBytes)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            using var content = new MultipartFormDataContent();

            var audioContent = new ByteArrayContent(audioBytes);
            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/mpeg");

            content.Add(audioContent, "file", "recording.mp3");
            content.Add(new StringContent("whisper-1"), "model");

            var response = await client.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);

            if (!response.IsSuccessStatusCode) return "Transcription failed.";

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);
            return document.RootElement.GetProperty("text").GetString();
        }

        // 3. 🚀 UPDATED: Analyze Call using JSON mode for detailed metrics
        public async Task<AiAnalysisResult> AnalyzeCallAsync(string rawTranscription)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            // Instruct the AI to strictly return JSON matching your database properties
            string systemPrompt = @"You are an expert CRM assistant analyzing a sales call transcription. 
            You MUST return your analysis ONLY as a valid JSON object with the following exact string properties:
            - ""AiSummary"": A brief paragraph summarizing the customer's intent, the salesperson's actions, and the outcome.
            - ""SalespersonConfidence"": A short phrase or score (e.g., 'High', '8/10', 'Hesitant').
            - ""SalespersonEmpathy"": A short phrase (e.g., 'Excellent', 'Moderate', 'Needs Improvement').
            - ""SalespersonTone"": A brief description (e.g., 'Professional & Helpful', 'Aggressive').
            - ""UserNeeds"": Bullet points defining what the customer explicitly wants.
            - ""RecommendedProducts"": Products or services that fit the customer's needs based on the call.
            - ""GapAnalysis"": Bullet points on what the salesperson missed or could have done better.";

            var payload = new
            {
                model = "gpt-4o-mini",
                response_format = new { type = "json_object" }, // Forces strict JSON output
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = $"Raw Transcription:\n{rawTranscription}" }
                },
                temperature = 0.7
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode) return null;

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);

            // Extract the JSON string from the AI's response
            var jsonContent = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString().Trim();

            // Deserialize the AI's JSON string directly into our C# object
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<AiAnalysisResult>(jsonContent, options);
        }
    }
}