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
                                DealOwnerId = lead.LeadOwnerId,
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
   

    // ==========================================
        // BULK UPLOAD EXCEL/CSV (HIGH PERFORMANCE)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var leadsToInsert = new List<Lead>();
            int currentRow = 1;

            // 🚀 HIGH PERFORMANCE CACHE: Get valid IDs into memory so we don't query the DB 1000 times
            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);
            var validAccountIds = new HashSet<int>(_context.Accounts.Select(a => a.Id));

            try
            {
                using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync(); // Skip header

                    while (!reader.EndOfStream)
                    {
                        currentRow++;
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);

                        if (values.Length >= 5)
                        {
                            // 🚀 SAFE ID EXTRACTION: Validate against the database before assigning to prevent crashes
                            string rawLeadOwnerId = GetVal(values, 1);
                            string rawCoOwnerId = GetVal(values, 52);
                            string rawAccountOwnerId = GetVal(values, 126);
                            string rawDemoOwnerId = GetVal(values, 128);

                            int? parsedAccountId = int.TryParse(GetVal(values, 100), out int accId) ? accId : null;

                            var newLead = new Lead
                            {
                                // --- Relational IDs ---
                                LeadOwnerId = validUserIds.Contains(rawLeadOwnerId) ? rawLeadOwnerId : null,
                                CoOwnerId = validUserIds.Contains(rawCoOwnerId) ? rawCoOwnerId : null,
                                AccountOwnerId = validUserIds.Contains(rawAccountOwnerId) ? rawAccountOwnerId : null,
                                DemoOwnerId = validUserIds.Contains(rawDemoOwnerId) ? rawDemoOwnerId : null,
                                AccountId = parsedAccountId.HasValue && validAccountIds.Contains(parsedAccountId.Value) ? parsedAccountId.Value : null,

                                // --- Basic Info ---
                                LeadName = GetVal(values, 4),
                                MobileNumber = GetVal(values, 71),
                                EmailID = GetVal(values, 74),
                                SocialLeadID = GetVal(values, 28),
                                DataSources = GetVal(values, 8),
                                CampaignSource = GetVal(values, 10),
                                CurrentStatus = GetVal(values, 116),
                                MetaCampaignName = GetVal(values, 50),
                                Pipeline = GetVal(values, 23),
                                ContactName = GetVal(values, 94),
                                GroupName = GetVal(values, 124),
                                AlternateMobile = GetVal(values, 73),
                                AlternateEmailID = GetVal(values, 125),
                                Description = GetVal(values, 22),
                                AccountType = GetVal(values, 114),

                                // --- Status & Financials ---
                                Stage = GetVal(values, 5),
                                LeadStatus = GetVal(values, 115),
                                Budget = decimal.TryParse(GetVal(values, 3), out decimal budget) ? budget : null,
                                ExpectedRevenue = decimal.TryParse(GetVal(values, 7), out decimal expRev) ? expRev : null,
                                Probability = decimal.TryParse(GetVal(values, 6), out decimal prob) ? prob : null,
                                TimePeriodToBuy = GetVal(values, 60),

                                // --- Professional Info ---
                                IsHomeopathicDoctor = GetVal(values, 102),
                                ClinicType = GetVal(values, 82),
                                WorkType = GetVal(values, 81),
                                HasComputer = GetVal(values, 63),
                                Qualification = GetVal(values, 83),
                                YearOfPassing = GetVal(values, 76),
                                CollegeName = GetVal(values, 72),
                                Age = int.TryParse(GetVal(values, 69), out int age) ? age : null,
                                YearOfPractice = int.TryParse(GetVal(values, 97), out int yop) ? yop : null,
                                TotalExperience = int.TryParse(GetVal(values, 68), out int tExp) ? tExp : null,
                                AveragePatientFee = decimal.TryParse(GetVal(values, 77), out decimal apf) ? apf : null,
                                NumberOfClinics = int.TryParse(GetVal(values, 70), out int noc) ? noc : null,
                                PatientsPerDay = int.TryParse(GetVal(values, 75), out int ppd) ? ppd : null,

                                // --- Dates ---
                                DateOfBirth = DateTime.TryParse(GetVal(values, 67), out DateTime dob) ? dob : null,
                                CreatedDateAndTime = DateTime.TryParse(GetVal(values, 54), out DateTime cdt) ? cdt : DateTime.Now,
                                LeadCreatedTime = DateTime.TryParse(GetVal(values, 51), out DateTime lct) ? lct : DateTime.Now,
                                DemoScheduledDate = DateTime.TryParse(GetVal(values, 56), out DateTime demo) ? demo : null,
                                FirstCallDate = DateTime.TryParse(GetVal(values, 55), out DateTime fcd) ? fcd : null,
                                NextFollowUpDate = DateTime.TryParse(GetVal(values, 57), out DateTime nfd) ? nfd : null,
                                LastContactDate = DateTime.TryParse(GetVal(values, 58), out DateTime lcd) ? lcd : null,
                                PurchaseDate = DateTime.TryParse(GetVal(values, 65), out DateTime pd) ? pd : null,

                                TrialStartDate = DateOnly.TryParse(GetVal(values, 109), out DateOnly tsd) ? tsd : null,
                                TrialEndDate = DateOnly.TryParse(GetVal(values, 110), out DateOnly ted) ? ted : null,

                                // --- Software & Purchasing ---
                                CurrentlyUsingSoftware = GetVal(values, 78),
                                CurrentSoftwareName = GetVal(values, 64),
                                RadarOpusVersion = GetVal(values, 112),
                                RadarOpusLicenseNo = GetVal(values, 111),
                                ProductPackage = GetVal(values, 96),
                                PurchaseValue = decimal.TryParse(GetVal(values, 66), out decimal pVal) ? pVal : null,
                                PaymentStatus = GetVal(values, 79),
                                CustomerStatus = GetVal(values, 80),

                                // --- Deals/Payments ---
                                DealType = GetVal(values, 107),
                                DealValue = decimal.TryParse(GetVal(values, 105), out decimal dVal) ? dVal : null,
                                PackageSelected = GetVal(values, 106),
                                Discount = decimal.TryParse(GetVal(values, 104), out decimal disc) ? disc : null,
                                PaymentMode = GetVal(values, 108),
                                Remarks = GetVal(values, 103),
                                PaymentType = GetVal(values, 113),
                                SubTotal = decimal.TryParse(GetVal(values, 130), out decimal subTot) ? subTot : null,
                                Adjustment = decimal.TryParse(GetVal(values, 131), out decimal adj) ? adj : null,
                                Taxes = decimal.TryParse(GetVal(values, 132), out decimal tax) ? tax : null,
                                GrandTotal = decimal.TryParse(GetVal(values, 133), out decimal grandTot) ? grandTot : null,

                                // --- Address 1 ---
                                Addr1_Country = GetVal(values, 135),
                                Addr1_FlatHouse = GetVal(values, 136),
                                Addr1_Street = GetVal(values, 137),
                                Addr1_City = GetVal(values, 138),
                                Addr1_State = GetVal(values, 139),
                                Addr1_Zip = GetVal(values, 140),
                                Addr1_Coordinates = GetVal(values, 141) + " " + GetVal(values, 142), // Combines Lat/Long

                                // --- Address 2 ---
                                Addr2_Country = GetVal(values, 143),
                                Addr2_FlatHouse = GetVal(values, 144),
                                Addr2_Street = GetVal(values, 145),
                                Addr2_City = GetVal(values, 146),
                                Addr2_State = GetVal(values, 147),
                                Addr2_Zip = GetVal(values, 148),
                                Addr2_Coordinates = GetVal(values, 149) + " " + GetVal(values, 150),

                                // --- Secondary Contacts ---
                                ContactPerson1 = GetVal(values, 117),
                                ContactPerson2 = GetVal(values, 118),
                                ContactPerson3 = GetVal(values, 120),
                                Contact1Phone = GetVal(values, 119),
                                Contact2Phone = GetVal(values, 121),
                                Contact3Phone = GetVal(values, 122),

                                // --- Other Remarks ---
                                ConversationRemarks = GetVal(values, 59),
                                InterestedPackage = GetVal(values, 62),
                                LostReason = GetVal(values, 61),
                                LeadProfile = GetVal(values, 95)
                            };

                            leadsToInsert.Add(newLead);
                        }
                    }
                }

                // 🚀 MASSIVE SPEED BOOST: Turn off tracking during bulk insert to stop Entity Framework from hanging
                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                await _context.Leads.AddRangeAsync(leadsToInsert);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Failed at Row {currentRow} -> {trueError}");
            }
            finally
            {
                // Always turn tracking back on when finished
                _context.ChangeTracker.AutoDetectChangesEnabled = true;
            }

            return Ok();
        }

        // --- Helper Methods to parse CSV properly ---
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
    }
    }