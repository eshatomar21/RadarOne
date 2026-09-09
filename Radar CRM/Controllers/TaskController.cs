using System;
using System.Linq;
using System.Threading.Tasks; // This is the system Task
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;

// CRITICAL: Create an alias for your model so C# doesn't get confused
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

        // GET: Tasks/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Tasks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CrmTaskModel taskModel) // Use the alias here
        {
            if (ModelState.IsValid)
            {
                taskModel.CreatedTime = DateTime.Now;
                _context.Add(taskModel);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(taskModel);
        }

        // GET: Tasks
        public async Task<IActionResult> Index()
        {
            // Fetches all tasks from the database and passes them to your Index view
            var tasks = await _context.Tasks.ToListAsync();
            return View(tasks);
        }

        // GET: Tasks/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var taskModel = await _context.Tasks.FindAsync(id); // Assumes your DbSet is named Tasks
            if (taskModel == null) return NotFound();

            return View(taskModel);
        }

        // POST: Tasks/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CrmTaskModel taskModel) // Use the alias here
        {
            if (id != taskModel.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    taskModel.ModifiedTime = DateTime.Now;
                    _context.Update(taskModel);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TaskExists(taskModel.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(taskModel);
        }

        private bool TaskExists(int id)
        {
            return _context.Tasks.Any(e => e.Id == id);
        }
    }
}