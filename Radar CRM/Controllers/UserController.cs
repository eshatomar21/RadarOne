using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;

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
        // INDEX: GET (Hierarchical List View)
        // ==========================================
        public async Task<IActionResult> Index()
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Users");

            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUser = await _context.Users.FindAsync(currentUserId);

            if (currentUser == null) return RedirectToAction("Login", "Users");

            var query = _context.Users.Include(u => u.Roles).AsQueryable();

            // 🚀 ADMIN CHECK BASED ON 'PROFILE' (Not Role)
            // If the user's Profile contains "Admin", they bypass all role hierarchy checks.
            bool isAdmin = !string.IsNullOrWhiteSpace(currentUser.Profile) &&
                           (currentUser.Profile.Contains("Admin", StringComparison.OrdinalIgnoreCase) ||
                            currentUser.Profile.Equals("Administrator", StringComparison.OrdinalIgnoreCase));

            // 🚀 THE LOGIC: 
            // If isAdmin is true (Profile = Admin), this IF block is completely skipped.
            // This allows Admins like Sitara and Jaspal to see each other's data AND all other users, 
            // regardless of where they sit in the Role hierarchy.
            if (!isAdmin)
            {
                var allRoles = await _context.Roles.ToListAsync();
                var visibleRoleIds = new List<int>();

                // Get subordinates (Managers see below users)
                var subordinateIds = GetSubordinateRoleIds(allRoles, currentUser.RoleId);
                visibleRoleIds.AddRange(subordinateIds);

                // Share with peers if enabled
                var currentUserRoleModel = allRoles.FirstOrDefault(r => r.Id == currentUser.RoleId);
                if (currentUserRoleModel != null && currentUserRoleModel.ShareDataWithPeers && currentUser.RoleId.HasValue)
                {
                    visibleRoleIds.Add(currentUser.RoleId.Value);
                }

                // Hierarchy filter applies ONLY to non-admins based on their Role
                query = query.Where(u => u.Id == currentUserId ||
                                         (u.RoleId.HasValue && visibleRoleIds.Contains(u.RoleId.Value)));
            }

            var users = await query.ToListAsync();
            return View(users);
        }

        // ==========================================
        // CREATE: GET
        // ==========================================
        public IActionResult Create()
        {
            ViewBag.RolesList = new SelectList(_context.Roles, "Id", "Name");
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "FirstName");

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
            ModelState.Remove("ParentRole");

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

            ViewBag.RolesList = new SelectList(_context.Roles, "Id", "Name");
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "FirstName", user.ReportingToId);

            return View(user);
        }

        // ==========================================
        // EDIT: GET
        // ==========================================
        public async Task<IActionResult> Edit(string? id)
        {
            // If no ID is provided in the URL, use the logged-in user's ID
            if (string.IsNullOrEmpty(id))
            {
                id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            }

            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            ViewBag.RolesList = new SelectList(_context.Roles, "Id", "Name");
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "FirstName", user.ReportingToId);

            return View(user);
        }

        // ==========================================
        // EDIT: POST
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, User user)
        {
            if (id != user.Id) return NotFound();

            ModelState.Remove("Roles");
            ModelState.Remove("ParentRole");

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
                    if (!UserExists(user.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.RolesList = new SelectList(_context.Roles, "Id", "Name");
            ViewBag.UsersList = new SelectList(_context.Users, "Id", "FirstName", user.ReportingToId);

            return View(user);
        }

        // ==========================================
        // UPLOAD FILE: POST
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var usersToInsert = new List<User>();
            int currentRow = 1;

            var existingUserIds = new HashSet<string>(_context.Users.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);

            try
            {
                using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync();

                    while (!reader.EndOfStream)
                    {
                        currentRow++;
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);

                        if (values.Length >= 39)
                        {
                            string recordId = !string.IsNullOrWhiteSpace(values[0]) ? values[0].Trim() : Guid.NewGuid().ToString();

                            if (!existingUserIds.Contains(recordId))
                            {
                                var newUser = new User
                                {
                                    Id = recordId,
                                    FirstName = GetVal(values, 1),
                                    LastName = GetVal(values, 2),
                                    Email = GetVal(values, 3),
                                    Role = GetVal(values, 5),
                                    AddedById = GetVal(values, 8),
                                    Phone = GetVal(values, 14),
                                    Mobile = GetVal(values, 15),
                                    Website = GetVal(values, 16),
                                    Fax = GetVal(values, 17),
                                    Profile = !string.IsNullOrWhiteSpace(GetVal(values, 19)) ? GetVal(values, 19) : "Standard",
                                    Street = GetVal(values, 20),
                                    City = GetVal(values, 21),
                                    State = GetVal(values, 22),
                                    Country = GetVal(values, 23),
                                    ZipCode = GetVal(values, 24),
                                    Language = GetVal(values, 25),
                                    CountryLocale = GetVal(values, 26),
                                    TimeZone = GetVal(values, 27),
                                    TimeFormat = GetVal(values, 28),
                                    UserStatus = !string.IsNullOrWhiteSpace(GetVal(values, 29)) ? GetVal(values, 29) : "Active",
                                    fullName = GetVal(values, 30),
                                    Zuid = GetVal(values, 32),
                                    SortOrderPreference = GetVal(values, 33),
                                    NameFormat = GetVal(values, 34),
                                    Type = GetVal(values, 35),
                                    StatusReason = GetVal(values, 36),
                                    Source = GetVal(values, 37),
                                    PreferredUnitForDistance = GetVal(values, 38)
                                };

                                if (DateTime.TryParse(GetVal(values, 12), out DateTime addedTime))
                                {
                                    newUser.AddedTime = addedTime;
                                }
                                else
                                {
                                    newUser.AddedTime = DateTime.Now;
                                }

                                usersToInsert.Add(newUser);
                                existingUserIds.Add(recordId);
                            }
                        }
                    }
                }

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
                _context.ChangeTracker.AutoDetectChangesEnabled = true;
            }
        }

        // ==========================================
        // GET: Users/Login
        // ==========================================
        [HttpGet]
        public IActionResult Login()
        {
            return View(new User());
        }

        // ==========================================
        // POST: Users/Login
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(User loginAttempt)
        {
            ModelState.Clear();

            if (!string.IsNullOrEmpty(loginAttempt.Email) && !string.IsNullOrEmpty(loginAttempt.Password))
            {
                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email == loginAttempt.Email &&
                    u.Password == loginAttempt.Password &&
                    u.UserStatus != null && u.UserStatus.ToLower() == "active");

                if (user != null)
                {
                    var claims = new List<Claim>
{
    new Claim(ClaimTypes.NameIdentifier, user.Id),
    new Claim(ClaimTypes.Name, user.fullName ?? $"{user.FirstName} {user.LastName}"),
    new Claim(ClaimTypes.Email, user.Email ?? ""),
    new Claim(ClaimTypes.Role, user.Role ?? "User"),
    new Claim("Profile", user.Profile ?? "Standard"),
    new Claim(ClaimTypes.MobilePhone, user.Phone ?? "N/A") // Added Phone claim
};

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError(string.Empty, "Invalid email or password. Please try again.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Both Email and Password are required to login.");
            }

            return View(loginAttempt);
        }

        // ==========================================
        // POST: Users/Logout
        // ==========================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Users");
        }

        // ==========================================
        // HELPER METHODS
        // ==========================================

        private List<int> GetSubordinateRoleIds(List<Role> allRoles, int? currentRoleId)
        {
            var subordinateIds = new List<int>();
            if (currentRoleId == null) return subordinateIds;

            var directChildren = allRoles.Where(r => r.ParentRoleId == currentRoleId).Select(r => r.Id).ToList();
            subordinateIds.AddRange(directChildren);

            foreach (var childId in directChildren)
            {
                subordinateIds.AddRange(GetSubordinateRoleIds(allRoles, childId));
            }

            return subordinateIds;
        }

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
                if (c == '\"')
                {
                    inQuotes = !inQuotes;
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

        private bool UserExists(string id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}