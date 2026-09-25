using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data; // Replace with your actual DbContext namespace
using Radar_CRM.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Radar_CRM.Controllers
{
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context; // Adjust to your DbContext name

        public SearchController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GlobalSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                return Json(new List<GlobalSearchResult>());
            }

            var results = new List<GlobalSearchResult>();

            // Note: EF Core requires sequential execution on a single DbContext instance.

            // 1. Search Accounts
            var accounts = await _context.Accounts
                .Where(a => a.AccountName.Contains(query) || a.Email.Contains(query) || a.MobileNumber.Contains(query))
                .Take(5)
                .Select(a => new GlobalSearchResult
                {
                    Id = a.Id.ToString(),
                    Title = a.AccountName,
                    Subtitle = a.Email ?? a.MobileNumber,
                    Module = "Accounts",
                    Icon = "bi-buildings",
                    Url = $"/Accounts/Edit/{a.Id}"
                }).ToListAsync();
            results.AddRange(accounts);

            // 2. Search Leads
            var leads = await _context.Leads
                .Where(l => l.LeadName.Contains(query) || l.EmailID.Contains(query) || l.MobileNumber.Contains(query))
                .Take(5)
                .Select(l => new GlobalSearchResult
                {
                    Id = l.Id.ToString(),
                    Title = l.LeadName,
                    Subtitle = l.EmailID ?? l.MobileNumber,
                    Module = "Leads",
                    Icon = "bi-person-lines-fill",
                    Url = $"/Leads/Edit/{l.Id}"
                }).ToListAsync();
            results.AddRange(leads);

            // 3. Search Deals
            var deals = await _context.Deals
                .Where(d => d.DealName.Contains(query))
                .Take(5)
                .Select(d => new GlobalSearchResult
                {
                    Id = d.Id.ToString(),
                    Title = d.DealName,
                    Subtitle = $"Value: {d.GrandTotal}",
                    Module = "Deals",
                    Icon = "bi-briefcase",
                    Url = $"/Deals/Edit/{d.Id}"
                }).ToListAsync();
            results.AddRange(deals);

            // 4. Search Tasks
            var tasks = await _context.Tasks
                .Where(t => t.Subject.Contains(query))
                .Take(5)
                .Select(t => new GlobalSearchResult
                {
                    Id = t.Id.ToString(),
                    Title = t.Subject,
                    Subtitle = $"Status: {t.Status}",
                    Module = "Tasks",
                    Icon = "bi-check2-square",
                    Url = $"/Tasks/Edit/{t.Id}"
                }).ToListAsync();
            results.AddRange(tasks);

            // 5. Search Products
            var products = await _context.Products
                .Where(p => p.ProductName.Contains(query) || p.ProductCode.Contains(query))
                .Take(5)
                .Select(p => new GlobalSearchResult
                {
                    Id = p.Id.ToString(),
                    Title = p.ProductName,
                    Subtitle = $"Code: {p.ProductCode}",
                    Module = "Products",
                    Icon = "bi-box",
                    Url = $"/Products/Edit/{p.Id}"
                }).ToListAsync();
            results.AddRange(products);

            // 6. Search Vendors
            var vendors = await _context.Vendors
                .Where(v => v.VendorName.Contains(query) || v.Email.Contains(query))
                .Take(5)
                .Select(v => new GlobalSearchResult
                {
                    Id = v.Id.ToString(),
                    Title = v.VendorName,
                    Subtitle = v.Email,
                    Module = "Vendors",
                    Icon = "bi-shop",
                    Url = $"/Vendors/Edit/{v.Id}"
                }).ToListAsync();
            results.AddRange(vendors);

            return Json(results);
        }
    }
}
