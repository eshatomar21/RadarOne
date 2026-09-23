using System;
using System.ComponentModel.DataAnnotations;

namespace Radar_CRM.Models
{
    public class Task
    {
        [Key]
        public int Id { get; set; } // Record Id

        // Ownership
        public string? TaskOwnerId { get; set; }
        public string? TaskOwner { get; set; }

        // Primary Info
        [Required(ErrorMessage = "Subject is required")]
        public string Subject { get; set; }
        public DateTime? DueDate { get; set; }

        // Relations (Updated)
        public int? AccountId { get; set; }
        public int? LeadId { get; set; }
        public string? RelatedToId { get; set; }
        public string? RelatedTo { get; set; }

        // Status & Priority
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public DateTime? ClosedTime { get; set; }

        public DateTime? CreatedDateAndTime { get; set; }

        // Scheduling
        public string? Repeat { get; set; }
        public DateTime? Reminder { get; set; }

        // Audit Fields
        public string? CreatedById { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedById { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime CreatedTime { get; set; } = DateTime.Now;
        public DateTime? ModifiedTime { get; set; }
        public DateTime? LastActivityTime { get; set; }
        public DateTime? ChangeLogTime { get; set; }

        // Additional Info
        public string? Tag { get; set; }
        public string? Description { get; set; }
        public bool? IsLocked { get; set; }
        public string? Mobile { get; set; }
        public string? LeadStatus { get; set; }
    }
}