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
using System.Collections.Generic;

namespace Radar_CRM.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Products/Index
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products.ToListAsync();
            return View(products);
        }

        // GET: Products/Create
        public IActionResult Create()
        {
            // Note: Ensure your User model uses 'fullName' based on your schema
            ViewBag.UsersList = new SelectList(_context.Users, "fullName", "fullName");
            return View(new Product { ProductActive = true, Taxable = true }); // Defaults
        }

        // POST: Products/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product)
        {
            ModelState.Clear();
            if (string.IsNullOrWhiteSpace(product.ProductName))
                ModelState.AddModelError("ProductName", "Product Name is required.");

            if (ModelState.IsValid)
            {
                product.RecordId = Guid.NewGuid().ToString();
                product.CreatedTime = DateTime.Now;

                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewBag.UsersList = new SelectList(_context.Users, "fullName", "fullName");
            return View(product);
        }

        // GET: Products/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            ViewBag.UsersList = new SelectList(_context.Users, "fullName", "fullName");
            return View(product);
        }

        // POST: Products/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.Id) return NotFound();

            ModelState.Clear();
            if (string.IsNullOrWhiteSpace(product.ProductName))
                ModelState.AddModelError("ProductName", "Product Name is required.");

            if (ModelState.IsValid)
            {
                try
                {
                    product.ModifiedTime = DateTime.Now;
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Products.Any(e => e.Id == id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }

            ViewBag.UsersList = new SelectList(_context.Users, "fullName", "fullName");
            return View(product);
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

                        var values = ParseCsvLine(line);

                        if (values.Length >= 4) // Ensure we have at least up to ProductName
                        {
                            string recordId = !string.IsNullOrWhiteSpace(values[0]) ? values[0].Trim() : Guid.NewGuid().ToString();

                            // Avoid inserting duplicates based on RecordId
                            if (!_context.Products.Any(p => p.RecordId == recordId))
                            {
                                var newProduct = new Product
                                {
                                    RecordId = recordId,
                                    ProductOwnerId = GetVal(values, 1),
                                    ProductOwner = GetVal(values, 2),
                                    ProductName = GetVal(values, 3),
                                    ProductCode = GetVal(values, 4),
                                    VendorId = GetVal(values, 5),
                                    VendorName = GetVal(values, 6),
                                    ProductActive = bool.TryParse(GetVal(values, 7), out bool active) ? active : true,
                                    Manufacturer = GetVal(values, 8),
                                    ProductCategory = GetVal(values, 9),

                                    // Dates
                                    SalesStartDate = DateTime.TryParse(GetVal(values, 10), out DateTime sd) ? sd : (DateTime?)null,
                                    SalesEndDate = DateTime.TryParse(GetVal(values, 11), out DateTime ed) ? ed : (DateTime?)null,
                                    SupportStartDate = DateTime.TryParse(GetVal(values, 12), out DateTime ssd) ? ssd : (DateTime?)null,
                                    SupportEndDate = DateTime.TryParse(GetVal(values, 13), out DateTime sed) ? sed : (DateTime?)null,

                                    // Audit Details
                                    CreatedById = GetVal(values, 14),
                                    CreatedBy = GetVal(values, 15),
                                    ModifiedById = GetVal(values, 16),
                                    ModifiedBy = GetVal(values, 17),
                                    CreatedTime = DateTime.TryParse(GetVal(values, 18), out DateTime ct) ? ct : DateTime.Now,
                                    ModifiedTime = DateTime.TryParse(GetVal(values, 19), out DateTime mt) ? mt : (DateTime?)null,

                                    Tag = GetVal(values, 20),

                                    // Decimals
                                    UnitPrice = decimal.TryParse(GetVal(values, 21), out decimal up) ? up : 0m,
                                    CommissionRate = decimal.TryParse(GetVal(values, 22), out decimal cr) ? cr : 0m,
                                    Tax = decimal.TryParse(GetVal(values, 23), out decimal tax) ? tax : 0m,
                                    Taxable = bool.TryParse(GetVal(values, 24), out bool taxable) ? taxable : false,

                                    UsageUnit = GetVal(values, 25),

                                    // Integers
                                    QtyOrdered = int.TryParse(GetVal(values, 26), out int qo) ? qo : 0,
                                    QuantityInStock = int.TryParse(GetVal(values, 27), out int qs) ? qs : 0,
                                    ReorderLevel = int.TryParse(GetVal(values, 28), out int rl) ? rl : 0,

                                    HandlerId = GetVal(values, 29),
                                    Handler = GetVal(values, 30),
                                    QuantityInDemand = int.TryParse(GetVal(values, 31), out int qd) ? qd : 0,
                                    Description = GetVal(values, 32),
                                    Locked = bool.TryParse(GetVal(values, 33), out bool locked) ? locked : false,
                                    LastActivityTime = DateTime.TryParse(GetVal(values, 34), out DateTime lat) ? lat : (DateTime?)null,
                                    ConnectedToModule = GetVal(values, 35),
                                    ConnectedToId = GetVal(values, 36),
                                    USD = decimal.TryParse(GetVal(values, 37), out decimal usd) ? usd : 0m,
                                    ChangeLogTime = DateTime.TryParse(GetVal(values, 38), out DateTime clt) ? clt : (DateTime?)null
                                };

                                _context.Add(newProduct);
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

        // 1. Helper to safely get array values without throwing Index Out of Range errors
        private string GetVal(string[] values, int index)
        {
            if (index < values.Length) return values[index]?.Trim() ?? "";
            return "";
        }

        // 2. Safely splits CSV lines but ignores commas inside quotation marks
        private string[] ParseCsvLine(string line)
        {
            var result = new List<string>();
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
    }
}