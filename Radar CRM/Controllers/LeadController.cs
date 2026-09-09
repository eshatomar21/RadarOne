using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;

namespace Radar_CRM.Controllers
{
    public class LeadsController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Inject the DbContext
        public LeadsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Leads/Index
        public async Task<IActionResult> Index()
        {
            // Fetch all leads from the database
            var leads = await _context.Leads.ToListAsync();
            return View(leads);
        }

        // GET: Leads/Create
        public IActionResult Create()
        {
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");

            // 🚀 Binds to ProductId and passes dictionary for Javascript pricing
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            return View(new Lead());
        }

        // ==========================================
        // POST: Leads/Create
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Lead lead)
        {
            // Clear validation errors for the payment rows
            var paymentKeys = ModelState.Keys.Where(k => k.StartsWith("PaymentRow")).ToList();
            foreach (var key in paymentKeys)
            {
                ModelState.Remove(key);
            }
            ModelState.Remove("PaymentRow");

            if (ModelState.IsValid)
            {
                // 1. 🚀 STRICT FILTER: Remove blank rows to prevent database Foreign Key crashes
                if (lead.PaymentRow != null)
                {
                    lead.PaymentRow = lead.PaymentRow
                        .Where(r => r.ProductId.HasValue && r.ProductId.Value > 0)
                        .ToList();
                }

                // =========================================================
                // AUTOMATED PIPELINE ASSIGNMENT
                // =========================================================
                if (lead.CurrentStatus == "User")
                {
                    lead.Pipeline = "Upgrade Software";
                }
                else if (lead.CurrentStatus == "Non-User")
                {
                    lead.Pipeline = "New Software";
                }

                // 2. Save new lead to DB (Generates the Lead.Id)
                _context.Add(lead);
                await _context.SaveChangesAsync();

                // 3. 🚀 Explicitly force the child rows to save and link to the new Lead ID
                if (lead.PaymentRow != null && lead.PaymentRow.Any())
                {
                    foreach (var row in lead.PaymentRow)
                    {
                        row.LeadId = lead.Id;
                        if (row.Id == 0)
                        {
                            _context.Add(row);
                        }
                    }
                    await _context.SaveChangesAsync(); // Saves the rows to the database
                }

                // 4. Automated Lead-to-Deal Mapping logic
                if (lead.Stage == "Direct Deal")
                {
                    var newDeal = new Deal
                    {
                        DealName = lead.LeadName + " - Direct Deal",

                        // 🚀 ACCOUNT MAPPING (Passes the ID so the Deal view can use the Lookup)
                        AccountId = lead.AccountId,

                        // 🚀 OWNER MAPPING FIXED (Only map the ID strings, not the Navigation Models)
                        DealOwnerId = lead.LeadOwnerId,
                        AccountOwner = lead.AccountOwnerId,
                        DemoOwner = lead.DemoOwnerId,

                        ContactPersonName = lead.ContactName,
                        LeadName = lead.LeadName,
                        LeadSource = lead.DataSources,
                        AccountType = lead.AccountType,
                        MetaCampaignName = lead.MetaCampaignName,
                        PaymentMode = lead.PaymentMode,
                        PaymentType = lead.PaymentType,
                        PaymentStatus = lead.PaymentStatus,
                        SubTotal = lead.SubTotal ?? 0m,
                        Taxes = lead.Taxes ?? 0m,
                        Adjustment = lead.Adjustment ?? 0m,
                        GrandTotal = lead.GrandTotal ?? 0m,
                        PaymentRows = lead.PaymentRow?.Select(row => new DealPaymentRow
                        {
                            DealType = row.DealType,
                            ProductId = row.ProductId,
                            ProductName = row.ProductName,
                            Quantity = row.Quantity ?? 1,
                            UnitPrice = row.ProductPrice ?? 0,
                            Amount = row.Total ?? 0,
                            Discount = row.DiscountPercent ?? 0,
                            Total = row.FinalAmount ?? 0
                        }).ToList() ?? new List<DealPaymentRow>()
                    };

                    _context.Deals.Add(newDeal);
                    await _context.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }

            // Fallback if model state fails
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            return View(lead);
        }

        // ==========================================
        // BULK & SINGLE DELETE: AJAX POST
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> BulkDelete([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "No records selected." });

            if (_context == null || _context.Leads == null)
                return Json(new { success = false, message = "Database context is not initialized." });

            try
            {
                var leadsToDelete = await _context.Leads
                                                  .Where(l => ids.Contains(l.Id))
                                                  .ToListAsync();

                if (leadsToDelete != null && leadsToDelete.Any())
                {
                    if (_context.Note != null)
                    {
                        var relatedNotes = await _context.Note
                            .Where(n => n.LeadId != null && ids.Contains(n.LeadId.Value))
                            .ToListAsync();

                        if (relatedNotes != null && relatedNotes.Any())
                        {
                            _context.Note.RemoveRange(relatedNotes);
                        }
                    }

                    _context.Leads.RemoveRange(leadsToDelete);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                string errorMsg = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Json(new { success = false, message = "Error: " + errorMsg });
            }
        }

        // GET: Leads/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Fetch the specific lead from the DB based on ID, including PaymentRows
            var lead = await _context.Leads
                .Include(l => l.PaymentRow)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (lead == null) return NotFound();

            // Populate ViewBags
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");

            // 🚀 Binds to ProductId and passes dictionary for Javascript pricing
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            return View(lead);
        }

        // POST: Leads/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Lead lead)
        {
            if (id != lead.Id)
            {
                return NotFound();
            }

            // Clears validation for "PaymentRow"
            ModelState.Remove("PaymentRow");
            var paymentKeys = ModelState.Keys.Where(k => k.StartsWith("PaymentRow")).ToList();
            foreach (var key in paymentKeys)
            {
                ModelState.Remove(key);
            }

            if (ModelState.IsValid)
            {
                // =========================================================
                // AUTOMATED PIPELINE ASSIGNMENT
                // =========================================================
                if (lead.CurrentStatus == "User")
                {
                    lead.Pipeline = "Upgrade Software";
                }
                else if (lead.CurrentStatus == "Non-User")
                {
                    lead.Pipeline = "New Software";
                }

                try
                {
                    // =========================================================
                    // HANDOVER LEAD AUTOMATION (.NET EF Core Implementation)
                    // =========================================================
                    bool isHandoverConditionMet = (lead.Stage == "Demo Done" && lead.LeadStatus == "Hot") ||
                                                  (lead.LeadStatus == "Warm");

                    if (isHandoverConditionMet)
                    {
                        var exemptUsers = await _context.Users
                            .Where(u => u.FirstName.Contains("Jaspal") ||
                                        u.FirstName.Contains("Akash") ||
                                        u.FirstName.Contains("Abhinav"))
                            .ToListAsync();

                        var exemptOwnerIds = exemptUsers.Select(u => u.Id).ToList();
                        string currentOwnerId = lead.LeadOwnerId;

                        if (string.IsNullOrWhiteSpace(lead.DemoOwnerId))
                        {
                            lead.DemoOwnerId = currentOwnerId;
                        }

                        if (currentOwnerId != null && !exemptOwnerIds.Contains(currentOwnerId))
                        {
                            var defaultNewOwner = exemptUsers.FirstOrDefault(u => u.FirstName.Contains("Jaspal"));
                            if (defaultNewOwner != null)
                            {
                                lead.LeadOwnerId = defaultNewOwner.Id;
                            }
                        }
                    }

                    // =========================================================
                    // PAYMENT ROWS SYNCHRONIZATION
                    // =========================================================
                    if (lead.PaymentRow != null)
                    {
                        // 1. 🚀 STRICT FILTER: Ignore blank fallback rows to prevent DB crash
                        var validRows = lead.PaymentRow
                            .Where(r => r.ProductId.HasValue && r.ProductId.Value > 0)
                            .ToList();

                        // 2. Identify rows that were deleted in the UI and remove them from the database
                        var validRowIds = validRows.Select(r => r.Id).ToList();

                        var rowsToDelete = _context.Set<ProductPaymentRow>()
                            .Where(r => r.LeadId == lead.Id && !validRowIds.Contains(r.Id))
                            .ToList();

                        if (rowsToDelete.Any())
                        {
                            _context.RemoveRange(rowsToDelete);
                        }

                        // 3. Add or Update the valid rows
                        foreach (var row in validRows)
                        {
                            row.LeadId = lead.Id; // CRITICAL: Explicitly link the row to the lead

                            if (row.Id == 0)
                            {
                                _context.Add(row); // Insert new row
                            }
                            else
                            {
                                _context.Update(row); // Update existing row
                            }
                        }

                        // Temporarily detach the list from the Lead model so _context.Update(lead) doesn't try to double-save them
                        lead.PaymentRow = null;
                    }

                    // Update existing lead in the DB
                    _context.Update(lead);
                    await _context.SaveChangesAsync();

                    // =========================================================
                    // DIRECT DEAL CREATION
                    // =========================================================
                    if (lead.Stage == "Direct Deal")
                    {
                        bool dealExists = _context.Deals.Any(d => d.LeadName == lead.LeadName);

                        if (!dealExists)
                        {
                            // Fetch the freshly saved rows to map them to the deal
                            var savedRows = _context.Set<ProductPaymentRow>().Where(r => r.LeadId == lead.Id).ToList();

                            var newDeal = new Deal
                            {
                                DealName = lead.LeadName + " - Direct Deal",
                                AccountId = lead.AccountId,
                                ContactPersonName = lead.ContactName,
                                LeadName = lead.LeadName,
                                LeadSource = lead.DataSources,
                                DealOwnerId=lead.LeadOwnerId,
                                AccountOwner = lead.AccountOwnerId,
                                DemoOwner = lead.DemoOwnerId,
                                AccountType = lead.AccountType,
                                MetaCampaignName = lead.MetaCampaignName,
                                PaymentMode = lead.PaymentMode,
                                PaymentType = lead.PaymentType,
                                PaymentStatus = lead.PaymentStatus,
                                SubTotal = lead.SubTotal ?? 0m,
                                Taxes = lead.Taxes ?? 0m,
                                Adjustment = lead.Adjustment ?? 0m,
                                GrandTotal = lead.GrandTotal ?? 0m,

                                PaymentRows = savedRows.Select(row => new DealPaymentRow
                                {
                                    DealType = row.DealType,
                                    ProductId = row.ProductId,
                                    ProductName = row.ProductName,
                                    Quantity = row.Quantity ?? 1,
                                    UnitPrice = row.ProductPrice ?? 0,
                                    Amount = row.Total ?? 0,
                                    Discount = row.DiscountPercent ?? 0,
                                    Total = row.FinalAmount ?? 0
                                }).ToList()
                            };

                            _context.Deals.Add(newDeal);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!LeadExists(lead.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            // Fallback if model state fails
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            ViewBag.AccountsList = new SelectList(_context.Accounts, "Id", "AccountName");
            ViewBag.ProductsList = new SelectList(_context.Products, "Id", "ProductName");
            ViewBag.ProductPrices = _context.Products.ToDictionary(p => p.Id, p => p.UnitPrice);

            return View(lead);
        }

        // Helper method to check if a lead exists during updates
        private bool LeadExists(int id)
        {
            return _context.Leads.Any(e => e.Id == id);
        }
    }
}