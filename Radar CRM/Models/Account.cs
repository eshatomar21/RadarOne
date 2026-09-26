using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Radar_CRM.Models
{
    public class Account
    {
        [Key]
        public int Id { get; set; }
        public string? ZohoRecordId { get; set; } // Used for migration

        // --- Accounts - Master Details ---
        public string? DataSource { get; set; }
        public string? AccountType { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime DateOfEntry { get; set; } = DateTime.Now;
        public string? CurrentStatus { get; set; }
        public string? ContactStatus { get; set; }
        public string? MetaCampaignName { get; set; }
        public string? SeminarName { get; set; }
        public string? LeadStatus { get; set; }

        [Column("Group_Name")]
        public string? GroupName { get; set; }

        // --- Relational Ownership ---
        public string? AccountOwnerId { get; set; }
        [ForeignKey("AccountOwnerId")]
        [InverseProperty("OwnedAccounts")]
        public virtual User? AccountOwner { get; set; }

        public string? CoOwnerId { get; set; }
        [ForeignKey("CoOwnerId")]
        public virtual User? CoOwner { get; set; }

        // --- Basic Contact Details ---
        [Required]
        public string? AccountName { get; set; }
        public string? MobileNumber { get; set; }
        public string? AlternateMobile { get; set; }
        public string? ContactPersonName { get; set; }
        public string? Email { get; set; }
        public string? AlternateEmailID { get; set; }
        public string? ProfilePendingReason { get; set; }
        public string? QualificationStatus { get; set; }

        // --- Address Information (Address 1) ---
        public string? Addr1_Country { get; set; }
        public string? Addr1_FlatHouse { get; set; }
        public string? Addr1_Street { get; set; }
        public string? Addr1_City { get; set; }
        public string? Addr1_State { get; set; }
        public string? Addr1_Zip { get; set; }
        public string? Addr1_Latitude { get; set; }
        public string? Addr1_Longitude { get; set; }

        [InverseProperty("Account")]
        public virtual ICollection<ProductPaymentRow> PaymentRows { get; set; } = new List<ProductPaymentRow>();

        // --- Address Information (Address 2) ---
        public string? Addr2_Country { get; set; }
        public string? Addr2_FlatHouse { get; set; }
        public string? Addr2_Street { get; set; }
        public string? Addr2_City { get; set; }
        public string? Addr2_State { get; set; }
        public string? Addr2_Zip { get; set; }
        public string? Addr2_Latitude { get; set; }
        public string? Addr2_Longitude { get; set; }

        // --- Professional Profile ---
        public string? IsHomeopathicDoctor { get; set; }
        public string? ClinicType { get; set; }
        public int? YearsOfPractice { get; set; }
        public string? Qualification { get; set; }
        public string? YearOfPassing { get; set; }
        public decimal? AveragePatientFee { get; set; }

        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }
        public string? WorkType { get; set; }
        public string? HasComputer { get; set; }
        public int? PatientsPerDay { get; set; }
        public string? CollegeName { get; set; }
        public int? TotalExperience { get; set; }
        public int? NumberOfClinics { get; set; }
        public int? Age { get; set; }

        // --- Software Details ---
        public string? CurrentlyUsingSoftware { get; set; }
        public string? CurrentSoftwareName { get; set; }
        public string? RadarOpusLicenseNo { get; set; }
        public string? RadarOpusVersion { get; set; }

        // --- Product & Purchase Details ---
        public string? ProductPurchased { get; set; }

        [DataType(DataType.Date)]
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchaseValue { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentStatus { get; set; }

        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }

        [NotMapped]
        public List<Notes> Notes { get; set; } = new List<Notes>();

        // --- Profile Completion Tracking ---
        public int? ProfileCompletionPercentage { get; set; }
        public string? ReferralSource { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? Profilestatus { get; set; }
        public int? ProfileRate { get; set; }

        // --- More Contact Details ---
        public string? ContactPerson1 { get; set; }
        public string? ContactPerson2 { get; set; }
        public string? ContactPerson3 { get; set; }
        public string? Contact1Phone { get; set; }
        public string? Contact2Phone { get; set; }
        public string? Contact3Phone { get; set; }

        // --- Description Information ---
        public string? Description { get; set; }
        public bool IsDuplicated { get; set; }

        // 🚀 --- NEW: AD & CAMPAIGN TRACKING --- 🚀
        public string? SocialLeadId { get; set; }
        public string? LeadStage { get; set; }
        public string? OldLeadStatus { get; set; }
        public string? Gclid { get; set; }
        public string? Zcampaignid { get; set; }
        public string? Adgroupid { get; set; }
        public string? Adid { get; set; }
        public string? Keywordid { get; set; }
        public string? Keyword { get; set; }
        public string? ClickType { get; set; }
        public string? DeviceType { get; set; }
        public string? AdNetwork { get; set; }
        public string? SearchPartnerNetwork { get; set; }
        public string? AdCampaignName { get; set; }
        public string? AdGroupName { get; set; }
        public string? Ad { get; set; }
        public string? Gadconfigid { get; set; }

        [DataType(DataType.Date)]
        public DateTime? AdClickDate { get; set; }
        public decimal? CostPerClick { get; set; }
        public decimal? CostPerConversion { get; set; }

        [DataType(DataType.Date)]
        public DateTime? ConversionExportedOn { get; set; }
        public string? ConversionExportStatus { get; set; }
        public string? ReasonForConversionFailure { get; set; }

        // --- Navigation Properties ---
        public virtual ICollection<Deal> Deals { get; set; } = new List<Deal>();
        public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
        public virtual ICollection<Notes> Note { get; set; } = new List<Notes>();
        public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
    }
}
