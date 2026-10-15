using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Services;
using System.Linq;
using System.Threading.Tasks;

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

        [HttpPost("ProcessAudio/{id}")]
        public async Task<IActionResult> ProcessAudio(int id, [FromServices] AiCallService aiCallService)
        {
            var callRecord = await _context.CallRecords.FindAsync(id);
            if (callRecord == null || string.IsNullOrEmpty(callRecord.RecordingUrl))
            {
                return NotFound("Call record or Audio URL not found.");
            }

            // 1. Download the audio
            byte[] audioBytes = await aiCallService.DownloadAudioAsync(callRecord.RecordingUrl);

            // 2. PASTE YOUR AI LOGIC HERE
            string transcriptionText = await aiCallService.TranscribeAudioAsync(audioBytes);
            callRecord.TranscriptionText = transcriptionText;

            var aiResult = await aiCallService.AnalyzeCallAsync(transcriptionText);

            if (aiResult != null)
            {
                callRecord.AiSummary = aiResult.AiSummary;
                callRecord.SalespersonConfidence = aiResult.SalespersonConfidence;
                callRecord.SalespersonEmpathy = aiResult.SalespersonEmpathy;
                callRecord.SalespersonTone = aiResult.SalespersonTone;
                callRecord.UserNeeds = aiResult.UserNeeds;
                callRecord.RecommendedProducts = aiResult.RecommendedProducts;
                callRecord.GapAnalysis = aiResult.GapAnalysis;
            }

            // 3. Save to database
            await _context.SaveChangesAsync();

            // 4. Reload the page to show the new data!
            return RedirectToAction("CallAnalysis", new { id = callRecord.Id });
        }
    }
}