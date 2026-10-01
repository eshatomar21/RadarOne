using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Radar_CRM.Models
{
    [Table("CallRecords")]
    public class CallRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        [Display(Name = "Customer Phone")]
        public string CustomerPhone { get; set; } = "";

        [MaxLength(20)]
        [Display(Name = "Salesperson Phone")]
        public string? SalespersonPhone { get; set; } = "";

        [DataType(DataType.Url)]
        public string? RecordingUrl { get; set; } = "";

        [Display(Name = "Duration (Seconds)")]
        public int DurationSeconds { get; set; }

        // 🚀 FIX: Made nullable and added default fallback text
        public string? TranscriptionText { get; set; } = "Processing...";
        public string? AiSummary { get; set; } = "Processing...";

        public DateTime CallDate { get; set; } = DateTime.UtcNow;

        public int? LeadId { get; set; }

        [ForeignKey("LeadId")]
        public virtual Lead Lead { get; set; }

        public int? AccountId { get; set; }

        [ForeignKey("AccountId")]
        public virtual Account Account { get; set; }
    }
}