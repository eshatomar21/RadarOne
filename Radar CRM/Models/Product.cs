using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Radar_CRM.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        public string? RecordId { get; set; }

        // --- Core Information ---
        public string? ProductOwnerId { get; set; }
        public string? ProductOwner { get; set; }

        [Required(ErrorMessage = "Product Name is required")]
        public string? ProductName { get; set; }
        public string? ProductCode { get; set; }
        public string? VendorId { get; set; }
        public string? VendorName { get; set; }
        public bool ProductActive { get; set; }
        public string? Manufacturer { get; set; }
        public string? ProductCategory { get; set; }

        // --- Dates ---
        [DataType(DataType.Date)]
        public DateTime? SalesStartDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? SalesEndDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? SupportStartDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? SupportEndDate { get; set; }

        // --- Pricing & Stock ---
        public decimal? UnitPrice { get; set; }
        public decimal? CommissionRate { get; set; }
        public decimal? Tax { get; set; }
        public bool Taxable { get; set; }
        public string? UsageUnit { get; set; }
        public int? QtyOrdered { get; set; }
        public int? QuantityInStock { get; set; }
        public int? ReorderLevel { get; set; }
        public int? QuantityInDemand { get; set; }

        // --- Logistics & Other ---
        public string? HandlerId { get; set; }
        public string? Handler { get; set; }
        public string? Description { get; set; }
        public bool Locked { get; set; }
        public string? ConnectedToModule { get; set; }
        public string? ConnectedToId { get; set; }
        public decimal? USD { get; set; }
        public string? Tag { get; set; }

        // Optional: This allows you to see all payment rows associated with a specific product
        [InverseProperty("Product")]
        public virtual ICollection<ProductPaymentRow> PaymentRows { get; set; } = new List<ProductPaymentRow>();

        // --- Audit Fields ---
        public string? CreatedById { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedById { get; set; }
        public string? ModifiedBy { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? CreatedTime { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? ModifiedTime { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? LastActivityTime { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? ChangeLogTime { get; set; }
    }
}