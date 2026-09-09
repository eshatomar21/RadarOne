using System;
using System.ComponentModel.DataAnnotations;

namespace Radar_CRM.Models
{
    public class Vendor
    {
        [Key]
        public int Id { get; set; }

        public string? RecordId { get; set; }

        // --- Core Information ---
        [Required(ErrorMessage = "Vendor Name is required")]
        public string? VendorName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Website { get; set; }
        public string? Category { get; set; }
        public string? VendorType { get; set; }

        // --- Accounting & Tax ---
        public string? GLAccount { get; set; }
        public string? PanNo { get; set; }
        public string? GstNo { get; set; }

        // --- Ownership ---
        public string? VendorOwnerId { get; set; }
        public string? VendorOwner { get; set; }

        // --- Address ---
        public string? Street { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Country { get; set; }

        // --- Preferences & Logistics ---
        public string? Description { get; set; }
        public bool Locked { get; set; }
        public bool EmailOptOut { get; set; }
        public string? UnsubscribedMode { get; set; }
        public string? Tag { get; set; }
        public string? ConnectedToModule { get; set; }
        public string? ConnectedToId { get; set; }

        // --- Audit & Dates ---
        public string? CreatedById { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedById { get; set; }
        public string? ModifiedBy { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? CreatedTime { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? ModifiedTime { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? UnsubscribedTime { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? LastActivityTime { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? ChangeLogTime { get; set; }
    }
}