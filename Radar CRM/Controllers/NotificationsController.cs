using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using System.Security.Claims;

namespace Radar_CRM.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetUnread()
        {
            // Get the ID of the currently logged-in user viewing the screen
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Json(new { success = false });

            // 🚀 FIX: The filter now correctly catches both Bulk ("assigned") and Single ("new owner") notifications
            var notifications = await _context.Set<Radar_CRM.Models.Task>()
                .Where(t => t.TaskOwnerId == userId
                         && t.Status == "Not Started"
                         && (t.Subject.Contains("assigned") || t.Subject.Contains("new owner")))
                .Select(t => new { id = t.Id, subject = t.Subject })
                .ToListAsync();

            return Json(new { success = true, data = notifications });
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var task = await _context.Set<Radar_CRM.Models.Task>().FindAsync(id);
            if (task != null)
            {
                task.Status = "Completed"; // Hides it from future popups
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }
    }
}