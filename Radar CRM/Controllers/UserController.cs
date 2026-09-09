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
        // UPLOAD FILE: POST 
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0)
            {
                return BadRequest("No file was uploaded.");
            }

            try
            {
                using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync(); // Skip the header row

                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Use the custom safe parser instead of standard .Split(',')
                        var values = ParseCsvLine(line);

                        if (values.Length >= 4)
                        {
                            string recordId = !string.IsNullOrWhiteSpace(values[0]) ? values[0].Trim() : Guid.NewGuid().ToString();

                            if (!_context.Users.Any(u => u.Id == recordId))
                            {
                                var newUser = new User
                                {
                                    Id = recordId,
                                    FirstName = GetVal(values, 1),
                                    LastName = GetVal(values, 2),
                                    Email = GetVal(values, 3),
                                    Role = GetVal(values, 5),
                                    AddedById = GetVal(values, 8),
                                    Phone = GetVal(values, 11),
                                    Mobile = GetVal(values, 12),
                                    Website = GetVal(values, 13),
                                    Fax = GetVal(values, 14),
                                    Profile = !string.IsNullOrWhiteSpace(GetVal(values, 16)) ? GetVal(values, 16) : "Standard",
                                    Street = GetVal(values, 17),
                                    City = GetVal(values, 18),
                                    State = GetVal(values, 19),
                                    Country = GetVal(values, 20),
                                    ZipCode = GetVal(values, 21),
                                    Language = GetVal(values, 22),
                                    CountryLocale = GetVal(values, 23),
                                    TimeZone = GetVal(values, 24),
                                    TimeFormat = GetVal(values, 25),
                                    UserStatus = !string.IsNullOrWhiteSpace(GetVal(values, 26)) ? GetVal(values, 26) : "Active",
                                    fullName = GetVal(values, 27), 
                                    Zuid = GetVal(values, 29),
                                    SortOrderPreference = GetVal(values, 30),
                                    NameFormat = GetVal(values, 31),
                                    Type = GetVal(values, 32),
                                    StatusReason = GetVal(values, 33),
                                    Source = GetVal(values, 34),
                                    PreferredUnitForDistance = GetVal(values, 35)
                                };

                                // Safely parse the Date Time, fallback to Now if empty
                                if (DateTime.TryParse(GetVal(values, 10), out DateTime addedTime))
                                {
                                    newUser.AddedTime = addedTime;
                                }
                                else
                                {
                                    newUser.AddedTime = DateTime.Now;
                                }

                                _context.Add(newUser);
                            }
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