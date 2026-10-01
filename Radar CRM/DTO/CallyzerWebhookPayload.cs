using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Radar_CRM.DTOs
{
    public class CallyzerWebhookEvent
    {
        public string? emp_name { get; set; }
        public string? emp_number { get; set; }
        public string? @event { get; set; }
        public List<CallyzerWebhookPayload>? call_logs { get; set; }
    }

    public class CallyzerWebhookPayload
    {
        public string? client_number { get; set; }
        public string? emp_no { get; set; }
        public string? call_recording_url { get; set; }

        // 🚀 CHANGED TO STRING to prevent JSON parsing crashes when Callyzer sends "0" in quotes
        public string? duration { get; set; }

        public string? call_type { get; set; }
    }
}