using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Radar_CRM.Models
{
    public class Lead
    {
        [Key]
        public int Id { get; set; }
        public string? ZohoRecordId { get; set; }

        // --- Account Information ---
        public string? LeadName { get; set; }
        public string? AccountType { get; set; }
        public string? MobileNumber { get; set; }
        public string? EmailID { get; set; }
        public string? CampaignSource { get; set; }
        public string? CurrentStatus { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? CreatedDateAndTime { get; set; }
        [DataType(DataType.DateTime)]
        public DateTime? ModifiedTime { get; set; }

        public DateOnly? TrialStartDate { get; set; }
        public DateOnly? TrialEndDate { get; set; }

        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }

        public DateTime? DemoScheduledDate { get; set; }
        public string? ContactName { get; set; }
        public string? GroupName { get; set; }
        public string? AlternateMobile { get; set; }
        public string? AlternateEmailID { get; set; }
        public string? Pipeline { get; set; }
        public string? NewSoftware { get; set; }
        public string? MetaCampaignName { get; set; }
        public string? DataSources { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime? LeadCreatedTime { get; set; }

        // --- Lead Information ---
        public string? Stage { get; set; }
        public decimal? Budget { get; set; }
        public decimal? ExpectedRevenue { get; set; }
        public string? LeadStatus { get; set; }
        public string? TimePeriodToBuy { get; set; }
        public decimal? Probability { get; set; }

        // --- Address Block ---
        public string? Addr1_Country { get; set; }
        public string? Addr1_FlatHouse { get; set; }
        public string? Addr1_Street { get; set; }
        public string? Addr1_City { get; set; }
        public string? Addr1_State { get; set; }
        public string? Addr1_Zip { get; set; }
        public string? Addr1_Coordinates { get; set; }

        public string? Addr2_Country { get; set; }
        public string? Addr2_FlatHouse { get; set; }
        public string? Addr2_Street { get; set; }
        public string? Addr2_City { get; set; }
        public string? Addr2_State { get; set; }
        public string? Addr2_Zip { get; set; }
        public string? Addr2_Coordinates { get; set; }

        public virtual ICollection<Notes> Notes { get; set; } = new List<Notes>();
        public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
        public virtual ICollection<Deal> Deals { get; set; } = new List<Deal>();

        // --- Relational Links ---
        public int? AccountId { get; set; }
        [ForeignKey("AccountId")]
        public virtual Account? Account { get; set; }

        public string? LeadOwnerId { get; set; }
        [ForeignKey("LeadOwnerId")]
        [InverseProperty("OwnedLeads")]
        public virtual User? LeadOwner { get; set; }

        public string? AccountOwnerId { get; set; }
        [ForeignKey("AccountOwnerId")]
        public virtual User? AccountOwner { get; set; }

        public string? CoOwnerId { get; set; }
        [ForeignKey("CoOwnerId")]
        public virtual User? CoOwner { get; set; }

        public string? DemoOwnerId { get; set; }
        [ForeignKey("DemoOwnerId")]
        public virtual User? DemoOwner { get; set; }

        // --- Professional Details ---
        public string? IsHomeopathicDoctor { get; set; }
        public string? ClinicType { get; set; }
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }
        public string? WorkType { get; set; }
        public string? HasComputer { get; set; }
        public int? Age { get; set; }

        // --- Practice Details ---
        public int? YearOfPractice { get; set; }
        public int? TotalExperience { get; set; }
        public decimal? AveragePatientFee { get; set; }
        public int? NumberOfClinics { get; set; }
        public int? PatientsPerDay { get; set; }

        // --- Qualification Details ---
        public string? Qualification { get; set; }
        public string? YearOfPassing { get; set; }
        public string? CollegeName { get; set; }

        // --- Software Details ---
        public string? CurrentlyUsingSoftware { get; set; }
        public string? RadarOpusVersion { get; set; }
        public string? CurrentSoftwareName { get; set; }
        public string? RadarOpusLicenseNo { get; set; }

        // --- Product & Purchase Details ---
        public string? ProductPackage { get; set; }
        public decimal? PurchaseValue { get; set; }
        [DataType(DataType.Date)]
        public DateTime? PurchaseDate { get; set; }
        public string? PaymentStatus { get; set; }
        public string? CustomerStatus { get; set; }

        // --- Deals Section ---
        public string? DealType { get; set; }
        public decimal? DealValue { get; set; }
        public string? PackageSelected { get; set; }
        public decimal? Discount { get; set; }
        public string? SeminarName { get; set; }

        [NotMapped]
        public List<Notes> Note { get; set; } = new List<Notes>();

        // --- Product Payments Section ---
        public decimal? SubTotal { get; set; }
        public decimal? Taxes { get; set; }
        public decimal? Adjustment { get; set; }
        public decimal? GrandTotal { get; set; }

        [InverseProperty("Lead")]
        public virtual IList<ProductPaymentRow> PaymentRow { get; set; } = new List<ProductPaymentRow>();

        // --- Payment Details ---
        public string? PaymentMode { get; set; }
        public string? Remarks { get; set; }
        public string? PaymentType { get; set; }

        // --- More Contact Details ---
        public string? ContactPerson1 { get; set; }
        public string? ContactPerson2 { get; set; }
        public string? ContactPerson3 { get; set; }
        public string? Contact1Phone { get; set; }
        public string? Contact2Phone { get; set; }
        public string? Contact3Phone { get; set; }

        // --- Conversation Details ---
        [DataType(DataType.Date)]
        public DateTime? FirstCallDate { get; set; }
        [DataType(DataType.Date)]
        public DateTime? NextFollowUpDate { get; set; }
        public string? ConversationRemarks { get; set; }
        [DataType(DataType.Date)]
        public DateTime? LastContactDate { get; set; }
        public string? InterestedPackage { get; set; }
        public string? LostReason { get; set; }
        public string? LeadProfile { get; set; }
        public string? NotInterestedReason { get; set; }

        // --- AD & CAMPAIGN TRACKING ---
        public string? SocialLeadID { get; set; }
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

        // ==============================================
        // UI Display Properties (Not saved in DB)
        // ==============================================
        [NotMapped]
        public int? DealId { get; set; }
        [NotMapped]
        public string? DealName { get; set; }
        [NotMapped]
        public string? AccountName { get; set; }

        // --- Description ---
        public string? Description { get; set; }
    } // <--- THIS CLOSES THE LEAD CLASS

    // THIS MUST BE OUTSIDE THE LEAD CLASS
    public class ProductPaymentRow
    {
        [Key]
        public int Id { get; set; }

        // --- 1. Lead Relationship ---
        public int LeadId { get; set; }

        [ForeignKey("LeadId")]
        [InverseProperty("PaymentRow")]
        public virtual Lead? Lead { get; set; }

        // --- 2. Account Relationship (NEW) ---
        public int? AccountId { get; set; }

        [ForeignKey("AccountId")]
        public virtual Account? Account { get; set; }

        [NotMapped]
        public string? AccountName { get; set; }

        // --- 3. Product Relationship (NEW) ---
        public int? ProductId { get; set; }

        [ForeignKey("ProductId")]
        public virtual Product? Product { get; set; }

        // --- Data Properties ---
        public string? DealType { get; set; }
        public string? ProductName { get; set; }
        public int? Quantity { get; set; }
        public decimal? ProductPrice { get; set; }
        public decimal? Total { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? FinalAmount { get; set; }
    } // <--- THIS CLOSES PRODUCTPAYMENTROW CLASS
} // <--- THIS CLOSES THE NAMESPACE