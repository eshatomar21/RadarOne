using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Migrations;
using Radar_CRM.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static Radar_CRM.Controllers.AccountsController;

namespace Radar_CRM.Controllers
{
    public class LeadsController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Inject the DbContext
        public LeadsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: Leads/Index (Paginated, Sorted, Filtered)
        // ==========================================
        public async Task<IActionResult> Index(int page = 1, string search = "", string sortCol = "Id", string sortDir = "desc", string advancedFilters = "")
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Users");

            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            if (currentUser == null) return RedirectToAction("Login", "Users");

            int pageSize = 100;

            // 🚀 Eager-load relational User objects for Leads
            var query = _context.Leads
                .Include(l => l.LeadOwner)
                .Include(l => l.CoOwner)
                .Include(l => l.AccountOwner)
                .Include(l => l.DemoOwner)
                .AsQueryable();

            // 🚀 ADMIN CHECK BASED ON 'PROFILE'
            bool isAdmin = !string.IsNullOrWhiteSpace(currentUser.Profile) &&
                           (currentUser.Profile.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
                            currentUser.Profile.Equals("Administrator", StringComparison.OrdinalIgnoreCase));

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
                query = query.Where(l => visibleUserIds.Contains(l.LeadOwnerId));
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

                        var propertyInfo = typeof(Lead).GetProperty(f.ColumnName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (propertyInfo == null) continue;

                        string dbColName = propertyInfo.Name;

                        // Switch complex navigation properties to their Foreign Key (Id)
                        if (propertyInfo.PropertyType.IsClass && propertyInfo.PropertyType != typeof(string))
                        {
                            var idProp = typeof(Lead).GetProperty(dbColName + "Id", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (idProp != null)
                            {
                                dbColName = idProp.Name;
                                propertyInfo = idProp;
                            }
                            else continue;
                        }

                        if (f.IsDate || propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(DateTime?))
                        {
                            if (DateTime.TryParse(f.Value, out DateTime dVal))
                            {
                                if (f.Condition == "on") query = query.Where(a => EF.Property<DateTime?>(a, dbColName) != null && EF.Property<DateTime?>(a, dbColName).Value.Date == dVal.Date);
                                else if (f.Condition == "before") query = query.Where(a => EF.Property<DateTime?>(a, dbColName) != null && EF.Property<DateTime?>(a, dbColName).Value.Date < dVal.Date);
                                else if (f.Condition == "after") query = query.Where(a => EF.Property<DateTime?>(a, dbColName) != null && EF.Property<DateTime?>(a, dbColName).Value.Date > dVal.Date);
                            }
                        }
                        else if (dbColName.Contains("Owner", StringComparison.OrdinalIgnoreCase))
                        {
                            var searchValue = f.Value?.ToLower().Trim() ?? "";
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
                            var searchValue = f.Value.ToLower().Trim();
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
                                if (int.TryParse(f.Value, out int numVal))
                                {
                                    if (f.Condition == "is" || f.Condition == "contains") query = query.Where(a => EF.Property<int?>(a, dbColName) == numVal);
                                    else if (f.Condition == "is_not" || f.Condition == "does_not_contain") query = query.Where(a => EF.Property<int?>(a, dbColName) != numVal);
                                }
                            }
                            else if (propertyInfo.PropertyType == typeof(decimal) || propertyInfo.PropertyType == typeof(decimal?))
                            {
                                if (decimal.TryParse(f.Value, out decimal decVal))
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

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(l =>
                    (l.LeadName != null && l.LeadName.ToLower().Contains(searchLower)) ||
                    (l.MobileNumber != null && l.MobileNumber.Contains(search)) ||
                    (l.EmailID != null && l.EmailID.ToLower().Contains(searchLower))
                );
            }

            if (sortDir == "desc")
            {
                query = sortCol switch
                {
                    "LeadName" => query.OrderByDescending(l => l.LeadName),
                    "MobileNumber" => query.OrderByDescending(l => l.MobileNumber),
                    "CurrentStatus" => query.OrderByDescending(l => l.CurrentStatus),
                    _ => query.OrderByDescending(l => l.Id)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "LeadName" => query.OrderBy(l => l.LeadName),
                    "MobileNumber" => query.OrderBy(l => l.MobileNumber),
                    "CurrentStatus" => query.OrderBy(l => l.CurrentStatus),
                    _ => query.OrderBy(l => l.Id)
                };
            }

            // Safely calculate total records
            var totalRecords = await query.CountAsync();

            if (page < 1) page = 1;
            int totalPagesCalc = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPagesCalc == 0) totalPagesCalc = 1;
            if (page > totalPagesCalc) page = totalPagesCalc;

            var leads = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPagesCalc;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");

            return View(leads);
        }

