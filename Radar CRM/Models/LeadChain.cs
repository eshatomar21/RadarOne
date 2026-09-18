using System;
using System.Collections.Generic;

namespace Radar_CRM.Models
{
    // 1. The main Lead Chain Model for your database
    public class LeadChain
    {
        public int Id { get; set; }
        public string ChainName { get; set; }
        public string FacebookPageId { get; set; }
        public string PageAccessToken { get; set; } // Required to fetch lead data
        public string DestinationModule { get; set; }
        public int TotalLeadsSynced { get; set; }
        public DateTime LastLeadDate { get; set; }
        public bool IsActive { get; set; }

        public List<FieldMapping> FieldMappings { get; set; } = new();
    }

    // 2. Stores the left-to-right mapping rules (Meta Field -> CRM Field)
    public class FieldMapping
    {
        public int Id { get; set; }
        public int LeadChainId { get; set; }
        public string MetaField { get; set; }
        public string CrmField { get; set; }
    }

    // 3. The target CRM Entity
    public class Contact
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string AccountName { get; set; }
    }

    // 4. ADDED: The missing ViewModel for your Create Page
    // This holds the exact fields your Controller and View were looking for.
    public class LeadChainCreateViewModel
    {
        public List<FieldMapping> Mappings { get; set; } = new();
        public List<string> CrmColumns { get; set; } = new();
    }
}