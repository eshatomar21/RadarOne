using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Required for IFormFile
using System.IO;                 // Required for StreamReader

namespace Radar_CRM.Controllers
{
    public class UsersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // INDEX: GET (List View)
        // ==========================================
        public async Task<IActionResult> Index()
        {
            // Includes the Role data so you can display Role.Name in your UI
            var users = await _context.Users.Include(u => u.Roles).ToListAsync();
            return View(users);
        }

        // ==========================================
        // CREATE: GET
        // ==========================================
        public IActionResult Create()
        {
            // Populate Dropdowns for the Create view
            ViewBag.Roles = new SelectList(_context.Roles, "Id", "Name");
            ViewBag.Users = new SelectList(_context.Users, "RecordId", "FirstName");

            return View();
        }

        // ==========================================
        // CREATE: POST
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(User user)
        {
            ModelState.Remove("Roles");

            if (ModelState.IsValid)
            {
                if (string.IsNullOrEmpty(user.Id))
                {
                    user.Id = Guid.NewGuid().ToString();
                }

                user.AddedTime = DateTime.Now;

                _context.Add(user);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).ToList();

            ViewBag.Roles = new SelectList(_context.Roles, "Id", "Name", user.RoleId);
            ViewBag.Users = new SelectList(_context.Users, "RecordId", "FirstName", user.ReportingToId);

            return View(user);
        }

        // ==========================================
        // UPLOAD FILE: POST (Mapped for Users_2026_09_10.csv)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0)
            {
                return BadRequest("No file was uploaded.");
            }

            var usersToInsert = new List<User>();
            int currentRow = 1;

            // 🚀 HIGH PERFORMANCE CACHE: Get existing IDs into memory to prevent duplicates without hammering the DB
            var existingUserIds = new HashSet<string>(_context.Users.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync(); // Skip the header row

                    while (!reader.EndOfStream)
                    {
                        currentRow++;
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);

                        // Checking length based on Users_2026_09_10.csv format (needs at least up to index 38)
                        if (values.Length >= 39)
                        {
                            string recordId = !string.IsNullOrWhiteSpace(values[0]) ? values[0].Trim() : Guid.NewGuid().ToString();

                            if (!existingUserIds.Contains(recordId))
                            {
                                var newUser = new User
                                {
                                    Id = recordId,                                // [0] Record Id
                                    FirstName = GetVal(values, 1),                // [1] First Name
                                    LastName = GetVal(values, 2),                 // [2] Last Name
                                    Email = GetVal(values, 3),                    // [3] Email
                                    Role = GetVal(values, 5),                     // [5] Role
                                    AddedById = GetVal(values, 8),                // [8] Added By.id
                                    Phone = GetVal(values, 14),                   // [14] Phone
                                    Mobile = GetVal(values, 15),                  // [15] Mobile
                                    Website = GetVal(values, 16),                 // [16] Website
                                    Fax = GetVal(values, 17),                     // [17] Fax
                                    Profile = !string.IsNullOrWhiteSpace(GetVal(values, 19)) ? GetVal(values, 19) : "Standard", // [19] Profile
                                    Street = GetVal(values, 20),                  // [20] Street
                                    City = GetVal(values, 21),                    // [21] City
                                    State = GetVal(values, 22),                   // [22] State
                                    Country = GetVal(values, 23),                 // [23] Country
                                    ZipCode = GetVal(values, 24),                 // [24] Zip Code
                                    Language = GetVal(values, 25),                // [25] Language
                                    CountryLocale = GetVal(values, 26),           // [26] Country Locale
                                    TimeZone = GetVal(values, 27),                // [27] Time Zone
                                    TimeFormat = GetVal(values, 28),              // [28] Time Format
                                    UserStatus = !string.IsNullOrWhiteSpace(GetVal(values, 29)) ? GetVal(values, 29) : "Active", // [29] Status
                                    fullName = GetVal(values, 30),                // [30] Full Name
                                    Zuid = GetVal(values, 32),                    // [32] Zuid
                                    SortOrderPreference = GetVal(values, 33),     // [33] Sort order preference
                                    NameFormat = GetVal(values, 34),              // [34] Name format
                                    Type = GetVal(values, 35),                    // [35] Type
                                    StatusReason = GetVal(values, 36),            // [36] status reason
                                    Source = GetVal(values, 37),                  // [37] Source
                                    PreferredUnitForDistance = GetVal(values, 38) // [38] Preferred Unit for Distance
                                };

                                // Safely parse the Added Time (Index 12), fallback to Now if empty
                                if (DateTime.TryParse(GetVal(values, 12), out DateTime addedTime))
                                {
                                    newUser.AddedTime = addedTime;
                                }
                                else
                                {
                                    newUser.AddedTime = DateTime.Now;
                                }

                                usersToInsert.Add(newUser);
                                existingUserIds.Add(recordId); // Add to local set to prevent duplicates within the same CSV upload
                            }
                        }
                    }
                }

                // 🚀 MASSIVE SPEED BOOST: Turn off tracking during bulk insert to stop Entity Framework from hanging
                _context.ChangeTracker.AutoDetectChangesEnabled = false;

                await _context.Users.AddRangeAsync(usersToInsert);
                await _context.SaveChangesAsync();

                return Ok();
            }
            catch (Exception ex)
            {
                string trueError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Failed at Row {currentRow} -> {trueError}");
            }
            finally
            {
                // Always turn tracking back on safely
                _context.ChangeTracker.AutoDetectChangesEnabled = true;
            }
        }
        // ==========================================
        // HELPER METHODS (Must be inside the class, outside other methods)
        // ==========================================

        // 1. Helper to safely get array values without throwing Index Out of Range errors
        private string GetVal(string[] values, int index)
        {
            if (index < values.Length) return values[index]?.Trim() ?? "";
            return "";
        }

        // 2. Safely splits CSV lines but ignores commas inside quotation marks
        private string[] ParseCsvLine(string line)
        {
            var result = new System.Collections.Generic.List<string>();
            bool inQuotes = false;
            var currentField = new System.Text.StringBuilder();

            foreach (char c in line)
            {
                if (c == '\"')
                {
                    inQuotes = !inQuotes; // Toggle quote status
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
            result.Add(currentField.ToString());
            return result.ToArray();
        }
        // ==========================================
        // EDIT: GET (Fetches data to show on the page)
        // ==========================================
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            ViewBag.Roles = new SelectList(_context.Roles, "Id", "Name", user.RoleId);
            ViewBag.Users = new SelectList(_context.Users, "RecordId", "FirstName", user.ReportingToId);

            return View(user);
        }

        // ==========================================
        // EDIT: POST (Saves the updated data)
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, User user)
        {
            if (id != user.Id)
            {
                return NotFound();
            }

            ModelState.Remove("Roles");

            if (ModelState.IsValid)
            {
                try
                {
                    user.ModifiedTime = DateTime.Now;

                    _context.Update(user);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(user.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Roles = new SelectList(_context.Roles, "Id", "Name", user.RoleId);
            ViewBag.Users = new SelectList(_context.Users, "RecordId", "FirstName", user.ReportingToId);

            return View(user);
        }

        // Helper method to check if a user exists
        private bool UserExists(string id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}