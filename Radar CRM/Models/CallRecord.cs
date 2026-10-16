using System;
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

        public string? LocalFilePath { get; set; }

        [Display(Name = "Duration (Seconds)")]
        public int DurationSeconds { get; set; }

        public string SalespersonName { get; set; } = "";

        // AI Results
        public string? TranscriptionText { get; set; } = "Processing...";
        public string? AiSummary { get; set; } = "Processing...";
        public string? SalespersonConfidence { get; set; } 
        public string? SalespersonEmpathy { get; set; } = "Processing...";
        public string? SalespersonTone { get; set; } = "Processing...";
        public string? UserNeeds { get; set; } = "Processing...";
        public string? RecommendedProducts { get; set; } = "Processing...";
        public string? GapAnalysis { get; set; } = "Processing...";

        public DateTime CallDate { get; set; } = DateTime.UtcNow;

        // --- CRM Linking ---
        public int? LeadId { get; set; }

        [ForeignKey("LeadId")]
        public virtual Lead? Lead { get; set; }

        public int? AccountId { get; set; }

        [ForeignKey("AccountId")]
        public virtual Account? Account { get; set; }

        // ==========================================
        // NEW PROFILE & SOFTWARE FIELDS (AI EXTRACTED)
        // ==========================================
        public string? IsHomeopathicDoctor { get; set; }
        public string? ClinicType { get; set; }
        public string? WorkType { get; set; }
        public string? HasComputer { get; set; }
        public string? DateOfBirth { get; set; }
        public string? Age { get; set; }
        public string? YearsOfPractice { get; set; }
        public string? TotalExperience { get; set; }
        public string? NumberOfClinics { get; set; }
        public string? PatientsPerDay { get; set; }
        public string? Qualification { get; set; }
        public string? YearOfPassing { get; set; }
        public string? CollegeName { get; set; }
        public string? AveragePatientFee { get; set; }
        public string? CurrentlyUsingSoftware { get; set; }
        public string? CurrentSoftwareName { get; set; }

    }
}