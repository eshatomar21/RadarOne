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

        // 🚀 NEW: Stores the physical C: drive path of the downloaded recording
        public string? LocalFilePath { get; set; }

        [Display(Name = "Duration (Seconds)")]
        public int DurationSeconds { get; set; }

        public string SalespersonName { get; set; }

        // AI Results - Core
        public string? TranscriptionText { get; set; } = "Processing...";
        public string? AiSummary { get; set; } = "Processing...";

        // 🚀 NEW: AI Results - Score Card & Analysis
        public string? SalespersonConfidence { get; set; } = "Processing...";
        public string? SalespersonEmpathy { get; set; } = "Processing...";
        public string? SalespersonTone { get; set; } = "Processing...";
        public string? UserNeeds { get; set; } = "Processing...";
        public string? RecommendedProducts { get; set; } = "Processing...";
        public string? GapAnalysis { get; set; } = "Processing...";

        public DateTime CallDate { get; set; } = DateTime.UtcNow;

        // --- CRM Linking ---
        public int? LeadId { get; set; }

        [ForeignKey("LeadId")]
        public virtual Lead Lead { get; set; }

        public int? AccountId { get; set; }

        [ForeignKey("AccountId")]
        public virtual Account Account { get; set; }
    }
}