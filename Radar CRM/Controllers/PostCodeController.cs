using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Radar_CRM.Controllers
{
    public class PostcodeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PostcodeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // INDEX: LIST ALL POSTCODES
        // ==========================================
        [HttpGet]
        public IActionResult Index()
        {
            var postcodes = _context.Postcodes
                                    .OrderBy(p => p.ZipCode)
                                    .ToList();
            return View(postcodes);
        }

        // ==========================================
        // BULK DELETE
        // ==========================================
        [HttpPost]
        public IActionResult DeleteSelected(int[] selectedIds)
        {
            if (selectedIds != null && selectedIds.Length > 0)
            {
                try
                {
                    // Find all records matching the checked IDs
                    var recordsToDelete = _context.Postcodes
                        .Where(p => selectedIds.Contains(p.Id))
                        .ToList();

                    if (recordsToDelete.Any())
                    {
                        _context.Postcodes.RemoveRange(recordsToDelete);
                        _context.SaveChanges();
                        TempData["SuccessMessage"] = $"Successfully deleted {recordsToDelete.Count} record(s).";
                    }
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"An error occurred while deleting records: {ex.Message}";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "No records were selected for deletion.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==========================================
        // CASCADING DROPDOWN AJAX ENDPOINTS
        // ==========================================

        [HttpGet]
        public JsonResult GetCountries()
        {
            var countries = _context.Postcodes
                .Where(p => !string.IsNullOrEmpty(p.Country))
                .Select(p => p.Country)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return Json(countries);
        }

        [HttpGet]
        public JsonResult GetStates(string country)
        {
            var states = _context.Postcodes
                .Where(p => p.Country == country && !string.IsNullOrEmpty(p.State_Name))
                .Select(p => p.State_Name)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            return Json(states);
        }

        [HttpGet]
        public JsonResult GetCities(string state)
        {
            var cities = _context.Postcodes
                .Where(p => p.State_Name == state && !string.IsNullOrEmpty(p.City))
                .Select(p => p.City)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            return Json(cities);
        }

        [HttpGet]
        public JsonResult GetZipCodes(string city, string state)
        {
            var zipCodes = _context.Postcodes
                .Where(p => p.City == city && p.State_Name == state && !string.IsNullOrEmpty(p.ZipCode))
                .Select(p => p.ZipCode)
                .Distinct()
                .OrderBy(z => z)
                .ToList();

            return Json(zipCodes);
        }

        // ==========================================
        // CREATE: SINGLE ENTRY
        // ==========================================
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Postcode postcode)
        {
            if (ModelState.IsValid)
            {
                _context.Postcodes.Add(postcode);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "New entry created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(postcode);
        }

        // ==========================================
        // IMPORT CSV FUNCTIONALITY
        // ==========================================
        [HttpPost]
        public IActionResult ImportCsv(IFormFile file)
        {
            if (file != null && file.Length > 0)
            {
                try
                {
                    using (var reader = new StreamReader(file.OpenReadStream()))
                    using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HeaderValidated = null,
                        MissingFieldFound = null,
                        ShouldSkipRecord = args => args.Row.Parser.Record.All(string.IsNullOrWhiteSpace)
                    }))
                    {
                        var allRecords = csv.GetRecords<Postcode>().ToList();
                        var validRecords = allRecords.Where(p => !string.IsNullOrWhiteSpace(p.ZipCode)).ToList();
                        int skippedCount = allRecords.Count - validRecords.Count;

                        if (validRecords.Any())
                        {
                            _context.Postcodes.AddRange(validRecords);
                            _context.SaveChanges();

                            string successMsg = $"{validRecords.Count} records imported successfully.";
                            if (skippedCount > 0)
                            {
                                successMsg += $" ({skippedCount} blank/invalid rows skipped).";
                            }
                            TempData["SuccessMessage"] = successMsg;
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Upload failed. No valid Zip Codes were found in the uploaded file.";
                        }
                    }
                }
                catch (Exception ex)
                {
                    var innerError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                    TempData["ErrorMessage"] = $"Error reading CSV file: {innerError}";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "Please select a valid CSV file before clicking upload.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}