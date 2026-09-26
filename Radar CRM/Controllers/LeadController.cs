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

                    // 🚀 FIX: Use the new LeadFilterCriteria class here
                    var filters = System.Text.Json.JsonSerializer.Deserialize<List<LeadFilterCriteria>>(jsonString, options);

                    if (filters != null && filters.Any())
                    {
                        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(Lead), "l");
                        System.Linq.Expressions.Expression combinedPredicate = null;

                        foreach (var f in filters)
                        {
                            if (string.IsNullOrWhiteSpace(f.Value) && f.Condition != "is_empty" && f.Condition != "is_not_empty") continue;

                            string dbColName = f.ColumnName;

                            // 1. FOREIGN KEY TRANSLATION
                            var propertyInfo = typeof(Lead).GetProperty(dbColName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (propertyInfo != null && propertyInfo.PropertyType.IsClass && propertyInfo.PropertyType != typeof(string))
                            {
                                var idProp = typeof(Lead).GetProperty(dbColName + "Id", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                                if (idProp != null)
                                {
                                    dbColName = idProp.Name;
                                    propertyInfo = idProp;
                                }
                            }

                            if (propertyInfo == null) continue;

                            var propExpr = System.Linq.Expressions.Expression.Property(parameter, propertyInfo);
                            System.Linq.Expressions.Expression conditionExpr = null;
                            string safeValue = f.Value?.Replace("+", " ").Trim() ?? "";

                            // 2. DATES
                            if (f.IsDate || propertyInfo.PropertyType == typeof(DateTime) || propertyInfo.PropertyType == typeof(DateTime?))
                            {
                                string[] dates = safeValue.Split('|');
                                if (DateTime.TryParse(dates[0], out DateTime dVal))
                                {
                                    DateTime dVal2 = dates.Length > 1 && DateTime.TryParse(dates[1], out var d2) ? d2 : dVal;

                                    var startOfDay = dVal.Date;
                                    var endOfDay = dVal.Date.AddDays(1).AddTicks(-1);
                                    var endOfDay2 = dVal2.Date.AddDays(1).AddTicks(-1);

                                    var constStart = System.Linq.Expressions.Expression.Constant(startOfDay, propertyInfo.PropertyType);
                                    var constEnd = System.Linq.Expressions.Expression.Constant(endOfDay, propertyInfo.PropertyType);
                                    var constEnd2 = System.Linq.Expressions.Expression.Constant(endOfDay2, propertyInfo.PropertyType);

                                    if (f.Condition == "on" || f.Condition == "On")
                                    {
                                        var gte = System.Linq.Expressions.Expression.GreaterThanOrEqual(propExpr, constStart);
                                        var lte = System.Linq.Expressions.Expression.LessThanOrEqual(propExpr, constEnd);
                                        conditionExpr = System.Linq.Expressions.Expression.AndAlso(gte, lte);
                                    }
                                    else if (f.Condition == "before")
                                    {
                                        conditionExpr = System.Linq.Expressions.Expression.LessThan(propExpr, constStart);
                                    }
                                    else if (f.Condition == "after")
                                    {
                                        conditionExpr = System.Linq.Expressions.Expression.GreaterThan(propExpr, constEnd);
                                    }
                                    else if (f.Condition == "between")
                                    {
                                        var gte = System.Linq.Expressions.Expression.GreaterThanOrEqual(propExpr, constStart);
                                        var lte = System.Linq.Expressions.Expression.LessThanOrEqual(propExpr, constEnd2);
                                        conditionExpr = System.Linq.Expressions.Expression.AndAlso(gte, lte);
                                    }
                                }
                            }
                            // 3. OWNER / USER MAPPING (AccountOwnerId, LeadOwnerId, etc.)
                            else if (dbColName.EndsWith("OwnerId", StringComparison.OrdinalIgnoreCase) || dbColName == "CreatedBy" || dbColName == "ModifiedBy")
                            {
                                var searchValue = safeValue.ToLower();
                                var matchingUserIds = _context.Users
                                    .Where(u => (u.fullName != null && u.fullName.ToLower().Contains(searchValue)) ||
                                                (u.FirstName != null && u.FirstName.ToLower().Contains(searchValue)))
                                    .Select(u => u.Id)
                                    .ToList();

                                var listExpr = System.Linq.Expressions.Expression.Constant(matchingUserIds);
                                var containsMethod = typeof(List<string>).GetMethod("Contains", new[] { typeof(string) });

                                if (f.Condition == "contains" || f.Condition == "is")
                                    conditionExpr = System.Linq.Expressions.Expression.Call(listExpr, containsMethod, propExpr);
                                else if (f.Condition == "does_not_contain" || f.Condition == "is_not")
                                    conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(listExpr, containsMethod, propExpr));
                            }
                            // 4. REGULAR STRINGS (Like LeadName, AccountType, CurrentStatus)
                            else if (propertyInfo.PropertyType == typeof(string))
                            {
                                var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                                var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
                                var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });

                                var notNullProp = System.Linq.Expressions.Expression.Coalesce(propExpr, System.Linq.Expressions.Expression.Constant(""));
                                var lowerProp = System.Linq.Expressions.Expression.Call(notNullProp, toLowerMethod);
                                var lowerVal = System.Linq.Expressions.Expression.Constant(safeValue.ToLower());

                                if (f.Condition == "is") conditionExpr = System.Linq.Expressions.Expression.Equal(lowerProp, lowerVal);
                                else if (f.Condition == "is_not") conditionExpr = System.Linq.Expressions.Expression.NotEqual(lowerProp, lowerVal);
                                else if (f.Condition == "contains") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal);
                                else if (f.Condition == "does_not_contain") conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal));
                                else if (f.Condition == "starts_with") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, startsWithMethod, lowerVal);
                                else if (f.Condition == "ends_with") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, endsWithMethod, lowerVal);
                                else if (f.Condition == "is_empty") conditionExpr = System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr);
                                else if (f.Condition == "is_not_empty") conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr));
                            }
                            // 5. NUMBERS
                            else
                            {
                                if (propertyInfo.PropertyType == typeof(int) || propertyInfo.PropertyType == typeof(int?))
                                {
                                    if (int.TryParse(safeValue, out int numVal))
                                    {
                                        var valExpr = System.Linq.Expressions.Expression.Constant(numVal, propertyInfo.PropertyType);
                                        if (f.Condition == "is" || f.Condition == "contains") conditionExpr = System.Linq.Expressions.Expression.Equal(propExpr, valExpr);
                                        else if (f.Condition == "is_not" || f.Condition == "does_not_contain") conditionExpr = System.Linq.Expressions.Expression.NotEqual(propExpr, valExpr);
                                    }
                                }
                                else if (propertyInfo.PropertyType == typeof(decimal) || propertyInfo.PropertyType == typeof(decimal?))
                                {
                                    if (decimal.TryParse(safeValue, out decimal decVal))
                                    {
                                        var valExpr = System.Linq.Expressions.Expression.Constant(decVal, propertyInfo.PropertyType);
                                        if (f.Condition == "is" || f.Condition == "contains") conditionExpr = System.Linq.Expressions.Expression.Equal(propExpr, valExpr);
                                        else if (f.Condition == "is_not" || f.Condition == "does_not_contain") conditionExpr = System.Linq.Expressions.Expression.NotEqual(propExpr, valExpr);
                                    }
                                }
                            }

                            if (conditionExpr != null)
                            {
                                if (combinedPredicate == null)
                                {
                                    combinedPredicate = conditionExpr;
                                }
                                else
                                {
                                    // 🚀 Ensure AND conditions stack properly without overwriting
                                    if (string.Equals(f.LogicalOperator, "OR", StringComparison.OrdinalIgnoreCase))
                                    {
                                        combinedPredicate = System.Linq.Expressions.Expression.OrElse(combinedPredicate, conditionExpr);
                                    }
                                    else
                                    {
                                        combinedPredicate = System.Linq.Expressions.Expression.AndAlso(combinedPredicate, conditionExpr);
                                    }
                                }
                            }
                        }
                        if (combinedPredicate != null)
                        {
                            var lambda = System.Linq.Expressions.Expression.Lambda<Func<Lead, bool>>(combinedPredicate, parameter);
                            query = query.Where(lambda);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FILTER ENGINE ERROR: " + ex.Message);
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
            // 🚀 ADD THIS LINE TO FIX THE CRASH:
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");

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

        // 🚀 ADD THIS NEW UNIQUE CLASS HERE:
        public class LeadFilterCriteria
        {
            public string LogicalOperator { get; set; }
            public string ColumnName { get; set; }
            public string Condition { get; set; }
            public string Value { get; set; }
            public bool IsDate { get; set; }
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

            string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System User";
            lead.CreatedBy = $"{currentUser} on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";
            ModelState.Remove("CreatedBy");

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

            // =========================================================
            // POPULATE THE SIDEBAR ACCOUNT & DEAL
            // =========================================================

            // 1. Get the Account Name
            if (lead.AccountId != null && lead.AccountId > 0)
            {
                var account = await _context.Accounts.FindAsync(lead.AccountId);
                lead.AccountName = account?.AccountName;
            }

            // 2. Get the linked Deal (Grabbing the most recent deal created for this lead)
            if (!string.IsNullOrEmpty(lead.LeadName))
            {
                var linkedDeal = await _context.Deals
                    .Where(d => d.LeadName == lead.LeadName)
                    .OrderByDescending(d => d.Id)
                    .FirstOrDefaultAsync();

                if (linkedDeal != null)
                {
                    lead.DealId = linkedDeal.Id;
                    lead.DealName = linkedDeal.DealName;
                }
            }

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

                // 🚀 1. SET "MODIFIED BY" WITH CURRENT LOGGED-IN USER & TIME
                string currentUser = User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "System User";
                lead.ModifiedBy = $"{currentUser} on {DateTime.Now.ToString("MMM dd, yyyy - hh:mm tt")}";
                ModelState.Remove("ModifiedBy");

                // 🚀 2. FETCH ORIGINAL RECORD TO PREVENT "CREATED BY" FROM BEING ERASED
                var existingLead = await _context.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == lead.Id);
                if (existingLead != null && string.IsNullOrEmpty(lead.CreatedBy))
                {
                    lead.CreatedBy = existingLead.CreatedBy;
                }
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

                        // 🚀 1. AUTOMATICALLY CHANGE LEAD STAGE TO 'WON'
                        lead.Stage = "Won";
                        _context.Update(lead);

                        if (!dealExists)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG-NOTES] Deal does NOT exist. Proceeding to create Deal for Lead ID: {lead.Id}");
                            var savedRows = _context.Set<ProductPaymentRow>().Where(r => r.LeadId == lead.Id).ToList();

                            var newDeal = new Deal
                            {
                                LeadId = lead.Id,
                                DealName = lead.LeadName + " - Direct Deal",
                                AccountId = lead.AccountId,
                                ContactPersonName = lead.ContactName,
                                LeadName = lead.LeadName,
                                LeadSource = lead.DataSources,
                                DealOwnerId = lead.LeadOwnerId,
                                AccountOwner = lead.AccountOwnerId,
                                DemoOwner = lead.DemoOwnerId,
                                AccountType = lead.AccountType,
                                CreatedBy=lead.CreatedBy,

                                // 🚀 FIX: Assign proper DateTime object, not a string
                                DateOfEntry = DateTime.Now,
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
        [DisableRequestSizeLimit]
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var leadsToInsert = new List<Lead>();
            var leadsToUpdate = new List<Lead>();
            var skippedRecords = new List<string>();
            int currentRow = 1;

            // Load Maps for fast Upsert checking and Relationship mapping
            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);

            var existingLeadsDb = await _context.Leads
                .Where(l => !string.IsNullOrEmpty(l.ZohoRecordId))
                .AsNoTracking()
                .GroupBy(l => l.ZohoRecordId)
                .ToDictionaryAsync(g => g.Key, g => g.First());

            var existingMobiles = new HashSet<string>(await _context.Leads.Where(l => !string.IsNullOrEmpty(l.MobileNumber)).Select(l => l.MobileNumber).ToListAsync());
            var existingEmails = new HashSet<string>(await _context.Leads.Where(l => !string.IsNullOrEmpty(l.EmailID)).Select(l => l.EmailID.ToLower()).ToListAsync());

            var accountLookup = _context.Accounts.Where(a => a.ZohoRecordId != null).ToDictionary(a => a.ZohoRecordId, a => a.Id);

            // Country Normalizer
            string NormalizeCountry(string input)
            {
                if (string.IsNullOrWhiteSpace(input)) return null;
                string upper = input.Trim().ToUpper();
                return (upper.Contains("INDIA") || upper == "IND" || upper == "IN") ? "INDIA" : upper;
            }

            try
            {
                using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(headerLine)) return BadRequest("Empty CSV");

                    var headers = ParseCsvLine(headerLine).Select(h => h.Trim().ToLower().Replace(" ", "")).ToList();

                    string GetValSafe(string[] vals, string colName)
                    {
                        var idx = headers.IndexOf(colName.ToLower().Replace(" ", ""));
                        return idx >= 0 && idx < vals.Length ? vals[idx]?.Trim() ?? "" : "";
                    }

                    decimal? GetDecimalSafe(string[] vals, string colName)
                    {
                        string raw = GetValSafe(vals, colName);
                        if (string.IsNullOrWhiteSpace(raw)) return null;
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
                        string mobile = GetValSafe(values, "MobileNumber");
                        string email = GetValSafe(values, "EmailID")?.ToLower();

                        bool isUpdate = false;
                        Lead lead;

                        // 🚀 UPSERT LOGIC
                        if (!string.IsNullOrEmpty(zohoRecordId) && existingLeadsDb.TryGetValue(zohoRecordId, out var existingLead))
                        {
                            lead = existingLead; // Found it -> Update
                            isUpdate = true;
                        }
                        else
                        {
                            // Duplication Check for purely new records
                            bool isMobileDup = !string.IsNullOrEmpty(mobile) && existingMobiles.Contains(mobile);
                            bool isEmailDup = !string.IsNullOrEmpty(email) && existingEmails.Contains(email);

                            if (isMobileDup || isEmailDup)
                            {
                                skippedRecords.Add($"Row {currentRow}: Skipped (Mobile or Email exists)");
                                continue;
                            }

                            lead = new Lead(); // Doesn't exist -> Insert
                            if (!string.IsNullOrEmpty(mobile)) existingMobiles.Add(mobile);
                            if (!string.IsNullOrEmpty(email)) existingEmails.Add(email);
                        }

                        // --- Extract Owners & Relations ---
                        string rawLeadOwnerId = GetValSafe(values, "LeadOwner.id");
                        string rawAccountOwnerId = GetValSafe(values, "AccountOwner.id");
                        string rawCoOwnerId = GetValSafe(values, "Co-Owner.id");
                        string rawDemoOwnerId = GetValSafe(values, "DemoOwner.id");
                        string rawAccountId = GetValSafe(values, "AccountName.id");

                        // --- Core Identity ---
                        lead.ZohoRecordId = zohoRecordId;
                        lead.LeadName = GetValSafe(values, "LeadName");
                        lead.EmailID = GetValSafe(values, "EmailID");
                        lead.MobileNumber = mobile;
                        lead.AlternateMobile = GetValSafe(values, "AlternateMobile");
                        lead.AlternateEmailID = GetValSafe(values, "AlternateEmailID");
                        lead.LeadOwnerId = validUserIds.Contains(rawLeadOwnerId) ? rawLeadOwnerId : null;
                        lead.AccountOwnerId = validUserIds.Contains(rawAccountOwnerId) ? rawAccountOwnerId : null;
                        lead.CoOwnerId = validUserIds.Contains(rawCoOwnerId) ? rawCoOwnerId : null;
                        lead.DemoOwnerId = validUserIds.Contains(rawDemoOwnerId) ? rawDemoOwnerId : null;

                        // MAPPING TO ACCOUNT VIA LOOKUP
                        lead.AccountId = accountLookup.TryGetValue(rawAccountId, out int accId) ? accId : null;

                        // --- Status & Pipeline ---
                        lead.Stage = GetValSafe(values, "Stage");
                        lead.Probability = GetDecimalSafe(values, "Probability(%)");
                        lead.LeadStatus = GetValSafe(values, "LeadStatus");
                        lead.CurrentStatus = GetValSafe(values, "CurrentStatus");
                        lead.Pipeline = GetValSafe(values, "Pipeline");
                        lead.DataSources = GetValSafe(values, "DataSources");
                        lead.CampaignSource = GetValSafe(values, "CampaignSource");
                        lead.MetaCampaignName = GetValSafe(values, "MetaCampaignName");
                        lead.Description = GetValSafe(values, "Description");
                        lead.Remarks = GetValSafe(values, "Remarks");
                        lead.ConversationRemarks = GetValSafe(values, "Remarks/Notes");

                        // --- Tracking & Financials ---
                        lead.Budget = GetDecimalSafe(values, "Budget");
                        lead.ExpectedRevenue = GetDecimalSafe(values, "ExpectedRevenue");
                        lead.TimePeriodToBuy = GetValSafe(values, "TimeperiodtoBuy");
                        lead.LostReason = GetValSafe(values, "LostReason");
                        lead.NotInterestedReason = GetValSafe(values, "NotInterestedReason");

                        // --- Professional Info ---
                        lead.IsHomeopathicDoctor = GetValSafe(values, "AreyouaHomopathicDoctor");
                        lead.ClinicType = GetValSafe(values, "ClinicType");
                        lead.WorkType = GetValSafe(values, "WorkType");
                        lead.HasComputer = GetValSafe(values, "HavingComputer/Laptop");
                        lead.Qualification = GetValSafe(values, "Qualification");
                        lead.YearOfPassing = GetValSafe(values, "Yearofpassing");
                        lead.CollegeName = GetValSafe(values, "CollegeName");
                        lead.Age = int.TryParse(GetValSafe(values, "Age"), out int age) ? age : null;
                        lead.YearOfPractice = int.TryParse(GetValSafe(values, "Yearofpractice"), out int yop) ? yop : null;
                        lead.TotalExperience = int.TryParse(GetValSafe(values, "Totalexperience"), out int te) ? te : null;
                        lead.AveragePatientFee = GetDecimalSafe(values, "AveragePatientFee");
                        lead.NumberOfClinics = int.TryParse(GetValSafe(values, "NumberofClinics"), out int noc) ? noc : null;
                        lead.PatientsPerDay = int.TryParse(GetValSafe(values, "PatientsperDay"), out int ppd) ? ppd : null;
                        lead.LeadProfile = GetValSafe(values, "Leadprofie");
                        lead.GroupName = GetValSafe(values, "GroupName");

                        // --- Software & Purchases ---
                        lead.CurrentlyUsingSoftware = GetValSafe(values, "CurrentlyUsingSoftware");
                        lead.CurrentSoftwareName = GetValSafe(values, "CurrentSoftwareName");
                        lead.NewSoftware = GetValSafe(values, "NewSoftware");
                        lead.RadarOpusVersion = GetValSafe(values, "RadarOpusversion");
                        lead.RadarOpusLicenseNo = GetValSafe(values, "RadarOpusLicenseno");
                        lead.ProductPackage = GetValSafe(values, "Product/Packagepackage");
                        lead.PurchaseValue = GetDecimalSafe(values, "PurchaseValue(â‚¹)");
                        lead.PaymentStatus = GetValSafe(values, "PaymentStatus");
                        lead.PaymentType = GetValSafe(values, "PaymentType");
                        lead.PaymentMode = GetValSafe(values, "PaymentMode");
                        lead.CustomerStatus = GetValSafe(values, "CustomerStatus");

                        // --- Deal Info directly on Lead ---
                        lead.DealType = GetValSafe(values, "DealType");
                        lead.DealValue = GetDecimalSafe(values, "DealValue(â‚¹)");
                        lead.Discount = GetDecimalSafe(values, "Discount");
                        lead.PackageSelected = GetValSafe(values, "PackageSelected");
                        lead.SubTotal = GetDecimalSafe(values, "SubTotal");
                        lead.Adjustment = GetDecimalSafe(values, "adjustment.");
                        lead.Taxes = GetDecimalSafe(values, "taxs.");
                        lead.GrandTotal = GetDecimalSafe(values, "grandtotal");

                        // --- Dates ---
                        lead.DateOfBirth = DateTime.TryParse(GetValSafe(values, "DateofBirth"), out DateTime dob) ? dob : null;
                        lead.CreatedDateAndTime = DateTime.TryParse(GetValSafe(values, "CreatedTime"), out DateTime cdt) ? cdt : DateTime.Now;
                        lead.ModifiedTime = DateTime.TryParse(GetValSafe(values, "ModifiedTime"), out DateTime mdt) ? mdt : null;
                        lead.LeadCreatedTime = DateTime.TryParse(GetValSafe(values, "Lead-Created-Time"), out DateTime lct) ? lct : DateTime.Now;
                        lead.DemoScheduledDate = DateTime.TryParse(GetValSafe(values, "Demoscheduleddateandtime"), out DateTime dsdt) ? dsdt : null;
                        lead.FirstCallDate = DateTime.TryParse(GetValSafe(values, "FirstcallDate"), out DateTime fcd) ? fcd : null;
                        lead.NextFollowUpDate = DateTime.TryParse(GetValSafe(values, "Nextfollow-upDate"), out DateTime nfd) ? nfd : null;
                        lead.LastContactDate = DateTime.TryParse(GetValSafe(values, "LastContactDate"), out DateTime lcd) ? lcd : null;
                        lead.PurchaseDate = DateTime.TryParse(GetValSafe(values, "PurchaseDate"), out DateTime pd) ? pd : null;
                        lead.TrialStartDate = DateOnly.TryParse(GetValSafe(values, "TrialStartDate"), out DateOnly tsd) ? tsd : null;
                        lead.TrialEndDate = DateOnly.TryParse(GetValSafe(values, "TrialEndDate"), out DateOnly ted) ? ted : null;
                        lead.CreatedBy = GetValSafe(values, "CreatedBy");
                        lead.ModifiedBy = GetValSafe(values, "ModifiedBy");

                        // --- Address 1 Mapping ---
                        // If Address 1 exists, use it. If not, fallback to generic Country/Region/City columns
                        string addr1Country = GetValSafe(values, "Address1-Country/Region");
                        lead.Addr1_Country = NormalizeCountry(!string.IsNullOrEmpty(addr1Country) ? addr1Country : GetValSafe(values, "Country/Region"));

                        string addr1Street = GetValSafe(values, "Address1-StreetAddress");
                        lead.Addr1_Street = !string.IsNullOrEmpty(addr1Street) ? addr1Street : GetValSafe(values, "StreetAddress");

                        string addr1City = GetValSafe(values, "Address1-City");
                        lead.Addr1_City = !string.IsNullOrEmpty(addr1City) ? addr1City : GetValSafe(values, "City");

                        string addr1State = GetValSafe(values, "Address1-State/Province");
                        lead.Addr1_State = !string.IsNullOrEmpty(addr1State) ? addr1State : GetValSafe(values, "State/Province");

                        string addr1Zip = GetValSafe(values, "Address1-Zip/PostalCode");
                        lead.Addr1_Zip = !string.IsNullOrEmpty(addr1Zip) ? addr1Zip : GetValSafe(values, "Zip/PostalCode");

                        string addr1Flat = GetValSafe(values, "Address1-Flat/HouseNo./Building/ApartmentName");
                        lead.Addr1_FlatHouse = !string.IsNullOrEmpty(addr1Flat) ? addr1Flat : GetValSafe(values, "Flat/HouseNo./Building/ApartmentName");

                        lead.Addr1_Coordinates = GetValSafe(values, "Address1-Latitude") + " " + GetValSafe(values, "Address1-Longitude");

                        // --- Address 2 Mapping ---
                        string addr2Country = GetValSafe(values, "Address2-Country/Region");
                        lead.Addr2_Country = NormalizeCountry(!string.IsNullOrEmpty(addr2Country) ? addr2Country : GetValSafe(values, "Country/Region2"));

                        string addr2Street = GetValSafe(values, "Address2-StreetAddress");
                        lead.Addr2_Street = !string.IsNullOrEmpty(addr2Street) ? addr2Street : GetValSafe(values, "StreetAddress2");

                        string addr2City = GetValSafe(values, "Address2-City");
                        lead.Addr2_City = !string.IsNullOrEmpty(addr2City) ? addr2City : GetValSafe(values, "City2");

                        string addr2State = GetValSafe(values, "Address2-State/Province");
                        lead.Addr2_State = !string.IsNullOrEmpty(addr2State) ? addr2State : GetValSafe(values, "State/Province2");

                        string addr2Zip = GetValSafe(values, "Address2-Zip/PostalCode");
                        lead.Addr2_Zip = !string.IsNullOrEmpty(addr2Zip) ? addr2Zip : GetValSafe(values, "Zip/PostalCode2");

                        string addr2Flat = GetValSafe(values, "Address2-Flat/HouseNo./Building/ApartmentName");
                        lead.Addr2_FlatHouse = !string.IsNullOrEmpty(addr2Flat) ? addr2Flat : GetValSafe(values, "Flat/HouseNo./Building/ApartmentName2");

                        lead.Addr2_Coordinates = GetValSafe(values, "Address2-Latitude") + " " + GetValSafe(values, "Address2-Longitude");

                        // --- Secondary Contacts ---
                        lead.ContactName = GetValSafe(values, "ContactName");
                        lead.ContactPerson1 = GetValSafe(values, "ContactPersonName1");
                        lead.ContactPerson2 = GetValSafe(values, "ContactPersonName2");
                        lead.ContactPerson3 = GetValSafe(values, "ContactPersonName3");
                        lead.Contact1Phone = GetValSafe(values, "Contact1PhoneNumber");
                        lead.Contact2Phone = GetValSafe(values, "Contact2PhoneNumber");
                        lead.Contact3Phone = GetValSafe(values, "Contact3PhoneNumber");

                        // 🚀 --- AD TRACKING --- 🚀
                        lead.SocialLeadID = GetValSafe(values, "SocialLeadID");
                        lead.Gclid = GetValSafe(values, "GCLID");
                        lead.Zcampaignid = GetValSafe(values, "ZCAMPAIGNID");
                        lead.Adgroupid = GetValSafe(values, "ADGROUPID");
                        lead.Adid = GetValSafe(values, "ADID");
                        lead.Keywordid = GetValSafe(values, "KEYWORDID");
                        lead.Keyword = GetValSafe(values, "Keyword");
                        lead.ClickType = GetValSafe(values, "ClickType");
                        lead.DeviceType = GetValSafe(values, "DeviceType");
                        lead.AdNetwork = GetValSafe(values, "AdNetwork");
                        lead.SearchPartnerNetwork = GetValSafe(values, "SearchPartnerNetwork");
                        lead.AdCampaignName = GetValSafe(values, "AdCampaignName");
                        lead.AdGroupName = GetValSafe(values, "AdGroupName");
                        lead.Ad = GetValSafe(values, "Ad");
                        lead.Gadconfigid = GetValSafe(values, "GADCONFIGID");
                        lead.AdClickDate = DateTime.TryParse(GetValSafe(values, "AdClickDate"), out DateTime acd) ? acd : null;
                        lead.CostPerClick = GetDecimalSafe(values, "CostperClick");
                        lead.CostPerConversion = GetDecimalSafe(values, "CostperConversion");
                        lead.ConversionExportedOn = DateTime.TryParse(GetValSafe(values, "ConversionExportedOn"), out DateTime ceo) ? ceo : null;
                        lead.ConversionExportStatus = GetValSafe(values, "ConversionExportStatus");
                        lead.ReasonForConversionFailure = GetValSafe(values, "ReasonforConversionFailure");

                        // Route to correct list
                        if (isUpdate) leadsToUpdate.Add(lead);
                        else leadsToInsert.Add(lead);
                    }
                }

                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                // 🚀 Execute Inserts & Updates
                if (leadsToInsert.Any()) await _context.Leads.AddRangeAsync(leadsToInsert);
                if (leadsToUpdate.Any()) _context.Leads.UpdateRange(leadsToUpdate);

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
                insertedCount = leadsToInsert.Count,
                updatedCount = leadsToUpdate.Count,
                skippedCount = skippedRecords.Count
            });
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