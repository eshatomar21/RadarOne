using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Radar_CRM.Controllers
{
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;

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

            // 🚀 NEW: Create a completely space-stripped version of the user's query
            // E.g., "John Doe" becomes "JohnDoe", "8859 401" becomes "8859401"
            string strippedQuery = query.Replace(" ", "");

            // ==========================================
            // 🚀 1. GET CURRENT USER & ADMIN STATUS
            // ==========================================
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            bool isAdmin = currentUser != null && !string.IsNullOrWhiteSpace(currentUser.Profile) &&
                           (currentUser.Profile.Contains("Admin", System.StringComparison.OrdinalIgnoreCase) ||
                            currentUser.Profile.Equals("Administrator", System.StringComparison.OrdinalIgnoreCase));

            // ==========================================
            // 🚀 2. BUILD VISIBLE USER IDs (Security Wall)
            // ==========================================
            var visibleUserIds = new List<string>();
            if (currentUserId != null) visibleUserIds.Add(currentUserId); // Always see own records

            if (!isAdmin && currentUser?.RoleId != null)
            {
                var allRoles = await _context.Roles.ToListAsync();
                var visibleRoleIds = new List<int>();

                // Add all subordinate roles dynamically from the tree
                visibleRoleIds.AddRange(GetSubordinateRoleIds(allRoles, currentUser.RoleId));

                // Add peer visibility if enabled for this role
                var currentUserRoleModel = allRoles.FirstOrDefault(r => r.Id == currentUser.RoleId);
                if (currentUserRoleModel != null && currentUserRoleModel.ShareDataWithPeers)
                {
                    visibleRoleIds.Add(currentUser.RoleId.Value);
                }

                // Fetch the actual User IDs belonging to these visible roles
                var subordinateUserIds = await _context.Users
                    .Where(u => u.RoleId.HasValue && visibleRoleIds.Contains(u.RoleId.Value))
                    .Select(u => u.Id)
                    .ToListAsync();

                visibleUserIds.AddRange(subordinateUserIds);
            }

            var results = new List<GlobalSearchResult>();

            // ==========================================
            // 🚀 3. SMART ACCOUNTS SEARCH
            // ==========================================
            var accounts = await _context.Accounts
                .Where(a => a.AccountName.Replace(" ", "").Contains(strippedQuery) ||
                            a.Email.Replace(" ", "").Contains(strippedQuery) ||
                            a.AlternateEmailID.Replace(" ", "").Contains(strippedQuery) ||
                            a.ContactPersonName.Replace(" ", "").Contains(strippedQuery) ||
                            a.MobileNumber.Replace(" ", "").Contains(strippedQuery) ||
                            a.AlternateMobile.Replace(" ", "").Contains(strippedQuery) ||
                            a.Phone.Replace(" ", "").Contains(strippedQuery) ||
                            a.Contact1Phone.Replace(" ", "").Contains(strippedQuery))
                .Take(5)
               .Select(a => new GlobalSearchResult
               {
                   Id = a.Id.ToString(),
                   Title = a.AccountName,
                   Subtitle = (a.Email ?? a.MobileNumber ?? "Account") + " | Owner: " + (_context.Users.Where(u => u.Id == a.AccountOwnerId).Select(u => u.FirstName).FirstOrDefault() ?? "Unassigned"),
                   Module = "Accounts",
                   Icon = "bi-buildings",
                   Url = (isAdmin || visibleUserIds.Contains(a.AccountOwnerId)) ? $"/Accounts/Edit/{a.Id}" : ""
               }).ToListAsync();
            results.AddRange(accounts);

            // ==========================================
            // 🚀 4. SMART LEADS SEARCH
            // ==========================================
            var leads = await _context.Leads
                .Where(l => l.LeadName.Replace(" ", "").Contains(strippedQuery) ||
                            l.EmailID.Replace(" ", "").Contains(strippedQuery) ||
                            l.AlternateEmailID.Replace(" ", "").Contains(strippedQuery) ||
                            l.ContactName.Replace(" ", "").Contains(strippedQuery) ||
                            l.MobileNumber.Replace(" ", "").Contains(strippedQuery) ||
                            l.AlternateMobile.Replace(" ", "").Contains(strippedQuery) ||
                            l.Phone.Replace(" ", "").Contains(strippedQuery) ||
                            l.Contact1Phone.Replace(" ", "").Contains(strippedQuery))
                .Take(5)
                .Select(l => new GlobalSearchResult
                {
                    Id = l.Id.ToString(),
                    Title = l.LeadName,
                    Subtitle = (l.EmailID ?? l.MobileNumber ?? "Lead") + " | Owner: " + (_context.Users.Where(u => u.Id == l.LeadOwnerId).Select(u => u.FirstName).FirstOrDefault() ?? "Unassigned"),
                    Module = "Leads",
                    Icon = "bi-person-lines-fill",
                    Url = (isAdmin || visibleUserIds.Contains(l.LeadOwnerId)) ? $"/Leads/Edit/{l.Id}" : ""
                }).ToListAsync();
            results.AddRange(leads);

            // ==========================================
            // 🚀 5. SMART DEALS SEARCH
            // ==========================================
            var deals = await _context.Deals
                .Where(d => d.DealName.Replace(" ", "").Contains(strippedQuery) ||
                            d.AccountName.Replace(" ", "").Contains(strippedQuery) ||
                            d.LeadName.Replace(" ", "").Contains(strippedQuery))
                .Take(5)
               .Select(d => new GlobalSearchResult
               {
                   Id = d.Id.ToString(),
                   Title = d.DealName,
                   Subtitle = (d.AccountName ?? $"Value: {d.GrandTotal}") + " | Owner: " + (_context.Users.Where(u => u.Id == d.DealOwnerId).Select(u => u.FirstName).FirstOrDefault() ?? "Unassigned"),
                   Module = "Deals",
                   Icon = "bi-briefcase",
                   Url = (isAdmin || visibleUserIds.Contains(d.DealOwnerId)) ? $"/Deals/Edit/{d.Id}" : ""
               }).ToListAsync();
            results.AddRange(deals);

            // ==========================================
            // 🚀 6. SMART TASKS SEARCH
            // ==========================================
            var tasks = await _context.Tasks
                .Where(t => t.Subject.Replace(" ", "").Contains(strippedQuery) ||
                            t.Mobile.Replace(" ", "").Contains(strippedQuery))
                .Take(5)
               .Select(t => new GlobalSearchResult
               {
                   Id = t.Id.ToString(),
                   Title = t.Subject,
                   Subtitle = $"Status: {t.Status} | Owner: " + (_context.Users.Where(u => u.Id == t.TaskOwnerId).Select(u => u.FirstName).FirstOrDefault() ?? "Unassigned"),
                   Module = "Tasks",
                   Icon = "bi-check2-square",
                   Url = (isAdmin || visibleUserIds.Contains(t.TaskOwnerId)) ? $"/Tasks/Edit/{t.Id}" : ""
               }).ToListAsync();
            results.AddRange(tasks);

            // ==========================================
            // 🚀 7. SMART VENDORS SEARCH
            // ==========================================
            var vendors = await _context.Vendors
                .Where(v => v.VendorName.Replace(" ", "").Contains(strippedQuery) ||
                            v.Email.Replace(" ", "").Contains(strippedQuery) ||
                            v.Phone.Replace(" ", "").Contains(strippedQuery))
                .Take(5)
                .Select(v => new GlobalSearchResult
                {
                    Id = v.Id.ToString(),
                    Title = v.VendorName,
                    Subtitle = v.Email ?? v.Phone ?? "Vendor",
                    Module = "Vendors",
                    Icon = "bi-shop",
                    Url = (isAdmin || visibleUserIds.Contains(v.VendorOwnerId)) ? $"/Vendors/Edit/{v.Id}" : ""
                }).ToListAsync();
            results.AddRange(vendors);

            return Json(results);
        }

        // ==========================================
        // HELPER: ROLE HIERARCHY RESOLVER
        // ==========================================
        private List<int> GetSubordinateRoleIds(List<Role> allRoles, int? currentRoleId)
        {
            var subordinateIds = new List<int>();
            if (currentRoleId == null) return subordinateIds;

            var directChildren = allRoles.Where(r => r.ParentRoleId == currentRoleId).Select(r => r.Id).ToList();
            subordinateIds.AddRange(directChildren);

            foreach (var childId in directChildren)
            {
                subordinateIds.AddRange(GetSubordinateRoleIds(allRoles, childId));
            }

            return subordinateIds;
        }
    }

    // Required struct for the JSON return
    public class GlobalSearchResult
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Module { get; set; }
        public string Icon { get; set; }
        public string Url { get; set; }
    }
}