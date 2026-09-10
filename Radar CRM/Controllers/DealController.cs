using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;

namespace Radar_CRM.Controllers
{
    public class DealsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DealsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // INDEX: Paginated & Sorted (100 per page)
        // ==========================================
        public async Task<IActionResult> Index(int page = 1, string search = "", string sortCol = "Id", string sortDir = "desc")
        {
            int pageSize = 100;
            var query = _context.Deals.AsQueryable();

            // Server-Side Filtering
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(d =>
                    (d.DealName != null && d.DealName.Contains(search)) ||
                    (d.ContactPersonName != null && d.ContactPersonName.Contains(search))
                );
            }

            // Server-Side Sorting 
            if (sortDir == "desc")
            {
                query = sortCol switch
                {
                    "DealName" => query.OrderByDescending(d => d.DealName),
                    "GrandTotal" => query.OrderByDescending(d => d.GrandTotal),
                    _ => query.OrderByDescending(d => d.Id)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "DealName" => query.OrderBy(d => d.DealName),
                    "GrandTotal" => query.OrderBy(d => d.GrandTotal),
                    _ => query.OrderBy(d => d.Id)
                };
            }

            // Execute Pagination on the Database
            var totalRecords = await query.CountAsync();
            var deals = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            ViewBag.TotalRecords = totalRecords;

