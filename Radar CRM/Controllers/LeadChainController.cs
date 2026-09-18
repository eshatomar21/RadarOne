using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using Radar_CRM.Data;
using Radar_CRM.Models;

namespace Radar_CRM.Controllers
{
    public class LeadChainsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LeadChainsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /LeadChains/Create
        [HttpGet]
        public IActionResult Create()
        {
            var viewModel = new LeadChainCreateViewModel
            {
                Mappings = new List<FieldMapping>
                {
                    new FieldMapping { MetaField = "Full name" },
                    new FieldMapping { MetaField = "Phone number" },
                    new FieldMapping { MetaField = "Email" },
                    new FieldMapping { MetaField = "Work Type" },
                    new FieldMapping { MetaField = "Clinic Type" }
                },
                CrmColumns = new List<string> 
                { 
                    "AccountName", 
                    "MobileNumber", 
                    "Email", 
                    "WorkType",
                    "ClinicType"
                }
            };

            return View(viewModel);
        }

        // POST: /LeadChains/Create
        [HttpPost]
        public IActionResult Create(LeadChainCreateViewModel model) // <--- Correct model type
        {
            if (ModelState.IsValid)
            {
                // Handle saving your mappings here if needed
                // e.g., saving to _context.LeadChains...
                
                return RedirectToAction("Index"); // Or wherever you want to go after saving
            }

            // If validation fails, re-populate CrmColumns and return view
            model.CrmColumns = new List<string> { "AccountName", "MobileNumber", "Email", "WorkType", "ClinicType" };
            return View(model);
        }
    }
}