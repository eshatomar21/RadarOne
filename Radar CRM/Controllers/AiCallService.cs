using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Radar_CRM.Services
{
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

        // 3. Summarize and Format using GPT-4o-mini
        public async Task<string> SummarizeCallAsync(string rawTranscription)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

            string systemPrompt = @"You are an expert CRM assistant. Analyze the raw transcription of a sales call. 
            Provide a clean, readable output with two sections:
            1. SUMMARY: A brief paragraph summarizing the customer's intent, the salesperson's actions, and the outcome.
            2. KEY POINTS: 3-4 bullet points highlighting important details or follow-up tasks.";

            var payload = new
            {
                model = "gpt-4o-mini",
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = $"Raw Transcription:\n{rawTranscription}" }
                },
                temperature = 0.7
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode) return "Summary generation failed.";

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);
            return document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString().Trim();
        }
    }
}