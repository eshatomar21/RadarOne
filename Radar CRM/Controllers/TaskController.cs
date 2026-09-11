using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Radar_CRM.Data;
using Radar_CRM.Models;

// 1. Force the word 'Task' to always mean the System's async Task
using Task = System.Threading.Tasks.Task;

// 2. Force your database model to use the alias 'CrmTaskModel'
using CrmTaskModel = Radar_CRM.Models.Task;

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
            int pageSize = 100;
            var query = _context.Tasks.AsQueryable();

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
        // CREATE: GET
        // ==========================================
        public IActionResult Create()
        {
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
                    taskModel.CreatedTime = DateTime.Now;

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
                    taskModel.ModifiedTime = DateTime.Now;

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

            return View(taskModel);
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
                for (int i = 0; i < descs.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(descs[i]))
                    {
                        var newNote = new Notes
                        {
                            TaskId = taskId,
                            Description = descs[i],
                            CreatedDateTime = dateTimes != null && dateTimes.Length > i && DateTime.TryParse(dateTimes[i], out DateTime parsed) ? parsed : DateTime.Now,
                            NoteOwnerId = "System User"
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