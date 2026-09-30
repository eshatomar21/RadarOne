using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Radar_CRM.Controllers
{
    public class DealsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DealsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // INDEX: Paginated & Sorted (100 per page)
        // ==========================================
        public async Task<IActionResult> Index(int page = 1, string search = "", string sortCol = "Id", string sortDir = "desc", string advancedFilters = "")
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Users");

            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            if (currentUser == null) return RedirectToAction("Login", "Users");

            int pageSize = 100;

            // 🚀 Added .Include() so the Account data is fetched from the database
            var query = _context.Deals
                .Include(d => d.Account)
                .AsQueryable();

            // 🚀 ADMIN CHECK BASED ON 'PROFILE'
            bool isAdmin = !string.IsNullOrWhiteSpace(currentUser.Profile) &&
                           (currentUser.Profile.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
                            currentUser.Profile.Equals("Administrator", StringComparison.OrdinalIgnoreCase));

            // 🚀 THE HIERARCHY LOGIC
            if (!isAdmin)
            {
                var allRoles = await _context.Roles.ToListAsync();
                var visibleRoleIds = new List<int>();

                var subordinateIds = GetSubordinateRoleIds(allRoles, currentUser.RoleId);
                visibleRoleIds.AddRange(subordinateIds);

                var currentUserRoleModel = allRoles.FirstOrDefault(r => r.Id == currentUser.RoleId);
                if (currentUserRoleModel != null && currentUserRoleModel.ShareDataWithPeers && currentUser.RoleId.HasValue)
                {
                    visibleRoleIds.Add(currentUser.RoleId.Value);
                }

                var visibleUserIds = await _context.Users
                    .Where(u => u.RoleId.HasValue && visibleRoleIds.Contains(u.RoleId.Value))
                    .Select(u => u.Id)
                    .ToListAsync();

                visibleUserIds.Add(currentUserId);
                query = query.Where(d => visibleUserIds.Contains(d.DealOwnerId));
            }

            // 🚀 BULLETPROOF FILTERING ENGINE (ENTIRE DB)
            if (!string.IsNullOrWhiteSpace(advancedFilters))
            {
                try
                {
                    string jsonString = advancedFilters;
                    if (jsonString.Contains("%5B") || jsonString.Contains("%7B"))
                    {
                        jsonString = Uri.UnescapeDataString(jsonString);
                    }

                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var filters = System.Text.Json.JsonSerializer.Deserialize<List<FilterCriteria>>(jsonString, options);

                    // 🚀 FIX: Allow these predefined conditions to run even if the Value input is physically empty
                    var noValueConditions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "is_empty", "is_not_empty", "today", "tomorrow", "yesterday",
                        "till yesterday", "starting tomorrow", "this week", "this month",
                        "this year", "previous week", "previous month", "previous year"
                    };

                    foreach (var f in filters)
                    {
                        string cond = f.Condition?.ToLower().Trim() ?? "";

                        // If it's empty AND doesn't match our allowed blank conditions, skip it
                        if (string.IsNullOrWhiteSpace(f.Value) && !noValueConditions.Contains(cond)) continue;

                        var propertyInfo = typeof(Deal).GetProperty(f.ColumnName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (propertyInfo == null) continue;

                        string dbColName = propertyInfo.Name;

                        if (propertyInfo.PropertyType.IsClass && propertyInfo.PropertyType != typeof(string))
                        {
                            var idProp = typeof(Deal).GetProperty(dbColName + "Id", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (idProp != null)
                            {
                                dbColName = idProp.Name;
                                propertyInfo = idProp;
                            }
                            else continue;
                        }

                        string safeValue = f.Value?.Replace("+", " ") ?? "";

                        // ==========================================
                        // 1. DATE FILTERS (Fixed for EF Core Translations)
                        // ==========================================
                        if (f.IsDate || propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(DateTime?))
                        {
                            DateTime today = DateTime.Today;

                            if (cond == "today")
                            {
                                var next = today.AddDays(1);
                                query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= today && EF.Property<DateTime?>(a, dbColName) < next);
                            }
                            else if (cond == "yesterday")
                            {
                                var yest = today.AddDays(-1);
                                query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= yest && EF.Property<DateTime?>(a, dbColName) < today);
                            }
                            else if (cond == "tomorrow")
                            {
                                var next = today.AddDays(1);
                                var dayAfter = today.AddDays(2);
                                query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= next && EF.Property<DateTime?>(a, dbColName) < dayAfter);
                            }
                            else if (cond == "till yesterday")
                            {
                                query = query.Where(a => EF.Property<DateTime?>(a, dbColName) < today);
                            }
                            else if (cond == "starting tomorrow")
                            {
                                var next = today.AddDays(1);
                                query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= next);
                            }
                            else if (cond == "this week")
                            {
                                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                                var endOfWeek = startOfWeek.AddDays(7);
                                query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= startOfWeek && EF.Property<DateTime?>(a, dbColName) < endOfWeek);
                            }
                            else if (cond == "this month")
                            {
                                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                                var endOfMonth = startOfMonth.AddMonths(1);
                                query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= startOfMonth && EF.Property<DateTime?>(a, dbColName) < endOfMonth);
                            }
                            else if (cond == "this year")
                            {
                                var startOfYear = new DateTime(today.Year, 1, 1);
                                var endOfYear = startOfYear.AddYears(1);
                                query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= startOfYear && EF.Property<DateTime?>(a, dbColName) < endOfYear);
                            }
                            else if (cond == "previous week")
                            {
                                var startOfPrevWeek = today.AddDays(-(int)today.DayOfWeek - 7);
                                var endOfPrevWeek = startOfPrevWeek.AddDays(7);
                                query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= startOfPrevWeek && EF.Property<DateTime?>(a, dbColName) < endOfPrevWeek);
                            }
                            else if (cond == "previous month")
                            {
                                var prevMonth = today.AddMonths(-1);
                                var startOfPrevMonth = new DateTime(prevMonth.Year, prevMonth.Month, 1);
                                var endOfPrevMonth = startOfPrevMonth.AddMonths(1);
                                query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= startOfPrevMonth && EF.Property<DateTime?>(a, dbColName) < endOfPrevMonth);
                            }
                            else if (cond == "previous year")
                            {
                                var prevYear = today.AddYears(-1);
                                var startOfPrevYear = new DateTime(prevYear.Year, 1, 1);
                                var endOfPrevYear = startOfPrevYear.AddYears(1);
                                query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= startOfPrevYear && EF.Property<DateTime?>(a, dbColName) < endOfPrevYear);
                            }
                            else
                            {
                                // Manual Date Inputs (Between, On, After, etc.)
                                DateTime d1 = DateTime.MinValue, d2 = DateTime.MaxValue;
                                bool isValidDate = false;

                                if (safeValue.Contains("|"))
                                {
                                    var dates = safeValue.Split('|');
                                    if (DateTime.TryParse(dates[0], out d1) && DateTime.TryParse(dates[1], out d2)) isValidDate = true;
                                }
                                else if (DateTime.TryParse(safeValue, out d1))
                                {
                                    isValidDate = true;
                                }

                                if (isValidDate)
                                {
                                    if (cond == "between") query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= d1 && EF.Property<DateTime?>(a, dbColName) <= d2);
                                    else if (cond == "not between") query = query.Where(a => EF.Property<DateTime?>(a, dbColName) < d1 || EF.Property<DateTime?>(a, dbColName) > d2);
                                    else if (cond == "on" || cond == "is") { var next = d1.AddDays(1); query = query.Where(a => EF.Property<DateTime?>(a, dbColName) >= d1 && EF.Property<DateTime?>(a, dbColName) < next); }
                                    else if (cond == "before") query = query.Where(a => EF.Property<DateTime?>(a, dbColName) < d1);
                                    else if (cond == "after") query = query.Where(a => EF.Property<DateTime?>(a, dbColName) > d1);
                                }
                            }
                        }
                        // ==========================================
                        // 2. TEXT & ID FILTERS
                        // ==========================================
                        else if (dbColName.Equals("AccountId", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(safeValue, out int accId))
                            {
                                if (f.Condition == "is" || f.Condition == "contains") query = query.Where(a => a.AccountId == accId);
                                else if (f.Condition == "is_not" || f.Condition == "does_not_contain") query = query.Where(a => a.AccountId != accId);
                            }
                            else
                            {
                                var searchValue = safeValue.ToLower().Trim();
                                var matchingAccIds = _context.Accounts.Where(acc => acc.AccountName != null && acc.AccountName.ToLower().Contains(searchValue)).Select(acc => acc.Id).ToList();
                                if (f.Condition == "contains" || f.Condition == "is") query = query.Where(a => a.AccountId.HasValue && matchingAccIds.Contains(a.AccountId.Value));
                                else if (f.Condition == "does_not_contain" || f.Condition == "is_not") query = query.Where(a => !a.AccountId.HasValue || !matchingAccIds.Contains(a.AccountId.Value));
                            }
                        }
                        else if (dbColName.EndsWith("OwnerId", StringComparison.OrdinalIgnoreCase))
                        {
                            var searchValue = safeValue.ToLower().Trim();
                            var matchingUserIds = _context.Users.Where(u => (u.fullName != null && u.fullName.ToLower().Contains(searchValue)) || (u.Id == searchValue)).Select(u => u.Id).ToList();

                            if (f.Condition == "contains" || f.Condition == "is") query = query.Where(a => matchingUserIds.Contains(EF.Property<string>(a, dbColName)));
                            else if (f.Condition == "does_not_contain" || f.Condition == "is_not") query = query.Where(a => !matchingUserIds.Contains(EF.Property<string>(a, dbColName)));
                            else if (f.Condition == "is_empty") query = query.Where(a => string.IsNullOrEmpty(EF.Property<string>(a, dbColName)));
                            else if (f.Condition == "is_not_empty") query = query.Where(a => !string.IsNullOrEmpty(EF.Property<string>(a, dbColName)));
                        }
                        else if (propertyInfo.PropertyType == typeof(string))
                        {
                            var searchValue = safeValue.ToLower().Trim();
                            if (f.Condition == "contains") query = query.Where(a => EF.Property<string>(a, dbColName) != null && EF.Property<string>(a, dbColName).ToLower().Contains(searchValue));
                            else if (f.Condition == "does_not_contain") query = query.Where(a => EF.Property<string>(a, dbColName) == null || !EF.Property<string>(a, dbColName).ToLower().Contains(searchValue));
                            else if (f.Condition == "starts_with") query = query.Where(a => EF.Property<string>(a, dbColName) != null && EF.Property<string>(a, dbColName).ToLower().StartsWith(searchValue));
                            else if (f.Condition == "ends_with") query = query.Where(a => EF.Property<string>(a, dbColName) != null && EF.Property<string>(a, dbColName).ToLower().EndsWith(searchValue));
                            else if (f.Condition == "is") query = query.Where(a => EF.Property<string>(a, dbColName) != null && EF.Property<string>(a, dbColName).ToLower() == searchValue);
                            else if (f.Condition == "is_not") query = query.Where(a => EF.Property<string>(a, dbColName) != searchValue);
                            else if (f.Condition == "is_empty") query = query.Where(a => string.IsNullOrEmpty(EF.Property<string>(a, dbColName)));
                            else if (f.Condition == "is_not_empty") query = query.Where(a => !string.IsNullOrEmpty(EF.Property<string>(a, dbColName)));
                        }
                        else if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(int?))
                        {
                            if (int.TryParse(safeValue, out int numVal))
                            {
                                if (f.Condition == "is" || f.Condition == "contains") query = query.Where(a => EF.Property<int?>(a, dbColName) == numVal);
                                else if (f.Condition == "is_not" || f.Condition == "does_not_contain") query = query.Where(a => EF.Property<int?>(a, dbColName) != numVal);
                            }
                        }
                        else if (propertyInfo.PropertyType == typeof(decimal) || propertyInfo.PropertyType == typeof(decimal?))
                        {
                            if (decimal.TryParse(safeValue, out decimal decVal))
                            {
                                if (f.Condition == "is" || f.Condition == "contains") query = query.Where(a => EF.Property<decimal?>(a, dbColName) == decVal);
                                else if (f.Condition == "is_not" || f.Condition == "does_not_contain") query = query.Where(a => EF.Property<decimal?>(a, dbColName) != decVal);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FILTER CRASH AVOIDED: " + ex.Message);
                }
            }

            // Server-Side Filtering
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(d =>
                    (d.DealName != null && d.DealName.Contains(search)) ||
                    (d.ContactPersonName != null && d.ContactPersonName.Contains(search))
                );
            }

            // Server-Side Sorting
            if (sortDir == "desc")
            {
                query = sortCol switch
                {
                    "DealName" => query.OrderByDescending(d => d.DealName),
                    "GrandTotal" => query.OrderByDescending(d => d.GrandTotal),
                    _ => query.OrderByDescending(d => d.Id)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "DealName" => query.OrderBy(d => d.DealName),
                    "GrandTotal" => query.OrderBy(d => d.GrandTotal),
                    _ => query.OrderBy(d => d.Id)
                };
            }

            // Execute Pagination on the Database safely
            var totalRecords = await query.CountAsync();
            if (page < 1) page = 1;
            int totalPagesCalc = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPagesCalc == 0) totalPagesCalc = 1;
            if (page > totalPagesCalc) page = totalPagesCalc;

            var deals = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPagesCalc;
            ViewBag.TotalRecords = totalRecords;

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");

            // 🚀 NEW: Safely get note counts only for the loaded deals
            var dealIds = deals.Select(d => d.Id).ToList();
            var noteCounts = await _context.Note
                .Where(n => n.DealId != null && dealIds.Contains(n.DealId.Value))
                .GroupBy(n => n.DealId.Value)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

            ViewBag.NoteCounts = noteCounts; // Send dictionary to the view

            return View(deals);
        }
        // ==========================================
        // HELPER METHOD (Add this to the bottom of the DealsController)
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

        // ==========================================
        // CREATE: GET
        // ==========================================
        public IActionResult Create()
        {
            // This line prevents the NullReferenceException
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            return View();
        }

        // ==========================================
        // CREATE: POST
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Deal deal, string[] SavedNoteOwner, string[] SavedNoteDateTime, string[] SavedNoteDesc)
        {
            ModelState.Clear(); // Clears strict implicit validation

            if (string.IsNullOrWhiteSpace(deal.DealName))
            {
                ModelState.AddModelError("DealName", "Deal Name is required.");
            }

            // 🚀 SET "CREATED BY" AUTOMATICALLY
            string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System User";
            deal.CreatedBy = $"{currentUser} on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";

            ModelState.Remove("CreatedBy");
            ModelState.Remove("ModifiedBy");

            if (ModelState.IsValid)
            {
                // 🚀 Cache valid user IDs to prevent Database Foreign Key crashes
                var validUserIds = _context.Users.Select(u => u.Id).ToHashSet();

                // 🚀 Use the unified Notes list instead of DealNote
                deal.Notes ??= new List<Notes>();

                if (SavedNoteDesc != null)
                {
                    for (int i = 0; i < SavedNoteDesc.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(SavedNoteDesc[i]))
                        {
                            string rawOwnerId = (SavedNoteOwner != null && SavedNoteOwner.Length > i) ? SavedNoteOwner[i]?.Trim() : null;

                            // 🚀 STRICT FK CHECK: Only assign if the ID actually exists in the Users table.
                            string safeOwnerId = (!string.IsNullOrWhiteSpace(rawOwnerId) && validUserIds.Contains(rawOwnerId))
                                                 ? rawOwnerId
                                                 : null;

                            deal.Notes.Add(new Notes
                            {
                                NoteOwnerId = safeOwnerId,
                                CreatedDateTime = DateTime.TryParse(SavedNoteDateTime[i], out DateTime parsedDate) ? parsedDate : DateTime.Now,
                                Description = SavedNoteDesc[i]
                            });
                        }
                    }
                }

                _context.Add(deal);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            return View(deal);
        }

        // ==========================================
        // EDIT: GET
        // ==========================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var deal = await _context.Deals
                .Include(d => d.PaymentRows)
                .Include(d => d.Notes)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (deal == null) return NotFound();
            // This line prevents the NullReferenceException on the Edit page
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            // 🚀 FETCH NOTES FOR THE DEAL EDIT PAGE
            // Fetch the notes
            // 🚀 FIX: Fetch actual 'Notes' models instead of Anonymous Types
            ViewBag.ExistingNotes = await _context.Note
                .Include(n => n.NoteOwner)
                .Where(n => n.DealId == id) // NOTE: Use n.LeadId == id if you are doing this in LeadsController
                .OrderByDescending(n => n.CreatedDateTime)
                .ToListAsync();
            return View(deal);
        }

        // ==========================================
        // UPLOAD FILE: Mapped for Deals_2026_09_10.csv
        // ==========================================
        [HttpPost]
        [DisableRequestSizeLimit]
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var dealsToInsert = new List<Deal>();
            var dealsToUpdate = new List<Deal>();
            var skippedRecords = new List<string>();
            int currentRow = 1;

            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);

            // UPSERT DICTIONARY: Load existing Deals by Zoho ID
            var existingDealsDb = await _context.Deals
                .Where(d => !string.IsNullOrEmpty(d.ZohoRecordId))
                .AsNoTracking()
                .GroupBy(d => d.ZohoRecordId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            // Lookups for linking
            var accountLookup = _context.Accounts.Where(a => a.ZohoRecordId != null).ToDictionary(a => a.ZohoRecordId, a => a.Id);
            var leadLookup = _context.Leads.Where(l => l.ZohoRecordId != null).ToDictionary(l => l.ZohoRecordId, l => l.Id);

            try
            {
                using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(headerLine)) return BadRequest("Empty CSV");

                    // 🚀 ENCODING FIX: Strips everything except Letters and Digits so "Unit Price(â‚¹)" becomes "unitprice"
                    var headers = ParseCsvLine(headerLine)
                        .Select(h => new string(h.Where(char.IsLetterOrDigit).ToArray()).ToLower())
                        .ToList();

                    string GetValSafe(string[] vals, string colName)
                    {
                        // Normalize the requested column name to match the stripped headers
                        var normalizedColName = new string(colName.Where(char.IsLetterOrDigit).ToArray()).ToLower();
                        var idx = headers.IndexOf(normalizedColName);
                        return idx >= 0 && idx < vals.Length ? vals[idx]?.Trim() ?? "" : "";
                    }

                    decimal? GetDecimalSafe(string[] vals, string colName)
                    {
                        string raw = GetValSafe(vals, colName);
                        if (string.IsNullOrWhiteSpace(raw)) return null;
                        // Strip currency symbols and commas from the actual data value
                        string clean = new string(raw.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
                        return decimal.TryParse(clean, out decimal result) ? result : null;
                    }

                    while (!reader.EndOfStream)
                    {
                        currentRow++;
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);
                        string zohoRecordId = GetValSafe(values, "RecordId");

                        if (string.IsNullOrEmpty(zohoRecordId))
                        {
                            skippedRecords.Add($"Row {currentRow}: Skipped (Missing Record ID)");
                            continue;
                        }

                        bool isUpdate = false;
                        Deal deal;

                        // UPSERT LOGIC
                        if (existingDealsDb.TryGetValue(zohoRecordId, out var existingDeal))
                        {
                            deal = existingDeal;
                            isUpdate = true;
                        }
                        else
                        {
                            deal = new Deal();
                        }

                        // Grab relational IDs from Zoho CSV
                        string rawAccountId = GetValSafe(values, "AccountNameid"); // Safe alphanumeric request
                        string rawLeadId = GetValSafe(values, "LeadNameid");
                        string rawDealOwnerId = GetValSafe(values, "DealsOwnerid");
                        string rawDemoOwnerId = GetValSafe(values, "DemoOwnerid");

                        // --- Core Identity & Mapping ---
                        deal.ZohoRecordId = zohoRecordId;
                        deal.DealName = GetValSafe(values, "DealName");
                        deal.AccountId = accountLookup.TryGetValue(rawAccountId, out int accId) ? accId : null;
                        deal.LeadId = leadLookup.TryGetValue(rawLeadId, out int lId) ? lId : null;
                        deal.DealOwnerId = validUserIds.Contains(rawDealOwnerId) ? rawDealOwnerId : null;
                        deal.DemoOwnerId = validUserIds.Contains(rawDemoOwnerId) ? rawDemoOwnerId : null;

                        // --- Text Names & Ownership ---
                        deal.AccountName = GetValSafe(values, "AccountName");
                        deal.LeadName = GetValSafe(values, "LeadName");
                        deal.ContactPersonName = GetValSafe(values, "ContactPersonName");
                        deal.MetaCampaignName = GetValSafe(values, "MetaCampaignName");
                        deal.AccountOwner = GetValSafe(values, "AccountOwner");
                        deal.DemoOwner = GetValSafe(values, "DemoOwner");
                        deal.CreatedBy = GetValSafe(values, "CreatedBy");

                        // --- Dates ---
                        deal.DateOfEntry = DateTime.TryParse(GetValSafe(values, "CreatedTime"), out DateTime doe) ? doe : DateTime.Now;
                        deal.ModifiedTime = DateTime.TryParse(GetValSafe(values, "ModifiedTime"), out DateTime mod) ? mod : null;

                        // --- Dropdowns & Info ---
                        deal.AccountType = GetValSafe(values, "AccountType");
                        deal.LeadSource = GetValSafe(values, "LeadSource");
                        deal.DealType = GetValSafe(values, "DealType");
                        deal.PaymentMode = GetValSafe(values, "paymentMode");
                        deal.PaymentType = GetValSafe(values, "PaymentType");
                        deal.PaymentStatus = GetValSafe(values, "PaymentStatus");
                        deal.Remarks = GetValSafe(values, "Remarks");
                        deal.ApprovalRequired = GetValSafe(values, "ApprovalRequired");
                        deal.ApprovedBy = GetValSafe(values, "ApprovedBy");

                        // --- Products ---
                        deal.ProductName = GetValSafe(values, "ProductName");
                        deal.AddonModuleName = GetValSafe(values, "AddonModuleName");
                        deal.CurrentPackage = GetValSafe(values, "CurrentPackage");
                        deal.Quantity = GetDecimalSafe(values, "Quantity");
                        deal.ProductCode = GetValSafe(values, "ProductCode");
                        deal.InvoiceNumber = GetValSafe(values, "InvoiceNumber");

                        // 🚀 --- Financials (Using clean alphanumeric requests) ---
                        deal.UnitPrice = GetDecimalSafe(values, "UnitPrice");
                        deal.Discount = GetDecimalSafe(values, "Discount");
                        deal.SubTotal = GetDecimalSafe(values, "SubTotal");
                        deal.Taxes = GetDecimalSafe(values, "Taxs") ?? 0m;
                        deal.Adjustment = GetDecimalSafe(values, "Adjustment") ?? 0m;
                        deal.GrandTotal = GetDecimalSafe(values, "GrandTotal") ?? 0m;
                        deal.GstAmount = GetDecimalSafe(values, "GSTAmount");
                        deal.FinalAmount = GetDecimalSafe(values, "FinalAmount");
                        deal.TotalWithGst = GetDecimalSafe(values, "TotalwithGST");

                        // Route to correct list
                        if (isUpdate) dealsToUpdate.Add(deal);
                        else dealsToInsert.Add(deal);
                    }
                }

                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                // Execute Inserts & Updates
                if (dealsToInsert.Any()) await _context.Deals.AddRangeAsync(dealsToInsert);
                if (dealsToUpdate.Any()) _context.Deals.UpdateRange(dealsToUpdate);

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed at Row {currentRow} -> {ex.Message}");
            }
            finally { _context.ChangeTracker.AutoDetectChangesEnabled = true; }

            return Json(new
            {
                success = true,
                insertedCount = dealsToInsert.Count,
                updatedCount = dealsToUpdate.Count,
                skippedCount = skippedRecords.Count
            });
        }


        // ==========================================
        // 🚀 AJAX: BULK DELETE (Fixed FK Constraint)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> BulkDelete([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
            {
                return Json(new { success = false, message = "No records selected." });
            }

            try
            {
                // 1. Safely remove related Notes first to satisfy the FK constraint
                var relatedNotes = await _context.Note
                    .Where(n => n.DealId != null && ids.Contains((int)n.DealId))
                    .ToListAsync();

                if (relatedNotes.Any())
                {
                    _context.Note.RemoveRange(relatedNotes);
                }

                // (Optional but recommended) If you also have Payment Rows attached, delete them here:
                var relatedPayments = await _context.DealPaymentRows
                    .Where(p => ids.Contains(p.DealId))
                    .ToListAsync();

                if (relatedPayments.Any())
                {
                    _context.DealPaymentRows.RemoveRange(relatedPayments);
                }

                // 2. Now delete the actual Deals
                var dealsToDelete = await _context.Deals
                    .Where(d => ids.Contains(d.Id))
                    .ToListAsync();

                if (dealsToDelete.Any())
                {
                    _context.Deals.RemoveRange(dealsToDelete);
                }

                // Commit all deletions together in one transaction
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.InnerException?.Message ?? ex.Message });
            }
        }

        // ==========================================
        // 🚀 AJAX: MASS UPDATE
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> BulkUpdate([FromBody] MassUpdateRequest request)
        {
            if (request == null || request.Ids == null || !request.Ids.Any() || string.IsNullOrWhiteSpace(request.FieldName))
            {
                return Json(new { success = false, message = "Invalid request or no records selected." });
            }

            try
            {
                // 1. Fetch all deals that match the selected IDs
                var dealsToUpdate = await _context.Deals
                    .Where(d => request.Ids.Contains(d.Id))
                    .ToListAsync();

                if (!dealsToUpdate.Any())
                {
                    return Json(new { success = false, message = "No matching records found." });
                }

                // 2. Use Reflection to find the exact property the user wants to update
                var propertyInfo = typeof(Deal).GetProperty(request.FieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                if (propertyInfo == null)
                {
                    return Json(new { success = false, message = $"Field '{request.FieldName}' does not exist on the Deal model." });
                }

                // 3. Determine target type (handle nullable types gracefully)
                Type targetType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
                object targetValue = null;

                // 4. Safely parse the incoming string value to the actual C# property type
                if (!string.IsNullOrWhiteSpace(request.NewValue))
                {
                    if (targetType == typeof(int))
                    {
                        if (int.TryParse(request.NewValue, out int intVal)) targetValue = intVal;
                    }
                    else if (targetType == typeof(decimal))
                    {
                        if (decimal.TryParse(request.NewValue, out decimal decVal)) targetValue = decVal;
                    }
                    else if (targetType == typeof(DateTime))
                    {
                        if (DateTime.TryParse(request.NewValue, out DateTime dtVal)) targetValue = dtVal;
                    }
                    else if (targetType == typeof(bool))
                    {
                        if (bool.TryParse(request.NewValue, out bool boolVal)) targetValue = boolVal;
                    }
                    else
                    {
                        // Fallback for strings and standard types
                        targetValue = Convert.ChangeType(request.NewValue, targetType);
                    }
                }

                // 5. Apply the value to all selected records
                foreach (var deal in dealsToUpdate)
                {
                    propertyInfo.SetValue(deal, targetValue);
                    _context.Entry(deal).State = EntityState.Modified;
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Capture inner exception for detailed DB constraint errors
                string errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = errorMsg });
            }
        }

        // ==========================================
        // HELPER METHODS FOR CSV PARSING
        // ==========================================
        private string GetVal(string[] values, int index)
        {
            if (index < values.Length) return values[index]?.Trim() ?? "";
            return "";
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var currentField = new System.Text.StringBuilder();

            foreach (char c in line)
            {
                if (c == '\"') inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else currentField.Append(c);
            }
            result.Add(currentField.ToString());
            return result.ToArray();
        }

        [HttpGet]
        public async Task<IActionResult> GetNotes(int dealId)
        {
            var notes = await _context.Note
                .Where(n => n.DealId == dealId)
                .OrderByDescending(n => n.CreatedDateTime)
                .Select(n => new {
                    ownerName = n.NoteOwner != null ? n.NoteOwner.fullName : "System",
                    createdDateTime = n.CreatedDateTime.ToString("MMM dd, yyyy h:mm tt"),
                    noteTitle = n.NoteTitle, // Required for UI
                    description = n.Description,
                    attachmentFileName = n.AttachmentFileName
                }).ToListAsync();

            return Json(notes);
        }

        [HttpPost]
        public async Task<IActionResult> SaveNoteAjax(int dealId, string noteTitle, string description, string ownerId, IFormFile attachment)
        {
            try
            {
                var note = new Notes
                {
                    DealId = dealId, // Mapped to Deal
                    NoteOwnerId = string.IsNullOrWhiteSpace(ownerId) ? null : ownerId,
                    NoteTitle = noteTitle ?? "",
                    NoteContent = description ?? "",
                    Description = description ?? "",
                    CreatedDateTime = DateTime.Now
                };

                if (attachment != null && attachment.Length > 0)
                {
                    string uploadPath = @"C:\CRM_Files\Notes";
                    if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                    string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(attachment.FileName);
                    string filePath = Path.Combine(uploadPath, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await attachment.CopyToAsync(stream);
                    }

                    note.AttachmentFileName = attachment.FileName;
                    note.AttachmentFilePath = filePath;
                }

                _context.Note.Add(note);
                await _context.SaveChangesAsync();

                string ownerName = "System User";
                if (!string.IsNullOrEmpty(note.NoteOwnerId))
                {
                    var user = await _context.Users.FindAsync(note.NoteOwnerId);
                    if (user != null) ownerName = user.fullName ?? user.FirstName;
                }

                return Json(new
                {
                    success = true,
                    note = new
                    {
                        ownerName = ownerName,
                        createdDateTime = note.CreatedDateTime.ToString("MMM dd, yyyy h:mm tt"),
                        noteTitle = note.NoteTitle,
                        description = note.Description,
                        attachmentFileName = note.AttachmentFileName
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult DownloadNoteAttachment(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return BadRequest("Filename missing.");

            string folderPath = @"C:\CRM_Files\Notes";
            string filePath = Path.Combine(folderPath, fileName);

            // Auto-search for matching GUID prefixes
            if (!System.IO.File.Exists(filePath))
            {
                var matchingFiles = Directory.GetFiles(folderPath, "*_" + fileName);
                if (matchingFiles.Length > 0)
                {
                    filePath = matchingFiles[0];
                }
                else
                {
                    return NotFound("File not found on server.");
                }
            }

            byte[] fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/octet-stream", fileName);
        }

        // ==========================================
        // EDIT: POST
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Deal deal, string[] SavedNoteOwner, string[] SavedNoteDateTime, string[] SavedNoteDesc)
        {
            if (id != deal.Id) return NotFound();

            ModelState.Clear(); // Clears strict implicit validation

            if (string.IsNullOrWhiteSpace(deal.DealName))
            {
                ModelState.AddModelError("DealName", "Deal Name is required.");
            }

            // 🚀 SET "MODIFIED BY" ONLY WHEN SAVE IS CLICKED
            string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System User";
            deal.ModifiedBy = $"{currentUser} on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";

            // Fetch original record to ensure CreatedBy isn't accidentally erased during save
            var existingDeal = await _context.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deal.Id);
            if (existingDeal != null && string.IsNullOrEmpty(deal.CreatedBy))
            {
                deal.CreatedBy = existingDeal.CreatedBy;
            }

            ModelState.Remove("CreatedBy");
            ModelState.Remove("ModifiedBy");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(deal);

                    if (deal.PaymentRows != null)
                    {
                        foreach (var row in deal.PaymentRows)
                        {
                            if (row.Id == 0) _context.DealPaymentRows.Add(row);
                            else _context.Update(row);
                        }
                    }

                    // =========================================================
                    // 🚀 NEW: SYNC FINANCIALS & PRODUCTS BACK TO THE LINKED LEAD
                    // =========================================================
                    if (deal.LeadId != null && deal.LeadId > 0)
                    {
                        var linkedLead = await _context.Leads
                            .Include(l => l.PaymentRow) // Load the lead's payment rows
                            .FirstOrDefaultAsync(l => l.Id == deal.LeadId);

                        if (linkedLead != null)
                        {
                            // 1. Sync scalar financial and payment fields
                            linkedLead.PaymentMode = deal.PaymentMode;
                            linkedLead.PaymentType = deal.PaymentType;
                            linkedLead.PaymentStatus = deal.PaymentStatus;
                            linkedLead.SubTotal = deal.SubTotal;
                            linkedLead.Taxes = deal.Taxes;
                            linkedLead.Adjustment = deal.Adjustment;
                            linkedLead.GrandTotal = deal.GrandTotal;

                            // 2. Sync the Product Payment Rows
                            if (deal.PaymentRows != null)
                            {
                                // Wipe out existing rows for this lead to perfectly mirror the Deal state
                                if (linkedLead.PaymentRow != null && linkedLead.PaymentRow.Any())
                                {
                                    _context.RemoveRange(linkedLead.PaymentRow);
                                }

                                // Create fresh mirrored rows for the Lead
                                foreach (var dealRow in deal.PaymentRows)
                                {
                                    _context.Add(new ProductPaymentRow
                                    {
                                        LeadId = linkedLead.Id,
                                        AccountId = linkedLead.AccountId, // Keep the account link intact
                                        ProductId = dealRow.ProductId,
                                        ProductName = dealRow.ProductName,
                                        DealType = dealRow.DealType,

                                        // 🚀 FIX: Explicitly cast the decimal? to int?
                                        Quantity = (int?)dealRow.Quantity,

                                        ProductPrice = dealRow.UnitPrice,
                                        Total = dealRow.Amount,
                                        DiscountPercent = dealRow.Discount,
                                        FinalAmount = dealRow.Total
                                    });
                                }
                            }

                            // Flag the lead as updated
                            _context.Update(linkedLead);
                        }
                    }

                    // 🚀 Cache valid user IDs to prevent Database Foreign Key crashes
                    var validUserIds = _context.Users.Select(u => u.Id).ToHashSet();

                    if (SavedNoteDesc != null)
                    {
                        for (int i = 0; i < SavedNoteDesc.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(SavedNoteDesc[i]))
                            {
                                string rawOwnerId = (SavedNoteOwner != null && SavedNoteOwner.Length > i) ? SavedNoteOwner[i]?.Trim() : null;

                                // 🚀 STRICT FK CHECK: Only assign if the ID actually exists in the Users table.
                                string safeOwnerId = (!string.IsNullOrWhiteSpace(rawOwnerId) && validUserIds.Contains(rawOwnerId))
                                                     ? rawOwnerId
                                                     : null;

                                // 🚀 FIXED: Save to the unified _context.Note instead of _context.DealNotes
                                _context.Note.Add(new Notes
                                {
                                    DealId = deal.Id,
                                    NoteOwnerId = safeOwnerId, // FIXED: Maps to NoteOwnerId
                                    CreatedDateTime = DateTime.TryParse(SavedNoteDateTime[i], out DateTime parsedDate) ? parsedDate : DateTime.Now, // FIXED: Maps to CreatedDateTime
                                    Description = SavedNoteDesc[i]
                                });
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Deals.Any(e => e.Id == deal.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            return View(deal);
        }
    }

    public class FilterCriteria
    {
        public string LogicalOperator { get; set; } // Add this property!
        public string ColumnName { get; set; }
        public string Condition { get; set; }
        public string Value { get; set; }
        public bool IsDate { get; set; }
    }
}

public class MassUpdateRequest
{
    public List<int> Ids { get; set; }
    public string FieldName { get; set; }
    public string NewValue { get; set; }
}
