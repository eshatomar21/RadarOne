using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Radar_CRM.DTOs
{
    public class CallyzerWebhookEvent
    {
        // Adding '?' makes the field optional so ASP.NET won't reject missing data
        public string? emp_name { get; set; }
        public string? emp_number { get; set; }
        public string? @event { get; set; }
        public List<CallyzerWebhookPayload>? call_logs { get; set; }
    }

    public class CallyzerWebhookPayload
    {
        public string? client_number { get; set; }

        // This '?' specifically fixes your 400 error
        public string? emp_no { get; set; }

        public string? call_recording_url { get; set; }

        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int duration { get; set; }

        public string? call_type { get; set; }
    }
}