            return View(deals);
        }

        // ==========================================
        // CREATE: GET 
        // ==========================================
        public IActionResult Create()
        {
            // This line prevents the NullReferenceException
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            return View();
        }

        // ==========================================
        // CREATE: POST 
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Deal deal, string[] SavedNoteOwner, string[] SavedNoteDateTime, string[] SavedNoteDesc)
        {
            ModelState.Clear(); // Clears strict implicit validation

            if (string.IsNullOrWhiteSpace(deal.DealName))
            {
                ModelState.AddModelError("DealName", "Deal Name is required.");
            }

            if (ModelState.IsValid)
            {
                // 🚀 Use the unified Notes list instead of DealNote
                deal.Notes ??= new List<Notes>();

                if (SavedNoteDesc != null)
                {
                    for (int i = 0; i < SavedNoteDesc.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(SavedNoteDesc[i]))
                        {
                            deal.Notes.Add(new Notes
                            {
                                // 🚀 Map to the new Foreign Key and Date properties
                                NoteOwnerId = SavedNoteOwner != null && SavedNoteOwner.Length > i ? SavedNoteOwner[i] : null,
                                CreatedDateTime = DateTime.TryParse(SavedNoteDateTime[i], out DateTime parsedDate) ? parsedDate : DateTime.Now,
                                Description = SavedNoteDesc[i]
                            });
                        }
                    }
                }

                _context.Add(deal);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            return View(deal);
        }

        // ==========================================
        // EDIT: GET 
        // ==========================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var deal = await _context.Deals
                .Include(d => d.PaymentRows)
                .Include(d => d.Notes)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (deal == null) return NotFound();
            // This line prevents the NullReferenceException on the Edit page
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            return View(deal);
        }

        // ==========================================
        // UPLOAD FILE: Mapped for Deals_2026_09_10.csv
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var dealsToInsert = new List<Deal>();
            int currentRow = 1;

            // 🚀 HIGH PERFORMANCE CACHE: Get valid IDs into memory so we don't query the DB thousands of times
            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);
            var validAccountIds = new HashSet<int>(_context.Accounts.Select(a => a.Id));

            try
            {
                using (var reader = new System.IO.StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync(); // Skip header row

                    while (!reader.EndOfStream)
                    {
                        currentRow++;
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);

                        // Verify the row has enough columns based on the Deals_2026_09_10.csv structure (49 columns)
                        if (values.Length >= 48)
                        {
                            // 🚀 SAFE ID EXTRACTION: Validate against the database before assigning to prevent crashes
                            string rawDealOwnerId = GetVal(values, 2);  // Deals Owner.id
                            string rawDemoOwnerId = GetVal(values, 46); // Demo Owner.id
                            int? parsedAccountId = int.TryParse(GetVal(values, 30), out int accId) ? accId : null; // AccountName.id

                            var newDeal = new Deal
                            {
                                // --- Core Identifiers ---
                                DealName = GetVal(values, 1),

                                // --- Relational IDs ---
                                DealOwnerId = validUserIds.Contains(rawDealOwnerId) ? rawDealOwnerId : null,
                                DemoOwner = validUserIds.Contains(rawDemoOwnerId) ? rawDemoOwnerId : null,
                                AccountId = parsedAccountId.HasValue && validAccountIds.Contains(parsedAccountId.Value) ? parsedAccountId.Value : null,

                                // --- Profile & Text Info ---
                                LeadSource = GetVal(values, 26),         // Lead Source
                                AccountType = GetVal(values, 48),        // Account Type
                                LeadName = GetVal(values, 39),           // Lead Name
                                ContactPersonName = GetVal(values, 45),  // Contact Person Name
                                MetaCampaignName = GetVal(values, 44),   // Meta Campaign Name

                                // --- Status & Types ---
                                PaymentStatus = GetVal(values, 25),      // Payment Status
                                PaymentType = GetVal(values, 36),        // Payment Type
                                PaymentMode = GetVal(values, 37),        // payment Mode

                                // --- Financials (With safe parsing) ---
                                SubTotal = decimal.TryParse(GetVal(values, 42), out decimal subTotal) ? subTotal : 0m,     // Sub Total
                                Taxes = decimal.TryParse(GetVal(values, 41), out decimal taxes) ? taxes : 0m,              // Taxs
                                Adjustment = decimal.TryParse(GetVal(values, 40), out decimal adj) ? adj : 0m,             // Adjustment
                                GrandTotal = decimal.TryParse(GetVal(values, 43), out decimal grandTot) ? grandTot : 0m    // GrandTotal
                            };

                            dealsToInsert.Add(newDeal);
                        }
                    }
                }

                // 🚀 MASSIVE SPEED BOOST: Turn off tracking during bulk insert to stop Entity Framework from hanging
                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                await _context.Deals.AddRangeAsync(dealsToInsert);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Extracts the deepest inner exception so you know EXACTLY what broke (e.g., column truncation, foreign key)
                string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Failed at Row {currentRow} -> {trueError}");
            }
            finally
            {
                // Always turn tracking back on safely
                _context.ChangeTracker.AutoDetectChangesEnabled = true;
            }

            return Ok();
        }

        // ==========================================
        // HELPER METHODS FOR CSV PARSING
        // ==========================================
        private string GetVal(string[] values, int index)
        {
            if (index < values.Length) return values[index]?.Trim() ?? "";
            return "";
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var currentField = new System.Text.StringBuilder();

            foreach (char c in line)
            {
                if (c == '\"') inQuotes = !inQuotes;
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else currentField.Append(c);
            }
            result.Add(currentField.ToString());
            return result.ToArray();
        }

        // ==========================================
        // EDIT: POST 
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Deal deal, string[] SavedNoteOwner, string[] SavedNoteDateTime, string[] SavedNoteDesc)
        {
            if (id != deal.Id) return NotFound();

            ModelState.Clear(); // Clears strict implicit validation

            if (string.IsNullOrWhiteSpace(deal.DealName))
            {
                ModelState.AddModelError("DealName", "Deal Name is required.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(deal);

                    if (deal.PaymentRows != null)
                    {
                        foreach (var row in deal.PaymentRows)
                        {
                            if (row.Id == 0) _context.DealPaymentRows.Add(row);
                            else _context.Update(row);
                        }
                    }

                    if (SavedNoteDesc != null)
                    {
                        for (int i = 0; i < SavedNoteDesc.Length; i++)
                        {
                            if (!string.IsNullOrWhiteSpace(SavedNoteDesc[i]))
                            {
                                _context.DealNotes.Add(new DealNote
                                {
                                    DealId = deal.Id,
                                    Owner = SavedNoteOwner != null && SavedNoteOwner.Length > i ? SavedNoteOwner[i] : "System",
                                    DateTime = DateTime.Parse(SavedNoteDateTime[i]),
                                    Description = SavedNoteDesc[i]
                                });
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Deals.Any(e => e.Id == deal.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.UsersList = new SelectList(_context.Users, "FullName", "FullName");
            return View(deal);
        }
    }
}