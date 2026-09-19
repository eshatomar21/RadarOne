using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

namespace Radar_CRM.Controllers
{
    public class AssignmentRulesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AssignmentRulesController(ApplicationDbContext context)
        {
            _context = context;
        }


        // GET: AssignmentRules
        public async Task<IActionResult> Index()
        {
            var rules = await _context.AssignmentRules.ToListAsync();
            return View(rules);
        }

        // Add this helper method inside your AssignmentRulesController class
        private string GetSimplifiedType(System.Type type)
        {
            // Handle nullable types (e.g., DateTime?)
            var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

            if (underlyingType == typeof(DateTime)) return "date";
            if (underlyingType == typeof(int) || underlyingType == typeof(decimal) || underlyingType == typeof(double)) return "number";
            if (underlyingType == typeof(bool)) return "boolean";

            return "string";
        }

        // GET: AssignmentRules/Create
        public IActionResult Create()
        {
            ViewBag.Modules = new SelectList(new[] { "Accounts", "Deals", "Leads", "Vendors", "Products", "Tasks" });

            var dynamicSchema = new Dictionary<string, object>
            {
                { "Accounts", typeof(Account).GetProperties().Select(p => new { name = p.Name, type = GetSimplifiedType(p.PropertyType) }) },
                { "Leads", typeof(Lead).GetProperties().Select(p => new { name = p.Name, type = GetSimplifiedType(p.PropertyType) }) },
                { "Deals", typeof(Deal).GetProperties().Select(p => new { name = p.Name, type = GetSimplifiedType(p.PropertyType) }) },
                { "Vendors", typeof(Vendor).GetProperties().Select(p => new { name = p.Name, type = GetSimplifiedType(p.PropertyType) }) }
            };
            ViewBag.ModuleSchemaJson = System.Text.Json.JsonSerializer.Serialize(dynamicSchema);

            var users = _context.Users != null ? _context.Users.ToList() : new List<User>();
            ViewBag.UsersList = new SelectList(users, "fullName", "fullName");

            // FETCH EXISTING RULES FOR THE SIDE PANEL
            ViewBag.ExistingRules = _context.AssignmentRules.OrderByDescending(r => r.CreatedOn).ToList();

            return View(new AssignmentRule());
        }

        // POST: AssignmentRules/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AssignmentRule rule)
        {
            if (ModelState.IsValid)
            {
                _context.Add(rule);
                await _context.SaveChangesAsync();

                // REDIRECT BACK TO CREATE (Not Index)
                return RedirectToAction(nameof(Create));
            }

            // Re-populate everything if validation fails
            ViewBag.Modules = new SelectList(new[] { "Accounts", "Deals", "Leads", "Vendors", "Products", "Tasks" });
            // (You should also repopulate ModuleSchemaJson here just like in the GET method)

            var users = _context.Users != null ? _context.Users.ToList() : new List<User>();
            ViewBag.UsersList = new SelectList(users, "fullName", "fullName");
            ViewBag.ExistingRules = _context.AssignmentRules.OrderByDescending(r => r.CreatedOn).ToList();

            return View(rule);
        }
        // POST: AssignmentRules/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var rule = await _context.AssignmentRules.FindAsync(id);
            if (rule != null)
            {
                _context.AssignmentRules.Remove(rule);
                await _context.SaveChangesAsync();
            }

            // REDIRECT BACK TO CREATE (Not Index)
            return RedirectToAction(nameof(Create));
        }
    }
}