using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Radar_CRM.Data;
using Radar_CRM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Radar_CRM.Controllers
{
    public class NotesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NotesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: Notes/Index (Paginated, Sorted, Filtered)
        // ==========================================
        public async Task<IActionResult> Index(int page = 1, string search = "", string sortCol = "Id", string sortDir = "desc", string advancedFilters = "")
        {
            if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Users");

            int pageSize = 100;

            var query = _context.Note
                .Include(n => n.NoteOwner)
                .AsQueryable();

            // 🚀 BULLETPROOF FILTERING ENGINE WITH AND/OR SUPPORT
            if (!string.IsNullOrWhiteSpace(advancedFilters))
            {
                try
                {
                    string jsonString = advancedFilters;
                    if (jsonString.Contains("%5B") || jsonString.Contains("%7B"))
                        jsonString = Uri.UnescapeDataString(jsonString);

                    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var filters = System.Text.Json.JsonSerializer.Deserialize<List<NoteFilterCriteria>>(jsonString, options);

                    if (filters != null && filters.Any())
                    {
                        var parameter = System.Linq.Expressions.Expression.Parameter(typeof(Notes), "n");
                        System.Linq.Expressions.Expression combinedPredicate = null;

                        foreach (var f in filters)
                        {
                            if (string.IsNullOrWhiteSpace(f.Value) && f.Condition != "is_empty" && f.Condition != "is_not_empty") continue;

                            var propertyInfo = typeof(Notes).GetProperty(f.ColumnName, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (propertyInfo == null) continue;

                            var propExpr = System.Linq.Expressions.Expression.Property(parameter, propertyInfo);
                            System.Linq.Expressions.Expression conditionExpr = null;

                            if (propertyInfo.PropertyType == typeof(string))
                            {
                                var toLowerMethod = typeof(string).GetMethod("ToLower", Type.EmptyTypes);
                                var containsMethod = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                                var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
                                var endsWithMethod = typeof(string).GetMethod("EndsWith", new[] { typeof(string) });

                                var notNullProp = System.Linq.Expressions.Expression.Coalesce(propExpr, System.Linq.Expressions.Expression.Constant(""));
                                var lowerProp = System.Linq.Expressions.Expression.Call(notNullProp, toLowerMethod);
                                var lowerVal = System.Linq.Expressions.Expression.Constant(f.Value?.ToLower().Trim() ?? "");

                                if (f.Condition == "is") conditionExpr = System.Linq.Expressions.Expression.Equal(lowerProp, lowerVal);
                                else if (f.Condition == "is_not") conditionExpr = System.Linq.Expressions.Expression.NotEqual(lowerProp, lowerVal);
                                else if (f.Condition == "contains") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal);
                                else if (f.Condition == "does_not_contain") conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(lowerProp, containsMethod, lowerVal));
                                else if (f.Condition == "starts_with") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, startsWithMethod, lowerVal);
                                else if (f.Condition == "ends_with") conditionExpr = System.Linq.Expressions.Expression.Call(lowerProp, endsWithMethod, lowerVal);
                                else if (f.Condition == "is_empty") conditionExpr = System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr);
                                else if (f.Condition == "is_not_empty") conditionExpr = System.Linq.Expressions.Expression.Not(System.Linq.Expressions.Expression.Call(typeof(string).GetMethod("IsNullOrEmpty"), propExpr));
                            }

                            if (conditionExpr != null)
                            {
                                if (combinedPredicate == null) combinedPredicate = conditionExpr;
                                else
                                {
                                    if (string.Equals(f.LogicalOperator, "OR", StringComparison.OrdinalIgnoreCase))
                                        combinedPredicate = System.Linq.Expressions.Expression.OrElse(combinedPredicate, conditionExpr);
                                    else
                                        combinedPredicate = System.Linq.Expressions.Expression.AndAlso(combinedPredicate, conditionExpr);
                                }
                            }
                        }

                        if (combinedPredicate != null)
                        {
                            var lambda = System.Linq.Expressions.Expression.Lambda<Func<Notes, bool>>(combinedPredicate, parameter);
                            query = query.Where(lambda);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("FILTER ENGINE ERROR: " + ex.Message);
                }
            }

            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(n =>
                    (n.NoteTitle != null && n.NoteTitle.ToLower().Contains(searchLower)) ||
                    (n.Description != null && n.Description.ToLower().Contains(searchLower)) ||
                    (n.NoteContent != null && n.NoteContent.ToLower().Contains(searchLower)) ||
                    (n.AccountName != null && n.AccountName.ToLower().Contains(searchLower)) ||
                    (n.LeadName != null && n.LeadName.ToLower().Contains(searchLower)) ||
                    (n.DealName != null && n.DealName.ToLower().Contains(searchLower))
                );
            }

            if (sortDir == "desc")
            {
                query = sortCol switch
                {
                    "CreatedDateTime" => query.OrderByDescending(n => n.CreatedDateTime),
                    "NoteTitle" => query.OrderByDescending(n => n.NoteTitle),
                    "AccountName" => query.OrderByDescending(n => n.AccountName),
                    _ => query.OrderByDescending(n => n.Id)
                };
            }
            else
            {
                query = sortCol switch
                {
                    "CreatedDateTime" => query.OrderBy(n => n.CreatedDateTime),
                    "NoteTitle" => query.OrderBy(n => n.NoteTitle),
                    "AccountName" => query.OrderBy(n => n.AccountName),
                    _ => query.OrderBy(n => n.Id)
                };
            }

            var totalRecords = await query.CountAsync();
            if (page < 1) page = 1;
            int totalPagesCalc = (int)Math.Ceiling(totalRecords / (double)pageSize);
            if (totalPagesCalc == 0) totalPagesCalc = 1;
            if (page > totalPagesCalc) page = totalPagesCalc;

            var notes = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPagesCalc;
            ViewBag.TotalRecords = totalRecords;

            return View(notes);
        }

        // ==========================================
        // BULK UPLOAD (MAPPED SECURELY TO newnotes.csv)
        // ==========================================
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile uploadedFile)
        {
            if (uploadedFile == null || uploadedFile.Length == 0) return BadRequest("No file was uploaded.");

            var notesToInsert = new List<Notes>();
            int currentRow = 1;
            int skippedCount = 0;

            var validUserIds = new HashSet<string>(_context.Users.Select(u => u.Id), StringComparer.OrdinalIgnoreCase);
            var usersByName = _context.Users.ToList().GroupBy(u => u.FirstName).ToDictionary(g => g.Key, g => g.First().Id);

            var accountLookup = _context.Accounts.Where(a => !string.IsNullOrEmpty(a.AccountName)).ToList()
                                        .GroupBy(a => a.AccountName).ToDictionary(g => g.Key, g => g.First().Id);

            try
            {
                using (var stream = uploadedFile.OpenReadStream())
                {
                    // 🚀 USE THE NEW ROBUST PARSER THAT HANDLES MULTI-LINE NOTES
                    var allRows = ParseCsvRobust(stream);

                    // Skip the header row (index 0)
                    for (int i = 1; i < allRows.Count; i++)
                    {
                        currentRow++;
                        var values = allRows[i];

                        // Extremely forgiving length check - allows processing even if trailing columns are missing
                        if (values.Length >= 9)
                        {
                            string rawRecordId = GetVal(values, 0);         // Record Id
                            string rawCreatedById = GetVal(values, 1);
                            string rawCreatedBy = GetVal(values, 2);        // Created By
                            string rawCreatedTime = GetVal(values, 3);      // Created Time
                            string rawModifiedById = GetVal(values, 4);
                            string rawModifiedBy = GetVal(values, 5);       // Modified By
                            string rawModifiedTime = GetVal(values, 6);     // Modified Time
                            string rawNoteContent = GetVal(values, 7);
                            string rawNoteOwnerId= GetVal(values, 8);
                            string rawNoteOwner = GetVal(values, 9);
                            string rawNoteTitle = GetVal(values, 10);
                            string rawAccountId = GetVal(values, 11);
                            string rawAccountName = GetVal(values, 12);
                            string rawDescription = GetVal(values, 13);      // Note Content

                      

                            // Fallback if Description is blank (Prevents EF Core from crashing on [Required] tags)
                            if (string.IsNullOrWhiteSpace(rawDescription))
                            {
                                rawDescription = "No Content Provided";
                            }

                            string assignedOwnerId = null;
                            if (validUserIds.Contains(rawNoteOwnerId)) assignedOwnerId = rawNoteOwnerId;
                            else if (usersByName.TryGetValue(rawNoteOwner, out string idByName)) assignedOwnerId = idByName;

                            int? linkedAccountId = null;
                            if (accountLookup.TryGetValue(rawAccountName, out int accId)) linkedAccountId = accId;

                            DateTime createdDt = DateTime.Now;
                            if (DateTime.TryParse(rawCreatedTime, out DateTime parsedCdt)) createdDt = parsedCdt;

                            DateTime? modifiedDt = null;
                            if (DateTime.TryParse(rawModifiedTime, out DateTime parsedMdt)) modifiedDt = parsedMdt;

                            var newNote = new Notes
                            {
                                ZohoRecordId = rawRecordId,
                                CreatedBy = rawCreatedBy,
                                CreatedDateTime = createdDt,
                                ModifiedBy = rawModifiedBy,
                                ModifiedTime = modifiedDt,

                                // Set both so EF Core validates, and UI binds perfectly
                                Description = rawDescription,
                                NoteContent = rawDescription,

                                NoteOwnerId = assignedOwnerId,

                                // Strictly mapping to NoteTitle per your instructions
                                NoteTitle = rawNoteTitle,

                                AccountName = rawAccountName,
                                AccountId = linkedAccountId
                            };

                            notesToInsert.Add(newNote);
                        }
                        else
                        {
                            skippedCount++; // Row was corrupt or missing mandatory columns
                        }
                    }
                }

                _context.ChangeTracker.AutoDetectChangesEnabled = false;
                await _context.Note.AddRangeAsync(notesToInsert);
                await _context.SaveChangesAsync();

                return Json(new { success = true, insertedCount = notesToInsert.Count, skippedCount = skippedCount });
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
        // ROBUST MULTI-LINE CSV PARSER 
        // ==========================================
        private string GetVal(string[] values, int index)
        {
            if (index < values.Length) return values[index]?.Trim() ?? "";
            return "";
        }

        private List<string[]> ParseCsvRobust(Stream stream)
        {
            var rows = new List<string[]>();
            using var reader = new StreamReader(stream);

            bool inQuotes = false;
            var currentField = new System.Text.StringBuilder();
            var currentRow = new List<string>();

            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                if (line == null) break;

                for (int i = 0; i < line.Length; i++)
                {
                    char c = line[i];

                    if (c == '\"')
                    {
                        if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                        {
                            currentField.Append('\"');
                            i++; // Skip the escaped quote
                        }
                        else
                        {
                            inQuotes = !inQuotes;
                        }
                    }
                    else if (c == ',' && !inQuotes)
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }

                if (inQuotes)
                {
                    // Field contains a line break, keep reading on the next loop
                    currentField.Append("\n");
                }
                else
                {
                    // Finished parsing row
                    currentRow.Add(currentField.ToString());
                    rows.Add(currentRow.ToArray());
                    currentRow.Clear();
                    currentField.Clear();
                }
            }
            return rows;
        }

        // ==========================================
        // HELPER CLASSES
        // ==========================================
        public class NoteFilterCriteria
        {
            public string LogicalOperator { get; set; }
            public string ColumnName { get; set; }
            public string Condition { get; set; }
            public string Value { get; set; }
            public bool IsDate { get; set; }
        }
    }
}