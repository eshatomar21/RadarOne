using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Collections.Generic;

namespace Radar_CRM.Controllers
{
    public class VendorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public VendorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Vendors/Index
        public async Task<IActionResult> Index()
        {
            var vendors = await _context.Vendors.ToListAsync();
            return View(vendors);
        }

        // GET: Vendors/Create
        public IActionResult Create()
        {
            ViewBag.UsersList = new SelectList(_context.Users, "fullName", "fullName");
            return View(new Vendor());
        }

        // POST: Vendors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Vendor vendor)
        {
            ModelState.Clear();
            if (string.IsNullOrWhiteSpace(vendor.VendorName))
                ModelState.AddModelError("VendorName", "Vendor Name is required.");

            if (ModelState.IsValid)
            {
                vendor.RecordId = Guid.NewGuid().ToString();
                vendor.CreatedTime = DateTime.Now;

                _context.Add(vendor);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.UsersList = new SelectList(_context.Users, "fullName", "fullName");
            return View(vendor);
        }

        // GET: Vendors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor == null) return NotFound();

            ViewBag.UsersList = new SelectList(_context.Users, "fullName", "fullName");
            return View(vendor);
        }

        // POST: Vendors/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Vendor vendor)
        {
            if (id != vendor.Id) return NotFound();

            ModelState.Clear();
            if (string.IsNullOrWhiteSpace(vendor.VendorName))
                ModelState.AddModelError("VendorName", "Vendor Name is required.");

            if (ModelState.IsValid)
            {
                try
                {
                    vendor.ModifiedTime = DateTime.Now;
                    _context.Update(vendor);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Vendors.Any(e => e.Id == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.UsersList = new SelectList(_context.Users, "fullName", "fullName");
            return View(vendor);
        }

        // ==========================================
        // UPLOAD FILE: POST 
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0)
                return BadRequest("No file was uploaded.");

            try
            {
                using (var reader = new StreamReader(uploadedFile.OpenReadStream()))
                {
                    var headerLine = await reader.ReadLineAsync(); // Skip header row

                    while (!reader.EndOfStream)
                    {
                        var line = await reader.ReadLineAsync();
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var values = ParseCsvLine(line);

                        if (values.Length >= 2) // Minimum to have a Vendor Name
                        {
                            string recordId = !string.IsNullOrWhiteSpace(values[0]) ? values[0].Trim() : Guid.NewGuid().ToString();

                            // Prevent duplicates
                            if (!_context.Vendors.Any(v => v.RecordId == recordId))
                            {
                                var newVendor = new Vendor
                                {
                                    RecordId = recordId,
                                    VendorName = GetVal(values, 1),
                                    Phone = GetVal(values, 2),
                                    Email = GetVal(values, 3),
                                    Website = GetVal(values, 4),
                                    GLAccount = GetVal(values, 5),
                                    Category = GetVal(values, 6),
                                    VendorOwnerId = GetVal(values, 7),
                                    VendorOwner = GetVal(values, 8),
                                    CreatedById = GetVal(values, 9),
                                    CreatedBy = GetVal(values, 10),
                                    ModifiedById = GetVal(values, 11),
                                    ModifiedBy = GetVal(values, 12),
                                    CreatedTime = DateTime.TryParse(GetVal(values, 13), out DateTime ct) ? ct : DateTime.Now,
                                    ModifiedTime = DateTime.TryParse(GetVal(values, 14), out DateTime mt) ? mt : (DateTime?)null,
                                    Tag = GetVal(values, 15),
                                    Street = GetVal(values, 16),
                                    City = GetVal(values, 17),
                                    State = GetVal(values, 18),
                                    ZipCode = GetVal(values, 19),
                                    Country = GetVal(values, 20),
                                    Description = GetVal(values, 21),
                                    Locked = bool.TryParse(GetVal(values, 22), out bool locked) ? locked : false,
                                    EmailOptOut = bool.TryParse(GetVal(values, 23), out bool optout) ? optout : false,
                                    UnsubscribedMode = GetVal(values, 24),
                                    UnsubscribedTime = DateTime.TryParse(GetVal(values, 25), out DateTime ut) ? ut : (DateTime?)null,
                                    LastActivityTime = DateTime.TryParse(GetVal(values, 26), out DateTime lat) ? lat : (DateTime?)null,
                                    ConnectedToModule = GetVal(values, 27),
                                    ConnectedToId = GetVal(values, 28),
                                    PanNo = GetVal(values, 29),
                                    GstNo = GetVal(values, 30),
                                    VendorType = GetVal(values, 31),
                                    ChangeLogTime = DateTime.TryParse(GetVal(values, 32), out DateTime clt) ? clt : (DateTime?)null
                                };

                                _context.Add(newVendor);
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
        // HELPER METHODS
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
    }
}