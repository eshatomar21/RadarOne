using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System;
using Task = System.Threading.Tasks.Task;

namespace Radar_CRM.Controllers
{
    public class AccountsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // INDEX: Shows all records in the database
        // ==========================================
        public async Task<IActionResult> Index()
        {
            var accounts = await _context.Accounts.ToListAsync();
            return View(accounts);
        }

        // ==========================================
        // CREATE: GET (Opens the blank form)
        // ==========================================
        public IActionResult Create()
        {
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            return View();
        }

        // ==========================================
        // CREATE: POST (Saves new data to the DB)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Account account, string[] SavedNoteOwner, string[] SavedNoteDateTime, string[] SavedNoteDesc, List<IFormFile> SavedNoteFiles)
        {
            account.DateOfEntry = DateTime.Now;
            ModelState.Remove("DateOfEntry");


            if (ModelState.IsValid)
            {
                _context.Add(account);
                await _context.SaveChangesAsync();

                // 🚀 UPDATED CALL: Passing 6 parameters including "Account" and SavedNoteFiles
                await SaveNotesAsync(account.Id, "Account", SavedNoteOwner, SavedNoteDateTime, SavedNoteDesc, SavedNoteFiles);

                // TRIGGER AUTO-CONTACT CREATION CHECK
                await CheckAndCreateContactFromAccountAsync(account);

                return RedirectToAction(nameof(Index));
            }

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            return View(account);
        }
        // ==========================================
        // EDIT: GET (Fetches specific record)
        // ==========================================
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var account = await _context.Accounts.FindAsync(id);
            if (account == null) return NotFound();

