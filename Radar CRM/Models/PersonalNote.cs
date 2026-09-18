using System;
using System.ComponentModel.DataAnnotations;

namespace Radar_CRM.Models
{
    public class PersonalNote
    {
        [Key]
        public int Id { get; set; }

        // Links the note strictly to the logged-in user
        [Required]
        public string UserId { get; set; }

        [Required]
        public DateTime DateForNote { get; set; }

        [Required]
        public string NoteText { get; set; }

        public DateTime? CreatedAt { get; set; } = DateTime.Now;
    }
}