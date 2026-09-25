using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Radar_CRM.Models
{
    public class Notes
    {
        [Key]
        public int Id { get; set; }
        public string? ZohoRecordId { get; set; }

        // 🚀 FIX: Spaces removed from the property names so C# can read them, 
        // but they still represent Note Content and Note Title perfectly!
        public string? NoteContent { get; set; }
        public string? NoteTitle { get; set; }

        [Required]
        public string Description { get; set; }
      
        // --- Relational Ownership ---
        public string? NoteOwnerId { get; set; }
        [ForeignKey("NoteOwnerId")]
        public virtual User? NoteOwner { get; set; }

        // --- Nullable Foreign Keys (The Polymorphic Fix) ---
        public int? AccountId { get; set; }
        [ForeignKey("AccountId")]
        public virtual Account? Account { get; set; }

        public int? LeadId { get; set; }
        [ForeignKey("LeadId")]
        public virtual Lead? Lead { get; set; }

        public int? TaskId { get; set; }
        [ForeignKey("TaskId")]
        public virtual Task? Task { get; set; }

        public int? DealId { get; set; }
        [ForeignKey("DealId")]
        public virtual Deal? Deal { get; set; }

        // --- Specific Parent Names (Replacing Generic Parent ID) ---
        public string? AccountName { get; set; }
        public string? LeadName { get; set; }
        public string? DealName { get; set; }
        public string? TaskName { get; set; }

        // --- Note Data ---
        public DateTime CreatedDateTime { get; set; } = DateTime.Now;
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }
        public DateTime? ModifiedTime { get; set; }

        public string? AttachmentFileName { get; set; }
        public string? AttachmentFilePath { get; set; }
    }
}