namespace Radar_CRM.Models
{
    public class GlobalSearchResult
    {
        public string Id { get; set; } // String to accommodate User GUIDs and Int IDs
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Module { get; set; }
        public string Url { get; set; }
        public string Icon { get; set; }
    }
}
