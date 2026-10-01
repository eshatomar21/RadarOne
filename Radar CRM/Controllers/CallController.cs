using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using System.Threading.Tasks;
using System.Linq;

namespace Radar_CRM.Controllers
{
    // 1. Force the application to map this exact base URL
    [Route("CallRecordings")]
    public class CallRecordingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CallRecordingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 2. Map this method to /CallRecordings AND /CallRecordings/Index
        [Route("")]
        [Route("Index")]
        public async Task<IActionResult> Index()
        {
            var callRecords = await _context.CallRecords
                .Include(c => c.Lead)
                .Include(c => c.Account)
                .OrderByDescending(c => c.CallDate)
                .ToListAsync();

            return View(callRecords);
        }

        // 🚀 NEW: Dedicated Call Analysis Page with explicit Route
        [Route("CallAnalysis/{id?}")]
        public async Task<IActionResult> CallAnalysis(int? id)
        {
            if (id == null)
            {
                return NotFound("Call Record ID not provided.");
            }

            var callRecord = await _context.CallRecords
                .Include(c => c.Lead)
                .Include(c => c.Account)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (callRecord == null)
            {
                return NotFound("Call Record not found in the database.");
            }

            return View(callRecord);
        }
    }
}