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

                    foreach (var f in filters)
                    {
                        if (string.IsNullOrWhiteSpace(f.Value) && f.Condition != "is_empty" && f.Condition != "is_not_empty") continue;

                        var propertyInfo = typeof(Deal).GetProperty(f.ColumnName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (propertyInfo == null) continue;

                        string dbColName = propertyInfo.Name;

                        // Switch complex navigation properties to their Foreign Key (Id)
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

                        // 🚀 FIX: Translate '+' back into ' ' to fix URL encoding breaks (e.g. "Jaspal+Rawat" -> "Jaspal Rawat")
                        string safeValue = f.Value?.Replace("+", " ") ?? "";

                        if (f.IsDate || propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(DateTime?))
                        {
                            if (DateTime.TryParse(safeValue, out DateTime dVal))
                            {
                                if (f.Condition == "on") query = query.Where(a => EF.Property<DateTime?>(a, dbColName) != null && EF.Property<DateTime?>(a, dbColName).Value.Date == dVal.Date);
                                else if (f.Condition == "before") query = query.Where(a => EF.Property<DateTime?>(a, dbColName) != null && EF.Property<DateTime?>(a, dbColName).Value.Date < dVal.Date);
                                else if (f.Condition == "after") query = query.Where(a => EF.Property<DateTime?>(a, dbColName) != null && EF.Property<DateTime?>(a, dbColName).Value.Date > dVal.Date);
                            }
                        }
                        // 🚀 CRITICAL FIX: Only run User ID mappings on actual ID columns!
                        else if (dbColName.EndsWith("OwnerId", StringComparison.OrdinalIgnoreCase))
                        {
                            var searchValue = safeValue.ToLower().Trim();
                            var matchingUserIds = _context.Users
                                .Where(u => (u.fullName != null && u.fullName.ToLower().Contains(searchValue)) ||
                                            (u.FirstName != null && u.FirstName.ToLower().Contains(searchValue)))
                                .Select(u => u.Id)
                                .ToList();

                            if (f.Condition == "contains" || f.Condition == "is")
                                query = query.Where(a => matchingUserIds.Contains(EF.Property<string>(a, dbColName)));
                            else if (f.Condition == "does_not_contain" || f.Condition == "is_not")
                                query = query.Where(a => !matchingUserIds.Contains(EF.Property<string>(a, dbColName)));
                            else if (f.Condition == "is_empty")
                                query = query.Where(a => string.IsNullOrEmpty(EF.Property<string>(a, dbColName)));
                            else if (f.Condition == "is_not_empty")
                                query = query.Where(a => !string.IsNullOrEmpty(EF.Property<string>(a, dbColName)));
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
                        else
                        {
                            if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(int?))
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
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var dealsToInsert = new List<Deal>();
            int currentRow = 1;

            // Cache valid IDs to prevent DB hanging
            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);
            var validAccountIds = new HashSet<int>(_context.Accounts.Select(a => a.Id));

            try
            {
                using (var reader = new System.IO.StreamReader(uploadedFile.OpenReadStream()))
                {
                    // 🚀 SMART FIX: Dynamically read headers so it never breaks when Zoho changes column order!
                    var headerLine = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(headerLine)) return BadRequest("Empty CSV");

                    // Create a lowercase lookup list of the headers
                    var headers = ParseCsvLine(headerLine).Select(h => h.Trim().ToLower()).ToList();

                    // Inner helper function to safely grab value by exact column name
                    string GetValSafe(string[] vals, string colName)
                    {
                        var normalizedTarget = colName.Replace(" ", "").ToLower();
                        var idx = headers.FindIndex(h => h.Replace(" ", "").ToLower() == normalizedTarget);
                        return idx >= 0 && idx < vals.Length ? vals[idx]?.Trim() ?? "" : "";
                    }

                    while (!reader.EndOfStream)
                    {
                        currentRow++;
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);

                        string rawDealOwnerId = GetValSafe(values, "DealsOwner.id");
                        int? parsedAccountId = int.TryParse(GetValSafe(values, "AccountName.id"), out int accId) ? accId : null;

                        var newDeal = new Deal
                        {
                            ZohoRecordId = GetValSafe(values, "RecordId"),

                            // --- Core Identifiers ---
                            DealName = GetValSafe(values, "DealName"),

                            // 🚀 This will now safely capture the text name regardless of spaces in the header
                            AccountName = GetValSafe(values, "AccountName"),

                            // --- Relational IDs ---
                            DealOwnerId = validUserIds.Contains(rawDealOwnerId) ? rawDealOwnerId : null,
                            AccountId = parsedAccountId.HasValue && validAccountIds.Contains(parsedAccountId.Value) ? parsedAccountId.Value : null,

                            // 🚀 Grabs the text names for the Owners
                            DemoOwner = GetValSafe(values, "DemoOwner"),
                            AccountOwner = GetValSafe(values, "AccountOwner"),

                            // --- Profile & Text Info ---
                            LeadSource = GetValSafe(values, "LeadSource"),
                            AccountType = GetValSafe(values, "AccountType"),
                            LeadName = GetValSafe(values, "LeadName"),
                            ContactPersonName = GetValSafe(values, "ContactPersonName"),
                            MetaCampaignName = GetValSafe(values, "MetaCampaignName"),

                            // --- Status & Types ---
                            PaymentStatus = GetValSafe(values, "PaymentStatus"),
                            PaymentType = GetValSafe(values, "PaymentType"),
                            PaymentMode = GetValSafe(values, "paymentMode"),

                            // --- Additional Info ---
                            Remarks = GetValSafe(values, "Remarks"),
                            ApprovedBy = GetValSafe(values, "ApprovedBy"),
                            ApprovalRequired = GetValSafe(values, "ApprovalRequired?"),

                            // --- Financials ---
                            SubTotal = decimal.TryParse(GetValSafe(values, "SubTotal"), out decimal subTotal) ? subTotal : 0m,
                            Taxes = decimal.TryParse(GetValSafe(values, "Taxs"), out decimal taxes) ? taxes : 0m,
                            Adjustment = decimal.TryParse(GetValSafe(values, "Adjustment"), out decimal adj) ? adj : 0m,
                            GrandTotal = decimal.TryParse(GetValSafe(values, "GrandTotal"), out decimal grandTot) ? grandTot : 0m
                        };

                        dealsToInsert.Add(newDeal);
                    }
                }

                // 🚀 Turn off tracking during bulk insert to stop EF Core from freezing
                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                await _context.Deals.AddRangeAsync(dealsToInsert);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Failed at Row {currentRow} -> {trueError}");
            }
            finally
            {
                _context.ChangeTracker.AutoDetectChangesEnabled = true;
            }

            return Ok();
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

        // ==========================================
        // 🚀 AJAX: GET NOTES FOR DEAL SIDE PANEL
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetNotes(int dealId)
        {
            try
            {
                // 🚀 FIXED: Query _context.Note instead of _context.DealNotes
                var rawNotes = await _context.Note
                    .Include(n => n.NoteOwner) // Join the user to get the name safely
                    .Where(n => n.DealId == dealId)
                    .OrderByDescending(n => n.CreatedDateTime)
                    .ToListAsync();

                var notes = rawNotes.Select(n => new
                {
                    id = n.Id,
                    // Assuming your User table has a fullName or FirstName property 
                    ownerName = n.NoteOwner != null ? n.NoteOwner.fullName : "System",
                    createdDateTime = n.CreatedDateTime.ToString("dd-MM-yyyy HH:mm"),
                    description = n.Description,
                    attachmentFileName = ""
                });

                return Json(notes);
            }
            catch (Exception)
            {
                return Json(new List<object>());
            }
        }
        // ==========================================
        // 🚀 AJAX: SAVE NEW NOTE FROM SIDE PANEL
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> SaveNoteAjax(int dealId, string description, string ownerId)
        {
            try
            {
                // 🚀 STRICT FK CHECK: Confirm the ownerId is an actual User ID in the database
                string safeOwnerId = null;
                if (!string.IsNullOrWhiteSpace(ownerId))
                {
                    if (await _context.Users.AnyAsync(u => u.Id == ownerId))
                    {
                        safeOwnerId = ownerId;
                    }
                }

                // 🚀 FIXED: Use 'Notes' entity instead of 'DealNote'
                var newNote = new Notes
                {
                    DealId = dealId,
                    Description = description,
                    NoteOwnerId = safeOwnerId,
                    CreatedDateTime = DateTime.Now
                };

                _context.Note.Add(newNote); // FIXED: Save to _context.Note
                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    note = new
                    {
                        ownerName = safeOwnerId != null ? safeOwnerId : "System",
                        createdDateTime = newNote.CreatedDateTime.ToString("dd-MM-yyyy HH:mm"),
                        description = newNote.Description,
                        attachmentFileName = ""
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
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