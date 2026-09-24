using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CsvHelper.Configuration.Attributes;

namespace Radar_CRM.Models
{
    public class Postcode
    {
        [Key]
        [Ignore] // Tells CsvHelper not to look for this in the CSV
        public int Id { get; set; } // Database Primary Key

        // Add this missing property for the cascading dropdowns:
        public string? Country { get; set; }

        // ==========================================
        // CRM CORE IDENTIFIERS
        // ==========================================
        
        [Name("Record Status", "RecordStatus")]
        public string? RecordStatus { get; set; }

    

        // ==========================================
        // LOCATION DATA
        // ==========================================
        [Name("Zipcodes", "ZipCode", "Zip Code")]
        public string ZipCode { get; set; }

        public string City { get; set; }
        public string State_Name { get; set; }
        public string State_Code { get; set; }

        // ==========================================
        // CONTACT INFO
        // ==========================================
        public string? Email { get; set; }

        [Name("Secondary Email", "SecondaryEmail")]
        public string? SecondaryEmail { get; set; }

        [Name("Email Opt Out")]
        public bool? EmailOptOut { get; set; }

       
        // ==========================================
        // FINANCIALS & SYSTEM METADATA
        // ==========================================
        public string? Currency { get; set; }

        [Name("Exchange Rate")]
        public decimal? ExchangeRate { get; set; }

        
        public string? Tag { get; set; }

        public bool? Locked { get; set; }

        [Name("Is Record Duplicate", "IsRecordDuplicate")]
        public bool? IsRecordDuplicate { get; set; }
    }
}