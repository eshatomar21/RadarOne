using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Radar_CRM.Models
{
    public class Notes
    {
        [Key]
        public int Id { get; set; }
        public string? ZohoRecordId { get; set; }

        // --- Relational Ownership ---
        public string? NoteOwnerId { get; set; }
        [ForeignKey("NoteOwnerId")]
        public virtual User? NoteOwner { get; set; }

        // --- Nullable Foreign Keys (The Polymorphic Fix) ---
        // Only one of these will be filled per note. EF Core can now securely JOIN and .Include() them.
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

        // --- Note Data ---
        [Required]
        public string Description { get; set; }
        public DateTime CreatedDateTime { get; set; } = DateTime.Now;

        public string? AttachmentFileName { get; set; }
        public string? AttachmentFilePath { get; set; }
    }
}