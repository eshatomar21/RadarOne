using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Radar_CRM.Controllers
{
    public class RolesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RolesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var allRoles = await _context.Roles.ToListAsync();

            // Build Hierarchy starting from root roles (roles with no parent)
            var topLevelRoles = allRoles.Where(r => r.ParentRoleId == null).ToList();
            var hierarchy = topLevelRoles.Select(r => BuildNode(r, allRoles)).ToList();

            ViewBag.ParentRoles = allRoles.Select(r => new SelectListItem
            {
                Value = r.Id.ToString(),
                Text = r.Name
            }).ToList();

            return View(hierarchy);
        }

        // Recursive function to build the tree
        private RoleHierarchyNode BuildNode(Role role, List<Role> allRoles)
        {
            return new RoleHierarchyNode
            {
                Id = role.Id,
                Name = role.Name,
                Children = allRoles
                    .Where(r => r.ParentRoleId == role.Id)
                    .Select(r => BuildNode(r, allRoles))
                    .ToList()
            };
        }

        [HttpPost]
        public async Task<IActionResult> CreateAjax(Role role)
        {
            if (ModelState.IsValid)
            {
                _context.Roles.Add(role);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Invalid data provided." });
        }

        [HttpPost]
        public async Task<IActionResult> EditAjax(Role role)
        {
            var existing = await _context.Roles.FindAsync(role.Id);
            if (existing != null)
            {
                // Prevent infinite loop circular dependency
                if (role.ParentRoleId == role.Id)
                {
                    return Json(new { success = false, message = "A role cannot report to itself." });
                }

                existing.Name = role.Name;
                existing.ParentRoleId = role.ParentRoleId;
                existing.Description = role.Description;

                _context.Update(existing);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Role not found." });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAjax(int id)
        {
            var role = await _context.Roles.Include(r => r.SubordinateRoles).FirstOrDefaultAsync(r => r.Id == id);
            if (role != null)
            {
                if (role.SubordinateRoles.Any())
                {
                    return Json(new { success = false, message = "Cannot delete. Please reassign subordinate roles first." });
                }

                _context.Roles.Remove(role);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Role not found." });
        }

        [HttpGet]
        public async Task<IActionResult> GetUsersByRole(string roleName)
        {
            // Placeholder: Replace '.Users' with your actual DbSet name for users once created.
            // This currently uses a mock return so the UI doesn't break while you build the User model.

            /* Example actual implementation:
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role == null) return Json(new List<object>());
            var users = await _context.Users.Where(u => u.RoleId == role.Id).Select(u => new { u.fullName, u.Email }).ToListAsync();
            return Json(users);
            */

            return Json(new List<object>()); // Returns empty list for now
        }
    }
}