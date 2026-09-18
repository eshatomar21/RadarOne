using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using Radar_CRM.Models; // Make sure this matches your Models namespace

namespace Radar_CRM.Views.LeadChains
{
    public class CreateModel : PageModel
    {
        [BindProperty]
        public List<FieldMapping> Mappings { get; set; } = new List<FieldMapping>();

        public List<string> CrmColumns { get; set; } = new List<string>();

        public void OnGet()
        {
            // Initialize the list with the incoming Meta fields
            Mappings = new List<FieldMapping>
            {
                new FieldMapping { MetaField = "Full name" },
                new FieldMapping { MetaField = "Phone number" },
                new FieldMapping { MetaField = "Email" },
                new FieldMapping { MetaField = "Work Type" },
                new FieldMapping { MetaField = "Clinic Type" }
            };

            // Set the available target columns to match your Account model properties
            CrmColumns = new List<string>
            {
                "AccountName",
                "MobileNumber",
                "Email",
                "WorkType",
                "ClinicType"
            };
        }

        public IActionResult OnPost()
        {
            if (Mappings != null && Mappings.Count > 0)
            {
                foreach (var mapping in Mappings)
                {
                    System.Console.WriteLine($"Meta Field: {mapping.MetaField} -> Account Column: {mapping.CrmField}");
                }
            }

            return RedirectToPage();
        }
    }
}