        // ==========================================
        // BULK UPDATE FOR LEADS: AJAX POST
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken] // Prevents 405/400 Anti-forgery mismatch with fetch()
        public async Task<IActionResult> BulkUpdate([FromBody] BulkUpdateRequest request)
        {
            if (request == null || request.Ids == null || !request.Ids.Any() || string.IsNullOrEmpty(request.FieldName))
            {
                return Json(new { success = false, message = "Invalid selection or field." });
            }

            try
            {
                var leadsToUpdate = await _context.Leads.Where(l => request.Ids.Contains(l.Id)).ToListAsync();

                // Locate the property dynamically via reflection
                var propertyInfo = typeof(Lead).GetProperty(request.FieldName,
                    System.Reflection.BindingFlags.IgnoreCase |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance);

                if (propertyInfo == null)
                    return Json(new { success = false, message = $"Field '{request.FieldName}' not found on Lead entity." });

                foreach (var lead in leadsToUpdate)
                {
                    object safeValue = null;
                    Type targetType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

                    if (!string.IsNullOrWhiteSpace(request.NewValue))
                    {
                        if (targetType == typeof(DateTime))
                        {
                            if (DateTime.TryParse(request.NewValue, out DateTime parsedDate)) safeValue = parsedDate;
                        }
                        else if (targetType == typeof(DateOnly))
                        {
                            if (DateOnly.TryParse(request.NewValue, out DateOnly parsedDateOnly)) safeValue = parsedDateOnly;
                        }
                        else if (targetType == typeof(bool))
                        {
                            if (bool.TryParse(request.NewValue, out bool parsedBool)) safeValue = parsedBool;
                        }
                        else
                        {
                            safeValue = Convert.ChangeType(request.NewValue, targetType);
                        }
                    }

                    propertyInfo.SetValue(lead, safeValue, null);
                }

                _context.UpdateRange(leadsToUpdate);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                string message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = message });
            }
        }

        public class BulkUpdateRequest
        {
            public List<int> Ids { get; set; }
            public string FieldName { get; set; }
            public string NewValue { get; set; }
        }

        // ==========================================
        // HELPER METHOD (Add this to the bottom of the LeadsController)
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

        // GET: Leads/Create
        public IActionResult Create()
        {
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");

            // 🚀 Binds to ProductId and passes dictionary for Javascript pricing
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            return View(new Lead());
        }

        // ==========================================
        // POST: Leads/Create
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lead lead)
        {
            var paymentKeys = ModelState.Keys.Where(k => k.StartsWith("PaymentRow")).ToList();
            foreach (var key in paymentKeys)
            {
                ModelState.Remove(key);
            }
            ModelState.Remove("PaymentRow");

            if (ModelState.IsValid)
            {
                // =========================================================
                // AUTOMATED PIPELINE ASSIGNMENT
                // =========================================================
                if (lead.CurrentStatus == "User")
                {
                    lead.Pipeline = "Upgrade Software";
                }
                else if (lead.CurrentStatus == "Non-User")
                {
                    lead.Pipeline = "New Software";
                }

                // 🚀 FIX: Prevent EF from Double-Inserting Products
                var rowsToSave = new List<ProductPaymentRow>();
                if (lead.PaymentRow != null)
                {
                    rowsToSave = lead.PaymentRow
                        .Where(r => (r.ProductId.HasValue && r.ProductId.Value > 0) || !string.IsNullOrWhiteSpace(r.ProductName))
                        .ToList();
                }
                // Detach the list so Entity Framework doesn't save them automatically (we will do it manually to prevent duplicates)
                lead.PaymentRow = null;

                // 1. Save new lead to DB 
                _context.Add(lead);
                await _context.SaveChangesAsync(); // Generates the new Lead.Id

                // 2. Manually link and save the products
                if (rowsToSave.Any())
                {
                    foreach (var row in rowsToSave)
                    {
                        row.Id = 0; // Force it to be a new record
                        row.LeadId = lead.Id;
                        _context.Add(row);
                    }
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            // Fallback if model state fails
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            return View(lead);
        }

        // ==========================================
        // AJAX: GET NOTES FOR SIDE PANEL
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetNotes(int leadId)
        {
            try
            {
                // 1. Fetch the raw data from the database first
                var rawNotes = await _context.Note
                    .Include(n => n.NoteOwner)
                    .Where(n => n.LeadId == leadId)
                    .OrderByDescending(n => n.CreatedDateTime)
                    .ToListAsync(); // <-- Call this BEFORE .Select()

                // 2. Format the dates in memory to avoid EF Core SQL translation errors
                var notes = rawNotes.Select(n => new
                {
                    id = n.Id,
                    ownerName = n.NoteOwner != null ? n.NoteOwner.FirstName : "System",
                    // Since it's a non-nullable DateTime, we can just call .ToString() directly
                    createdDateTime = n.CreatedDateTime.ToString("dd-MM-yyyy HH:mm"),
                    description = n.Description,
                    attachmentFileName = n.AttachmentFileName
                });

                return Json(notes);
            }
            catch (Exception ex)
            {
                // Return an empty array instead of crashing if something goes wrong
                return Json(new List<object>());
            }
        }

        // ==========================================
        // AJAX: SAVE NEW NOTE FROM SIDE PANEL
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> SaveNoteAjax(int leadId, string description, string ownerId, IFormFile attachment)
        {
            try
            {
                string fileName = null;

                // Handle basic file attachment info if provided
                if (attachment != null && attachment.Length > 0)
                {
                    fileName = attachment.FileName;
                    // Note: Add your actual file system saving logic here if you want to store the physical file.
                    // Example: var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads", fileName);
                    // using (var stream = new FileStream(filePath, FileMode.Create)) { await attachment.CopyToAsync(stream); }
                }

                // Create the new Note object (Make sure your model is actually called 'Notes' or 'Note' as defined in your DB Context)
                var newNote = new Notes
                {
                    LeadId = leadId,
                    Description = description,
                    NoteOwnerId = string.IsNullOrWhiteSpace(ownerId) ? null : ownerId,
                    CreatedDateTime = DateTime.Now,
                    AttachmentFileName = fileName
                };

                _context.Note.Add(newNote);
                await _context.SaveChangesAsync();

                // Fetch the owner's name so we can return it to the UI instantly
                var owner = await _context.Users.FindAsync(ownerId);
                string ownerName = owner != null ? owner.FirstName : "System";

                // Return exactly what the JavaScript is expecting
                return Json(new
                {
                    success = true,
                    note = new
                    {
                        ownerName = ownerName,
                        createdDateTime = newNote.CreatedDateTime.ToString("dd-MM-yyyy HH:mm"),
                        description = newNote.Description,
                        attachmentFileName = newNote.AttachmentFileName
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ==========================================
        // BULK & SINGLE DELETE: AJAX POST
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> BulkDelete([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "No records selected." });

            if (_context == null || _context.Leads == null)
                return Json(new { success = false, message = "Database context is not initialized." });

            try
            {
                var leadsToDelete = await _context.Leads
                                                  .Where(l => ids.Contains(l.Id))
                                                  .ToListAsync();

                if (leadsToDelete != null && leadsToDelete.Any())
                {
                    if (_context.Note != null)
                    {
                        var relatedNotes = await _context.Note
                            .Where(n => n.LeadId != null && ids.Contains(n.LeadId.Value))
                            .ToListAsync();

                        if (relatedNotes != null && relatedNotes.Any())
                        {
                            _context.Note.RemoveRange(relatedNotes);
                        }
                    }

                    _context.Leads.RemoveRange(leadsToDelete);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                string errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Error: " + errorMsg });
            }
        }

        // GET: Leads/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var lead = await _context.Leads
                .Include(l => l.PaymentRow)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (lead == null) return NotFound();

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            // 🚀 FIX: Changed DealId to LeadId so it successfully finds the Lead's notes!
            ViewBag.ExistingNotes = await _context.Note
                .Include(n => n.NoteOwner)
                .Where(n => n.LeadId == id)
                .OrderByDescending(n => n.CreatedDateTime)
                .ToListAsync();
            return View(lead);
        }

        // POST: Leads/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Lead lead)
        {
            if (id != lead.Id)
            {
                return NotFound();
            }

            ModelState.Remove("PaymentRow");
            var paymentKeys = ModelState.Keys.Where(k => k.StartsWith("PaymentRow")).ToList();
            foreach (var key in paymentKeys)
            {
                ModelState.Remove(key);
            }

            if (ModelState.IsValid)
            {
                if (lead.CurrentStatus == "User")
                {
                    lead.Pipeline = "Upgrade Software";
                }
                else if (lead.CurrentStatus == "Non-User")
                {
                    lead.Pipeline = "New Software";
                }

                try
                {
                    // =========================================================
                    // 🚀 FIX: SAVE NEW NOTES APPENDED BY AJAX
                    // =========================================================
                    var newNoteOwners = Request.Form["SavedNoteOwner[]"];
                    var newNoteDates = Request.Form["SavedNoteDateTime[]"];
                    var newNoteDescs = Request.Form["SavedNoteDesc[]"];

                    if (newNoteDescs.Count > 0)
                    {
                        for (int i = 0; i < newNoteDescs.Count; i++)
                        {
                            var noteDesc = newNoteDescs[i];
                            if (!string.IsNullOrWhiteSpace(noteDesc))
                            {
                                // 🟢 CRITICAL FIX: Convert empty string "" to actual null to prevent FK constraint crashes
                                string rawOwnerId = newNoteOwners.Count > i ? newNoteOwners[i] : null;
                                string safeOwnerId = string.IsNullOrWhiteSpace(rawOwnerId) ? null : rawOwnerId;

                                var newNote = new Notes
                                {
                                    LeadId = lead.Id,
                                    NoteOwnerId = safeOwnerId, // Passes null if empty, preventing the SQL crash
                                    Description = noteDesc,
                                    CreatedDateTime = DateTime.TryParse(newNoteDates.Count > i ? newNoteDates[i] : "", out DateTime dt) ? dt : DateTime.Now
                                };
                                _context.Note.Add(newNote);
                            }
                        }
                        // Save the notes so they are immediately available for the Deal Transfer query
                        await _context.SaveChangesAsync();
                    }
                    // =========================================================
                    // HANDOVER LEAD AUTOMATION
                    // =========================================================
                    bool isHandoverConditionMet = (lead.Stage == "Demo Done" && lead.LeadStatus == "Hot") ||
                                                  (lead.LeadStatus == "Warm");

                    if (isHandoverConditionMet)
                    {
                        var exemptUsers = await _context.Users
                            .Where(u => u.FirstName.Contains("Jaspal") ||
                                        u.FirstName.Contains("Akash") ||
                                        u.FirstName.Contains("Abhinav"))
                            .ToListAsync();

                        var exemptOwnerIds = exemptUsers.Select(u => u.Id).ToList();
                        string currentOwnerId = lead.LeadOwnerId;

                        if (string.IsNullOrWhiteSpace(lead.DemoOwnerId))
                        {
                            lead.DemoOwnerId = currentOwnerId;
                        }

                        if (currentOwnerId != null && !exemptOwnerIds.Contains(currentOwnerId))
                        {
                            var defaultNewOwner = exemptUsers.FirstOrDefault(u => u.FirstName.Contains("Jaspal"));
                            if (defaultNewOwner != null)
                            {
                                lead.LeadOwnerId = defaultNewOwner.Id;
                            }
                        }
                    }

                    // =========================================================
                    // 🚀 FIX: PAYMENT ROWS DUPLICATION SYNCHRONIZATION
                    // =========================================================
                    if (lead.PaymentRow != null)
                    {
                        // Clean sync process: Get valid rows from UI
                        var validRows = lead.PaymentRow
                            .Where(r => (r.ProductId.HasValue && r.ProductId.Value > 0) || !string.IsNullOrWhiteSpace(r.ProductName))
                            .ToList();

                        // Detach to stop EF double tracking
                        lead.PaymentRow = null;

                        // Wipe out existing rows for this lead in the DB to perfectly mirror the UI state
                        var oldRows = _context.Set<ProductPaymentRow>().Where(r => r.LeadId == lead.Id).ToList();
                        if (oldRows.Any())
                        {
                            _context.RemoveRange(oldRows);
                        }

                        // Re-add the fresh list from the UI
                        foreach (var row in validRows)
                        {
                            row.Id = 0; // Force as new
                            row.LeadId = lead.Id;
                            _context.Add(row);
                        }
                    }

                    // Update existing lead in the DB
                    _context.Update(lead);
                    await _context.SaveChangesAsync();

                    // =========================================================
                    // 🚀 NEW: SYNC UPDATES TO THE LINKED ACCOUNT
                    // =========================================================
                    if (lead.AccountId.HasValue)
                    {
                        var linkedAccount = await _context.Accounts.FindAsync(lead.AccountId.Value);
                        if (linkedAccount != null)
                        {
                            // --- Core Identifiers ---
                            linkedAccount.ContactPersonName = lead.ContactName;
                            linkedAccount.AccountName = lead.LeadName;

                            // --- Ownership Mapping ---
                            linkedAccount.AccountOwnerId = lead.AccountOwnerId;
                            linkedAccount.CoOwnerId = lead.CoOwnerId;

                            // --- Source Mapping ---
                            linkedAccount.DataSource = lead.DataSources;
                            linkedAccount.MetaCampaignName = lead.MetaCampaignName;
                            linkedAccount.SeminarName = lead.SeminarName;

                            // --- Basic Details ---
                            linkedAccount.MobileNumber = lead.MobileNumber;
                            linkedAccount.AlternateMobile = lead.AlternateMobile;
                            linkedAccount.Email = lead.EmailID;
                            linkedAccount.AlternateEmailID = lead.AlternateEmailID;
                            linkedAccount.AccountType = lead.AccountType;
                            linkedAccount.CurrentStatus = lead.CurrentStatus;

                            // --- Address Mapping ---
                            linkedAccount.Addr1_Country = lead.Addr1_Country;
                            linkedAccount.Addr1_FlatHouse = lead.Addr1_FlatHouse;
                            linkedAccount.Addr1_Street = lead.Addr1_Street;
                            linkedAccount.Addr1_City = lead.Addr1_City;
                            linkedAccount.Addr1_State = lead.Addr1_State;
                            linkedAccount.Addr1_Zip = lead.Addr1_Zip;

                            linkedAccount.Addr2_Country = lead.Addr2_Country;
                            linkedAccount.Addr2_FlatHouse = lead.Addr2_FlatHouse;
                            linkedAccount.Addr2_Street = lead.Addr2_Street;
                            linkedAccount.Addr2_City = lead.Addr2_City;
                            linkedAccount.Addr2_State = lead.Addr2_State;
                            linkedAccount.Addr2_Zip = lead.Addr2_Zip;

                            // --- Professional Mapping ---
                            linkedAccount.IsHomeopathicDoctor = lead.IsHomeopathicDoctor;
                            linkedAccount.ClinicType = lead.ClinicType;
                            linkedAccount.WorkType = lead.WorkType;
                            linkedAccount.HasComputer = lead.HasComputer;
                            linkedAccount.DateOfBirth = lead.DateOfBirth;
                            linkedAccount.Age = lead.Age;
                            linkedAccount.YearsOfPractice = lead.YearOfPractice;
                            linkedAccount.AveragePatientFee = lead.AveragePatientFee;
                            linkedAccount.PatientsPerDay = lead.PatientsPerDay;
                            linkedAccount.TotalExperience = lead.TotalExperience;
                            linkedAccount.NumberOfClinics = lead.NumberOfClinics;
                            linkedAccount.Qualification = lead.Qualification;
                            linkedAccount.YearOfPassing = lead.YearOfPassing;
                            linkedAccount.CollegeName = lead.CollegeName;

                            // --- Software & Purchases ---
                            linkedAccount.CurrentlyUsingSoftware = lead.CurrentlyUsingSoftware;
                            linkedAccount.CurrentSoftwareName = lead.CurrentSoftwareName;
                            linkedAccount.PurchaseDate = lead.PurchaseDate;
                            linkedAccount.PurchaseValue = lead.PurchaseValue;
                            linkedAccount.PaymentType = lead.PaymentType;
                            linkedAccount.PaymentStatus = lead.PaymentStatus;

                            linkedAccount.ContactPerson1 = lead.ContactPerson1;
                            linkedAccount.Contact1Phone = lead.Contact1Phone;
                            linkedAccount.ContactPerson2 = lead.ContactPerson2;
                            linkedAccount.Contact2Phone = lead.Contact2Phone;
                            linkedAccount.ContactPerson3 = lead.ContactPerson3;
                            linkedAccount.Contact3Phone = lead.Contact3Phone;

                            linkedAccount.Description = lead.Description;

                            _context.Update(linkedAccount);
                            await _context.SaveChangesAsync();
                        }
                    }

                    // =========================================================
                    // 🚀 DIRECT DEAL CREATION WITH NOTE TRANSFER (WITH LOGS)
                    // =========================================================
                    if (lead.Stage == "Direct Deal")
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Checking if Deal exists for LeadName: '{lead.LeadName}'...");
                        bool dealExists = _context.Deals.Any(d => d.LeadName == lead.LeadName);

                        if (!dealExists)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Deal does NOT exist. Proceeding to create Deal for Lead ID: {lead.Id}");
                            var savedRows = _context.Set<ProductPaymentRow>().Where(r => r.LeadId == lead.Id).ToList();

                            var newDeal = new Deal
                            {
                                DealName = lead.LeadName + " - Direct Deal",
                                AccountId = lead.AccountId,
                                ContactPersonName = lead.ContactName,
                                LeadName = lead.LeadName,
                                LeadSource = lead.DataSources,
                                DealOwnerId = lead.LeadOwnerId,
                                AccountOwner = lead.AccountOwnerId,
                                DemoOwner = lead.DemoOwnerId,
                                AccountType = lead.AccountType,
                                MetaCampaignName = lead.MetaCampaignName,
                                PaymentMode = lead.PaymentMode,
                                PaymentType = lead.PaymentType,
                                PaymentStatus = lead.PaymentStatus,
                                SubTotal = lead.SubTotal ?? 0m,
                                Taxes = lead.Taxes ?? 0m,
                                Adjustment = lead.Adjustment ?? 0m,
                                GrandTotal = lead.GrandTotal ?? 0m,
                                PaymentRows = savedRows.Select(row => new DealPaymentRow
                                {
                                    DealType = row.DealType,
                                    ProductId = row.ProductId,
                                    ProductName = row.ProductName,
                                    Quantity = row.Quantity ?? 1,
                                    UnitPrice = row.ProductPrice ?? 0,
                                    Amount = row.Total ?? 0,
                                    Discount = row.DiscountPercent ?? 0,
                                    Total = row.FinalAmount ?? 0
                                }).ToList()
                            };

                            // 1. SAVE THE NEW DEAL FIRST TO GENERATE THE NEW DEAL ID
                            _context.Deals.Add(newDeal);
                            await _context.SaveChangesAsync();
                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Successfully created Deal with ID: {newDeal.Id}");

                            // ==========================================
                            // 2. 🚀 COPY/TRANSFER NOTES TO THE NEW DEAL
                            // ==========================================
                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Fetching notes for Lead ID: {lead.Id}...");
                            var leadNotes = await _context.Note
                                 .Where(n => n.LeadId == lead.Id)
                                 .AsNoTracking() // Ensures we create new copies, not modifying old ones
                                 .ToListAsync();

                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Found {leadNotes.Count} notes attached to Lead {lead.Id}");

                            if (leadNotes.Any())
                            {
                                var validUserIds = await _context.Users.Select(u => u.Id).ToListAsync();
                                var dealNotes = new List<Notes>();

                                foreach (var note in leadNotes)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Mapping Note ID {note.Id} | Original Owner: '{note.NoteOwnerId}'");

                                    // Validate the owner ID to prevent Foreign Key constraint crashes silently killing the save
                                    string safeOwnerId = null;
                                    if (!string.IsNullOrEmpty(note.NoteOwnerId) && validUserIds.Contains(note.NoteOwnerId))
                                    {
                                        safeOwnerId = note.NoteOwnerId;
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] WARNING: NoteOwnerId '{note.NoteOwnerId}' is invalid or missing from Users table. Safely setting to null.");
                                    }

                                    dealNotes.Add(new Notes
                                    {
                                        DealId = newDeal.Id, // Link to the newly created Deal
                                        LeadId = null,       // Set to null so it uniquely maps to the Deal
                                        AccountId = null,

                                        NoteOwnerId = safeOwnerId,
                                        CreatedDateTime = note.CreatedDateTime,
                                        Description = note.Description,
                                        AttachmentFileName = note.AttachmentFileName,
                                        AttachmentFilePath = note.AttachmentFilePath
                                    });
                                }

                                try
                                {
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Attempting to save {dealNotes.Count} notes to DB for Deal {newDeal.Id}...");
                                    await _context.Note.AddRangeAsync(dealNotes);
                                    int rowsSaved = await _context.SaveChangesAsync();
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] SUCCESS! {rowsSaved} note records saved successfully.");
                                }
                                catch (Exception ex)
                                {
                                    // THIS CATCHES SILENT DATABASE CRASHES
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] ERROR SAVING NOTES: {ex.Message}");
                                    if (ex.InnerException != null)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] INNER EXCEPTION: {ex.InnerException.Message}");
                                    }
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Skipping transfer because 0 notes were found for Lead {lead.Id}.");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Deal creation skipped because a Deal already exists for LeadName: '{lead.LeadName}'");
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeadExists(lead.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            // Fallback if model state fails
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            return View(lead);
        }

        private bool LeadExists(int id)
        {
            return _context.Leads.Any(e => e.Id == id);
        }

        // ==========================================
        // BULK UPLOAD EXCEL/CSV (HIGH PERFORMANCE)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var leadsToInsert = new List<Lead>();
            int currentRow = 1;

            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);
            var validAccountIds = new HashSet<int>(_context.Accounts.Select(a => a.Id));

            try
            {
                using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync(); // Skip header

                    while (!reader.EndOfStream)
                    {
                        currentRow++;
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);

                        if (values.Length >= 5)
                        {
                            string rawLeadOwnerId = GetVal(values, 1);
                            string rawCoOwnerId = GetVal(values, 52);
                            string rawAccountOwnerId = GetVal(values, 126);
                            string rawDemoOwnerId = GetVal(values, 128);

                            int? parsedAccountId = int.TryParse(GetVal(values, 100), out int accId) ? accId : null;

                            var newLead = new Lead
                            {
                                // --- Relational IDs ---
                                LeadOwnerId = validUserIds.Contains(rawLeadOwnerId) ? rawLeadOwnerId : null,
                                CoOwnerId = validUserIds.Contains(rawCoOwnerId) ? rawCoOwnerId : null,
                                AccountOwnerId = validUserIds.Contains(rawAccountOwnerId) ? rawAccountOwnerId : null,
                                DemoOwnerId = validUserIds.Contains(rawDemoOwnerId) ? rawDemoOwnerId : null,
                                AccountId = parsedAccountId.HasValue && validAccountIds.Contains(parsedAccountId.Value) ? parsedAccountId.Value : null,

                                // --- Basic Info ---
                                LeadName = GetVal(values, 4),
                                MobileNumber = GetVal(values, 71),
                                EmailID = GetVal(values, 74),
                                SocialLeadID = GetVal(values, 28),
                                DataSources = GetVal(values, 8),
                                CampaignSource = GetVal(values, 10),
                                CurrentStatus = GetVal(values, 116),
                                MetaCampaignName = GetVal(values, 50),
                                Pipeline = GetVal(values, 23),
                                ContactName = GetVal(values, 94),
                                GroupName = GetVal(values, 124),
                                AlternateMobile = GetVal(values, 73),
                                AlternateEmailID = GetVal(values, 125),
                                Description = GetVal(values, 22),
                                AccountType = GetVal(values, 114),

                                // --- Status & Financials ---
                                Stage = GetVal(values, 5),
                                LeadStatus = GetVal(values, 115),
                                Budget = decimal.TryParse(GetVal(values, 3), out decimal budget) ? budget : null,
                                ExpectedRevenue = decimal.TryParse(GetVal(values, 7), out decimal expRev) ? expRev : null,
                                Probability = decimal.TryParse(GetVal(values, 6), out decimal prob) ? prob : null,
                                TimePeriodToBuy = GetVal(values, 60),

                                // --- Professional Info ---
                                IsHomeopathicDoctor = GetVal(values, 102),
                                ClinicType = GetVal(values, 82),
                                WorkType = GetVal(values, 81),
                                HasComputer = GetVal(values, 63),
                                Qualification = GetVal(values, 83),
                                YearOfPassing = GetVal(values, 76),
                                CollegeName = GetVal(values, 72),
                                Age = int.TryParse(GetVal(values, 69), out int age) ? age : null,
                                YearOfPractice = int.TryParse(GetVal(values, 97), out int yop) ? yop : null,
                                TotalExperience = int.TryParse(GetVal(values, 68), out int tExp) ? tExp : null,
                                AveragePatientFee = decimal.TryParse(GetVal(values, 77), out decimal apf) ? apf : null,
                                NumberOfClinics = int.TryParse(GetVal(values, 70), out int noc) ? noc : null,
                                PatientsPerDay = int.TryParse(GetVal(values, 75), out int ppd) ? ppd : null,

                                // --- Dates ---
                                DateOfBirth = DateTime.TryParse(GetVal(values, 67), out DateTime dob) ? dob : null,
                                CreatedDateAndTime = DateTime.TryParse(GetVal(values, 54), out DateTime cdt) ? cdt : DateTime.Now,
                                LeadCreatedTime = DateTime.TryParse(GetVal(values, 51), out DateTime lct) ? lct : DateTime.Now,
                                DemoScheduledDate = DateTime.TryParse(GetVal(values, 56), out DateTime demo) ? demo : null,
                                FirstCallDate = DateTime.TryParse(GetVal(values, 55), out DateTime fcd) ? fcd : null,
                                NextFollowUpDate = DateTime.TryParse(GetVal(values, 57), out DateTime nfd) ? nfd : null,
                                LastContactDate = DateTime.TryParse(GetVal(values, 58), out DateTime lcd) ? lcd : null,
                                PurchaseDate = DateTime.TryParse(GetVal(values, 65), out DateTime pd) ? pd : null,

                                TrialStartDate = DateOnly.TryParse(GetVal(values, 109), out DateOnly tsd) ? tsd : null,
                                TrialEndDate = DateOnly.TryParse(GetVal(values, 110), out DateOnly ted) ? ted : null,

                                // --- Software & Purchasing ---
                                CurrentlyUsingSoftware = GetVal(values, 78),
                                CurrentSoftwareName = GetVal(values, 64),
                                RadarOpusVersion = GetVal(values, 112),
                                RadarOpusLicenseNo = GetVal(values, 111),
                                ProductPackage = GetVal(values, 96),
                                PurchaseValue = decimal.TryParse(GetVal(values, 66), out decimal pVal) ? pVal : null,
                                PaymentStatus = GetVal(values, 79),
                                CustomerStatus = GetVal(values, 80),

                                // --- Deals/Payments ---
                                DealType = GetVal(values, 107),
                                DealValue = decimal.TryParse(GetVal(values, 105), out decimal dVal) ? dVal : null,
                                PackageSelected = GetVal(values, 106),
                                Discount = decimal.TryParse(GetVal(values, 104), out decimal disc) ? disc : null,
                                PaymentMode = GetVal(values, 108),
                                Remarks = GetVal(values, 103),
                                PaymentType = GetVal(values, 113),
                                SubTotal = decimal.TryParse(GetVal(values, 130), out decimal subTot) ? subTot : null,
                                Adjustment = decimal.TryParse(GetVal(values, 131), out decimal adj) ? adj : null,
                                Taxes = decimal.TryParse(GetVal(values, 132), out decimal tax) ? tax : null,
                                GrandTotal = decimal.TryParse(GetVal(values, 133), out decimal grandTot) ? grandTot : null,

                                // --- Address 1 ---
                                Addr1_Country = GetVal(values, 135),
                                Addr1_FlatHouse = GetVal(values, 136),
                                Addr1_Street = GetVal(values, 137),
                                Addr1_City = GetVal(values, 138),
                                Addr1_State = GetVal(values, 139),
                                Addr1_Zip = GetVal(values, 140),
                                Addr1_Coordinates = GetVal(values, 141) + " " + GetVal(values, 142),

                                // --- Address 2 ---
                                Addr2_Country = GetVal(values, 143),
                                Addr2_FlatHouse = GetVal(values, 144),
                                Addr2_Street = GetVal(values, 145),
                                Addr2_City = GetVal(values, 146),
                                Addr2_State = GetVal(values, 147),
                                Addr2_Zip = GetVal(values, 148),
                                Addr2_Coordinates = GetVal(values, 149) + " " + GetVal(values, 150),

                                // --- Secondary Contacts ---
                                ContactPerson1 = GetVal(values, 117),
                                ContactPerson2 = GetVal(values, 118),
                                ContactPerson3 = GetVal(values, 120),
                                Contact1Phone = GetVal(values, 119),
                                Contact2Phone = GetVal(values, 121),
                                Contact3Phone = GetVal(values, 122),

                                // --- Other Remarks ---
                                ConversationRemarks = GetVal(values, 59),
                                InterestedPackage = GetVal(values, 62),
                                LostReason = GetVal(values, 61),
                                LeadProfile = GetVal(values, 95)
                            };

                            leadsToInsert.Add(newLead);
                        }
                    }
                }

                _context.ChangeTracker.AutoDetectChangesEnabled = false;
                await _context.Leads.AddRangeAsync(leadsToInsert);
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
    }
}