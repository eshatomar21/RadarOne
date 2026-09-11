using System;
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
        public string? DataSource { get; set; } // Meta ads, Google ads
        
       
        public string? AccountType { get; set; } // None, Individual, Group, Institutional, Government

        [DataType(DataType.DateTime)]
        public DateTime DateOfEntry { get; set; } = DateTime.Now;
        public string? CurrentStatus { get; set; } // None, User, Non User
        public string? ContactStatus { get; set; } // Readonly in UI

        public string? MetaCampaignName {get; set;}

        public string? SeminarName { get; set; }

        public string? LeadStatus { get; set; }

        // --- Relational Ownership ---
        public string? AccountOwnerId { get; set; }
        [ForeignKey("AccountOwnerId")]
        [InverseProperty("OwnedAccounts")] // 🚀 ADD THIS LINE
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

        // Optional: This allows you to see all payment rows associated with this account
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

        // --- Product & Purchase Details ---
        public string? ProductPurchased { get; set; }

        [DataType(DataType.Date)]
        public DateTime? PurchaseDate { get; set; }
        public decimal? PurchaseValue { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentStatus { get; set; }

        // Inside your Account or Lead class...
        [NotMapped] // Tells Entity Framework not to create a direct SQL column for this
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

        // --- Navigation Properties ---
        // Entity Framework will now map Notes, Tasks, Leads, and Contacts directly to this Account

        public virtual ICollection<Deal> Deals { get; set; } = new List<Deal>();
        public virtual ICollection<Lead> Leads { get; set; } = new List<Lead>();
        public virtual ICollection<Notes> Note { get; set; } = new List<Notes>();
        public virtual ICollection<Task> Tasks { get; set; } = new List<Task>();
    }
}