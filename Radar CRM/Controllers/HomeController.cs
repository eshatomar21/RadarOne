using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Radar_CRM.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Get the current logged-in user's ID
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId))
            {
                currentUserId = "TEST-USER-ID"; // Remove this in production
            }

            // 🚀 ABSOLUTE FIX: Define the start of today and tomorrow safely in memory.
            // This prevents Entity Framework from crashing when it reads "DateTime?" columns.
            var startOfToday = DateTime.Today;
            var startOfTomorrow = startOfToday.AddDays(1);

            // 1. SYSTEM-WIDE TOTALS
            ViewBag.TotalSystemLeads = await _context.Leads.CountAsync();
            ViewBag.TotalSystemAccounts = await _context.Accounts.CountAsync();
            ViewBag.TotalSystemDeals = await _context.Deals.CountAsync();

            // 2. USER-SPECIFIC "TODAY" METRICS
            // Using >= and < cleanly handles nullable fields (CreatedDateAndTime, DateOfEntry) without throwing errors!
            var userLeadsToday = await _context.Leads
                .Where(l => l.LeadOwnerId == currentUserId && l.CreatedDateAndTime >= startOfToday && l.CreatedDateAndTime < startOfTomorrow)
                .CountAsync();

            var userAccountsToday = await _context.Accounts
                .Where(a => a.AccountOwnerId == currentUserId && a.DateOfEntry >= startOfToday && a.DateOfEntry < startOfTomorrow)
                .CountAsync();

            var userDealsToday = await _context.Deals
                .Where(d => d.DealOwnerId == currentUserId)
                .CountAsync();

            ViewBag.UserLeadsToday = userLeadsToday;
            ViewBag.UserAccountsToday = userAccountsToday;
            ViewBag.UserDealsToday = userDealsToday;
            ViewBag.CurrentUserId = currentUserId;

            return View();
        }

        // ==========================================
        // AJAX: GET NOTES FOR SPECIFIC DATE
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetNotesForDate(DateTime date, string userId)
        {
            // 🚀 FIX: Safely define ranges so we never use .Date in the SQL query
            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1);

            var notes = await _context.PersonalNotes
                .Where(n => n.UserId == userId && n.DateForNote >= startOfDay && n.DateForNote < endOfDay)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new {
                    id = n.Id,
                    noteText = n.NoteText,
                    createdAt = n.CreatedAt
                })
                .ToListAsync();

            return Json(notes);
        }

        // ==========================================
        // AJAX: SAVE NEW NOTE FOR DATE
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> SaveNote([FromBody] PersonalNote note)
        {
            if (string.IsNullOrWhiteSpace(note.NoteText) || string.IsNullOrEmpty(note.UserId))
            {
                return BadRequest("Invalid note data.");
            }

            // Cleanly format the incoming date
            note.DateForNote = note.DateForNote.Date;
            note.CreatedAt = DateTime.Now;

            _context.PersonalNotes.Add(note);
            await _context.SaveChangesAsync();

            return Json(new { success = true, note = note });
        }

        // ==========================================
        // AJAX: DELETE NOTE
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> DeleteNote([FromBody] int id)
        {
            var note = await _context.PersonalNotes.FindAsync(id);
            if (note != null)
            {
                _context.PersonalNotes.Remove(note);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false, message = "Note not found." });
        }
    }
}