            // 🚀 Fetch Existing Notes for this Account to display on the Edit Page
            ViewBag.ExistingNotes = await _context.Note
     .Where(n => n.AccountId == id)
     .OrderByDescending(n => n.CreatedDateTime)
     .ToListAsync();

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            return View(account);
        }

        // ==========================================
        // EDIT: POST (Saves changes back to the DB)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Account account, string[] SavedNoteOwner, string[] SavedNoteDateTime, string[] SavedNoteDesc, List<IFormFile> SavedNoteFiles)
        {
            if (id != account.Id) return NotFound();

            // PROFILE COMPLETION CHECK
            bool isProfileIncomplete =
                string.IsNullOrWhiteSpace(account.IsHomeopathicDoctor) || account.IsHomeopathicDoctor == "-None-" || account.IsHomeopathicDoctor == "false" ||
                string.IsNullOrWhiteSpace(account.WorkType) || account.WorkType == "-None-" || account.WorkType == "false" ||
                string.IsNullOrWhiteSpace(account.ClinicType) || account.ClinicType == "-None-" || account.ClinicType == "false" ||
                string.IsNullOrWhiteSpace(account.HasComputer) || account.HasComputer == "-None-" || account.HasComputer == "false";

            if (isProfileIncomplete && (string.IsNullOrWhiteSpace(account.ProfilePendingReason) || account.ProfilePendingReason == "-None-"))
            {
                // Attaches the error to the field so the view blocks the save
                ModelState.AddModelError("ProfilePendingReason", "Your profile is incomplete. If you still want to save, you must provide a Profile Pending Reason.");

                // Use TempData to trigger an alert popup on the frontend
                TempData["ErrorMessage"] = "Your profile is incomplete. If you still want to save, you must provide a Profile Pending Reason.";
            }

            // Keep your existing if (ModelState.IsValid) block below this...

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(account);
                    await _context.SaveChangesAsync();

                    // 🚀 UPDATED CALL: Passing 6 parameters including "Account" and SavedNoteFiles
                    await SaveNotesAsync(account.Id, "Account", SavedNoteOwner, SavedNoteDateTime, SavedNoteDesc, SavedNoteFiles);

                    // TRIGGER AUTO-CONTACT CREATION CHECK 
                    await CheckAndCreateContactFromAccountAsync(account);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AccountExists(account.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.ExistingNotes = await _context.Note
     .Where(n => n.AccountId == id)
     .OrderByDescending(n => n.CreatedDateTime)
     .ToListAsync();

            ViewBag.UsersList = new SelectList(_context.Users, "Id", "fullName");
            return View(account);
        }

        // ==========================================
        // HELPER: Save Notes & Files to C Drive
        // ==========================================
        private async Task SaveNotesAsync(int recordId, string moduleName, string[] owners, string[] dateTimes, string[] descs, List<IFormFile> files)
        {
            // Define your C: Drive folder path
            string uploadPath = @"C:\CRM_Files\Notes";

            // Create the directory automatically if it doesn't exist
            if (!Directory.Exists(uploadPath))
            {
                Directory.CreateDirectory(uploadPath);
            }

            if (descs != null && descs.Length > 0)
            {
                for (int i = 0; i < descs.Length; i++)
                {
                    // Only save if there is text OR a file attached
                    if (!string.IsNullOrWhiteSpace(descs[i]) || (files != null && i < files.Count && files[i] != null && files[i].Length > 0))
                    {
                        var newNote = new Notes
                        {
                            // Removed Id = recordId (Let the database auto-generate the Note's ID)
                            // Removed ModuleName = moduleName

                            // Maps the owner (NoteOwnerId replaces NoteOwner)
                            NoteOwnerId = owners != null && owners.Length > i ? owners[i] : null,

                            CreatedDateTime = dateTimes != null && dateTimes.Length > i && DateTime.TryParse(dateTimes[i], out DateTime parsedDate) ? parsedDate : DateTime.Now,
                            Description = descs[i]
                        };

                        // Handle File Attachment
                        if (files != null && i < files.Count && files[i] != null && files[i].Length > 0)
                        {
                            var file = files[i];

                            // Generate a unique filename to prevent overwriting
                            string fileName = Guid.NewGuid().ToString() + "_" + Path.GetFileName(file.FileName);
                            string filePath = Path.Combine(uploadPath, fileName);

                            // Save physical file to C: drive
                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await file.CopyToAsync(stream);
                            }

                            // Save paths to database
                            newNote.AttachmentFileName = file.FileName;
                            newNote.AttachmentFilePath = filePath;
                        }

                        _context.Note.Add(newNote);
                    }
                }
                await _context.SaveChangesAsync();
            }
        }

        // ==========================================
        // ACTION: Download/View Saved File
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> DownloadNoteFile(int noteId)
        {
            var note = await _context.Note.FindAsync(noteId);
            if (note == null || string.IsNullOrEmpty(note.AttachmentFilePath) || !System.IO.File.Exists(note.AttachmentFilePath))
            {
                return NotFound("File not found on server.");
            }

            var memory = new MemoryStream();
            using (var stream = new FileStream(note.AttachmentFilePath, FileMode.Open, FileAccess.Read))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            return File(memory, "application/octet-stream", note.AttachmentFileName);
        }

        // ==========================================
        // AUTOMATION: Condition Check & Record Creation
        // ==========================================
        private async Task CheckAndCreateContactFromAccountAsync(Account acc)
        {
            // 1. Specific Condition: Must be explicitly marked as 'Qualified'
            if (acc.QualificationStatus != "Qualified") return;

            // 2. Count the specific required fields
            int filledFieldsCount = 0;

            if (!string.IsNullOrWhiteSpace(acc.IsHomeopathicDoctor)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.WorkType)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.HasComputer)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.ClinicType)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.Qualification)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.CollegeName)) filledFieldsCount++;
            if (!string.IsNullOrWhiteSpace(acc.YearOfPassing)) filledFieldsCount++;
            if (acc.TotalExperience.HasValue) filledFieldsCount++;
            if (acc.YearsOfPractice.HasValue) filledFieldsCount++;
            if (acc.AveragePatientFee.HasValue) filledFieldsCount++;
            if (acc.NumberOfClinics.HasValue) filledFieldsCount++;
            if (acc.DateOfBirth.HasValue) filledFieldsCount++;
            if (acc.Age.HasValue) filledFieldsCount++;
            if (acc.PatientsPerDay.HasValue) filledFieldsCount++;

            bool hasComputer = acc.HasComputer != "No";
            bool isHomeopath = acc.IsHomeopathicDoctor != "No" && acc.IsHomeopathicDoctor != "नहीं";

            // 3. Determine Profile Status based on logic
            string profileStatus = "Not Completed";

            if (filledFieldsCount > 4 && hasComputer && isHomeopath)
            {
                profileStatus = "Strongly Completed";
            }
            else if (filledFieldsCount == 4 && hasComputer && isHomeopath)
            {
                profileStatus = "Completed";
            }
            else if (filledFieldsCount >= 4 && (!hasComputer || !isHomeopath))
            {
                profileStatus = "Not-Qualified";
            }

            // 4. Update the Account record silently
            acc.Profilestatus = profileStatus;
            acc.ProfileRate = filledFieldsCount;

            _context.Accounts.Update(acc);
            await _context.SaveChangesAsync();

            // 5. Condition Check: Automatically trigger Contact creation if conditions are met
            if (profileStatus == "Strongly Completed" || profileStatus == "Completed")
            {
                // Duplicate Check: Ensure the record doesn't already exist in Contacts
                bool recordExists = await _context.Leads.AnyAsync(c => c.AccountId == acc.Id || c.MobileNumber == acc.MobileNumber);
                if (recordExists) return;

                // ==========================================
                // OWNER ASSIGNMENT LOGIC 
                // ==========================================
                string assignedOwnerId = acc.AccountOwnerId;

                // 🚀 FIX: Ensure we retrieve the user safely to grab their full name string
                var ownerUser = await _context.Users.FindAsync(acc.AccountOwnerId);
                string ownerFullName = ownerUser?.fullName ?? ownerUser?.FirstName ?? "System User";

                if (ownerFullName.Contains("Vishakha", StringComparison.OrdinalIgnoreCase) ||
                    ownerFullName.Contains("Mamta", StringComparison.OrdinalIgnoreCase))
                {
                    var amanUser = await _context.Users.FirstOrDefaultAsync(u => u.FirstName.Contains("Aman"));
                    if (amanUser != null)
                    {
                        assignedOwnerId = amanUser.Id;
                        ownerFullName = amanUser.fullName ?? amanUser.FirstName; // 🚀 FIX: Capture Aman's name string too
                    }
                }

                // Creating as a Contact Record within the Lead model structure
                var newContact = new Lead
                {
                    // --- Core Identifiers ---
                    ContactName = acc.ContactPersonName ?? "Unknown Contact",
                    LeadName = acc.AccountName ?? "Unknown Lead", // Required in UI
                    AccountId = acc.Id,

                    // --- Ownership Mapping ---
                    LeadOwnerId = assignedOwnerId,
                    AccountOwnerId = acc.AccountOwnerId,
                    CoOwnerId = acc.CoOwnerId,

                    // --- Source Mapping ---
                    DataSources = acc.DataSource,
                    CampaignSource = acc.DataSource, // Mapped here as well in case the UI expects it

                    // --- Basic Details ---
                    MobileNumber = acc.MobileNumber,
                    AlternateMobile = acc.AlternateMobile,
                    EmailID = acc.Email,
                    AlternateEmailID = acc.AlternateEmailID,
                    AccountType = acc.AccountType,
                    CurrentStatus = acc.CurrentStatus,
                    MetaCampaignName = acc.MetaCampaignName,
                    SeminarName = acc.SeminarName,

                    // --- Address Mapping ---
                    Addr1_Country = acc.Addr1_Country,
                    Addr1_FlatHouse = acc.Addr1_FlatHouse,
                    Addr1_Street = acc.Addr1_Street,
                    Addr1_City = acc.Addr1_City,
                    Addr1_State = acc.Addr1_State,
                    Addr1_Zip = acc.Addr1_Zip,

                    Addr2_Country = acc.Addr2_Country,
                    Addr2_FlatHouse = acc.Addr2_FlatHouse,
                    Addr2_Street = acc.Addr2_Street,
                    Addr2_City = acc.Addr2_City,
                    Addr2_State = acc.Addr2_State,
                    Addr2_Zip = acc.Addr2_Zip,

                    // --- Professional Mapping ---
                    IsHomeopathicDoctor = acc.IsHomeopathicDoctor,
                    ClinicType = acc.ClinicType,
                    WorkType = acc.WorkType,
                    HasComputer = acc.HasComputer,
                    DateOfBirth = acc.DateOfBirth,
                    Age = acc.Age,
                    YearOfPractice = acc.YearsOfPractice,
                    AveragePatientFee = acc.AveragePatientFee,
                    PatientsPerDay = acc.PatientsPerDay,
                    TotalExperience = acc.TotalExperience,
                    NumberOfClinics = acc.NumberOfClinics,
                    Qualification = acc.Qualification,
                    YearOfPassing = acc.YearOfPassing,
                    CollegeName = acc.CollegeName,

                    // --- Software & Purchases ---
                    CurrentlyUsingSoftware = acc.CurrentlyUsingSoftware,
                    CurrentSoftwareName = acc.CurrentSoftwareName,
                    PurchaseDate = acc.PurchaseDate,
                    PurchaseValue = acc.PurchaseValue,
                    PaymentType = acc.PaymentType,
                    PaymentStatus = acc.PaymentStatus,

                    ContactPerson1 = acc.ContactPerson1,
                    Contact1Phone = acc.Contact1Phone,
                    ContactPerson2 = acc.ContactPerson2,
                    Contact2Phone = acc.Contact2Phone,
                    ContactPerson3 = acc.ContactPerson3,
                    Contact3Phone = acc.Contact3Phone,

                    Description = acc.Description,

                    // --- System Logic Default Fields ---
                    Stage = "Open",
                    CreatedDateAndTime = DateTime.Now,
                    LeadCreatedTime = DateTime.Now,
                    Pipeline = acc.CurrentStatus == "User" ? "Upgrade Software" : "New Software"
                };

                _context.Leads.Add(newContact);
                await _context.SaveChangesAsync();
            }
        }

        // ==========================================
        // DELETE: GET (Fetches record for confirmation)
        // ==========================================
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var account = await _context.Accounts.FirstOrDefaultAsync(m => m.Id == id);
            if (account == null) return NotFound();

            return View(account);
        }

        // ==========================================
        // DELETE: POST (Actually removes from DB)
        // ==========================================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account != null)
            {
                _context.Accounts.Remove(account);

                // Optional: You may also want to delete associated notes/files here 
                // before deleting the account to prevent orphaned records in the DB.

                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // BULK DELETE: AJAX POST
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> BulkDelete([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
                return Json(new { success = false, message = "No records selected." });

            if (_context == null || _context.Accounts == null)
                return Json(new { success = false, message = "Database context is not initialized." });

            try
            {
                var accountsToDelete = await _context.Accounts
                                                     .Where(a => ids.Contains(a.Id))
                                                     .ToListAsync();

                if (accountsToDelete != null && accountsToDelete.Any())
                {
                    // Safely remove associated notes if the table exists
                    if (_context.Note != null)
                    {
                        // 🚀 Check against the AccountId foreign key instead of ModuleName
                        var relatedNotes = await _context.Note
                            .Where(n => n.AccountId != null && ids.Contains(n.AccountId.Value))
                            .ToListAsync();

                        if (relatedNotes != null && relatedNotes.Any())
                        {
                            _context.Note.RemoveRange(relatedNotes);
                        }
                    }

                    // Remove the Accounts
                    _context.Accounts.RemoveRange(accountsToDelete);
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
        // ==========================================
        // CHECK DUPLICATE PHONE VIA AJAX
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> CheckDuplicatePhone(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return Json(new { isDuplicate = false });

            bool exists = await _context.Accounts.AnyAsync(a => a.MobileNumber == phone || a.AlternateMobile == phone);

            return Json(new { isDuplicate = exists });
        }

        // ==========================================
        // UPLOAD FILE
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            try
            {
                using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync(); // Skip the header

                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);

                        if (values.Length >= 4)
                        {
                            var newAccount = new Account
                            {
                                AccountOwnerId = GetVal(values, 2),
                                AccountName = GetVal(values, 3),
                                Email = GetVal(values, 5),
                                AlternateMobile = GetVal(values, 6),
                                MobileNumber = GetVal(values, 7),
                                DataSource = GetVal(values, 8),
                                Description = GetVal(values, 17),
                                IsDuplicated = bool.TryParse(GetVal(values, 62), out bool isDup) && isDup,
                                MetaCampaignName = GetVal(values, 63),
                                CoOwnerId = GetVal(values, 65),
                                CurrentStatus = GetVal(values, 112),
                                AccountType = GetVal(values, 113),
                                ContactPersonName = GetVal(values, 114),
                                ProfilePendingReason = GetVal(values, 115),
                                AlternateEmailID = GetVal(values, 126),
                                QualificationStatus = GetVal(values, 129),
                                SeminarName = GetVal(values, 88),
                                DateOfEntry = DateTime.TryParse(GetVal(values, 95), out DateTime doe) ? doe : DateTime.Now,
                            };

                            _context.Add(newAccount);
                        }
                    }
                    await _context.SaveChangesAsync();
                }
                return Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "An error occurred while processing the file.");
            }
        }

        private string GetVal(string[] values, int index)
        {
            if (index < values.Length) return values[index]?.Trim() ?? "";
            return "";
        }

        private string[] ParseCsvLine(string line)
        {
            var result = new System.Collections.Generic.List<string>();
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

        private bool AccountExists(int id)
        {
            return _context.Accounts.Any(e => e.Id == id);
        }
    }
}