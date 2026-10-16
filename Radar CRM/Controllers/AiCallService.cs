using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Radar_CRM.Services
{
    public class AiAnalysisResult
    {
        public string FormattedChatJson { get; set; }
        public string AiSummary { get; set; }
        public string SalespersonConfidence { get; set; }
        public string SalespersonEmpathy { get; set; }
        public string SalespersonTone { get; set; }
        public string UserNeeds { get; set; }
        public string RecommendedProducts { get; set; }
        public string GapAnalysis { get; set; }

        // New Profile Fields
        public string IsHomeopathicDoctor { get; set; }
        public string ClinicType { get; set; }
        public string WorkType { get; set; }
        public string HasComputer { get; set; }
        public string DateOfBirth { get; set; }
        public string Age { get; set; }
        public string YearsOfPractice { get; set; }
        public string TotalExperience { get; set; }
        public string NumberOfClinics { get; set; }
        public string PatientsPerDay { get; set; }
        public string Qualification { get; set; }
        public string YearOfPassing { get; set; }
        public string CollegeName { get; set; }
        public string AveragePatientFee { get; set; }
        public string CurrentlyUsingSoftware { get; set; }
        public string CurrentSoftwareName { get; set; }
    }

    public class AiCallService
    {
        private readonly string _groqApiKey;

        public AiCallService(IConfiguration configuration)
        {
            _groqApiKey = configuration["Groq:ApiKey"];
        }

        public async Task<byte[]> DownloadAudioAsync(string url)
        {
            using var client = new HttpClient();
            return await client.GetByteArrayAsync(url);
        }

        // ==============================================================
        // GROQ API WHISPER TRANSCRIPTION (Lightning Fast & High Accuracy)
        // ==============================================================
        public async Task<string> TranscribeAudioAsync(byte[] audioBytes)
        {
            if (audioBytes == null || audioBytes.Length == 0) return "Audio file empty.";
            if (string.IsNullOrEmpty(_groqApiKey)) return "Groq API key missing.";

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _groqApiKey);

            using var content = new MultipartFormDataContent();

            var audioContent = new ByteArrayContent(audioBytes);
            // Callyzer provides MP3s, so we label it accordingly
            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/mpeg");
            content.Add(audioContent, "file", "recording.mp3");

            // Use Groq's hosted Whisper V3 Turbo
            content.Add(new StringContent("whisper-large-v3-turbo"), "model");

            // Give Whisper context to improve medical term accuracy
            content.Add(new StringContent("Homeopathy, Radar software, homeopathic doctor, rubric, repertory, materia medica, medicine."), "prompt");
            content.Add(new StringContent("text"), "response_format");

            var response = await client.PostAsync("https://api.groq.com/openai/v1/audio/transcriptions", content);

            if (!response.IsSuccessStatusCode)
            {
                string error = await response.Content.ReadAsStringAsync();
                return $"[GROQ WHISPER ERROR] {response.StatusCode}: {error}";
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<AiAnalysisResult> AnalyzeCallAsync(string rawTranscription)
        {
            if (string.IsNullOrEmpty(_groqApiKey)) return null;

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(3);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _groqApiKey);

            string systemPrompt = @"You are an expert CRM sales call analyst for 'Radar', a software for homeopathic practitioners.
Analyze the transcription and return ONLY valid JSON. 

CRITICAL INSTRUCTION: Condense the dialogue into the 15 to 20 most important exchanges. Do NOT transcribe word-for-word.
Extract the customer's professional profile details. If a detail is NOT mentioned in the call, you MUST put 'N/A'.

JSON Structure:
{
  ""formatted_chat"": [
    { ""Speaker"": ""Salesperson"", ""Text"": ""..."" }
  ],
  ""AiSummary"": ""2-3 sentence overview."",
  ""SalespersonConfidence"": ""High / Moderate / Low"",
  ""SalespersonEmpathy"": ""Excellent / Moderate / Needs Improvement"",
  ""SalespersonTone"": ""Professional / Scripted / Unfocused"",
  ""UserNeeds"": ""Specific requests"",
  ""RecommendedProducts"": ""Radar software packages discussed"",
  ""GapAnalysis"": ""What the agent missed"",
  
  ""IsHomeopathicDoctor"": ""Yes / No / Pursuing BHMS / N/A"",
  ""ClinicType"": ""Own / Visiting / Hospital Attached / Govt / Chain / N/A"",
  ""WorkType"": ""Practicing / Job / Research / N/A"",
  ""HasComputer"": ""Yes / No / N/A"",
  ""DateOfBirth"": ""Extract if mentioned, else N/A"",
  ""Age"": ""Extract if mentioned, else N/A"",
  ""YearsOfPractice"": ""Extract if mentioned, else N/A"",
  ""TotalExperience"": ""Extract if mentioned, else N/A"",
  ""NumberOfClinics"": ""Extract if mentioned, else N/A"",
  ""PatientsPerDay"": ""Extract if mentioned, else N/A"",
  ""Qualification"": ""BHMS / MD / BAMS / Student / N/A"",
  ""YearOfPassing"": ""Extract if mentioned, else N/A"",
  ""CollegeName"": ""Extract if mentioned, else N/A"",
  ""AveragePatientFee"": ""Extract if mentioned, else N/A"",
  ""CurrentlyUsingSoftware"": ""Yes / No / N/A"",
  ""CurrentSoftwareName"": ""Extract software name if yes, else N/A""
}";

            var payload = new
            {
                model = "openai/gpt-oss-20b",
                messages = new[] { new { role = "system", content = systemPrompt }, new { role = "user", content = $"Transcription:\n{rawTranscription}" } },
                temperature = 0.2,
                max_tokens = 8192
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
                return new AiAnalysisResult { AiSummary = $"Groq Error {(int)response.StatusCode}", SalespersonTone = "ERROR" };

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);
            var jsonContent = document.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString().Trim();

            if (jsonContent.StartsWith("```json")) jsonContent = jsonContent.Replace("```json", "").Replace("```", "").Trim();
            else if (jsonContent.StartsWith("```")) jsonContent = jsonContent.Replace("```", "").Trim();

            try
            {
                using var resultDoc = JsonDocument.Parse(jsonContent);
                var root = resultDoc.RootElement;

                string GetStr(string key) => root.TryGetProperty(key, out var prop) ? prop.GetString() : "N/A";

                return new AiAnalysisResult
                {
                    FormattedChatJson = root.TryGetProperty("formatted_chat", out var chat) ? chat.GetRawText() : "[]",
                    AiSummary = GetStr("AiSummary"),
                    SalespersonConfidence = GetStr("SalespersonConfidence"),
                    SalespersonEmpathy = GetStr("SalespersonEmpathy"),
                    SalespersonTone = GetStr("SalespersonTone"),
                    UserNeeds = GetStr("UserNeeds"),
                    RecommendedProducts = GetStr("RecommendedProducts"),
                    GapAnalysis = GetStr("GapAnalysis"),

                    // Map the new fields safely
                    IsHomeopathicDoctor = GetStr("IsHomeopathicDoctor"),
                    ClinicType = GetStr("ClinicType"),
                    WorkType = GetStr("WorkType"),
                    HasComputer = GetStr("HasComputer"),
                    DateOfBirth = GetStr("DateOfBirth"),
                    Age = GetStr("Age"),
                    YearsOfPractice = GetStr("YearsOfPractice"),
                    TotalExperience = GetStr("TotalExperience"),
                    NumberOfClinics = GetStr("NumberOfClinics"),
                    PatientsPerDay = GetStr("PatientsPerDay"),
                    Qualification = GetStr("Qualification"),
                    YearOfPassing = GetStr("YearOfPassing"),
                    CollegeName = GetStr("CollegeName"),
                    AveragePatientFee = GetStr("AveragePatientFee"),
                    CurrentlyUsingSoftware = GetStr("CurrentlyUsingSoftware"),
                    CurrentSoftwareName = GetStr("CurrentSoftwareName")
                };
            }
            catch (JsonException ex)
            {
                return new AiAnalysisResult { FormattedChatJson = "[]", AiSummary = "JSON Parsing Error", SalespersonTone = "ERROR" };
            }
        }
    }
}