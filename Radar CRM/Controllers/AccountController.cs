using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;

namespace Radar_CRM.Controllers
{
    public class AccountsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // INDEX: GET (Hierarchical List View)
        // ==========================================
        public async Task<IActionResult> Index(int page = 1, string search = "", string sortCol = "Id", string sortDir = "desc", string advancedFilters = "")
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Users");

            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            if (currentUser == null) return RedirectToAction("Login", "Users");

            int pageSize = 100;
            var query = _context.Accounts.AsQueryable();

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
                query = query.Where(a => visibleUserIds.Contains(a.AccountOwnerId));
            }

            // 🚀 BULLETPROOF FILTERING ENGINE
            if (!string.IsNullOrWhiteSpace(advancedFilters))
            {
                try
                {
                    // 1. Prevent JSON crashing by ensuring the string is properly decoded
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

                        // 2. Safely grab the property (Case-Insensitive to prevent UI/DB casing mismatches)
                        var propertyInfo = typeof(Account).GetProperty(f.ColumnName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (propertyInfo == null) continue;

                        string dbColName = propertyInfo.Name; // Use the exact database casing

                        // If it's a complex class, switch to its foreign key 'Id' column to prevent crashes
                        if (propertyInfo.PropertyType.IsClass && propertyInfo.PropertyType != typeof(string))
                        {
                            var idProp = typeof(Account).GetProperty(dbColName + "Id", System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
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
                            // Lookup Users whose names match the text input, then query by those IDs
                            var searchValue = f.Value?.ToLower().Trim() ?? "";
                            var matchingUserIds = _context.Users
                                .Where(u => (u.fullName != null && u.fullName.ToLower().Contains(searchValue)) ||
                                            (u.FirstName != null && u.FirstName.ToLower().Contains(searchValue)))
                                .Select(u => u.Id)
                                .ToList();

                            if (f.Condition == "contains" || f.Condition == "is")
                            {
                                query = query.Where(a => matchingUserIds.Contains(EF.Property<string>(a, dbColName)));
                            }
                            else if (f.Condition == "does_not_contain" || f.Condition == "is_not")
                            {
                                query = query.Where(a => !matchingUserIds.Contains(EF.Property<string>(a, dbColName)));
                            }
                            else if (f.Condition == "is_empty")
                            {
                                query = query.Where(a => string.IsNullOrEmpty(EF.Property<string>(a, dbColName)));
                            }
                            else if (f.Condition == "is_not_empty")
                            {
                                query = query.Where(a => !string.IsNullOrEmpty(EF.Property<string>(a, dbColName)));
                            }
                        }
                        else if (propertyInfo.PropertyType == typeof(string))
                        {
                            // 3. Fully fleshed out string conditions mapped directly to your UI dropdowns
                            if (f.Condition == "contains") query = query.Where(a => EF.Property<string>(a, dbColName) != null && EF.Property<string>(a, dbColName).Contains(f.Value));
                            else if (f.Condition == "does_not_contain") query = query.Where(a => EF.Property<string>(a, dbColName) == null || !EF.Property<string>(a, dbColName).Contains(f.Value));
                            else if (f.Condition == "starts_with") query = query.Where(a => EF.Property<string>(a, dbColName) != null && EF.Property<string>(a, dbColName).StartsWith(f.Value));
                            else if (f.Condition == "ends_with") query = query.Where(a => EF.Property<string>(a, dbColName) != null && EF.Property<string>(a, dbColName).EndsWith(f.Value));
                            else if (f.Condition == "is") query = query.Where(a => EF.Property<string>(a, dbColName) == f.Value);
                            else if (f.Condition == "is_not") query = query.Where(a => EF.Property<string>(a, dbColName) != f.Value);
                            else if (f.Condition == "is_empty") query = query.Where(a => string.IsNullOrEmpty(EF.Property<string>(a, dbColName)));
                            else if (f.Condition == "is_not_empty") query = query.Where(a => !string.IsNullOrEmpty(EF.Property<string>(a, dbColName)));
                        }
                        else
                        {
                            // Safe fallback for numeric values
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
                query = query.Where(a =>
                    (a.AccountName != null && a.AccountName.ToLower().Contains(searchLower)) ||
                    (a.MobileNumber != null && a.MobileNumber.Contains(search)) ||
                    (a.Email != null && a.Email.ToLower().Contains(searchLower))
                );
            }

            if (sortDir == "desc")
            {
                query = sortCol switch
                {
                    "AccountName" => query.OrderByDescending(a => a.AccountName),
                    "MobileNumber" => query.OrderByDescending(a => a.MobileNumber),
                    "CurrentStatus" => query.OrderByDescending(a => a.CurrentStatus),
                    _ => query.OrderByDescending(a => a.Id)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "AccountName" => query.OrderBy(a => a.AccountName),
                    "MobileNumber" => query.OrderBy(a => a.MobileNumber),
                    "CurrentStatus" => query.OrderBy(a => a.CurrentStatus),
                    _ => query.OrderBy(a => a.Id)
                };
            }

            var totalRecords = await query.CountAsync();

            if (page < 1) page = 1;

            int totalPagesCalc = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPagesCalc == 0) totalPagesCalc = 1;

            if (page > totalPagesCalc) page = totalPagesCalc;

            var accounts = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPagesCalc;
            ViewBag.TotalRecords = totalRecords;

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");

            return View(accounts);
        }
        // Ensure this class is at the bottom of your controller file
        public class FilterCriteria
        {
            public string ColumnName { get; set; }
            public string Condition { get; set; }
            public string Value { get; set; }
            public bool IsDate { get; set; }
        }
        // ==========================================
        // HELPER METHOD (Add this to the bottom of the AccountsController)
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
        // DYNAMIC FIELD OPTIONS FOR MASS UPDATE
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetFieldOptions(string fieldName)
        {
            try
            {
                var propertyInfo = typeof(Account).GetProperty(fieldName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                // If the field isn't a string (dropdowns are usually strings), return empty so JS shows a textbox
                if (propertyInfo == null || propertyInfo.PropertyType != typeof(string))
                {
                    return Json(new string[] { });
                }

                // 🚀 Query the DB for all unique, non-null values that currently exist for this column
                var distinctValues = await _context.Accounts
                    .Select(a => EF.Property<string>(a, propertyInfo.Name))
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct()
                    .OrderBy(v => v)
                    .ToListAsync();

                return Json(distinctValues);
            }
            catch
            {
                return Json(new string[] { });
            }
        }

        // ==========================================
        // BULK UPDATE: AJAX POST
        // ==========================================
        [HttpPost]
        [IgnoreAntiforgeryToken] // 🚀 FIX: Added this to prevent 400 Bad Request errors from anti-forgery mismatches in AJAX
        public async Task<IActionResult> BulkUpdate([FromBody] BulkUpdateRequest request)
        {
            if (request == null || request.Ids == null || !request.Ids.Any() || string.IsNullOrEmpty(request.FieldName))
            {
                return Json(new { success = false, message = "Invalid data provided." });
            }

            try
            {
                var accountsToUpdate = await _context.Accounts.Where(a => request.Ids.Contains(a.Id)).ToListAsync();

                // Use reflection to find the property dynamically
                var propertyInfo = typeof(Account).GetProperty(request.FieldName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (propertyInfo == null)
                    return Json(new { success = false, message = "Field not found in database." });

                foreach (var account in accountsToUpdate)
                {
                    // Handle Types Safely
                    object safeValue = null;
                    Type t = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

                    if (!string.IsNullOrWhiteSpace(request.NewValue))
                    {
                        if (t == typeof(DateTime))
                        {
                            if (DateTime.TryParse(request.NewValue, out DateTime d)) safeValue = d;
                        }
                        else
                        {
                            safeValue = Convert.ChangeType(request.NewValue, t);
                        }
                    }

                    propertyInfo.SetValue(account, safeValue, null);
                }

                _context.UpdateRange(accountsToUpdate);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = trueError });
            }
        }

        // 🚀 Ensure this class is defined inside the controller for the JSON binding to work
        public class BulkUpdateRequest
        {
            public List<int> Ids { get; set; }
            public string FieldName { get; set; }
            public string NewValue { get; set; }
        }

        // ==========================================
        // AJAX: GET NOTES FOR OFFCANVAS
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetNotes(int accountId)
        {
            var notes = await _context.Note
                .Include(n => n.NoteOwner)
                .Where(n => n.AccountId == accountId)
                .OrderByDescending(n => n.CreatedDateTime)
                .Select(n => new {
                    id = n.Id,
                    ownerName = n.NoteOwner != null ? (n.NoteOwner.fullName ?? n.NoteOwner.FirstName) : "System User",
                    createdDateTime = n.CreatedDateTime.ToString("MMM dd, yyyy h:mm tt"),
                    description = n.Description,
                    attachmentFileName = n.AttachmentFileName
                })
                .ToListAsync();

            return Json(notes);
        }

        // ==========================================
        // AJAX: SAVE NOTE FROM OFFCANVAS
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> SaveNoteAjax(int accountId, string description, string ownerId, IFormFile attachment)
        {
            try
            {
                var note = new Notes
                {
                    AccountId = accountId,
                    NoteOwnerId = string.IsNullOrWhiteSpace(ownerId) ? null : ownerId,
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

                // Get owner name to pass back to UI
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
        // ==========================================
        // CREATE: GET (Opens the blank form)
        // ==========================================
        public IActionResult Create()
        {
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");
            return View();
        }

        // ==========================================
        // CREATE: POST (Saves new data to the DB)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Account account, string[] SavedNoteOwner, string[] SavedNoteDateTime, string[] SavedNoteDesc, List<IFormFile> SavedNoteFiles)
        {
            account.DateOfEntry = DateTime.Now;
            ModelState.Remove("DateOfEntry");


            if (ModelState.IsValid)
            {
                _context.Add(account);
                await _context.SaveChangesAsync();

                // 🚀 UPDATED CALL: Passing 6 parameters including "Account" and SavedNoteFiles
                await SaveNotesAsync(account.Id, "Account", SavedNoteOwner, SavedNoteDateTime, SavedNoteDesc, SavedNoteFiles);

                // TRIGGER AUTO-CONTACT CREATION CHECK
                await CheckAndCreateContactFromAccountAsync(account);

                return RedirectToAction(nameof(Index));
            }

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");
            return View(account);
        }
        // ==========================================
        // EDIT: GET (Fetches specific record & automatically linked data)
        // ==========================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var account = await _context.Accounts.FindAsync(id);
            if (account == null) return NotFound();

            // 1. Fetch Existing Notes
            ViewBag.ExistingNotes = await _context.Note
                .Include(n => n.NoteOwner)
                .Where(n => n.AccountId == id)
                .OrderByDescending(n => n.CreatedDateTime)
                .ToListAsync();

            // 2. Fetch Automatically Linked Leads (Contacts)
            var relatedLeads = await _context.Leads
                .Where(l => l.AccountId == id)
                .ToListAsync();

            ViewBag.RelatedLeads = relatedLeads;

            // 🚀 NEW: Safely pull the LeadStatus from the linked Lead into the Account
            var linkedLeadWithStatus = relatedLeads.FirstOrDefault(l => !string.IsNullOrEmpty(l.LeadStatus));
            if (linkedLeadWithStatus != null)
            {
                account.LeadStatus = linkedLeadWithStatus.LeadStatus;
            }

            // 3. Fetch Automatically Linked Deals
            ViewBag.RelatedDeals = await _context.Set<Deal>()
                .Where(d => d.AccountId == id)
                .ToListAsync();

            // 4. Fetch Automatically Linked Tasks
            ViewBag.RelatedTasks = await _context.Set<Radar_CRM.Models.Task>()
                .Where(t => t.AccountId == id)
                .ToListAsync();

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");

            return View(account);
        }
        // ==========================================
        // EDIT: POST (Saves changes back to the DB)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Account account, string[] SavedNoteOwner, string[] SavedNoteDateTime, string[] SavedNoteDesc, List<IFormFile> SavedNoteFiles)
        {
            if (id != account.Id) return NotFound();

            // PROFILE COMPLETION CHECK
            bool isProfileIncomplete =
                string.IsNullOrWhiteSpace(account.IsHomeopathicDoctor) || account.IsHomeopathicDoctor == "-None-" || account.IsHomeopathicDoctor == "false" ||
                string.IsNullOrWhiteSpace(account.WorkType) || account.WorkType == "-None-" || account.WorkType == "false" ||
                string.IsNullOrWhiteSpace(account.ClinicType) || account.ClinicType == "-None-" || account.ClinicType == "false" ||
                string.IsNullOrWhiteSpace(account.HasComputer) || account.HasComputer == "-None-" || account.HasComputer == "false";

            if (isProfileIncomplete && (string.IsNullOrWhiteSpace(account.ProfilePendingReason) || account.ProfilePendingReason == "-None-"))
            {
                // Attaches the error to the field so the view blocks the save
                ModelState.AddModelError("ProfilePendingReason", "Your profile is incomplete. If you still want to save, you must provide a Profile Pending Reason.");

                // Use TempData to trigger an alert popup on the frontend
                TempData["ErrorMessage"] = "Your profile is incomplete. If you still want to save, you must provide a Profile Pending Reason.";
            }

            // Keep your existing if (ModelState.IsValid) block below this...

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(account);
                    await _context.SaveChangesAsync();

                    // 🚀 UPDATED CALL: Passing 6 parameters including "Account" and SavedNoteFiles
                    await SaveNotesAsync(account.Id, "Account", SavedNoteOwner, SavedNoteDateTime, SavedNoteDesc, SavedNoteFiles);

                    // TRIGGER AUTO-CONTACT CREATION CHECK 
                    await CheckAndCreateContactFromAccountAsync(account);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountExists(account.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ExistingNotes = await _context.Note
     .Where(n => n.AccountId == id)
     .OrderByDescending(n => n.CreatedDateTime)
     .ToListAsync();

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.VendorsList = new SelectList(_context.Vendors, "Id", "VendorName");
            return View(account);
        }

        // ==========================================
        // HELPER: Save Notes & Files to C Drive
        // ==========================================
        private async Task SaveNotesAsync(int recordId, string moduleName, string[] owners, string[] dateTimes, string[] descs, List<IFormFile> files)
        {
            string uploadPath = @"C:\CRM_Files\Notes";

            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            // 1. Fetch all valid User IDs from the database to be 100% certain
            var validUserIds = await _context.Users.Select(u => u.Id).ToListAsync();

            if (descs != null && descs.Length > 0)
            {
                for (int i = 0; i < descs.Length; i++)
                {
                    // Only save if there is text OR a file attached
                    if (!string.IsNullOrWhiteSpace(descs[i]) || (files != null && i < files.Count && files[i] != null && files[i].Length > 0))
                    {
                        // 2. BULLETPROOF FOREIGN KEY FIX:
                        // Extract whatever the frontend sent (could be an ID, could be "System User", could be empty)
                        string incomingOwner = (owners != null && owners.Length > i) ? owners[i]?.Trim() : null;

                        // 3. Only assign the ID if it STRICTLY exists in the database. Otherwise, force it to NULL.
                        string finalOwnerId = null;
                        if (!string.IsNullOrEmpty(incomingOwner) && validUserIds.Contains(incomingOwner))
                        {
                            finalOwnerId = incomingOwner;
                        }

                        var newNote = new Notes
                        {
                            AccountId = recordId,
                            NoteOwnerId = finalOwnerId, // Guaranteed to either be a real User ID or safely NULL
                            CreatedDateTime = dateTimes != null && dateTimes.Length > i && DateTime.TryParse(dateTimes[i], out DateTime parsedDate) ? parsedDate : DateTime.Now,
                            Description = descs[i]
                        };

                        // Handle File Attachment
                        if (files != null && i < files.Count && files[i] != null && files[i].Length > 0)
                        {
                            var file = files[i];

                            string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                            string filePath = Path.Combine(uploadPath, fileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            newNote.AttachmentFileName = file.FileName;
                            newNote.AttachmentFilePath = filePath;
                        }

                        _context.Note.Add(newNote);
                    }
                }
                await _context.SaveChangesAsync();
            }
        }

        // ==========================================
        // ACTION: Download/View Saved File
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> DownloadNoteFile(int noteId)
        {
            var note = await _context.Note.FindAsync(noteId);
            if (note == null || string.IsNullOrEmpty(note.AttachmentFilePath) || !System.IO.File.Exists(note.AttachmentFilePath))
            {
                return NotFound("File not found on server.");
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(note.AttachmentFilePath, FileMode.Open, FileAccess.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, "application/octet-stream", note.AttachmentFileName);
        }

        // ==========================================
        // AUTOMATION: Condition Check & Record Creation
        // ==========================================
        private async Task CheckAndCreateContactFromAccountAsync(Account acc)
        {
            // 1. Specific Condition: Must be explicitly marked as 'Qualified'
            if (acc.QualificationStatus != "Qualified") return;

            // 2. Count the specific required fields
            int filledFieldsCount = 0;

            if (!string.IsNullOrWhiteSpace(acc.IsHomeopathicDoctor)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.WorkType)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.HasComputer)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.ClinicType)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.Qualification)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.CollegeName)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.YearOfPassing)) filledFieldsCount++;
            if (acc.TotalExperience.HasValue) filledFieldsCount++;
            if (acc.YearsOfPractice.HasValue) filledFieldsCount++;
            if (acc.AveragePatientFee.HasValue) filledFieldsCount++;
            if (acc.NumberOfClinics.HasValue) filledFieldsCount++;
            if (acc.DateOfBirth.HasValue) filledFieldsCount++;
            if (acc.Age.HasValue) filledFieldsCount++;
            if (acc.PatientsPerDay.HasValue) filledFieldsCount++;

            bool hasComputer = acc.HasComputer != "No";
            bool isHomeopath = acc.IsHomeopathicDoctor != "No" && acc.IsHomeopathicDoctor != "नहीं";

            // 3. Determine Profile Status based on logic
            string profileStatus = "Not Completed";

            if (filledFieldsCount > 4 && hasComputer && isHomeopath)
            {
                profileStatus = "Strongly Completed";
            }
            else if (filledFieldsCount == 4 && hasComputer && isHomeopath)
            {
                profileStatus = "Completed";
            }
            else if (filledFieldsCount >= 4 && (!hasComputer || !isHomeopath))
            {
                profileStatus = "Not-Qualified";
            }

            // 4. Update the Account record silently
            acc.Profilestatus = profileStatus;
            acc.ProfileRate = filledFieldsCount;

            _context.Accounts.Update(acc);
            await _context.SaveChangesAsync();

            // 5. Condition Check: Automatically trigger Contact creation if conditions are met
            if (profileStatus == "Strongly Completed" || profileStatus == "Completed")
            {
                // Duplicate Check: Ensure the record doesn't already exist in Contacts
                bool recordExists = await _context.Leads.AnyAsync(c => c.AccountId == acc.Id || c.MobileNumber == acc.MobileNumber);
                if (recordExists) return;

                // ==========================================
                // OWNER ASSIGNMENT LOGIC 
                // ==========================================
                string assignedOwnerId = acc.AccountOwnerId;

                // 🚀 FIX: Ensure we retrieve the user safely to grab their full name string
                var ownerUser = await _context.Users.FindAsync(acc.AccountOwnerId);
                string ownerFullName = ownerUser?.fullName ?? ownerUser?.FirstName ?? "System User";

                // 🚀 FIX: Removed Vishakha from here. Now ONLY Mamta assigns to Aman. 
                // Vishakha will keep her own ID naturally.
                if (ownerFullName.Contains("Mamta", StringComparison.OrdinalIgnoreCase))
                {
                    var amanUser = await _context.Users.FirstOrDefaultAsync(u => u.FirstName.Contains("Aman"));
                    if (amanUser != null)
                    {
                        assignedOwnerId = amanUser.Id;
                        ownerFullName = amanUser.fullName ?? amanUser.FirstName; // Capture Aman's name
                    }
                }
                // Creating as a Contact Record within the Lead model structure
                var newContact = new Lead
                {
                    // --- Core Identifiers ---
                    ContactName = acc.ContactPersonName ?? "Unknown Contact",
                    LeadName = acc.AccountName ?? "Unknown Lead", // Required in UI
                    AccountId = acc.Id,

                    // --- Ownership Mapping ---
                    LeadOwnerId = assignedOwnerId,
                    AccountOwnerId = acc.AccountOwnerId,
                    CoOwnerId = acc.CoOwnerId,

                    // --- Source Mapping ---
                    DataSources = acc.DataSource,
                    CampaignSource = acc.DataSource,
                    GroupName = acc.GroupName, // Mapped here as well in case the UI expects it

                    // --- Basic Details ---
                    MobileNumber = acc.MobileNumber,
                    AlternateMobile = acc.AlternateMobile,
                    EmailID = acc.Email,
                    AlternateEmailID = acc.AlternateEmailID,
                    AccountType = acc.AccountType,
                    CurrentStatus = acc.CurrentStatus,
                    MetaCampaignName = acc.MetaCampaignName,
                    SeminarName = acc.SeminarName,

                    // --- Address Mapping ---
                    Addr1_Country = acc.Addr1_Country,
                    Addr1_FlatHouse = acc.Addr1_FlatHouse,
                    Addr1_Street = acc.Addr1_Street,
                    Addr1_City = acc.Addr1_City,
                    Addr1_State = acc.Addr1_State,
                    Addr1_Zip = acc.Addr1_Zip,

                    Addr2_Country = acc.Addr2_Country,
                    Addr2_FlatHouse = acc.Addr2_FlatHouse,
                    Addr2_Street = acc.Addr2_Street,
                    Addr2_City = acc.Addr2_City,
                    Addr2_State = acc.Addr2_State,
                    Addr2_Zip = acc.Addr2_Zip,

                    // --- Professional Mapping ---
                    IsHomeopathicDoctor = acc.IsHomeopathicDoctor,
                    ClinicType = acc.ClinicType,
                    WorkType = acc.WorkType,
                    HasComputer = acc.HasComputer,
                    DateOfBirth = acc.DateOfBirth,
                    Age = acc.Age,
                    YearOfPractice = acc.YearsOfPractice,
                    AveragePatientFee = acc.AveragePatientFee,
                    PatientsPerDay = acc.PatientsPerDay,
                    TotalExperience = acc.TotalExperience,
                    NumberOfClinics = acc.NumberOfClinics,
                    Qualification = acc.Qualification,
                    YearOfPassing = acc.YearOfPassing,
                    CollegeName = acc.CollegeName,

                    // --- Software & Purchases ---
                    CurrentlyUsingSoftware = acc.CurrentlyUsingSoftware,
                    CurrentSoftwareName = acc.CurrentSoftwareName,
                    PurchaseDate = acc.PurchaseDate,
                    PurchaseValue = acc.PurchaseValue,
                    PaymentType = acc.PaymentType,
                    PaymentStatus = acc.PaymentStatus,

                    ContactPerson1 = acc.ContactPerson1,
                    Contact1Phone = acc.Contact1Phone,
                    ContactPerson2 = acc.ContactPerson2,
                    Contact2Phone = acc.Contact2Phone,
                    ContactPerson3 = acc.ContactPerson3,
                    Contact3Phone = acc.Contact3Phone,

                    Description = acc.Description,
                    // --- System Logic Default Fields ---
                    Stage = "Open",
                    CreatedDateAndTime = DateTime.Now,
                    LeadCreatedTime = DateTime.Now,
                    Pipeline = acc.CurrentStatus == "User" ? "Upgrade Software" : "New Software"
                };

                // 1. SAVE THE NEW CONTACT FIRST
                _context.Leads.Add(newContact);
                await _context.SaveChangesAsync(); // This generates the newContact.Id

                // ==========================================
                // 2. 🚀 NEW: COPY/TRANSFER NOTES TO THE NEW CONTACT
                // ==========================================
                var accountNotes = await _context.Note
                     .Where(n => n.AccountId == acc.Id)
                     .AsNoTracking() // Ensures we create new copies
                     .ToListAsync();

                if (accountNotes.Any())
                {
                    // Fetch valid user IDs to prevent Foreign Key crashes if NoteOwnerId is invalid
                    var validUserIds = await _context.Users.Select(u => u.Id).ToListAsync();

                    // Declaring contactNotes so Entity Framework can save it
                    var contactNotes = new List<Notes>();

                    foreach (var note in accountNotes)
                    {
                        contactNotes.Add(new Notes
                        {
                            LeadId = newContact.Id,
                            AccountId = null, // Set to null so it uniquely maps to the new Lead/Contact record

                            // EXACT COPY: Safely transfer Owner, Date, Time, and Text
                            NoteOwnerId = (!string.IsNullOrEmpty(note.NoteOwnerId) && validUserIds.Contains(note.NoteOwnerId)) ? note.NoteOwnerId : null,
                            CreatedDateTime = note.CreatedDateTime,
                            Description = note.Description,

                            AttachmentFileName = note.AttachmentFileName,
                            AttachmentFilePath = note.AttachmentFilePath
                        });
                    }
                    await _context.Note.AddRangeAsync(contactNotes);
                    await _context.SaveChangesAsync();
                }
            }
        }
        // ==========================================
        // DELETE: GET (Fetches record for confirmation)
        // ==========================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var account = await _context.Accounts.FirstOrDefaultAsync(m => m.Id == id);
            if (account == null) return NotFound();

            return View(account);
        }

        // ==========================================
        // DELETE: POST (Actually removes from DB)
        // ==========================================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account != null)
            {
                // 1. Delete Tasks
                var relatedTasks = await _context.Set<Radar_CRM.Models.Task>().Where(t => t.AccountId == id).ToListAsync();
                if (relatedTasks.Any()) _context.Set<Radar_CRM.Models.Task>().RemoveRange(relatedTasks);

                // 2. Delete Deals
                var relatedDeals = await _context.Set<Deal>().Where(d => d.AccountId == id).ToListAsync();
                if (relatedDeals.Any()) _context.Set<Deal>().RemoveRange(relatedDeals);

                // 3. 🚀 FIX: Find Leads, delete their Notes FIRST, then delete Leads
                if (_context.Leads != null)
                {
                    var relatedLeads = await _context.Leads.Where(l => l.AccountId == id).ToListAsync();
                    if (relatedLeads.Any())
                    {
                        var leadIds = relatedLeads.Select(l => l.Id).ToList();

                        // Delete Notes attached to these Leads to satisfy FK_Note_Leads_LeadId
                        if (_context.Note != null)
                        {
                            var leadNotes = await _context.Note.Where(n => n.LeadId != null && leadIds.Contains((int)n.LeadId)).ToListAsync();
                            if (leadNotes.Any()) _context.Note.RemoveRange(leadNotes);
                        }

                        _context.Leads.RemoveRange(relatedLeads);
                    }
                }

                // 4. Delete Notes attached directly to the Account
                if (_context.Note != null)
                {
                    var relatedNotes = await _context.Note.Where(n => n.AccountId == id).ToListAsync();
                    if (relatedNotes.Any()) _context.Note.RemoveRange(relatedNotes);
                }

                // 5. Now it's safe to remove the account!
                _context.Accounts.Remove(account);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> CheckEmailDuplicate(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { isDuplicate = false });
            }

            // REPLACE '_context.Users' with the correct table you are checking against (e.g., _context.Leads)
            bool exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower().Trim());

            return Json(new { isDuplicate = exists });
        }

        // ==========================================
        // BULK DELETE: AJAX POST
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> BulkDelete([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "No records selected." });

            if (_context == null || _context.Accounts == null)
                return Json(new { success = false, message = "Database context is not initialized." });

            try
            {
                var accountsToDelete = await _context.Accounts.Where(a => ids.Contains(a.Id)).ToListAsync();

                if (accountsToDelete != null && accountsToDelete.Any())
                {
                    // 1. Delete Tasks
                    var relatedTasks = await _context.Set<Radar_CRM.Models.Task>().Where(t => t.AccountId != null && ids.Contains((int)t.AccountId)).ToListAsync();
                    if (relatedTasks.Any()) _context.Set<Radar_CRM.Models.Task>().RemoveRange(relatedTasks);

                    // 2. Delete Deals
                    var relatedDeals = await _context.Set<Deal>().Where(d => d.AccountId != null && ids.Contains((int)d.AccountId)).ToListAsync();
                    if (relatedDeals.Any()) _context.Set<Deal>().RemoveRange(relatedDeals);

                    // 3. 🚀 FIX: Find Leads, delete their Notes FIRST, then delete Leads
                    if (_context.Leads != null)
                    {
                        var relatedLeads = await _context.Leads.Where(l => l.AccountId != null && ids.Contains((int)l.AccountId)).ToListAsync();

                        if (relatedLeads != null && relatedLeads.Any())
                        {
                            var leadIds = relatedLeads.Select(l => l.Id).ToList();

                            // Delete Notes attached to these Leads to satisfy FK_Note_Leads_LeadId
                            if (_context.Note != null)
                            {
                                var leadNotes = await _context.Note.Where(n => n.LeadId != null && leadIds.Contains((int)n.LeadId)).ToListAsync();
                                if (leadNotes.Any()) _context.Note.RemoveRange(leadNotes);
                            }

                            _context.Leads.RemoveRange(relatedLeads);
                        }
                    }

                    // 4. Delete Notes attached directly to the Account
                    if (_context.Note != null)
                    {
                        var relatedNotes = await _context.Note.Where(n => n.AccountId != null && ids.Contains((int)n.AccountId)).ToListAsync();
                        if (relatedNotes != null && relatedNotes.Any()) _context.Note.RemoveRange(relatedNotes);
                    }

                    // 5. Finally, remove the Accounts!
                    _context.Accounts.RemoveRange(accountsToDelete);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                Console.WriteLine("DELETE ERROR: " + errorMessage);
                return StatusCode(500, errorMessage);
            }
        }
        // ==========================================
        // CHECK DUPLICATE PHONE VIA AJAX
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> CheckDuplicatePhone(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return Json(new { isDuplicate = false });

            bool exists = await _context.Accounts.AnyAsync(a => a.MobileNumber == phone || a.AlternateMobile == phone);

            return Json(new { isDuplicate = exists });
        }

        [HttpGet]
        public async Task<IActionResult> CheckMobileDuplicate(string mobile)
        {
            if (string.IsNullOrWhiteSpace(mobile))
            {
                return Json(new { success = true, isDuplicate = false });
            }

            try
            {
                string cleanMobile = mobile.Trim();

                // REPLACE '_context.Leads' with the actual table you are checking (e.g., _context.Users or _context.Accounts)
                bool exists = await _context.Leads.AnyAsync(l => l.MobileNumber == cleanMobile);

                return Json(new { success = true, isDuplicate = exists });
            }
            catch (Exception ex)
            {
                // This will capture the exact SQL/Connection error on your live server
                string errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "DB Connection Error: " + errorMessage });
            }
        }

        // ==========================================
        // UPLOAD FILE (With Advanced Error Tracking)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var accountsToInsert = new List<Account>();
            int currentRow = 1; // Start at 1 for the header
            // NEW: Fetch all valid User IDs into a super-fast lookup list
            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id).ToList());

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

                        if (values.Length >= 4)
                        {
                            // 🚀 FIX: Used indexes 1 and 64 to get the actual ID, not the Name string
                            string rawOwnerId = GetVal(values, 1);
                            string rawCoOwnerId = GetVal(values, 64);

                            var newAccount = new Account
                            {
                                // --- Existing Fields ---
                                AccountOwnerId = validUserIds.Contains(rawOwnerId) ? rawOwnerId : null,
                                AccountName = GetVal(values, 3),
                                Email = GetVal(values, 5),
                                AlternateMobile = GetVal(values, 6),
                                MobileNumber = GetVal(values, 7),
                                DataSource = GetVal(values, 8),
                                Description = GetVal(values, 17),
                                IsDuplicated = bool.TryParse(GetVal(values, 62), out bool isDup) && isDup,
                                MetaCampaignName = GetVal(values, 63),
                                CoOwnerId = validUserIds.Contains(rawCoOwnerId) ? rawCoOwnerId : null,
                                CurrentStatus = GetVal(values, 112),
                                AccountType = GetVal(values, 113),
                                ContactPersonName = GetVal(values, 114),
                                ProfilePendingReason = GetVal(values, 115),
                                AlternateEmailID = GetVal(values, 126),
                                QualificationStatus = GetVal(values, 129),
                                SeminarName = GetVal(values, 88),
                                DateOfEntry = DateTime.TryParse(GetVal(values, 95), out DateTime doe) ? doe : DateTime.Now,
                                LeadStatus = GetVal(values, 127),

                                // 🚀 NEW: Address 1 Mapping
                                Addr1_Country = GetVal(values, 96),
                                Addr1_FlatHouse = GetVal(values, 97),
                                Addr1_Street = GetVal(values, 98),
                                Addr1_City = GetVal(values, 99),
                                Addr1_State = GetVal(values, 100),
                                Addr1_Zip = GetVal(values, 101),
                                Addr1_Latitude = GetVal(values, 102),
                                Addr1_Longitude = GetVal(values, 103),

                                // 🚀 NEW: Address 2 Mapping
                                Addr2_Country = GetVal(values, 67),
                                Addr2_FlatHouse = GetVal(values, 68),
                                Addr2_Street = GetVal(values, 69),
                                Addr2_City = GetVal(values, 70),
                                Addr2_State = GetVal(values, 71),
                                Addr2_Zip = GetVal(values, 72),
                                Addr2_Latitude = GetVal(values, 73),
                                Addr2_Longitude = GetVal(values, 74),

                                // 🚀 NEW: Professional Profile (Strings)
                                IsHomeopathicDoctor = GetVal(values, 124),
                                ClinicType = GetVal(values, 110),
                                Qualification = GetVal(values, 86),
                                YearOfPassing = GetVal(values, 77),
                                WorkType = GetVal(values, 123),
                                HasComputer = GetVal(values, 122),
                                CollegeName = GetVal(values, 78),

                                // 🚀 NEW: Professional Profile (Numbers/Dates)
                                YearsOfPractice = int.TryParse(GetVal(values, 75), out int yop) ? yop : null,
                                AveragePatientFee = decimal.TryParse(GetVal(values, 79), out decimal fee) ? fee : null,
                                DateOfBirth = DateTime.TryParse(GetVal(values, 76), out DateTime dob) ? dob : null,
                                PatientsPerDay = int.TryParse(GetVal(values, 104), out int ppd) ? ppd : null,
                                TotalExperience = int.TryParse(GetVal(values, 92), out int exp) ? exp : null,
                                NumberOfClinics = int.TryParse(GetVal(values, 83), out int noc) ? noc : null,
                                Age = int.TryParse(GetVal(values, 82), out int age) ? age : null,

                                // 🚀 NEW: Software & Purchases
                                CurrentlyUsingSoftware = GetVal(values, 111),
                                CurrentSoftwareName = GetVal(values, 85),
                                ProductPurchased = GetVal(values, 106),
                                PurchaseDate = DateTime.TryParse(GetVal(values, 105), out DateTime pDate) ? pDate : null,
                                PurchaseValue = decimal.TryParse(GetVal(values, 107), out decimal pVal) ? pVal : null,
                                PaymentType = GetVal(values, 108),
                                PaymentStatus = GetVal(values, 109),

                                // 🚀 NEW: Additional Contact Persons
                                ContactPerson1 = GetVal(values, 116),
                                ContactPerson2 = GetVal(values, 117),
                                ContactPerson3 = GetVal(values, 119),
                                Contact1Phone = GetVal(values, 118),
                                Contact2Phone = GetVal(values, 120),
                                Contact3Phone = GetVal(values, 121),

                                // 🚀 NEW: Profile Tracking
                                ProfileCompletionPercentage = int.TryParse(GetVal(values, 89), out int pc) ? pc : null,
                                ReferralSource = GetVal(values, 90),
                                InvoiceNumber = GetVal(values, 91),
                                Profilestatus = GetVal(values, 125),
                                ProfileRate = int.TryParse(GetVal(values, 93), out int pr) ? pr : null
                            };

                            accountsToInsert.Add(newAccount);
                        }
                    }
                }

                // Turn off change tracking for fast bulk insert
                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                await _context.Accounts.AddRangeAsync(accountsToInsert);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // This digs into the database to find the EXACT error message
                string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;

                // Returns the row number and the exact error to your JavaScript popup
                return StatusCode(500, $"Failed at Row {currentRow} -> {trueError}");
            }
            finally
            {
                // Always turn tracking back on safely
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
            var result = new System.Collections.Generic.List<string>();
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

        private bool AccountExists(int id)
        {
            return _context.Accounts.Any(e => e.Id == id);
        }
    }
}