namespace Radar_CRM.Models
{
    public class WordpressLeadDto
    {
        public string? FormId { get; set; } // <-- ADDED THIS
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Message { get; set; }

        public string? DoctorRole { get; set; } // Catch the new dropdown

        // UTM Tracking Fields
        public string? UtmSource { get; set; }
        public string? UtmMedium { get; set; }
        public string? UtmCampaign { get; set; }
        public string? UtmTerm { get; set; }
        public string? UtmContent { get; set; }
        public string? Gclid { get; set; }
    }
}