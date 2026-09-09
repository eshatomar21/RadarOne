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

        public async Task<IActionResult> Index()
        {
            var deals = await _context.Deals.ToListAsync();
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