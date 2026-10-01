using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Radar_CRM.DTOs
{
    public class CallyzerWebhookEvent
    {
        public string emp_name { get; set; }
        public string emp_number { get; set; }
        public string @event { get; set; }
        public List<CallyzerWebhookPayload> call_logs { get; set; }
    }

    public class CallyzerWebhookPayload
    {
        // Callyzer sends "client_number"
        public string client_number { get; set; }

        public string emp_no { get; set; }

        // Callyzer sends "call_recording_url"
        public string call_recording_url { get; set; }

        // Allows reading "0" formatted as a string
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int duration { get; set; }

        public string call_type { get; set; }
    }
}