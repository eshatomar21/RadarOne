using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Radar_CRM.Models
{
    public class TimelineEvent
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string EntityType { get; set; } // e.g., "Account", "Lead", "Deal"

        [Required]
        public int EntityId { get; set; }

        public string EventType { get; set; } // e.g., "FieldUpdate", "SystemAction", "NoteAdded"

        public string ActionDescription { get; set; } // e.g., "AccountStatus was updated from blank to Active"

        public string FieldName { get; set; } // e.g., "MobileNumber"
        public string OldValue { get; set; }
        public string NewValue { get; set; }

        public string CreatedById { get; set; }
        public string CreatedByName { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}