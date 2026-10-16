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
using System.Text.Json;

using CrmTaskModel = Radar_CRM.Models.Task;
using Task = System.Threading.Tasks.Task;

namespace Radar_CRM.Controllers
{
    public class TasksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        // 🚀 FIX: Add the configuration parameter inside these parentheses
        public TasksController(ApplicationDbContext context, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration; // Now it successfully maps!
        }

        // ==========================================
        // INDEX
        // ==========================================
        // Inside TasksController class, update the Index method:
        public async Task<IActionResult> Index(int page = 1, string search = "", string sortCol = "Id", string sortDir = "desc")
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Users");

            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUser = await _context.Users.FindAsync(currentUserId);
            if (currentUser == null) return RedirectToAction("Login", "Users");

            // 1. FIX: Supply UsersList for the TaskOwner dropdowns
            ViewBag.UsersList = new SelectList(await _context.Users.ToListAsync(), "Id", "fullName");

            int pageSize = 100;
            var query = _context.Tasks
.Include(t => t.Account)
.Include(t => t.Lead)
.AsQueryable();

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

                // 🚀 FIX: Must compare against TaskOwnerId, not TaskOwner
                query = query.Where(t => visibleUserIds.Contains(t.TaskOwnerId));
            }

            // 2. FIX: Parse and Apply Advanced Filters dynamically
            // 2. FIX: Parse and Apply Advanced Filters dynamically
            string advancedFilters = Request.Query["advancedFilters"];
            if (!string.IsNullOrEmpty(advancedFilters))
            {
                try
                {
                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var filters = System.Text.Json.JsonSerializer.Deserialize<List<AdvancedFilter>>(advancedFilters, options);

                    // 🚀 FIX: Do not drop these filters even if the user didn't type a value
                    var noValueConditions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "is_empty", "is_not_empty", "today", "tomorrow", "yesterday",
                        "till yesterday", "starting tomorrow", "this week", "this month",
                        "this year", "previous week", "previous month", "previous year"
                    };

                    if (filters != null && filters.Any())
                    {
                        foreach (var filter in filters)
                        {
                            string cond = filter.Condition?.ToLower().Trim() ?? "";

                            // Allow predefined dates through the null check
                            if (string.IsNullOrWhiteSpace(filter.Value) && !noValueConditions.Contains(cond)) continue;

                            string col = filter.ColumnName;
                            if (col == "TaskOwner" || col == "Owner") col = "TaskOwnerId";
                            if (col == "CreatedBy") col = "CreatedById";
                            if (col == "ModifiedBy") col = "ModifiedById";

                            string val = filter.Value?.Trim() ?? "";
                            string lowerVal = val.ToLower();

                            if (filter.IsDate)
                            {
                                DateTime today = DateTime.Today;

                                // DYNAMIC EF CORE DATE QUERIES
                                if (cond == "today")
                                {
                                    var next = today.AddDays(1);
                                    query = query.Where(t => EF.Property<DateTime?>(t, col) >= today && EF.Property<DateTime?>(t, col) < next);
                                }
                                else if (cond == "yesterday")
                                {
                                    var yest = today.AddDays(-1);
                                    query = query.Where(t => EF.Property<DateTime?>(t, col) >= yest && EF.Property<DateTime?>(t, col) < today);
                                }
                                else if (cond == "tomorrow")
                                {
                                    var next = today.AddDays(1);
                                    var dayAfter = today.AddDays(2);
                                    query = query.Where(t => EF.Property<DateTime?>(t, col) >= next && EF.Property<DateTime?>(t, col) < dayAfter);
                                }
                                else if (cond == "till yesterday")
                                {
                                    query = query.Where(t => EF.Property<DateTime?>(t, col) < today);
                                }
                                else if (cond == "starting tomorrow")
                                {
                                    var next = today.AddDays(1);
                                    query = query.Where(t => EF.Property<DateTime?>(t, col) >= next);
                                }
                                else if (cond == "this week")
                                {
                                    var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
                                    var endOfWeek = startOfWeek.AddDays(7);
                                    query = query.Where(t => EF.Property<DateTime?>(t, col) >= startOfWeek && EF.Property<DateTime?>(t, col) < endOfWeek);
                                }
                                else if (cond == "this month")
                                {
                                    var startOfMonth = new DateTime(today.Year, today.Month, 1);
                                    var endOfMonth = startOfMonth.AddMonths(1);
                                    query = query.Where(t => EF.Property<DateTime?>(t, col) >= startOfMonth && EF.Property<DateTime?>(t, col) < endOfMonth);
                                }
                                else if (cond == "this year")
                                {
                                    var startOfYear = new DateTime(today.Year, 1, 1);
                                    var endOfYear = startOfYear.AddYears(1);
                                    query = query.Where(t => EF.Property<DateTime?>(t, col) >= startOfYear && EF.Property<DateTime?>(t, col) < endOfYear);
                                }
                                else
                                {
                                    // Custom Selected Dates
                                    DateTime d1 = DateTime.MinValue, d2 = DateTime.MaxValue;
                                    bool isValidDate = false;

                                    if (val.Contains("|"))
                                    {
                                        var dates = val.Split('|');
                                        if (DateTime.TryParse(dates[0], out d1) && DateTime.TryParse(dates[1], out d2)) isValidDate = true;
                                    }
                                    else if (DateTime.TryParse(val, out d1))
                                    {
                                        isValidDate = true;
                                    }

                                    if (isValidDate)
                                    {
                                        if (cond.Contains("not between")) query = query.Where(t => EF.Property<DateTime?>(t, col) < d1 || EF.Property<DateTime?>(t, col) > d2);
                                        else if (cond.Contains("between")) query = query.Where(t => EF.Property<DateTime?>(t, col) >= d1 && EF.Property<DateTime?>(t, col) <= d2);
                                        else if (cond == "on" || cond == "is") { var next = d1.AddDays(1); query = query.Where(t => EF.Property<DateTime?>(t, col) >= d1 && EF.Property<DateTime?>(t, col) < next); }
                                        else if (cond == "before") query = query.Where(t => EF.Property<DateTime?>(t, col) < d1);
                                        else if (cond == "after") query = query.Where(t => EF.Property<DateTime?>(t, col) > d1);
                                    }
                                }
                            }
                            else
                            {
                                // 🚀 FIX: Apply String / ID Filters, including "is_not" and robust Name-to-ID matching
                                if (col == "Subject")
                                {
                                    if (cond == "is_not" || cond == "does_not_contain")
                                        query = query.Where(t => t.Subject == null || !t.Subject.ToLower().Contains(lowerVal));
                                    else
                                        query = query.Where(t => t.Subject != null && t.Subject.ToLower().Contains(lowerVal));
                                }
                                else if (col == "Status")
                                {
                                    if (cond == "is_not") query = query.Where(t => t.Status != val);
                                    else query = query.Where(t => t.Status == val);
                                }
                                else if (col == "Priority")
                                {
                                    if (cond == "is_not") query = query.Where(t => t.Priority != val);
                                    else query = query.Where(t => t.Priority == val);
                                }
                                else if (col == "TaskOwnerId" || col == "TaskOwner" || col == "Owner")
                                {
                                    // 🚀 CRITICAL FIX: Find the User ID in case the filter sent a Text Name ("Sitara Khan")
                                    var matchedUserIds = _context.Users
                                        .Where(u => u.fullName == val || u.FirstName == val || u.Id == val)
                                        .Select(u => u.Id)
                                        .ToList();

                                    if (cond == "is_not")
                                        query = query.Where(t => !matchedUserIds.Contains(t.TaskOwnerId) && t.TaskOwner != val);
                                    else
                                        query = query.Where(t => matchedUserIds.Contains(t.TaskOwnerId) || t.TaskOwner == val);
                                }
                                else if (col == "CreatedById" || col == "CreatedBy")
                                {
                                    var matchedUserIds = _context.Users.Where(u => u.fullName == val || u.FirstName == val || u.Id == val).Select(u => u.Id).ToList();

                                    if (cond == "is_not")
                                        query = query.Where(t => !matchedUserIds.Contains(t.CreatedById) && t.CreatedBy != val);
                                    else
                                        query = query.Where(t => matchedUserIds.Contains(t.CreatedById) || t.CreatedBy == val);
                                }
                                else if (col == "ModifiedById" || col == "ModifiedBy")
                                {
                                    var matchedUserIds = _context.Users.Where(u => u.fullName == val || u.FirstName == val || u.Id == val).Select(u => u.Id).ToList();

                                    if (cond == "is_not")
                                        query = query.Where(t => !matchedUserIds.Contains(t.ModifiedById) && t.ModifiedBy != val);
                                    else
                                        query = query.Where(t => matchedUserIds.Contains(t.ModifiedById) || t.ModifiedBy == val);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Filter Parse Error: " + ex.Message);
                }
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

            var taskIds = tasks.Select(t => t.Id).ToList();
            ViewBag.NoteCounts = await _context.Note
                .Where(n => n.TaskId != null && taskIds.Contains(n.TaskId.Value))
                .GroupBy(n => n.TaskId.Value)
                .ToDictionaryAsync(g => g.Key, g => g.Count());

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
        public async Task<IActionResult> Create()
        {
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.LeadsList = new SelectList(_context.Leads, "Id", "LeadName");

            // 1. Get the currently logged-in user's ID
            string loggedInUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var model = new CrmTaskModel
            {
                TaskOwnerId = loggedInUserId
            };

            // 2. 🚀 FIX FOR SELECT2: Fetch the user's name so the dropdown shows text, not just an empty ID
            if (!string.IsNullOrEmpty(loggedInUserId))
            {
                var user = await _context.Users.FindAsync(loggedInUserId);
                if (user != null)
                {
                    ViewBag.TaskOwnerName = user.fullName;
                    model.TaskOwner = user.fullName;
                }
            }

            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> GetNotes(int taskId)
        {
            var notes = await _context.Note
                .Where(n => n.TaskId == taskId)
                .OrderByDescending(n => n.CreatedDateTime)
                .Select(n => new {
                    // 🚀 Added fallback to FirstName to match your SaveNoteAjax logic perfectly
                    ownerName = n.NoteOwner != null ? (n.NoteOwner.fullName ?? n.NoteOwner.FirstName) : "System User",
                    createdDateTime = n.CreatedDateTime.ToString("MMM dd, yyyy h:mm tt"),
                    noteTitle = n.NoteTitle,
                    description = n.Description,
                    attachmentFileName = n.AttachmentFileName
                }).ToListAsync();

            return Json(notes);
        }

        [HttpPost]
        public async Task<IActionResult> SaveNoteAjax(int taskId, string noteTitle, string description, string ownerId, IFormFile attachment)
        {
            try
            {
                var note = new Notes
                {
                    TaskId = taskId, // Mapped specifically to Task
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

            // Auto-search for matching GUID prefixes to guarantee download works
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

                    // 🚀 NEW: Auto-Populate RelatedTo and RelatedToId based on attached Account or Lead
                    if (taskModel.AccountId.HasValue && taskModel.AccountId.Value > 0)
                    {
                        var account = await _context.Accounts.FindAsync(taskModel.AccountId.Value);
                        if (account != null)
                        {
                            taskModel.RelatedToId = account.Id.ToString();
                            taskModel.RelatedTo = account.AccountName;
                        }
                    }
                    else if (taskModel.LeadId.HasValue && taskModel.LeadId.Value > 0)
                    {
                        var lead = await _context.Leads.FindAsync(taskModel.LeadId.Value);
                        if (lead != null)
                        {
                            taskModel.RelatedToId = lead.Id.ToString();
                            taskModel.RelatedTo = string.IsNullOrWhiteSpace(lead.LeadName) ? "Unnamed Lead" : lead.LeadName;
                        }
                    }

                    _context.Tasks.Add(taskModel);
                    await _context.SaveChangesAsync();

                    await SaveNotesAsync(taskModel.Id, SavedNoteDesc, SavedNoteDateTime);

                    return RedirectToAction(nameof(Edit), new { id = taskModel.Id });
                }
                catch (Exception ex)
                {
                    var trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    ModelState.AddModelError(string.Empty, "Database Save Failed: " + trueError);
                }
            }

            if (!string.IsNullOrEmpty(taskModel.TaskOwnerId))
            {
                var user = await _context.Users.FindAsync(taskModel.TaskOwnerId);
                if (user != null) taskModel.TaskOwner = user.fullName;
            }

            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.LeadsList = new SelectList(_context.Leads, "Id", "LeadName");
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");

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
        [HttpGet]
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

            ViewBag.ExistingNotes = await _context.Note
                .Where(n => n.TaskId == id)
                .OrderByDescending(n => n.CreatedDateTime)
                .ToListAsync();

            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.LeadsList = new SelectList(_context.Leads, "Id", "LeadName");

            // 🚀 DEFAULT FALLBACKS FOR SIDEBAR
            ViewBag.RelatedEntityName = "No Related Record";
            ViewBag.RelatedEntityId = null;
            ViewBag.RelatedEntityUrl = "#";
            ViewBag.RelatedEntityType = "Record";

            ViewBag.Budget = "-";
            ViewBag.LeadOwnerName = "-";
            ViewBag.LeadMobile = "-";
            ViewBag.LeadEmail = "-";
            ViewBag.LeadSource = "-";
            ViewBag.LeadStatusValue = "-";

            // 🚀 FIX: FETCH ACCOUNT DETAILS & FILL SIDEBAR
            // 🚀 FIX: FETCH ACCOUNT DETAILS & FILL SIDEBAR
            if (taskModel.AccountId.HasValue && taskModel.AccountId.Value > 0)
            {
                var account = await _context.Accounts
                    .Include(a => a.AccountOwner) // Load Account Owner Name
                    .FirstOrDefaultAsync(a => a.Id == taskModel.AccountId.Value);

                if (account != null)
                {
                    ViewBag.RelatedEntityName = account.AccountName;
                    ViewBag.RelatedEntityId = account.Id;
                    ViewBag.RelatedEntityUrl = Url.Action("Edit", "Accounts", new { id = account.Id });
                    ViewBag.RelatedEntityType = "Account";

                    // 🚀 FIX: Safely set Budget to "-" since Accounts don't have ExpectedRevenue
                    ViewBag.Budget = "-";
                    ViewBag.LeadOwnerName = account.AccountOwner?.fullName ?? "System User";
                    ViewBag.LeadMobile = !string.IsNullOrWhiteSpace(account.MobileNumber) ? account.MobileNumber : "-";
                    ViewBag.LeadEmail = !string.IsNullOrWhiteSpace(account.Email) ? account.Email : "-";
                    ViewBag.LeadSource = !string.IsNullOrWhiteSpace(account.DataSource) ? account.DataSource : "-";
                    ViewBag.LeadStatusValue = !string.IsNullOrWhiteSpace(account.CurrentStatus) ? account.CurrentStatus : "-";
                }
            }
            // 🚀 FIX: FETCH LEAD DETAILS & FILL SIDEBAR
            else if (taskModel.LeadId.HasValue && taskModel.LeadId.Value > 0)
            {
                var lead = await _context.Leads
                    .Include(l => l.LeadOwner) // Load Lead Owner Name
                    .FirstOrDefaultAsync(l => l.Id == taskModel.LeadId.Value);

                if (lead != null)
                {
                    ViewBag.RelatedEntityName = string.IsNullOrWhiteSpace(lead.LeadName) ? "Unnamed Lead" : lead.LeadName;
                    ViewBag.RelatedEntityId = lead.Id;
                    ViewBag.RelatedEntityUrl = Url.Action("Edit", "Leads", new { id = lead.Id });
                    ViewBag.RelatedEntityType = "Lead";

                    // Map actual Lead data to sidebar ViewBags
                    ViewBag.Budget = lead.Budget?.ToString() ?? "-";
                    ViewBag.LeadOwnerName = lead.LeadOwner?.fullName ?? "System User";
                    ViewBag.LeadMobile = !string.IsNullOrWhiteSpace(lead.MobileNumber) ? lead.MobileNumber : "-";
                    ViewBag.LeadEmail = !string.IsNullOrWhiteSpace(lead.EmailID) ? lead.EmailID : "-";
                    ViewBag.LeadSource = !string.IsNullOrWhiteSpace(lead.DataSources) ? lead.DataSources : "-";
                    ViewBag.LeadStatusValue = !string.IsNullOrWhiteSpace(lead.LeadStatus) ? lead.LeadStatus : "-";
                }
            }

            return View(taskModel);
        }

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
                    string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    string currentUserName = User.FindFirstValue(ClaimTypes.Name);

                    // =========================================================
                    // 1. REPOPULATE MISSING FIELDS *BEFORE* RUNNING THE TRACKER
                    // =========================================================
                    taskModel.ModifiedTime = DateTime.Now;
                    taskModel.ModifiedById = currentUserId;
                    taskModel.ModifiedBy = currentUserName ?? "System";

                    if (!string.IsNullOrEmpty(taskModel.TaskOwnerId))
                    {
                        var user = await _context.Users.FindAsync(taskModel.TaskOwnerId);
                        if (user != null) taskModel.TaskOwner = user.fullName;
                    }

                    // Auto-Populate RelatedTo and RelatedToId based on attached Account or Lead
                    if (taskModel.AccountId.HasValue && taskModel.AccountId.Value > 0)
                    {
                        var account = await _context.Accounts.FindAsync(taskModel.AccountId.Value);
                        if (account != null)
                        {
                            taskModel.RelatedToId = account.Id.ToString();
                            taskModel.RelatedTo = account.AccountName;
                        }
                    }
                    else if (taskModel.LeadId.HasValue && taskModel.LeadId.Value > 0)
                    {
                        var lead = await _context.Leads.FindAsync(taskModel.LeadId.Value);
                        if (lead != null)
                        {
                            taskModel.RelatedToId = lead.Id.ToString();
                            taskModel.RelatedTo = string.IsNullOrWhiteSpace(lead.LeadName) ? "Unnamed Lead" : lead.LeadName;
                        }
                    }

                    // =========================================================
                    // 2. FETCH ORIGINAL DB RECORD FOR COMPARISON
                    // =========================================================
                    var existingTask = await _context.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskModel.Id);

                    // Preserve creation dates that aren't in the UI
                    if (existingTask != null)
                    {
                        if (string.IsNullOrEmpty(taskModel.CreatedBy)) taskModel.CreatedBy = existingTask.CreatedBy;
                        if (taskModel.CreatedDateAndTime == null || taskModel.CreatedDateAndTime == DateTime.MinValue) taskModel.CreatedDateAndTime = existingTask.CreatedDateAndTime;
                        if (taskModel.CreatedTime == null || taskModel.CreatedTime == DateTime.MinValue) taskModel.CreatedTime = existingTask.CreatedTime;
                    }

                    // =========================================================
                    // 3. 🚀 ZOHO-STYLE TIMELINE TRACKER ENGINE
                    // =========================================================
                    if (existingTask != null)
                    {
                        var timelineEvents = new List<TimelineEvent>();

                        // 🚀 CRITICAL FIX: Exclude system IDs and string-cache fields from spamming the timeline
                        var excludedAuditFields = new[] {
                        "ModifiedBy", "ModifiedById", "ModifiedTime", "CreatedBy", "CreatedTime", "CreatedDateAndTime",
                        "TaskOwner", "RelatedTo", "RelatedToId"
                    };

                        var properties = typeof(CrmTaskModel).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                            .Where(p => !excludedAuditFields.Contains(p.Name) && (!p.PropertyType.IsGenericType || p.PropertyType.GetGenericTypeDefinition() != typeof(List<>)));

                        foreach (var prop in properties)
                        {
                            var oldObj = prop.GetValue(existingTask);
                            var newObj = prop.GetValue(taskModel);

                            string oldVal = oldObj?.ToString()?.Trim() ?? "blank value";
                            string newVal = newObj?.ToString()?.Trim() ?? "blank value";

                            // If the field changed, log it!
                            if (oldVal != newVal && !(oldVal == "blank value" && newVal == ""))
                            {
                                timelineEvents.Add(new TimelineEvent
                                {
                                    EntityType = "Task",
                                    EntityId = taskModel.Id,
                                    EventType = "FieldUpdate",
                                    FieldName = prop.Name,
                                    OldValue = oldVal,
                                    NewValue = newVal,
                                    ActionDescription = $"{prop.Name} was updated from {oldVal} to {newVal}",
                                    CreatedById = currentUserId,
                                    CreatedByName = currentUserName,
                                    CreatedDate = DateTime.Now
                                });
                            }
                        }

                        if (timelineEvents.Any())
                        {
                            await _context.TimelineEvents.AddRangeAsync(timelineEvents);
                        }
                    }
                    // =========================================================

                    // 4. SAVE TO DATABASE
                    // 🚀 NEW: Detect if the owner changed before saving
                    bool ownerChanged = existingTask != null &&
                        !string.Equals(existingTask.TaskOwnerId?.Trim(), taskModel.TaskOwnerId?.Trim(), StringComparison.OrdinalIgnoreCase);

                    // 4. SAVE TO DATABASE
                    _context.Update(taskModel);
                    await _context.SaveChangesAsync();

                    await SaveNotesAsync(taskModel.Id, SavedNoteDesc, SavedNoteDateTime);

                    // 🚀 NEW: Trigger email and in-app notification if owner was changed
                    if (ownerChanged && !string.IsNullOrWhiteSpace(taskModel.TaskOwnerId))
                    {
                        var assignedTaskDict = new Dictionary<int, string> { { taskModel.Id, taskModel.Subject ?? "Unknown Task" } };
                        await NotifyNewOwnerAsync(taskModel.TaskOwnerId, assignedTaskDict);
                    }

                    // Stay on the Edit page instead of redirecting to Index
                    return RedirectToAction(nameof(Edit), new { id = taskModel.Id });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TaskExists(taskModel.Id)) return NotFound();
                    else throw;
                }
                catch (Exception ex)
                {
                    var trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    ModelState.AddModelError(string.Empty, "Database Update Failed: " + trueError);
                }
            }

            // FIX: Repopulate ViewBags if validation fails so the page doesn't crash on reload
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.LeadsList = new SelectList(_context.Leads, "Id", "LeadName");

            // Default Fallbacks
            ViewBag.RelatedEntityName = "No Related Record";
            ViewBag.RelatedEntityId = null;
            ViewBag.RelatedEntityUrl = "#";
            ViewBag.RelatedEntityType = "Record";
            ViewBag.Budget = "-";
            ViewBag.LeadOwnerName = "-";
            ViewBag.LeadMobile = "-";
            ViewBag.LeadEmail = "-";
            ViewBag.LeadSource = "-";
            ViewBag.LeadStatusValue = "-";

            // Re-fetch sidebar data for fallback
            if (taskModel.AccountId.HasValue && taskModel.AccountId.Value > 0)
            {
                var account = await _context.Accounts.Include(a => a.AccountOwner).FirstOrDefaultAsync(a => a.Id == taskModel.AccountId.Value);
                if (account != null)
                {
                    ViewBag.RelatedEntityName = account.AccountName;
                    ViewBag.RelatedEntityId = account.Id;
                    ViewBag.RelatedEntityUrl = Url.Action("Edit", "Accounts", new { id = account.Id });
                    ViewBag.RelatedEntityType = "Account";
                    ViewBag.LeadOwnerName = account.AccountOwner?.fullName ?? "System User";
                    ViewBag.LeadMobile = !string.IsNullOrWhiteSpace(account.MobileNumber) ? account.MobileNumber : "-";
                    ViewBag.LeadEmail = !string.IsNullOrWhiteSpace(account.Email) ? account.Email : "-";
                    ViewBag.LeadSource = !string.IsNullOrWhiteSpace(account.DataSource) ? account.DataSource : "-";
                    ViewBag.LeadStatusValue = !string.IsNullOrWhiteSpace(account.CurrentStatus) ? account.CurrentStatus : "-";
                }
            }
            else if (taskModel.LeadId.HasValue && taskModel.LeadId.Value > 0)
            {
                var lead = await _context.Leads.Include(l => l.LeadOwner).FirstOrDefaultAsync(l => l.Id == taskModel.LeadId.Value);
                if (lead != null)
                {
                    ViewBag.RelatedEntityName = string.IsNullOrWhiteSpace(lead.LeadName) ? "Unnamed Lead" : lead.LeadName;
                    ViewBag.RelatedEntityId = lead.Id;
                    ViewBag.RelatedEntityUrl = Url.Action("Edit", "Leads", new { id = lead.Id });
                    ViewBag.RelatedEntityType = "Lead";
                    ViewBag.Budget = lead.Budget?.ToString() ?? "-";
                    ViewBag.LeadOwnerName = lead.LeadOwner?.fullName ?? "System User";
                    ViewBag.LeadMobile = !string.IsNullOrWhiteSpace(lead.MobileNumber) ? lead.MobileNumber : "-";
                    ViewBag.LeadEmail = !string.IsNullOrWhiteSpace(lead.EmailID) ? lead.EmailID : "-";
                    ViewBag.LeadSource = !string.IsNullOrWhiteSpace(lead.DataSources) ? lead.DataSources : "-";
                    ViewBag.LeadStatusValue = !string.IsNullOrWhiteSpace(lead.LeadStatus) ? lead.LeadStatus : "-";
                }
            }

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
        // 🚀 GLOBAL REMINDER POLLING API
        // ==========================================
        // ==========================================
        // 🚀 GLOBAL REMINDER POLLING API (TIMEZONE PROOF)
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetUpcomingReminders()
        {
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Json(new { success = false });

            // We let the frontend Javascript handle the time math to avoid server TimeZone bugs
            var reminders = await _context.Tasks
                .Where(t => t.TaskOwnerId == currentUserId
                         && t.Status != "Completed"
                         && t.Reminder.HasValue
                         && t.ReminderNotifyType == "Popup") // 🚀 Only pull Popup reminders
                .Select(t => new {
                    id = t.Id,
                    subject = t.Subject,
                    // Send strictly as Local ISO string so JS reads it perfectly
                    reminderTime = t.Reminder.Value.ToString("yyyy-MM-ddTHH:mm:ss")
                })
                .ToListAsync();

            return Json(new { success = true, data = reminders });
        }


        [HttpGet]
        public async Task<IActionResult> GetTodaysTasks()
        {
            string currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var today = DateTime.Today;

            var todaysTasks = await _context.Tasks
                .Where(t => t.TaskOwnerId == currentUserId
                         && t.Status != "Completed"
                         && t.DueDate.HasValue
                         && t.DueDate.Value.Date == today)
                .Select(t => new
                {
                    t.Id,
                    t.Subject,
                    t.Priority,
                    Module = t.LeadId != null ? "Lead" : (t.AccountId != null ? "Account" : "General")
                })
                .ToListAsync();

            return Json(new { success = true, data = todaysTasks });
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

        // ==========================================
        // NOTIFICATION HELPER: Email & In-App Popup
        // ==========================================
        private async System.Threading.Tasks.Task NotifyNewOwnerAsync(string newOwnerId, Dictionary<int, string> assignedTasks)
        {
            if (string.IsNullOrEmpty(newOwnerId) || assignedTasks == null || !assignedTasks.Any()) return;

            var newOwner = await _context.Users.FindAsync(newOwnerId);
            if (newOwner == null || string.IsNullOrEmpty(newOwner.Email)) return;

            string ownerName = newOwner.fullName ?? newOwner.FirstName ?? "Team Member";
            bool isBulk = assignedTasks.Count > 1;

            string subject = isBulk
                ? $"Action Required: {assignedTasks.Count} Tasks Assigned to You"
                : $"Action Required: Task '{assignedTasks.Values.First()}' Assigned to You";

            // Build the live clickable links using Url.Action
            string taskListHtml = string.Join("", assignedTasks.Select(t =>
            {
                string linkUrl = Url.Action("Edit", "Tasks", new { id = t.Key }, Request.Scheme);
                return $"<li style='margin-bottom: 8px;'><a href='{linkUrl}' style='color: #2563eb; text-decoration: none; font-weight: bold;'>{t.Value}</a></li>";
            }));

            string body = $@"
        <div style='font-family: Arial, sans-serif; color: #333;'>
            <h3>Hi {ownerName},</h3>
            <p>You have been assigned as the new owner for the following {(isBulk ? "tasks" : "task")}:</p>
            <ul>{taskListHtml}</ul>
            <p>Please click the link(s) above or log in to the CRM to review your new assignments.</p>
            <br/>
            <p><small>This is an automated notification from Radar CRM.</small></p>
        </div>";

            // 1. Send Email via Resend API
            try
            {
                string apiKey = _configuration["Resend:ApiKey"];
                if (string.IsNullOrEmpty(apiKey)) throw new Exception("Resend API Key is missing in appsettings.json.");

                string senderEmail = "Radar CRM <no-reply@bjaincorp.com>";
                var emailPayload = new Dictionary<string, object>
                {
                    { "from", senderEmail },
                    { "to", new List<string> { newOwner.Email.Trim() } },
                    { "subject", subject },
                    { "html", body }
                };

                using (var client = new System.Net.Http.HttpClient())
                {
                    var requestMessage = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "https://api.resend.com/emails");
                    requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");
                    requestMessage.Content = new System.Net.Http.StringContent(System.Text.Json.JsonSerializer.Serialize(emailPayload), System.Text.Encoding.UTF8, "application/json");

                    var response = await client.SendAsync(requestMessage);
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Resend API Error: {errorContent}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Email Dispatch Failed: {ex.Message}");
            }

            // 2. Create In-App Notification (System Task)
            try
            {
                var systemTask = new CrmTaskModel
                {
                    Subject = isBulk ? $"You have been assigned {assignedTasks.Count} new tasks." : $"You are the new owner of {assignedTasks.Values.First()}.",
                    Status = "Not Started",
                    Priority = "High",
                    TaskOwnerId = newOwnerId,
                    CreatedDateAndTime = DateTime.Now,
                    DueDate = DateTime.Now.AddDays(1)
                };

                _context.Set<CrmTaskModel>().Add(systemTask);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"In-App Notification Failed: {ex.Message}");
            }
        }

        private bool TaskExists(int id)
        {
            return _context.Tasks.Any(e => e.Id == id);
        }
    }
    // Add this class anywhere inside your namespace (e.g., at the bottom of the file)
    public class AdvancedFilter
    {
        public string LogicalOperator { get; set; }
        public string ColumnName { get; set; }
        public string Condition { get; set; }
        public string Value { get; set; }
        public bool IsDate { get; set; }
    }
}