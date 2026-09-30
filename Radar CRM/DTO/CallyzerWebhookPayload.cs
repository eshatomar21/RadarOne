using System;

namespace Radar_CRM.DTOs
{
    public class CallyzerWebhookPayload
    {
        // Notice these property names are lowercase with underscores.
        // This is required because we must match the exact JSON format Callyzer sends.

        public string client_no { get; set; }
        public string emp_no { get; set; }
        public string recording_url { get; set; }
        public int duration { get; set; }
        public string call_type { get; set; }
    }
}