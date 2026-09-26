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
// 2. Force your database model to use the alias 'CrmTaskModel'
using CrmTaskModel = Radar_CRM.Models.Task;
// 1. Force the word 'Task' to always mean the System's async Task
using Task = System.Threading.Tasks.Task;

namespace Radar_CRM.Controllers
{
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TasksController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // INDEX
        // ==========================================
        public async Task<IActionResult> Index(int page = 1, string search = "", string sortCol = "Id", string sortDir = "desc")
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Users");

            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            if (currentUser == null) return RedirectToAction("Login", "Users");

            int pageSize = 100;
            var query = _context.Tasks.AsQueryable();

            // 🚀 ADMIN CHECK BASED ON 'PROFILE'
            bool isAdmin = !string.IsNullOrWhiteSpace(currentUser.Profile) &&
                           (currentUser.Profile.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
                            currentUser.Profile.Equals("Administrator", StringComparison.OrdinalIgnoreCase));

            // 🚀 THE HIERARCHY LOGIC 
            // Applies ONLY to non-admins. Filters Tasks based on TaskOwner.
            if (!isAdmin)
            {
                var allRoles = await _context.Roles.ToListAsync();
                var visibleRoleIds = new List<int>();

                // Get subordinates
                var subordinateIds = GetSubordinateRoleIds(allRoles, currentUser.RoleId);
                visibleRoleIds.AddRange(subordinateIds);

                // Share with peers if enabled
                var currentUserRoleModel = allRoles.FirstOrDefault(r => r.Id == currentUser.RoleId);
                if (currentUserRoleModel != null && currentUserRoleModel.ShareDataWithPeers && currentUser.RoleId.HasValue)
                {
                    visibleRoleIds.Add(currentUser.RoleId.Value);
                }

                // Get the User IDs belonging to those visible roles
                var visibleUserIds = await _context.Users
                    .Where(u => u.RoleId.HasValue && visibleRoleIds.Contains(u.RoleId.Value))
                    .Select(u => u.Id)
                    .ToListAsync();

                // Always include the current user's own ID
                visibleUserIds.Add(currentUserId);

                // Filter the tasks by TaskOwner
                // Note: Ensure 'TaskOwner' is the field storing the User ID. If it's named 'TaskOwnerId', change it below.
                query = query.Where(t => visibleUserIds.Contains(t.TaskOwner));
            }

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(t =>
                    (t.Subject != null && t.Subject.ToLower().Contains(searchLower)) ||
                    (t.TaskOwner != null && t.TaskOwner.ToLower().Contains(searchLower)) ||
                    (t.Status != null && t.Status.ToLower().Contains(searchLower))
                );
            }

            if (sortDir == "desc")
            {
                query = sortCol switch
                {
                    "Subject" => query.OrderByDescending(t => t.Subject),
                    "TaskOwner" => query.OrderByDescending(t => t.TaskOwner),
                    "DueDate" => query.OrderByDescending(t => t.DueDate),
                    "Status" => query.OrderByDescending(t => t.Status),
                    _ => query.OrderByDescending(t => t.Id)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "Subject" => query.OrderBy(t => t.Subject),
                    "TaskOwner" => query.OrderBy(t => t.TaskOwner),
                    "DueDate" => query.OrderBy(t => t.DueDate),
                    "Status" => query.OrderBy(t => t.Status),
                    _ => query.OrderBy(t => t.Id)
                };
            }

            var totalRecords = await query.CountAsync();
            var tasks = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            ViewBag.TotalRecords = totalRecords;

            return View(tasks);
        }

        // ==========================================
        // HELPER METHOD (Add this to the bottom of the TasksController)
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

            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.LeadsList = new SelectList(_context.Leads, "Id", "LeadName");

            return View(new CrmTaskModel());
        }

        // ==========================================
        // CREATE: POST
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CrmTaskModel taskModel, string[] SavedNoteDesc, string[] SavedNoteDateTime)
        {
            // AGGRESSIVE VALIDATION CLEAR:
            var keysToKeep = new[] { "Subject" };
            var keysToRemove = ModelState.Keys.Where(k => !keysToKeep.Contains(k)).ToList();

            foreach (var key in keysToRemove)
            {
                ModelState.Remove(key);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // 🚀 FIX: Assign Creation Audit Fields dynamically
                    string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    string currentUserName = User.FindFirstValue(ClaimTypes.Name);

                    taskModel.CreatedTime = DateTime.Now;
                    taskModel.CreatedById = currentUserId;
                    taskModel.CreatedBy = currentUserName ?? "System";

                    if (!string.IsNullOrEmpty(taskModel.TaskOwnerId))
                    {
                        var user = await _context.Users.FindAsync(taskModel.TaskOwnerId);
                        if (user != null) taskModel.TaskOwner = user.fullName;
                    }

                    _context.Tasks.Add(taskModel);
                    await _context.SaveChangesAsync();

                    await SaveNotesAsync(taskModel.Id, SavedNoteDesc, SavedNoteDateTime);

                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    // 🔥 SHOW THE TRUE ERROR ON THE UI
                    var trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    ModelState.AddModelError(string.Empty, "Database Save Failed: " + trueError);
                }
            }

            // Fallback: Restore names for the Select2 dropdowns so they don't break if validation fails
            if (!string.IsNullOrEmpty(taskModel.TaskOwnerId))
            {
                var user = await _context.Users.FindAsync(taskModel.TaskOwnerId);
                if (user != null) taskModel.TaskOwner = user.fullName;
            }

            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.LeadsList = new SelectList(_context.Leads, "Id", "LeadName");


            return View(taskModel);
        }
        // ==========================================
        // AJAX LAZY-LOAD LOOKUPS FOR SELECT2
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> SearchUsers(string q)
        {
            var query = _context.Users.AsQueryable();
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(u => u.fullName.Contains(q));
            }
            var results = await query.Take(30).Select(u => new { id = u.Id, text = u.fullName }).ToListAsync();
            return Json(new { results });
        }

        [HttpGet]
        public async Task<IActionResult> SearchAccounts(string q)
        {
            var query = _context.Accounts.AsQueryable();
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(a => a.AccountName.Contains(q));
            }
            var results = await query.Take(30).Select(a => new { id = a.Id, text = a.AccountName }).ToListAsync();
            return Json(new { results });
        }

        [HttpGet]
        public async Task<IActionResult> SearchLeads(string q)
        {
            var query = _context.Leads.AsQueryable();
            if (!string.IsNullOrEmpty(q))
            {
                query = query.Where(l => l.LeadName.Contains(q));
            }
            var results = await query.Take(30).Select(l => new { id = l.Id, text = l.LeadName }).ToListAsync();
            return Json(new { results });
        }

        // ==========================================
        // EDIT: GET
        // ==========================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var taskModel = await _context.Tasks.FindAsync(id);
            if (taskModel == null) return NotFound();

            // Pre-load values
            if (!string.IsNullOrEmpty(taskModel.TaskOwnerId))
            {
                var user = await _context.Users.FindAsync(taskModel.TaskOwnerId);
                if (user != null) ViewBag.TaskOwnerName = user.fullName;
            }

            ViewBag.ExistingNotes = await _context.Note.Where(n => n.TaskId == id).OrderByDescending(n => n.CreatedDateTime).ToListAsync();
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.LeadsList = new SelectList(_context.Leads, "Id", "LeadName");




            return View(taskModel);
        }

        // ==========================================
        // EDIT: POST
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CrmTaskModel taskModel, string[] SavedNoteDesc, string[] SavedNoteDateTime)
        {
            if (id != taskModel.Id) return NotFound();

            var keysToKeep = new[] { "Subject" };
            var keysToRemove = ModelState.Keys.Where(k => !keysToKeep.Contains(k)).ToList();

            foreach (var key in keysToRemove)
            {
                ModelState.Remove(key);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // 🚀 FIX: Assign Modification Audit Fields dynamically
                    string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    string currentUserName = User.FindFirstValue(ClaimTypes.Name);

                    taskModel.ModifiedTime = DateTime.Now;
                    taskModel.ModifiedById = currentUserId;
                    taskModel.ModifiedBy = currentUserName ?? "System";

                    if (!string.IsNullOrEmpty(taskModel.TaskOwnerId))
                    {
                        var user = await _context.Users.FindAsync(taskModel.TaskOwnerId);
                        if (user != null) taskModel.TaskOwner = user.fullName;
                    }

                    _context.Update(taskModel);
                    await _context.SaveChangesAsync();

                    await SaveNotesAsync(taskModel.Id, SavedNoteDesc, SavedNoteDateTime);

                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TaskExists(taskModel.Id)) return NotFound();
                    else throw;
                }
                catch (Exception ex)
                {
                    // 🔥 SHOW THE TRUE ERROR ON THE UI
                    var trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    ModelState.AddModelError(string.Empty, "Database Update Failed: " + trueError);
                }
            }

            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.LeadsList = new SelectList(_context.Leads, "Id", "LeadName");


            return View(taskModel);
        }

        [HttpPost]
        [DisableRequestSizeLimit] // Prevents 30MB upload limit errors
        [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            // 🚀 Explicitly specify Radar_CRM.Models.Task 
            var tasksToInsert = new List<Radar_CRM.Models.Task>();
            int currentRow = 1;
            int skippedCount = 0; // 🚀 Added to track skipped records

            // Load Maps for Polymorphic linking
            var accountLookup = _context.Accounts.Where(a => a.ZohoRecordId != null).ToDictionary(a => a.ZohoRecordId, a => a.Id);
            var leadLookup = _context.Leads.Where(l => l.ZohoRecordId != null).ToDictionary(l => l.ZohoRecordId, l => l.Id);
            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id));

            // 🚀 NEW: Load existing Tasks ZohoRecordIds to skip duplicates ultra-fast
            var existingTasks = new HashSet<string>(
                await _context.Tasks
                    .Where(t => !string.IsNullOrEmpty(t.ZohoRecordId))
                    .Select(t => t.ZohoRecordId)
                    .ToListAsync()
            );

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

                    while (!reader.EndOfStream)
                    {
                        currentRow++;
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);

                        string zohoRecordId = GetValSafe(values, "RecordId");

                        // 🚀 SKIP LOGIC: If the task already exists in the database, skip it entirely
                        if (!string.IsNullOrEmpty(zohoRecordId) && existingTasks.Contains(zohoRecordId))
                        {
                            skippedCount++;
                            continue;
                        }

                        // Zoho Tasks link using "Related To.id" or "Contact Name.id"
                        string relatedId = GetValSafe(values, "RelatedTo.id");
                        string contactId = GetValSafe(values, "ContactName.id");
                        string targetZohoId = !string.IsNullOrEmpty(relatedId) ? relatedId : contactId;

                        // Map the text fields so TaskOwner, CreatedBy, etc., are not Null
                        var newTask = new Radar_CRM.Models.Task
                        {
                            ZohoRecordId = zohoRecordId,
                            Subject = GetValSafe(values, "Subject"),
                            Status = GetValSafe(values, "Status"),
                            Priority = GetValSafe(values, "Priority"),
                            DueDate = DateTime.TryParse(GetValSafe(values, "DueDate"), out DateTime due) ? due : null,

                            // IDs
                            TaskOwnerId = validUserIds.Contains(GetValSafe(values, "TaskOwner.id")) ? GetValSafe(values, "TaskOwner.id") : null,
                            CreatedById = GetValSafe(values, "CreatedBy.id"),
                            ModifiedById = GetValSafe(values, "ModifiedBy.id"),

                            // Text Names (Fixes the Null issues)
                            TaskOwner = GetValSafe(values, "TaskOwner"),
                            CreatedBy = GetValSafe(values, "CreatedBy"),
                            ModifiedBy = GetValSafe(values, "ModifiedBy"),
                            RelatedTo = GetValSafe(values, "RelatedTo"),

                            // Dates
                            CreatedTime = DateTime.TryParse(GetValSafe(values, "CreatedTime"), out DateTime ct) ? ct : DateTime.Now,
                            ModifiedTime = DateTime.TryParse(GetValSafe(values, "ModifiedTime"), out DateTime mt) ? mt : null,
                        };

                        // DYNAMIC POLYMORPHIC LINKING
                        if (!string.IsNullOrEmpty(targetZohoId))
                        {
                            if (accountLookup.TryGetValue(targetZohoId, out int aId)) newTask.AccountId = aId;
                            else if (leadLookup.TryGetValue(targetZohoId, out int lId)) newTask.LeadId = lId;
                        }

                        // 🚀 Add to HashSet to prevent duplicate inserts if the CSV file itself contains duplicate rows
                        if (!string.IsNullOrEmpty(zohoRecordId))
                        {
                            existingTasks.Add(zohoRecordId);
                        }

                        tasksToInsert.Add(newTask);
                    }
                }
                _context.ChangeTracker.AutoDetectChangesEnabled = false;
                await _context.Tasks.AddRangeAsync(tasksToInsert);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed at Row {currentRow} -> {ex.Message}");
            }
            finally { _context.ChangeTracker.AutoDetectChangesEnabled = true; }

            // 🚀 Return counts so the UI knows what happened
            return Json(new { success = true, insertedCount = tasksToInsert.Count, skippedCount = skippedCount });
        }

        // 🚀 FIX 2: Add the missing ParseCsvLine method inside the controller class
        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var currentItem = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        currentItem.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentItem.ToString());
                    currentItem.Clear();
                }
                else
                {
                    currentItem.Append(c);
                }
            }
            result.Add(currentItem.ToString());
            return result.ToArray();
        }


        // ==========================================
        // BULK DELETE
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> BulkDelete([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any()) return Json(new { success = false });

            var tasksToDelete = await _context.Tasks.Where(t => ids.Contains(t.Id)).ToListAsync();
            if (tasksToDelete.Any())
            {
                _context.Tasks.RemoveRange(tasksToDelete);
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }

        // ==========================================
        // HELPER METHOD FOR NOTES
        // ==========================================
        private async Task SaveNotesAsync(int taskId, string[] descs, string[] dateTimes)
        {
            if (descs != null && descs.Length > 0)
            {
                // Fetch the logged-in user to act as the Note Owner
                string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var validUserIds = await _context.Users.Select(u => u.Id).ToListAsync();

                // Safely assign the Owner ID, falling back to NULL instead of an invalid text string
                string safeOwnerId = validUserIds.Contains(currentUserId) ? currentUserId : null;

                for (int i = 0; i < descs.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(descs[i]))
                    {
                        var newNote = new Notes
                        {
                            TaskId = taskId,
                            Description = descs[i],
                            CreatedDateTime = dateTimes != null && dateTimes.Length > i && DateTime.TryParse(dateTimes[i], out DateTime parsed) ? parsed : DateTime.Now,

                            // 🚀 CRITICAL FIX: Assigning actual User ID instead of "System User"
                            NoteOwnerId = safeOwnerId
                        };
                        _context.Note.Add(newNote);
                    }
                }
                await _context.SaveChangesAsync();
            }
        }

        private bool TaskExists(int id)
        {
            return _context.Tasks.Any(e => e.Id == id);
        }
    }
}