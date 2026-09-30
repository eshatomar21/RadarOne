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
        public string CustomerPhone { get; set; }

        [MaxLength(20)]
        [Display(Name = "Salesperson Phone")]
        public string SalespersonPhone { get; set; }

        [DataType(DataType.Url)]
        public string RecordingUrl { get; set; }

        [Display(Name = "Duration (Seconds)")]
        public int DurationSeconds { get; set; }

        // AI Results
        public string TranscriptionText { get; set; }
        public string AiSummary { get; set; }

        public DateTime CallDate { get; set; } = DateTime.UtcNow;

        // --- CRM Linking ---
        // Nullable (?) because a call might belong to a Lead, an Account, both, or neither yet.

        public int? LeadId { get; set; }

        [ForeignKey("LeadId")]
        public virtual Lead Lead { get; set; }

        public int? AccountId { get; set; }

        [ForeignKey("AccountId")]
        public virtual Account Account { get; set; }
    }
}