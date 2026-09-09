using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Radar_CRM.Models
{
    public class Deal
    {
        [Key]
        public int Id { get; set; }
        public string? ZohoRecordId { get; set; }// Auto-generated Deal ID

        // Basic Info
        public int? AccountId { get; set; }
        [ForeignKey("AccountId")]
        public virtual Account? Account { get; set; }
        public string? DealOwnerId { get; set; }
        [ForeignKey("DealOwnerId")]
        [InverseProperty("OwnedDeals")] // 🚀 ADD THIS LINE
        public virtual User? DealOwner { get; set; }
        public string? AccountOwner { get; set; }
        public string? DemoOwner { get; set; }

        [Required]
        public string? DealName { get; set; }
        public string? AccountName { get; set; }
        public string? ContactPersonName { get; set; }
        public string? LeadName { get; set; }
        public string? MetaCampaignName { get; set; }

        // Dropdowns
        public string? AccountType { get; set; }
        public string? LeadSource { get; set; }
        public string? PaymentMode { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentStatus { get; set; }

        // Additional Info
        public string? Remarks { get; set; }
        public string? ApprovalRequired { get; set; } // Yes/No
        public string? ApprovedBy { get; set; }

        // Financial Summary
        public decimal? SubTotal { get; set; }
        public decimal Taxes { get; set; }
        public decimal Adjustment { get; set; }
        public decimal GrandTotal { get; set; }
        public virtual ICollection<Notes> Notes { get; set; } = new List<Notes>();
        public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();

        // Related Lists
        public List<DealPaymentRow> PaymentRows { get; set; } = new List<DealPaymentRow>();
        public List<DealNote> Note { get; set; } = new List<DealNote>();
    }

    public class DealPaymentRow
    {
        [Key]
        public int Id { get; set; }
        public int DealId { get; set; }

        public int LeadId { get; set; }

        public int AccountId { get; set; }
        public Deal Deal { get; set; }

        public string? DealType { get; set; }

       public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Amount { get; set; }
        public decimal? Discount { get; set; }
        public decimal? Total { get; set; }
    }

    public class DealNote
    {
        [Key]
        public int Id { get; set; }
        public int DealId { get; set; }
        public Deal Deal { get; set; }

        public string? Owner { get; set; }
        public DateTime DateTime { get; set; } = DateTime.Now;
        public string? Description { get; set; }
        public string? AttachmentName { get; set; }
    }
}